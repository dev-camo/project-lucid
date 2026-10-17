using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.OriginalInOutTestSupport;
using AnimState = Hardlight.HLUIAnimationInOut.AnimState;
using PlayDirection = Hardlight.HLUIAnimationInOut.PlayDirection;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.HLUIAnimationInOut (TypeDef 0x020000ab, dump.cs 11998)
    // with PlayDirection, AnimState and <AutoAnimateOut>d__36, against the x86-64 bodies of the
    // pinned 6c89 dylib. The type is abstract; its non-virtual and inherited bodies are driven
    // through the sealed HLAlphaInOut, which does not override Awake, OnDisable, Animate,
    // AutoAnimateOut or IndependentTimeDelta. Unity never calls Awake or OnEnable on these plain
    // MonoBehaviours in EditMode, so every lifecycle method is invoked explicitly.
    public sealed class OriginalHLUIAnimationInOutTests
    {
        // dump.cs 11998: abstract, Il2CppSetOption(NullChecks) before Il2CppSetOption(ArrayBoundsChecks),
        // the two literal constants, the CurrentState backing field at +0x20 ahead of the sixteen
        // [SerializeField] fields (+0x24..+0x70) and the seven runtime fields (+0x78..+0xA8). The
        // member order fixes <AutoAnimateOut>d__36: two nested enums, 26 fields, the property and its
        // two accessors, then Awake..Animate put AutoAnimateOut at ordinal 36.
        [Test]
        public void BaseRetainsWholeDeclarationAndMemberOrder()
        {
            Type type = typeof(HLUIAnimationInOut);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That((int)type.Attributes, Is.EqualTo(0x100081), "public abstract beforefieldinit");
            CheckOptions(type, 1, 2);
            Assert.That(type.GetCustomAttributesData().Count, Is.EqualTo(2));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "Zero", "One", "<CurrentState>k__BackingField", "m_initialState", "m_animateToStateOnEnable",
                "m_useUnscaledTime", "m_forceActivateGameObjectBeforeAnimation", "m_shouldDelayAnimateIn",
                "m_animateInDelaySeconds", "m_shouldDelayAnimateOut", "m_animateOutDelaySeconds",
                "m_autoAnimateOutAfterTimeout", "m_autoAnimateOutTimeoutSeconds", "m_onAnimateIn",
                "m_onAnimateInStart", "m_onAnimateInComplete", "m_onAnimateOut", "m_onAnimateOutStart",
                "m_onAnimateOutComplete", "m_playDirection", "m_trackedAnimation", "m_trackedAutoAnimateOut",
                "m_autoAnimateOutWait", "m_animateInDelayWait", "m_animateOutDelayWait", "m_normalisedInOutProgress",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(float), typeof(float), typeof(AnimState), typeof(AnimState), typeof(AnimState),
                typeof(bool), typeof(bool), typeof(bool), typeof(float), typeof(bool), typeof(float),
                typeof(bool), typeof(float), typeof(UnityEvent), typeof(UnityEvent), typeof(UnityEvent),
                typeof(UnityEvent), typeof(UnityEvent), typeof(UnityEvent), typeof(PlayDirection),
                typeof(Coroutine), typeof(Coroutine), typeof(WaitForSeconds), typeof(WaitForSeconds),
                typeof(WaitForSeconds), typeof(float),
            }));
            Assert.That(fields[0].IsLiteral && fields[0].IsFamily && (float)fields[0].GetRawConstantValue() == 0f, Is.True, "protected const float Zero = 0");
            Assert.That(fields[1].IsLiteral && fields[1].IsFamily && (float)fields[1].GetRawConstantValue() == 1f, Is.True, "protected const float One = 1");
            Assert.That((int)fields[2].Attributes, Is.EqualTo(1), "private backing field");
            Assert.That(fields[2].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            for (int i = 3; i < fields.Length; i++)
            {
                Assert.That((int)fields[i].Attributes, Is.EqualTo(4), fields[i].Name + " is protected");
                Type[] expected = i <= 18 ? new[] { typeof(SerializeField) } : Type.EmptyTypes;
                Assert.That(fields[i].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected), fields[i].Name);
            }

            PropertyInfo state = type.GetProperties(Declared).Single();
            Assert.That(state.Name, Is.EqualTo("CurrentState"));
            Assert.That(state.PropertyType, Is.EqualTo(typeof(AnimState)));

            string[] names =
            {
                "get_CurrentState", "set_CurrentState", "Awake", "OnEnable", "OnDisable", "ScheduleAutoAnimateOut",
                "Animate", "AutoAnimateOut", "RunAnimation", "AnimateIn", "AnimateOut", "ForceFinishAnimation",
                "SetFinalState", "IndependentTimeDelta",
            };
            // public/family, virtual|newslot, abstract and specialname from the dump modifiers.
            int[] flags = { 0x886, 0x884, 0x1C4, 0x1C4, 0x1C4, 0x84, 0x1C4, 0x1C4, 0x5C4, 0x5C6, 0x5C6, 0x5C6, 0x5C4, 0x84 };
            Type[] returns =
            {
                typeof(AnimState), typeof(void), typeof(void), typeof(void), typeof(void), typeof(void), typeof(void),
                typeof(IEnumerator), typeof(IEnumerator), typeof(void), typeof(void), typeof(void), typeof(void), typeof(float),
            };
            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(names));
            for (int i = 0; i < methods.Length; i++)
            {
                Assert.That((int)methods[i].Attributes, Is.EqualTo(flags[i]), names[i]);
                Assert.That(methods[i].ReturnType, Is.EqualTo(returns[i]), names[i]);
            }
            Assert.That(methods[1].GetParameters().Single().Name, Is.EqualTo("value"));
            ParameterInfo animatingIn = methods[12].GetParameters().Single();
            Assert.That(animatingIn.Name, Is.EqualTo("animatingIn"));
            Assert.That(animatingIn.ParameterType, Is.EqualTo(typeof(bool)));
            Assert.That(methods[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            Assert.That(methods[1].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            CustomAttributeData iterator = methods[7].GetCustomAttributesData().Single();
            Assert.That(iterator.AttributeType, Is.EqualTo(typeof(IteratorStateMachineAttribute)));
            Assert.That(((Type)iterator.ConstructorArguments.Single().Value).Name, Is.EqualTo("<AutoAnimateOut>d__36"));
            foreach (MethodInfo method in methods.Skip(2).Where(m => m.Name != "AutoAnimateOut"))
                Assert.That(method.GetCustomAttributesData(), Is.Empty, method.Name);

            ConstructorInfo constructor = type.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(0x1884), "protected .ctor()");
            Assert.That(constructor.GetParameters(), Is.Empty);

            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "PlayDirection", "AnimState", "<AutoAnimateOut>d__36" }));
        }

        // dump.cs 11995/11996: the literal values of both nested enums.
        [Test]
        public void NestedEnumsRetainTheirLiterals()
        {
            Assert.That(Enum.GetNames(typeof(PlayDirection)), Is.EqualTo(new[] { "None", "In", "Out" }));
            Assert.That(Enum.GetValues(typeof(PlayDirection)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(Enum.GetNames(typeof(AnimState)), Is.EqualTo(new[] { "None", "In", "AnimatingIn", "Out", "AnimatingOut" }));
            Assert.That(Enum.GetValues(typeof(AnimState)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(typeof(PlayDirection).IsNestedPublic && typeof(AnimState).IsNestedPublic, Is.True);
            Assert.That(Enum.GetUnderlyingType(typeof(AnimState)), Is.EqualTo(typeof(int)));
        }

        // dump.cs 11997: the natural compiler iterator with no hoisted locals in any configuration.
        [Test]
        public void AutoAnimateOutIteratorRetainsTheGeneratedShape()
        {
            Type iterator = typeof(HLUIAnimationInOut).GetNestedType("<AutoAnimateOut>d__36", BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x100103), "nested private sealed beforefieldinit");
            Assert.That(iterator.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            CollectionAssert.AreEquivalent(new[] { typeof(IEnumerator<object>), typeof(IEnumerator), typeof(IDisposable) }, iterator.GetInterfaces());
            FieldInfo[] fields = iterator.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "<>1__state", "<>2__current", "<>4__this" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(int), typeof(object), typeof(HLUIAnimationInOut) }));
            ConstructorInfo constructor = iterator.GetConstructors(Declared).Single();
            Assert.That(constructor.GetParameters().Single().Name, Is.EqualTo("<>1__state"));
            Assert.That(constructor.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(DebuggerHiddenAttribute) }));
        }

        // .ctor 0x0600050c @0x1B616D0 is only MonoBehaviour..ctor: every field keeps its zero default.
        [Test]
        public void ConstructionLeavesEveryFieldAtItsDefault()
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-defaults", false).AddComponent<HLAlphaInOut>();
                Assert.That(alpha.CurrentState, Is.EqualTo(AnimState.None));
                foreach (FieldInfo field in typeof(HLUIAnimationInOut).GetFields(Declared).Where(f => !f.IsLiteral && !f.Name.StartsWith("m_on", StringComparison.Ordinal)))
                {
                    object value = field.GetValue(alpha);
                    object zero = field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null;
                    Assert.That(value, Is.EqualTo(zero), field.Name);
                }
            }
        }

        // Awake 0x06000500 @0x1B61AF0: three WaitForSeconds built from +0x40 (timeout) into +0x90,
        // +0x30 (in delay) into +0x98 and +0x38 (out delay) into +0xA0. Distinct seconds catch a swap;
        // a second Awake builds new instances, and later edits to the seconds are not picked up.
        [Test]
        public void AwakeBuildsTheThreeWaitsFromTheirOwnSeconds()
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-awake", false).AddComponent<HLAlphaInOut>();
                Set(alpha, "m_autoAnimateOutTimeoutSeconds", 1.5f);
                Set(alpha, "m_animateInDelaySeconds", 0.25f);
                Set(alpha, "m_animateOutDelaySeconds", 0.75f);
                Call(alpha, "Awake");
                var timeout = Get<WaitForSeconds>(alpha, "m_autoAnimateOutWait");
                var inDelay = Get<WaitForSeconds>(alpha, "m_animateInDelayWait");
                var outDelay = Get<WaitForSeconds>(alpha, "m_animateOutDelayWait");
                Assert.That(Seconds(timeout), Is.EqualTo(1.5f));
                Assert.That(Seconds(inDelay), Is.EqualTo(0.25f));
                Assert.That(Seconds(outDelay), Is.EqualTo(0.75f));

                Set(alpha, "m_animateInDelaySeconds", 9f);
                Assert.That(Seconds(Get<WaitForSeconds>(alpha, "m_animateInDelayWait")), Is.EqualTo(0.25f));
                Call(alpha, "Awake");
                Assert.That(Get<WaitForSeconds>(alpha, "m_animateInDelayWait"), Is.Not.SameAs(inDelay));
                Assert.That(Seconds(Get<WaitForSeconds>(alpha, "m_animateInDelayWait")), Is.EqualTo(9f));
                Assert.That(alpha.CurrentState, Is.EqualTo(AnimState.None), "Awake does not touch the state");
            }
        }

        // OnEnable 0x06000501 @0x1B61390 (inlined after HLAlphaInOut's pre-step, which only acts on an
        // initial In or Out): CurrentState = m_initialState; if m_animateToStateOnEnable differs, In
        // calls AnimateIn (slot 10) and Out calls AnimateOut (slot 11); None and the two Animating
        // values do nothing. The owner is inactive, so the AnimateIn/AnimateOut bodies stop after
        // storing m_playDirection and Animate's activeInHierarchy guard starts nothing.
        // From an Animating initial state the subclass guards still apply: AnimateIn returns at once
        // for AnimatingIn and AnimateOut for AnimatingOut.
        [TestCase(0, 0, 0)]
        [TestCase(0, 1, 1)]
        [TestCase(0, 3, 2)]
        [TestCase(0, 2, 0)]
        [TestCase(0, 4, 0)]
        [TestCase(2, 1, 0)]
        [TestCase(2, 3, 2)]
        [TestCase(4, 1, 1)]
        [TestCase(4, 3, 0)]
        [TestCase(2, 2, 0)]
        public void OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(int initial, int target, int direction)
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-onenable", false).AddComponent<HLAlphaInOut>();
                Set(alpha, "m_initialState", (AnimState)initial);
                Set(alpha, "m_animateToStateOnEnable", (AnimState)target);
                Call(alpha, "set_CurrentState", AnimState.Out);
                Call(alpha, "OnEnable");
                Assert.That(alpha.CurrentState, Is.EqualTo((AnimState)initial));
                Assert.That(Get(alpha, "m_playDirection"), Is.EqualTo((PlayDirection)direction));
                Assert.That(Get(alpha, "m_trackedAnimation"), Is.Null);
            }
        }

        // 0x1B6139A stores CurrentState before the target comparison (0x1B6139D compares with the
        // initial state), so a stale In left from before does not block AnimateIn; an implementation
        // comparing with the previous state, or dispatching before the store, keeps no direction.
        [Test]
        public void OnEnableComparesTheTargetWithTheInitialStateNotThePreviousState()
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-onenable-previous", false).AddComponent<HLAlphaInOut>();
                Set(alpha, "m_initialState", AnimState.None);
                Set(alpha, "m_animateToStateOnEnable", AnimState.In);
                Call(alpha, "set_CurrentState", AnimState.In);
                Call(alpha, "OnEnable");
                Assert.That(alpha.CurrentState, Is.EqualTo(AnimState.None));
                Assert.That(Get(alpha, "m_playDirection"), Is.EqualTo(PlayDirection.In), "the stale In state does not block AnimateIn");
            }
        }

        // Animate 0x06000504 @0x1B62E20: nothing without a direction (+0x78 == 0) or while the owner is
        // inactive in the hierarchy; otherwise stop the tracked animation and start RunAnimation
        // (slot 9). The engine runs the first MoveNext synchronously, so a started alpha animation
        // is visible at once as AnimatingIn with m_onAnimateIn fired.
        [Test]
        public void AnimateNeedsADirectionAndAnActiveHierarchy()
        {
            using (var owned = new Owned())
            {
                GameObject parent = owned.Host("InOut-animate-parent", false);
                GameObject child = owned.Host("InOut-animate-child", true);
                child.transform.SetParent(parent.transform, false);
                HLAlphaInOut hidden = child.AddComponent<HLAlphaInOut>();
                Set(hidden, "m_alphaCurve", owned.Linear(0f, 0f, 1f, 1f));
                WireEvents(owned, hidden);
                Set(hidden, "m_playDirection", PlayDirection.In);
                Call(hidden, "Animate");
                Assert.That(Get(hidden, "m_trackedAnimation"), Is.Null, "inactive parent");

                HLAlphaInOut alpha = owned.Host("InOut-animate", true).AddComponent<HLAlphaInOut>();
                Set(alpha, "m_alphaCurve", owned.Linear(0f, 0f, 1f, 1f));
                WireEvents(owned, alpha);
                Call(alpha, "Animate");
                Assert.That(Get(alpha, "m_trackedAnimation"), Is.Null, "no direction");
                Assert.That(alpha.CurrentState, Is.EqualTo(AnimState.None));

                Set(alpha, "m_playDirection", PlayDirection.In);
                Call(alpha, "Animate");
                Assert.That(Get(alpha, "m_trackedAnimation"), Is.Not.Null);
                Assert.That(alpha.CurrentState, Is.EqualTo(AnimState.AnimatingIn));
                Assert.That(owned.Log, Is.EqualTo(new[] { "onAnimateIn:AnimatingIn 0" }));
                alpha.StopAllCoroutines();
            }
        }

        // OnDisable 0x06000502 @0x1B62DD0: SafeStopCoroutine on +0x80 and +0x88 (both cleared), then
        // ForceFinishAnimation (slot 12). With the alpha running in, the force finish applies the
        // curve end, reaches In and fires m_onAnimateInComplete once.
        [Test]
        public void OnDisableStopsBothCoroutinesAndForceFinishes()
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-ondisable", true, typeof(CanvasGroup)).AddComponent<HLAlphaInOut>();
                CanvasGroup group = alpha.GetComponent<CanvasGroup>();
                Set(alpha, "m_targetCanvasGroup", group);
                Set(alpha, "m_alphaCurve", owned.Linear(0f, 0.25f, 2f, 0.75f));
                WireEvents(owned, alpha);
                Call(alpha, "Awake");
                Set(alpha, "m_autoAnimateOutAfterTimeout", false);
                Call(alpha, "ScheduleAutoAnimateOut");
                Assert.That(Get(alpha, "m_trackedAutoAnimateOut"), Is.Null, "flag clear");
                Set(alpha, "m_autoAnimateOutAfterTimeout", true);
                Call(alpha, "ScheduleAutoAnimateOut");
                Assert.That(Get(alpha, "m_trackedAutoAnimateOut"), Is.Not.Null, "flag set");
                Set(alpha, "m_autoAnimateOutAfterTimeout", false);
                alpha.AnimateIn();
                Assert.That(Get(alpha, "m_trackedAnimation"), Is.Not.Null);
                group.alpha = 0.5f;

                Call(alpha, "OnDisable");
                Assert.That(Get(alpha, "m_trackedAnimation"), Is.Null);
                Assert.That(Get(alpha, "m_trackedAutoAnimateOut"), Is.Null);
                Assert.That(group.alpha, Is.EqualTo(0.75f).Within(1e-6f));
                Assert.That(alpha.CurrentState, Is.EqualTo(AnimState.In));
                Assert.That(Get(alpha, "m_playDirection"), Is.EqualTo(PlayDirection.None));
                Assert.That(owned.Log, Is.EqualTo(new[] { "onAnimateIn:AnimatingIn 0", "onAnimateInComplete:In 1" }));
            }
        }

        // AutoAnimateOut 0x06000505 only builds d__36; MoveNext 0x0600050f @0x1B62F40 yields the
        // +0x90 wait instance itself, then calls AnimateOut (slot 11) and finishes; a further step
        // does nothing. Reset throws NotSupportedException and Dispose is empty.
        [Test]
        public void AutoAnimateOutYieldsTheTimeoutWaitThenAnimatesOutOnce()
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-auto", false).AddComponent<HLAlphaInOut>();
                Set(alpha, "m_autoAnimateOutTimeoutSeconds", 2f);
                Call(alpha, "Awake");
                var wait = Get<WaitForSeconds>(alpha, "m_autoAnimateOutWait");
                var routine = (IEnumerator)Call(alpha, "AutoAnimateOut");
                Assert.That(routine.GetType().Name, Is.EqualTo("<AutoAnimateOut>d__36"));
                Assert.That(Get(routine, "<>4__this"), Is.SameAs(alpha));
                Assert.That(Get(routine, "<>1__state"), Is.EqualTo(0));

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.SameAs(wait));
                Assert.That(Get(alpha, "m_playDirection"), Is.EqualTo(PlayDirection.None), "AnimateOut waits for the timeout");
                ((IDisposable)routine).Dispose();
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(Get(alpha, "m_playDirection"), Is.EqualTo(PlayDirection.Out));

                Set(alpha, "m_playDirection", PlayDirection.None);
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(Get(alpha, "m_playDirection"), Is.EqualTo(PlayDirection.None));
                Assert.Throws<NotSupportedException>(() => routine.Reset());
            }
        }

        // IndependentTimeDelta 0x0600050b @0x1B619B0: +0x2C selects Time.unscaledDeltaTime, otherwise
        // Time.deltaTime. EditMode usually reports equal values, so this case only shows both calls
        // succeed; OriginalHLUIAnimationInOutPlayTests separates the branches at time scale zero.
        [Test]
        public void IndependentTimeDeltaFollowsTheUnscaledFlag()
        {
            using (var owned = new Owned())
            {
                HLAlphaInOut alpha = owned.Host("InOut-delta", false).AddComponent<HLAlphaInOut>();
                Assert.That((float)Call(alpha, "IndependentTimeDelta"), Is.EqualTo(Time.deltaTime));
                Set(alpha, "m_useUnscaledTime", true);
                Assert.That((float)Call(alpha, "IndependentTimeDelta"), Is.EqualTo(Time.unscaledDeltaTime));
            }
        }

        internal static void CheckOptions(Type type, params int[] options)
        {
            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            for (int i = 0; i < options.Length; i++)
            {
                CustomAttributeData option = attributes[i];
                Assert.That(option.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)), type.Name);
                Assert.That(Convert.ToInt32(option.ConstructorArguments[0].Value), Is.EqualTo(options[i]), type.Name);
                Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false), type.Name);
            }
        }

        // The hoisted fields are those of dump.cs. Only an optimised (/optimize+) compile hoists just
        // the locals that live across a yield and numbers them from 5__2; Unity compiles optimised
        // only in Release code optimisation, so a Debug-mode Editor reports the field check as
        // inconclusive rather than as a declaration failure.
        internal static void CheckIterator(Type owner, string name, params string[] hoisted)
        {
            Type iterator = owner.GetNestedType(name, BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null, name);
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x100103), name);
            Assert.That(iterator.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            CollectionAssert.AreEquivalent(new[] { typeof(IEnumerator<object>), typeof(IEnumerator), typeof(IDisposable) }, iterator.GetInterfaces());
            MethodInfo run = owner.GetMethod("RunAnimation", Declared);
            Assert.That(run.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType, Is.EqualTo(iterator));
            Assert.That((int)run.Attributes, Is.EqualTo(0xC4), "protected override");

            Assume.That(UnityEditor.Compilation.CompilationPipeline.codeOptimization,
                Is.EqualTo(UnityEditor.Compilation.CodeOptimization.Release),
                "the hoisted iterator fields of dump.cs only match an optimised compile");
            FieldInfo[] fields = iterator.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "<>1__state", "<>2__current", "<>4__this" }.Concat(hoisted).ToArray()), name);
            Assert.That(fields[2].FieldType, Is.EqualTo(owner), name);
            Assert.That(fields.Skip(3).Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(bool), typeof(float), typeof(float) }), name);
        }
    }
}
