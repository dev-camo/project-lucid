using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.AutoPlay;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime AutoPlayTrigger(Conditional), BoolEventProxy, CycleGameObjectList,
    // DelayedActivator, DeleteChildren, DestroyObject and EnumValuePair`2 against the 1.10.1
    // x86-64 bodies. Edit Mode sends no Unity messages, so each case calls the hooks itself;
    // coroutines are refused on inactive hosts and their iterators are stepped by hand, and
    // the waits they yield are read without being started.
    public sealed class OriginalWorldTriggerUtilityTests
    {
        private static IEnumerator Forever()
        {
            while (true) yield return null;
        }

        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");
        private const string SelfTargetError = "Cannot target self with BoolEventProxy, otherwise will cause infinite callback.";

        // Metadata of TypeDefs 0x02000019/0x0200001a: RequireComponent(BoxCollider) before
        // NullChecks then ArrayBoundsChecks; the backing field follows m_triggerTag and the
        // setter is protected (0x0884). Both constructors (0x508580, 0x5085E0) store "Player".
        [Test]
        public void AutoPlayTriggersKeepTheirDeclarationsAndDefaultToThePlayerTag()
        {
            AssertMembers(typeof(AutoPlayTrigger), 0x00100001,
                new[] { ("m_triggerTag", 0x0001), ("<IsTriggered>k__BackingField", 0x0001) },
                new[] { ("get_IsTriggered", 0x0886), ("set_IsTriggered", 0x0884), ("OnTriggerEnter", 0x01c4), (".ctor", 0x1886) });
            AssertMembers(typeof(AutoPlayTriggerConditional), 0x00100001,
                new[] { ("m_testSuccessLocation", 0x0001) },
                new[] { ("get_IsPassPoint", 0x0886), (".ctor", 0x1886) });
            foreach (Type type in new[] { typeof(AutoPlayTrigger), typeof(AutoPlayTriggerConditional) })
            {
                Assert.That(AttributeTypes(type).First(), Is.EqualTo(typeof(RequireComponent)), type.Name);
                AssertIl2CppOptions(type, 1, 2);
            }
            Assert.That(Tooltip(Field(typeof(AutoPlayTrigger), "m_triggerTag")),
                Is.EqualTo("Will only trigger if entering collider has this tag. Set to empty to ignore tags."));

            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid autoplay trigger", false);
                var trigger = host.AddComponent<AutoPlayTrigger>();
                Assert.That(host.GetComponent<BoxCollider>(), Is.Not.Null, "RequireComponent adds the box collider");
                Assert.That(Get<string>(trigger, "m_triggerTag"), Is.EqualTo("Player"));
                Assert.That(trigger.IsTriggered, Is.False);
                var conditional = owned.Host("Lucid conditional autoplay trigger", false).AddComponent<AutoPlayTriggerConditional>();
                Assert.That(Get<string>(conditional, "m_triggerTag"), Is.EqualTo("Player"));
                Assert.That(conditional.IsPassPoint, Is.False);
                Set(conditional, "m_testSuccessLocation", true);
                Assert.That(conditional.IsPassPoint, Is.True, "0x5085D4 reads m_testSuccessLocation");
            });
        }

        // OnTriggerEnter 0x06000049 (0x508540): String.IsNullOrEmpty(m_triggerTag) at 0x508553
        // short-circuits to the store at 0x50856e; otherwise CompareTag 0x508565 must pass. No
        // path stores false, so a later rejected collider leaves the flag set.
        [Test]
        public void AutoPlayTriggerAcceptsItsTagOrAnyColliderWhenTheTagIsEmpty()
        {
            Owned.Run(owned =>
            {
                var trigger = owned.Host("Lucid autoplay trigger", false).AddComponent<AutoPlayTrigger>();
                var untagged = owned.Host("Lucid untagged collider", false).AddComponent<BoxCollider>();
                GameObject playerHost = owned.Host("Lucid player collider", false);
                playerHost.tag = "Player";
                var player = playerHost.AddComponent<BoxCollider>();

                Call(trigger, "OnTriggerEnter", untagged);
                Assert.That(trigger.IsTriggered, Is.False, "an untagged collider is rejected");
                Call(trigger, "OnTriggerEnter", player);
                Assert.That(trigger.IsTriggered, Is.True);
                Call(trigger, "OnTriggerEnter", untagged);
                Assert.That(trigger.IsTriggered, Is.True, "nothing clears the flag");

                foreach (string tag in new[] { "", null })
                {
                    var open = owned.Host("Lucid open autoplay trigger", false).AddComponent<AutoPlayTriggerConditional>();
                    Set(open, "m_triggerTag", tag);
                    Call(open, "OnTriggerEnter", untagged);
                    Assert.That(open.IsTriggered, Is.True, "an empty tag accepts any collider: " + (tag ?? "null"));
                }
            });
        }

        // Trigger 0x060038ec (0x6A6D20): setne on m_inverseBool, then xor with value (0x6a6d50)
        // before UnityEvent<bool>.Invoke. The table separates XOR from value, !value, the
        // flag alone, OR and value && !flag.
        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void BoolEventProxyForwardsTheValueXorTheInverseFlag(bool inverse, bool value, bool expected)
        {
            Owned.Run(owned =>
            {
                var proxy = owned.Host("Lucid bool proxy", false).AddComponent<BoolEventProxy>();
                var forwarded = new List<bool>();
                var unityEvent = new UnityEvent<bool>();
                unityEvent.AddListener(forwarded.Add);
                Set(proxy, "m_unityEvent", unityEvent);
                Set(proxy, "m_inverseBool", inverse);
                proxy.Trigger(value);
                Assert.That(forwarded, Is.EqualTo(new[] { expected }));
            });
        }

        // OnValidate 0x060038ed (0x6A6D70): CanValidate (always true in 1.10.1), the persistent
        // count read once at 0x6a6ddc, then one HLOutput.LogError of the literal for each
        // persistent target that equals this component (op_Equality 0x6a6e46).
        [Test]
        public void BoolEventProxyValidationReportsEachPersistentListenerTargetingItself()
        {
            AssertMembers(typeof(BoolEventProxy), 0x00100001,
                new[] { ("m_inverseBool", 0x0001), ("m_unityEvent", 0x0001) },
                new[] { ("Trigger", 0x0086), ("OnValidate", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(BoolEventProxy), 1, 2);

            Owned.Run(owned =>
            {
                var proxy = owned.Host("Lucid validated bool proxy", false).AddComponent<BoolEventProxy>();
                var other = owned.Host("Lucid other bool proxy", false).AddComponent<BoolEventProxy>();
                var unityEvent = new UnityEvent<bool>();
                Set(proxy, "m_unityEvent", unityEvent);
                using (var capture = new OwnedHLOutputErrorCapture())
                {
                    Call(proxy, "OnValidate");
                    Assert.That(capture.Errors, Is.Empty, "no persistent listener");

                    UnityEventTools.AddPersistentListener(unityEvent, other.Trigger);
                    Call(proxy, "OnValidate");
                    Assert.That(capture.Errors, Is.Empty, "another proxy is a valid target");

                    UnityEventTools.AddPersistentListener(unityEvent, proxy.Trigger);
                    UnityEventTools.AddPersistentListener(unityEvent, proxy.Trigger);
                    Call(proxy, "OnValidate");
                    Assert.That(capture.Errors, Is.EqualTo(new[] { SelfTargetError + "|0", SelfTargetError + "|0" }));
                }
            });
        }

        // UpdateIndexActive 0x0600394e (0x6AF8E0): (index + length) idiv length, one
        // SetActive(i == index) per element. Action_Next/Previous (0x6AF940/0x6AF9A0) add or
        // subtract one first. A reflection-set -4 of 3 (unreachable through the API) separates a
        // single wrap (-1, every object inactive) from a double modulo (the last object). idiv by a zero length faults.
        [Test]
        public void CycleGameObjectListShowsOnlyTheWrappedIndex()
        {
            AssertMembers(typeof(CycleGameObjectList), 0x00100001,
                new[] { ("m_objects", 0x0001), ("m_currentIndex", 0x0001) },
                new[] { ("OnEnable", 0x0081), ("Action_Next", 0x0086), ("Action_Previous", 0x0086), ("UpdateIndexActive", 0x0081),
                        ("AssignFromChildren", 0x0081), (".ctor", 0x1886) });
            Assert.That(typeof(CycleGameObjectList).GetMethod("AssignFromChildren", Declared).GetCustomAttributesData()
                .Single().ConstructorArguments[0].Value, Is.EqualTo("Assign From Children"));

            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid cycle list", false);
                GameObject[] children = { owned.Host("a", true, host.transform), owned.Host("b", true, host.transform), owned.Host("c", true, host.transform) };
                var list = host.AddComponent<CycleGameObjectList>();
                Call(list, "AssignFromChildren");
                Assert.That(Get<GameObject[]>(list, "m_objects"), Is.EqualTo(children), "children in sibling order");

                bool[] Active() => children.Select(child => child.activeSelf).ToArray();
                Call(list, "OnEnable");
                Assert.That(Active(), Is.EqualTo(new[] { true, false, false }));
                list.Action_Next();
                Assert.That(Active(), Is.EqualTo(new[] { false, true, false }));
                list.Action_Next();
                list.Action_Next();
                Assert.That(Active(), Is.EqualTo(new[] { true, false, false }), "next wraps to the first");
                list.Action_Previous();
                Assert.That(Active(), Is.EqualTo(new[] { false, false, true }), "previous wraps to the last");
                Assert.That(Get<int>(list, "m_currentIndex"), Is.EqualTo(2));

                Set(list, "m_currentIndex", 5);
                Call(list, "OnEnable");
                Assert.That(Get<int>(list, "m_currentIndex"), Is.EqualTo(2));
                Set(list, "m_currentIndex", -4);
                Call(list, "UpdateIndexActive");
                Assert.That(Get<int>(list, "m_currentIndex"), Is.EqualTo(-1), "one wrap only");
                Assert.That(Active(), Is.EqualTo(new[] { false, false, false }));

                Set(list, "m_objects", new GameObject[0]);
                Assert.Throws<DivideByZeroException>(() => list.Action_Next());
            });
        }

        // .ctor 0x06003959 (0x6AFF70) stores TimeCategory 0x34dab335 (Environment) and a new
        // UnityEvent. OnEnable 0x06003955 starts <ProcessActions>d__7 at state 0.
        [Test]
        public void DelayedActivatorDefaultsAndStartsItsRoutineOnEnable()
        {
            AssertMembers(typeof(DelayedActivator), 0x00100001,
                new[] { ("m_timeToStart", 0x0001), ("m_timeDelay", 0x0001), ("m_timeCategory", 0x0001), ("m_gameObjects", 0x0001),
                        ("m_components", 0x0001), ("m_onComplete", 0x0001) },
                new[] { ("OnEnable", 0x0081), ("ProcessActions", 0x0081), ("SetGameObjectActive", 0x0081), ("SetComponentActive", 0x0081),
                        (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(DelayedActivator), 2, 1);
            Assert.That(typeof(DelayedActivator).GetNestedTypes(Declared).Select(type => type.Name), Is.EqualTo(new[] { "<ProcessActions>d__7" }));
            Assert.That(AttributeTypes(Field(typeof(DelayedActivator), "m_timeToStart")), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(DelayedActivator), "m_timeDelay")), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));

            Owned.Run(owned =>
            {
                var activator = owned.Host("Lucid delayed activator", false).AddComponent<DelayedActivator>();
                Assert.That(Get<TimeCategory>(activator, "m_timeCategory"), Is.EqualTo(TimeCategory.Environment));
                Assert.That((int)TimeCategory.Environment, Is.EqualTo(0x34dab335));
                Assert.That(Get<UnityEvent>(activator, "m_onComplete"), Is.Not.Null);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Call(activator, "OnEnable");
            });
        }

        // <ProcessActions>d__7.MoveNext 0x0600395c (0x6AFFF0): state 0 yields WaitForFixedUpdate;
        // state 1 deactivates every object then every component (SetGameObjectActive and
        // SetComponentActive inlined, skipping Unity-null entries), then yields
        // WaitForFixedSeconds(m_timeToStart, m_timeCategory) only when m_timeToStart > 0
        // (jbe 0x6b01ca); the loop to max(count) activates object i then component i and yields
        // WaitForFixedSeconds(m_timeDelay, ...) unless m_timeDelay == 0 (jne/jp 0x6b0303);
        // m_onComplete.Invoke follows the loop.
        [Test]
        public void DelayedActivatorDeactivatesAllThenActivatesPairsWithTheConfiguredWaits()
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid delayed activator", false);
                GameObject first = owned.Host("first", true, host.transform);
                GameObject third = owned.Host("third", true, host.transform);
                var component = owned.Host("Lucid delayed component", false).AddComponent<CycleGameObjectList>();
                var activator = host.AddComponent<DelayedActivator>();
                Set(activator, "m_gameObjects", new[] { first, null, third });
                Set(activator, "m_components", new MonoBehaviour[] { component });
                Set(activator, "m_timeToStart", 0.5f);
                Set(activator, "m_timeDelay", 0.25f);
                Set(activator, "m_timeCategory", TimeCategory.Effects);
                int completed = 0;
                Get<UnityEvent>(activator, "m_onComplete").AddListener(() => ++completed);

                var routine = (IEnumerator)Call(activator, "ProcessActions");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.InstanceOf<WaitForFixedUpdate>());
                Assert.That(first.activeSelf && third.activeSelf && component.enabled, Is.True, "nothing changes before the fixed update");

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(new[] { first.activeSelf, third.activeSelf, component.enabled }, Is.EqualTo(new[] { false, false, false }));
                Assert.That(WaitArguments(routine.Current), Is.EqualTo((0.5f, (object)TimeCategory.Effects)));

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(new[] { first.activeSelf, third.activeSelf, component.enabled }, Is.EqualTo(new[] { true, false, true }));
                Assert.That(WaitArguments(routine.Current), Is.EqualTo((0.25f, (object)TimeCategory.Effects)));
                Assert.That(routine.MoveNext(), Is.True, "the null object still takes a gap");
                Assert.That(third.activeSelf, Is.False);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(third.activeSelf, Is.True);
                Assert.That(completed, Is.Zero);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(completed, Is.EqualTo(1));
            });
        }

        // The same MoveNext with m_timeToStart -1 and NaN (no start wait: both fail > 0) and a
        // NaN gap (waits: unordered compares unequal), against a zero gap that never waits.
        [TestCase(-1f, float.NaN, 4)]
        [TestCase(float.NaN, 0f, 2)]
        [TestCase(0f, 0f, 2)]
        public void DelayedActivatorWaitsOnlyForAPositiveStartAndANonZeroGap(float timeToStart, float timeDelay, int steps)
        {
            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid delayed activator", false);
                GameObject[] objects = { owned.Host("a", false, host.transform), owned.Host("b", false, host.transform) };
                var activator = host.AddComponent<DelayedActivator>();
                Set(activator, "m_gameObjects", objects);
                Set(activator, "m_components", new MonoBehaviour[0]);
                Set(activator, "m_timeToStart", timeToStart);
                Set(activator, "m_timeDelay", timeDelay);
                var routine = (IEnumerator)Call(activator, "ProcessActions");
                int yielded = 0;
                while (routine.MoveNext())
                {
                    if (yielded++ > 0) Assert.That(WaitArguments(routine.Current).seconds, Is.EqualTo(timeDelay));
                }
                Assert.That(yielded, Is.EqualTo(steps - 1), "fixed update plus one wait per non-zero gap");
                Assert.That(objects.All(gameObject => gameObject.activeSelf), Is.True);
            });
        }

        // DeleteAllChildren 0x06003962 (0x6B0410) keeps its [Conditional("UNITY_EDITOR")] body:
        // from childCount - 1 down to 0 it destroys (DestroyImmediate) each child whose name
        // contains "(Clone)", or every child when forceDelete is set. ForceDelete
        // 0x06003960/0x06003961 reads and writes m_forceDelete only.
        [Test]
        public void DeleteChildrenRemovesClonesOrEveryChildWhenForced()
        {
            AssertMembers(typeof(DeleteChildren), 0x00100001,
                new[] { ("m_forceDelete", 0x0001) },
                new[] { ("get_ForceDelete", 0x0886), ("set_ForceDelete", 0x0886), ("DeleteAllChildren", 0x0086), (".ctor", 0x1886) });
            Assert.That(typeof(DeleteChildren).GetMethod("DeleteAllChildren").GetCustomAttributesData().Single().ConstructorArguments[0].Value,
                Is.EqualTo("UNITY_EDITOR"));
            Assert.That(AttributeTypes(Field(typeof(DeleteChildren), "m_forceDelete")), Is.EqualTo(new[] { typeof(HideInInspector), typeof(SerializeField) }));

            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid delete children", false);
                owned.Host("a(Clone)", true, host.transform);
                GameObject kept = owned.Host("kept", true, host.transform);
                owned.Host("b (Clone) 1", true, host.transform);
                var deleter = host.AddComponent<DeleteChildren>();
                deleter.ForceDelete = true;
                Assert.That(Get<bool>(deleter, "m_forceDelete"), Is.True);
                Assert.That(deleter.ForceDelete, Is.True);

                deleter.DeleteAllChildren(false);
                Assert.That(host.transform.childCount, Is.EqualTo(1), "the stored ForceDelete is not consulted");
                Assert.That(host.transform.GetChild(0).gameObject, Is.SameAs(kept));
                deleter.DeleteAllChildren(true);
                Assert.That(host.transform.childCount, Is.Zero);
            });
        }

        // .ctor 0x06003967 stores TimeCategory 0x34dab335; Awake 0x06003964 calls Destroy only
        // with m_onAwake. Destroy 0x06003965 (0x6B05D0) starts DelayedDestroy only when
        // m_delaySeconds > 0 (jbe 0x6b0604) and no routine is stored, otherwise Object.Destroy
        // (refused in Edit Mode, so the log shows which branch ran).
        [Test]
        public void DestroyObjectDestroysAtOnceUnlessAPositiveDelayStartsTheRoutine()
        {
            AssertMembers(typeof(DestroyObject), 0x00100001,
                new[] { ("m_onAwake", 0x0001), ("m_delaySeconds", 0x0001), ("m_timeCategory", 0x0001), ("m_delayedDestroyRoutine", 0x0001) },
                new[] { ("Awake", 0x0081), ("Destroy", 0x0086), ("DelayedDestroy", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(DestroyObject), 2, 1);
            Assert.That(typeof(DestroyObject).GetNestedTypes(Declared).Select(type => type.Name), Is.EqualTo(new[] { "<DelayedDestroy>d__6" }));
            var hideIf = Field(typeof(DestroyObject), "m_timeCategory").GetCustomAttributesData().Single(data => data.AttributeType == typeof(HideIfAttribute));
            Assert.That(hideIf.ConstructorArguments.Select(argument => argument.Value), Is.EqualTo(new object[] { "m_delaySeconds", 0f }));

            Owned.Run(owned =>
            {
                GameObject host = owned.Host("Lucid destroy object", false);
                var destroyer = host.AddComponent<DestroyObject>();
                Assert.That(Get<TimeCategory>(destroyer, "m_timeCategory"), Is.EqualTo(TimeCategory.Environment));

                Call(destroyer, "Awake");
                LogAssert.NoUnexpectedReceived();

                Set(destroyer, "m_onAwake", true);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                Call(destroyer, "Awake");

                foreach (float delay in new[] { 0f, -1f, float.NaN })
                {
                    Set(destroyer, "m_delaySeconds", delay);
                    LogAssert.Expect(LogType.Error, RefusedDestroy);
                    destroyer.Destroy();
                }

                Set(destroyer, "m_delaySeconds", 0.75f);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                destroyer.Destroy();
                Assert.That(Get<Coroutine>(destroyer, "m_delayedDestroyRoutine"), Is.Null, "the refused start returned null");
                Assert.That(host != null, Is.True);
            });
        }

        // Destroy 0x6B05D0 starts the routine only while the field is null (cmp [rbx+0x30], 0;
        // je at 0x6b060b): with a routine stored, a positive delay still reaches Object.Destroy
        // and the field keeps the stored routine.
        [Test]
        public void DestroyObjectDestroysAtOnceWhileARoutineIsStored()
        {
            Owned.Run(owned =>
            {
                var destroyer = owned.Host("Lucid destroy object", false).AddComponent<DestroyObject>();
                // A genuine engine handle for a coroutine that never finishes, started on an active
                // owned host (Awake does not run in Edit Mode, so the host registers nothing).
                Coroutine stored = owned.Host("Lucid coroutine runner").AddComponent<CoroutineUtils>().StartCoroutine(Forever());
                Set(destroyer, "m_delayedDestroyRoutine", stored);
                Set(destroyer, "m_delaySeconds", 0.75f);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                destroyer.Destroy();
                LogAssert.NoUnexpectedReceived();
                Assert.That(Get<Coroutine>(destroyer, "m_delayedDestroyRoutine"), Is.SameAs(stored));
            });
        }

        // <DelayedDestroy>d__6.MoveNext 0x0600396a (0x6B0780): state 0 yields
        // WaitForFixedSeconds(m_delaySeconds, m_timeCategory); state 1 calls Object.Destroy on
        // the game object and ends.
        [Test]
        public void DestroyObjectRoutineWaitsThenDestroysTheGameObject()
        {
            Owned.Run(owned =>
            {
                var destroyer = owned.Host("Lucid destroy object", false).AddComponent<DestroyObject>();
                Set(destroyer, "m_delaySeconds", 1.5f);
                Set(destroyer, "m_timeCategory", TimeCategory.PlayerMovement);
                var routine = (IEnumerator)Call(destroyer, "DelayedDestroy");
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(WaitArguments(routine.Current), Is.EqualTo((1.5f, (object)TimeCategory.PlayerMovement)));
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                Assert.That(routine.MoveNext(), Is.False);
            });
        }

        // TypeDef 0x02000a03: Serializable pseudo-flag (0x2000), TEnum with the struct and
        // new() flags (0x0018) over IConvertible and ValueType, TValue unconstrained. The
        // Int32Enum getters 0x1183990/0x11839A0 read the fields at 0x10 and 0x14.
        [Test]
        public void EnumValuePairReturnsItsOwnKeyAndValue()
        {
            Type definition = typeof(EnumValuePair<,>);
            AssertMembers(definition, 0x00102001,
                new[] { ("m_key", 0x0001), ("m_value", 0x0001) },
                new[] { ("get_Key", 0x0886), ("get_Value", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(definition, 2, 1);
            Type[] parameters = definition.GetGenericArguments();
            Assert.That(parameters.Select(parameter => parameter.Name), Is.EqualTo(new[] { "TEnum", "TValue" }));
            Assert.That((int)parameters[0].GenericParameterAttributes, Is.EqualTo(0x0018));
            Assert.That(parameters[0].GetGenericParameterConstraints(), Is.EquivalentTo(new[] { typeof(IConvertible), typeof(System.ValueType) }));
            Assert.That((int)parameters[1].GenericParameterAttributes, Is.Zero);
            Assert.That(parameters[1].GetGenericParameterConstraints(), Is.Empty);

            var pair = new EnumValuePair<TimeCategory, int>();
            Assert.That(pair.Key, Is.EqualTo(default(TimeCategory)));
            Set(pair, "m_key", TimeCategory.Camera);
            Set(pair, "m_value", 7);
            Assert.That(pair.Key, Is.EqualTo(TimeCategory.Camera));
            Assert.That(pair.Value, Is.EqualTo(7));
        }
    }
}
