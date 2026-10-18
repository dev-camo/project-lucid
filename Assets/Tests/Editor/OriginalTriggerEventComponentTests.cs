using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime UnityEventTrigger (TypeDef 0x02000a3d, x86-64 0x6B94A0..0x6B97D0 with its
    // parameters class 0x02000a3e and iterator 0x02000a3f), VisibilityEventGroup (0x02000a41,
    // 0x6B99D0..0x6B9D17), TriggerCascade (0x02000a37, 0x6B82E0..0x6B8367), TriggerUITransition
    // (0x0200044d, 0x7A65D0..0x7A68F7) and Transition_Ability (0x02000409, 0x78EAC0..0x78EAF0 with
    // 0x0200040a). The hooks are called directly; Edit Mode sends no Awake or OnEnable.
    public sealed class OriginalTriggerEventComponentTests
    {
        private static readonly Type ParametersType = typeof(UnityEventTrigger).GetNestedType("UnityEventParameters", BindingFlags.NonPublic);

        private static UnityEvent Logged(List<string> log, string label)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(label));
            return unityEvent;
        }

        private static object Parameters(UnityEvent triggerEvent, float delaySeconds)
        {
            object parameters = Activator.CreateInstance(ParametersType);
            Set(parameters, "TriggerEvent", triggerEvent);
            Set(parameters, "DelaySeconds", delaySeconds);
            return parameters;
        }

        // Metadata of the four TypeDefs and the iterator, whose ordinal 11 follows the nested
        // parameters class, the five fields and five methods declared before DelayCoroutine.
        [Test]
        public void UnityEventTriggerDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(UnityEventTrigger), 0x00100001,
                new[] { ("m_onStart", 0x0001), ("m_onEnabled", 0x0001), ("m_onDisabled", 0x0001), ("m_onDestroy", 0x0001), ("m_coroutineSystem", 0x0021) },
                new[] { ("Start", 0x0081), ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnDestroy", 0x0081), ("TriggerEvent", 0x0081),
                        ("DelayCoroutine", 0x0091), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UnityEventTrigger), 2, 1);
            Assert.That(ParametersType, Is.Not.Null);
            AssertMembers(ParametersType, 0x00102003,
                new[] { ("TriggerEvent", 0x0006), ("DelaySeconds", 0x0006), ("DelayCategory", 0x0006) }, new[] { (".ctor", 0x1886) });
            Type iterator = typeof(UnityEventTrigger).GetNestedType("<DelayCoroutine>d__11", Declared);
            Assert.That(iterator, Is.Not.Null);
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Assert.That(iterator.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(field => (field.Name, (int)field.Attributes)),
                Is.EqualTo(new[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("eventParameters", 0x0006) }));
        }

        // The parameters constructor 0x6B96C0 stores TimeCategory.System (0xC64A29F2) and leaves the
        // delay at zero. Start 0x6B94A0, OnEnable 0x6B9580, OnDisable 0x6B9590 and OnDestroy 0x6B95A0
        // each tail-call TriggerEvent with their own parameters (+0x20, +0x28, +0x30, +0x38); a zero
        // delay raises the event at once (0x6B94E8).
        [Test]
        public void EachLifecycleHookRaisesItsOwnEventAtOnce()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CoroutineUtils>();
                object defaults = Activator.CreateInstance(ParametersType);
                Assert.That(Get<TimeCategory>(defaults, "DelayCategory"), Is.EqualTo(TimeCategory.System));
                Assert.That(Get<float>(defaults, "DelaySeconds"), Is.Zero);

                var trigger = owned.Host("Lucid unity event trigger", false).AddComponent<UnityEventTrigger>();
                var log = new List<string>();
                string[] fields = { "m_onStart", "m_onEnabled", "m_onDisabled", "m_onDestroy" };
                string[] hooks = { "Start", "OnEnable", "OnDisable", "OnDestroy" };
                foreach (string field in fields) Set(trigger, field, Parameters(Logged(log, field), 0f));
                for (int index = 0; index < hooks.Length; index++)
                {
                    log.Clear();
                    Call(trigger, hooks[index]);
                    Assert.That(log, Is.EqualTo(new[] { fields[index] }), hooks[index]);
                }
            });
        }

        // TriggerEvent 0x6B94B0 raises immediately only for a delay that compares equal to zero (0x6B94E1
        // ucomiss, so negative zero does); NaN and every other delay go to the CoroutineUtils system,
        // and are dropped when it is not registered (IsNull, 0x6B9505).
        [Test]
        public void DelayedEventsAreDroppedWithoutTheCoroutineSystem()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CoroutineUtils>();
                var trigger = owned.Host("Lucid unity event trigger", false).AddComponent<UnityEventTrigger>();
                var log = new List<string>();
                foreach (var row in new[]
                {
                    (delay: 0f, raised: true),
                    (delay: -0f, raised: true),
                    (delay: 0.5f, raised: false),
                    (delay: -1f, raised: false),
                    (delay: float.NaN, raised: false),
                })
                {
                    log.Clear();
                    Set(trigger, "m_onEnabled", Parameters(Logged(log, "raised"), row.delay));
                    Assert.DoesNotThrow(() => Call(trigger, "OnEnable"), row.ToString());
                    Assert.That(log.Count, Is.EqualTo(row.raised ? 1 : 0), row.ToString());
                }
            });
        }

        // DelayCoroutine 0x6B95B0 with its iterator 0x6B96F0: the first step yields the
        // TimeScaledUtilities_SDT fixed-seconds wait for the delay and category (0x6B9747) without
        // raising anything; the second raises the event and ends.
        [Test]
        public void TheDelayCoroutineWaitsForTheDelayBeforeRaisingTheEvent()
        {
            var log = new List<string>();
            object parameters = Parameters(Logged(log, "raised"), 2.5f);
            Set(parameters, "DelayCategory", TimeCategory.Effects);
            var iterator = (IEnumerator)typeof(UnityEventTrigger).GetMethod("DelayCoroutine", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { parameters });
            Assert.That(log, Is.Empty, "creating the iterator runs nothing");
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(WaitArguments(iterator.Current), Is.EqualTo((2.5f, (object)TimeCategory.Effects)));
            Assert.That(log, Is.Empty, "the event waits for the delay");
            Assert.That(iterator.MoveNext(), Is.False);
            Assert.That(log, Is.EqualTo(new[] { "raised" }));
            Assert.That(iterator.MoveNext(), Is.False, "a finished iterator stays finished");
            Assert.That(log.Count, Is.EqualTo(1));
        }

        private static EventOnVisibility Visibility(Owned owned, string name, bool active = false, Transform parent = null)
        {
            GameObject host = owned.Host(name, active, parent);
            host.AddComponent<MeshRenderer>();
            var visibility = host.AddComponent<EventOnVisibility>();
            Set(visibility, "m_onVisibleEvent", new UnityEvent());
            Set(visibility, "m_onInvisibleEvent", new UnityEvent());
            return visibility;
        }

        // Metadata, including the context-menu entry of GetRenderersInChildren.
        [Test]
        public void VisibilityEventGroupDeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(VisibilityEventGroup), 0x00100001,
                new[] { ("m_visibleEvents", 0x0001), ("m_onAnyVisibleEvent", 0x0001), ("m_onAllInvisibleEvent", 0x0001), ("m_anyWasVisible", 0x0001) },
                new[] { ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnVisible", 0x0081), ("OnHide", 0x0081), ("GetRenderersInChildren", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(VisibilityEventGroup), 1, 2);
            var menu = (ContextMenu)typeof(VisibilityEventGroup).GetMethod("GetRenderersInChildren", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttributes(typeof(ContextMenu), false).Single();
            Assert.That(menu.menuItem, Is.EqualTo("Get EventOnVisibility in children"));
        }

        // OnEnable 0x6B99D0 adds OnVisible and OnHide to every listed renderer's visible and invisible
        // events, OnDisable 0x6B9AD0 removes them. OnVisible 0x6B9BD0 raises the any-visible event only
        // while m_anyWasVisible is clear. OnHide 0x6B9BF0 polls every IsVisible (+0x30) and, when none
        // is set, raises the all-invisible event and clears m_anyWasVisible, whatever that flag was.
        [Test]
        public void TheGroupRaisesWhenTheFirstBecomesVisibleAndWhenNoneIsLeft()
        {
            Owned.Run(owned =>
            {
                EventOnVisibility first = Visibility(owned, "Lucid visibility 0");
                EventOnVisibility second = Visibility(owned, "Lucid visibility 1");
                var group = owned.Host("Lucid visibility group", false).AddComponent<VisibilityEventGroup>();
                var log = new List<string>();
                Set(group, "m_visibleEvents", new[] { first, second });
                Set(group, "m_onAnyVisibleEvent", Logged(log, "any"));
                Set(group, "m_onAllInvisibleEvent", Logged(log, "allInvisible"));

                first.OnVisibleEvent.Invoke();
                first.OnInvisibleEvent.Invoke();
                Assert.That(log, Is.Empty, "nothing is subscribed before OnEnable");

                Call(group, "OnEnable");
                Set(first, "<IsVisible>k__BackingField", true);
                first.OnVisibleEvent.Invoke();
                Assert.That(log, Is.EqualTo(new[] { "any" }));
                Assert.That(Get<bool>(group, "m_anyWasVisible"), Is.True);
                Set(second, "<IsVisible>k__BackingField", true);
                second.OnVisibleEvent.Invoke();
                Assert.That(log, Is.EqualTo(new[] { "any" }), "only the first visible renderer raises it");

                Set(first, "<IsVisible>k__BackingField", false);
                first.OnInvisibleEvent.Invoke();
                Assert.That(log, Is.EqualTo(new[] { "any" }), "the second renderer is still visible");
                Set(second, "<IsVisible>k__BackingField", false);
                second.OnInvisibleEvent.Invoke();
                Assert.That(log, Is.EqualTo(new[] { "any", "allInvisible" }));
                Assert.That(Get<bool>(group, "m_anyWasVisible"), Is.False);
                first.OnVisibleEvent.Invoke();
                Assert.That(log, Is.EqualTo(new[] { "any", "allInvisible", "any" }), "the flag was cleared, so it raises again");

                log.Clear();
                Set(group, "m_anyWasVisible", false);
                Set(first, "<IsVisible>k__BackingField", false);
                first.OnInvisibleEvent.Invoke();
                Assert.That(log, Is.EqualTo(new[] { "allInvisible" }), "OnHide does not look at m_anyWasVisible");

                Call(group, "OnDisable");
                log.Clear();
                first.OnVisibleEvent.Invoke();
                first.OnInvisibleEvent.Invoke();
                second.OnVisibleEvent.Invoke();
                Assert.That(log, Is.Empty, "OnDisable removed both listeners from every renderer");

                Set(group, "m_visibleEvents", new EventOnVisibility[0]);
                Call(group, "OnHide");
                Assert.That(log, Is.EqualTo(new[] { "allInvisible" }), "no renderers means none is visible");
            });
        }

        // GetRenderersInChildren 0x6B9CC0 replaces the list with GetComponentsInChildren<EventOnVisibility>()
        // (no inactive objects): the object's own component, a child and a grandchild, not an inactive child.
        [Test]
        public void TheRenderersAreCollectedFromTheActiveChildren()
        {
            Owned.Run(owned =>
            {
                EventOnVisibility own = Visibility(owned, "Lucid visibility root", true);
                var group = own.gameObject.AddComponent<VisibilityEventGroup>();
                EventOnVisibility child = Visibility(owned, "Lucid visibility child", true, own.transform);
                EventOnVisibility grandchild = Visibility(owned, "Lucid visibility grandchild", true, child.transform);
                Visibility(owned, "Lucid visibility inactive child", false, own.transform);
                Call(group, "GetRenderersInChildren");
                Assert.That(Get<EventOnVisibility[]>(group, "m_visibleEvents"), Is.EquivalentTo(new[] { own, child, grandchild }));
            });
        }

        // TriggerCascade metadata. Trigger 0x6B82E0 asks CascadeObjectPool.GetCascadeObjectPoolByType
        // (0x728DC0) first, which is null while no PrefabPoolManager is registered, and calls
        // TriggerCascade on the result with no null test.
        [Test]
        public void TriggerCascadeDeclarationsAndTheMissingPoolFault()
        {
            AssertMembers(typeof(TriggerCascade), 0x00100001,
                new[] { ("m_cascadeObjectType", 0x0001), ("m_amount", 0x0001), ("m_origin", 0x0001), ("m_initialVelocity", 0x0001) },
                new[] { ("Trigger", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(TriggerCascade), 2, 1);

            Owned.Run(owned =>
            {
                owned.LeaseRow<PrefabPoolManager>();
                GameObject host = owned.Host("Lucid trigger cascade", false);
                var cascade = host.AddComponent<TriggerCascade>();
                Set(cascade, "m_origin", host.transform);
                Assert.Throws<NullReferenceException>(() => cascade.Trigger());
            });
        }

        // TriggerUITransition metadata. OnEnable 0x7A65D0 fetches the App before the
        // UIScreenTransitionManager (0x7A668A, then 0x7A66A3), so a missing App is the fault reported.
        // The App storage and the queued transition need the application and are not set up here.
        [Test]
        public void TriggerUITransitionDeclarationsAndTheMissingAppFault()
        {
            AssertMembers(typeof(TriggerUITransition), 0x00100001,
                new[] { ("m_transitionContainer", 0x0001) }, new[] { ("OnEnable", 0x01c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(TriggerUITransition), 1, 2);

            Owned.Run(owned =>
            {
                owned.LeaseRow<App>();
                owned.LeaseRow<UIScreenTransitionManager>();
                var transition = owned.Host("Lucid trigger UI transition", false).AddComponent<TriggerUITransition>();
                var missing = Assert.Throws<NullReferenceException>(() => Call(transition, "OnEnable"));
                Assert.That(missing.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<App>() + "' is null"));
            });
        }

        // Transition_Ability metadata. The constructor 0x78EAC0 registers the transition with its
        // machine through FSMTransition and copies the arguments' AbilityType (+0x10) to +0x1C.
        [Test]
        public void TransitionAbilityKeepsTheAbilityTypeOfItsJsonArguments()
        {
            AssertMembers(typeof(Transition_Ability), 0x00100081,
                new[] { ("m_abilityType", 0x0004) }, new[] { (".ctor", 0x1884) });
            AssertIl2CppOptions(typeof(Transition_Ability), 1, 2);
            Assert.That(typeof(Transition_Ability).BaseType, Is.EqualTo(typeof(FSMTransition)));
            Type arguments = TransitionAbilityProbe.ArgumentsType;
            Assert.That(arguments.DeclaringType, Is.EqualTo(typeof(Transition_Ability)));
            AssertMembers(arguments, 0x00102004, new[] { ("AbilityType", 0x0006) }, new[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(arguments, 2, 1);
            Assert.That(AttributeTypes(arguments).Select(type => type.Name), Does.Contain("FiniteStateMachineJsonCtorArgsAttribute"));

            var machine = new FiniteStateMachine("Lucid-transition-ability-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);
            foreach (ActorAbilityType ability in new[] { ActorAbilityType.Boss_BodySlam, ActorAbilityType.None })
            {
                string name = "LucidAbilityTransition" + ability;
                var transition = new TransitionAbilityProbe(machine, name, ability);
                Assert.That(transition.Type, Is.EqualTo(ability));
                Assert.That(transition.FSM, Is.SameAs(machine));
                Assert.That(machine.TransitionExists(name, out IFSMTransition registered), Is.True);
                Assert.That(registered, Is.SameAs(transition));
            }
        }
    }
}
