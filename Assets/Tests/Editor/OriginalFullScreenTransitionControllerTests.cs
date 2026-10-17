using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using State = Hardlight.FullScreenTransitionController.State;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.FullScreenTransitionController (HLUnityUI.Runtime
    // 0x060000e0..ef) against the shipped x86-64 bodies. Each case owns its
    // GameObject, Animator and in-memory AnimatorController (with its undo
    // history cleared), so no shared editor or scene state outlives the case.
    //
    // Observation: every UnityEvent listener and callback appends
    // "name:<state> <animator.speed> <in><out><reset>" where the last three
    // digits are the Animator trigger parameters. Triggers are never consumed
    // in EditMode, so the log shows exactly which SetTrigger calls ran before
    // each event. Fixture defaults: in trigger "In", out "Out", reset "Reset",
    // m_transitionInDuration 0.25 (speed 4), m_transitionOutDuration 0.2 (speed 5).
    [TestFixture]
    public sealed class OriginalFullScreenTransitionControllerTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.DeclaredOnly;
        const string InTrigger = "In";
        const string OutTrigger = "Out";
        const string ResetTrigger = "Reset";

        sealed class Owned : IDisposable
        {
            readonly GameObject root;
            readonly AnimatorController controller;
            public readonly Animator Animator;
            public readonly FullScreenTransitionController Transition;
            public readonly List<string> Log = new List<string>();

            public Owned(FullScreenTransitionType type, float inDuration = 0.25f, float outDuration = 0.2f, bool withAnimator = true)
            {
                controller = new AnimatorController { name = "FullScreenTransition fixture", hideFlags = HideFlags.HideAndDontSave };
                root = new GameObject("FullScreenTransitionController fixture") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    controller.AddLayer("Base");
                    controller.AddParameter(InTrigger, AnimatorControllerParameterType.Trigger);
                    controller.AddParameter(OutTrigger, AnimatorControllerParameterType.Trigger);
                    controller.AddParameter(ResetTrigger, AnimatorControllerParameterType.Trigger);
                    Undo.ClearUndo(controller);
                    Animator = root.AddComponent<Animator>();
                    Animator.runtimeAnimatorController = controller;
                    Animator.Rebind();
                    Assert.That(Animator.isInitialized, Is.True, "fixture Animator binds the owned controller");
                    Assert.That(Triggers(), Is.EqualTo("000"), "fixture triggers start clear");

                    Transition = root.AddComponent<FullScreenTransitionController>();
                    Set(Transition, "m_type", type);
                    Set(Transition, "m_animator", withAnimator ? Animator : null);
                    Set(Transition, "m_animateInTrigger", InTrigger);
                    Set(Transition, "m_animateOutTrigger", OutTrigger);
                    Set(Transition, "m_animatorResetTrigger", ResetTrigger);
                    Set(Transition, "m_transitionInDuration", inDuration);
                    Set(Transition, "m_transitionOutDuration", outDuration);
                    Set(Transition, "m_onTransitionIn", Event("onTransitionIn"));
                    Set(Transition, "m_onTransitionInComplete", Event("onTransitionInComplete"));
                    Set(Transition, "m_onTransitionOut", Event("onTransitionOut"));
                    Set(Transition, "m_onTransitionOutComplete", Event("onTransitionOutComplete"));
                    Set(Transition, "m_onReset", Event("onReset"));
                }
                catch { Dispose(); throw; }
            }

            UnityEvent Event(string name)
            {
                var unityEvent = new UnityEvent();
                unityEvent.AddListener(() => Record(name));
                return unityEvent;
            }

            public Action Callback(string name) => () => Record(name);
            public void Record(string name) => Log.Add(name + ":" + Snapshot());

            public string Snapshot() =>
                (int)Transition.CurrentState + " " + Animator.speed.ToString("R", CultureInfo.InvariantCulture) + " " + Triggers();

            public string Triggers() =>
                (Animator.GetBool(InTrigger) ? "1" : "0") + (Animator.GetBool(OutTrigger) ? "1" : "0") + (Animator.GetBool(ResetTrigger) ? "1" : "0");

            public void SetState(State state) => Call(Transition, "set_CurrentState", state);
            public Action CachedMidpoint => (Action)Get(Transition, "m_cachedMidpointCallback");
            public Action CachedCompletion => (Action)Get(Transition, "m_cachedCompletionCallback");
            public float OverriddenIn => (float)Get(Transition, "m_overriddenTransitionInDuration");
            public float OverriddenOut => (float)Get(Transition, "m_overriddenTransitionOutDuration");
            public UnityEvent EventField(string name) => (UnityEvent)Get(Transition, name);

            public void Dispose()
            {
                try
                {
                    if (root != null) Object.DestroyImmediate(root);
                }
                finally
                {
                    if (controller != null)
                    {
                        foreach (AnimatorControllerLayer layer in controller.layers)
                            if (layer.stateMachine != null) Object.DestroyImmediate(layer.stateMachine);
                        Undo.ClearUndo(controller);
                        Object.DestroyImmediate(controller);
                    }
                }
            }
        }

        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);

        static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        // Declarations as in dump.cs TypeDefIndex 11858/11856/11857 and the
        // DummyDll metadata: public sealed beforefieldinit (0x100101), the two
        // Il2CppSetOption(NullChecks/ArrayBoundsChecks, false) attributes, the
        // field order with [SerializeField] on the first twelve, the auto
        // property backing field, the State literals, the optional parameters
        // (0f, null, null) and <>c__DisplayClass25_0, whose ordinal fixes the
        // member declaration order before StartTransition.
        [Test]
        public void OriginalDeclarationsFieldOrderAndClosureShape()
        {
            Type type = typeof(FullScreenTransitionController);
            Assert.AreEqual("Hardlight.FullScreenTransitionController", type.FullName);
            Assert.AreEqual("HLUnityUI.Runtime", type.Assembly.GetName().Name);
            Assert.AreEqual(0x100101, (int)type.Attributes);
            Assert.AreEqual(typeof(MonoBehaviour), type.BaseType);
            CollectionAssert.AreEquivalent(new[] { "NullChecks=False", "ArrayBoundsChecks=False" },
                type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false).Cast<Il2CppSetOptionAttribute>()
                    .Select(option => option.Option + "=" + option.Value));

            string[] fields =
            {
                "m_type:FullScreenTransitionType", "m_animator:Animator", "m_animateInTrigger:String",
                "m_animateOutTrigger:String", "m_animatorResetTrigger:String", "m_transitionInDuration:Single",
                "m_transitionOutDuration:Single", "m_onTransitionIn:UnityEvent", "m_onTransitionInComplete:UnityEvent",
                "m_onTransitionOut:UnityEvent", "m_onTransitionOutComplete:UnityEvent", "m_onReset:UnityEvent",
                "m_overriddenTransitionInDuration:Single", "m_overriddenTransitionOutDuration:Single",
                "m_cachedMidpointCallback:Action", "m_cachedCompletionCallback:Action", "<CurrentState>k__BackingField:State",
            };
            FieldInfo[] declared = type.GetFields(Declared).OrderBy(field => field.MetadataToken).ToArray();
            CollectionAssert.AreEqual(fields, declared.Select(field => field.Name + ":" + field.FieldType.Name));
            for (int i = 0; i < declared.Length; i++)
            {
                Assert.IsTrue(declared[i].IsPrivate && !declared[i].IsStatic && !declared[i].IsInitOnly, declared[i].Name);
                Assert.AreEqual(i < 12, declared[i].IsDefined(typeof(SerializeField), false), declared[i].Name);
            }
            Assert.IsTrue(declared[16].IsDefined(typeof(CompilerGeneratedAttribute), false));

            string[] methods =
            {
                "get_Type:Public", "set_Type:Private", "get_CurrentState:Public", "set_CurrentState:Private",
                "Awake:Public", "StartTransition:Public", "ContinueTransition:Public", "ResetTransition:Public",
                "Action_TransitionAnimationComplete:Public", "TransitionIn:Private", "TransitionOut:Private",
                "OnMidPointReached:Private", "OnCompletion:Private",
            };
            MethodInfo[] declaredMethods = type.GetMethods(Declared).OrderBy(method => method.MetadataToken).ToArray();
            CollectionAssert.AreEqual(methods, declaredMethods.Select(method =>
                method.Name + ":" + (method.IsPublic ? "Public" : method.IsPrivate ? "Private" : "Other")));
            Assert.IsTrue(declaredMethods.All(method => !method.IsStatic && !method.IsVirtual));
            Assert.IsTrue(declaredMethods[2].IsDefined(typeof(CompilerGeneratedAttribute), false));
            Assert.IsTrue(declaredMethods[3].IsDefined(typeof(CompilerGeneratedAttribute), false));
            Assert.IsFalse(declaredMethods[0].IsDefined(typeof(CompilerGeneratedAttribute), false), "Type wraps m_type");
            CollectionAssert.AreEqual(new[] { "Type", "CurrentState" },
                type.GetProperties(Declared).OrderBy(property => property.MetadataToken).Select(property => property.Name));

            ParameterInfo[] start = type.GetMethod("StartTransition").GetParameters();
            CollectionAssert.AreEqual(new[] { "overrideTransitionDuration:Single", "midPointCallback:Action", "completionCallback:Action" },
                start.Select(parameter => parameter.Name + ":" + parameter.ParameterType.Name));
            ParameterInfo[] resume = type.GetMethod("ContinueTransition").GetParameters();
            CollectionAssert.AreEqual(new[] { "overrideTransitionDuration:Single", "completionCallback:Action" },
                resume.Select(parameter => parameter.Name + ":" + parameter.ParameterType.Name));
            foreach (ParameterInfo parameter in start.Concat(resume))
                Assert.IsTrue(parameter.IsOptional && parameter.HasDefaultValue, parameter.Name);
            Assert.AreEqual(0f, start[0].DefaultValue);
            Assert.IsNull(start[1].DefaultValue);
            Assert.IsNull(start[2].DefaultValue);
            Assert.AreEqual(0f, resume[0].DefaultValue);
            Assert.IsNull(resume[1].DefaultValue);
            Assert.AreEqual(typeof(bool), type.GetMethod("StartTransition").ReturnType);
            Assert.AreEqual(typeof(bool), type.GetMethod("ContinueTransition").ReturnType);

            Type state = typeof(State);
            Assert.IsTrue(state.IsNestedPublic && state.IsEnum);
            Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(state));
            CollectionAssert.AreEqual(new[] { "HeldOut=0", "TransitioningIn=1", "HeldIn=2", "TransitioningOut=3" },
                state.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(field => field.MetadataToken)
                    .Select(field => field.Name + "=" + Convert.ToInt32(field.GetValue(null), CultureInfo.InvariantCulture)));

            Type closure = type.GetNestedType("<>c__DisplayClass25_0", BindingFlags.NonPublic);
            Assert.IsNotNull(closure, "StartTransition closure keeps its original ordinal");
            Assert.IsTrue(closure.IsNestedPrivate && closure.IsSealed && closure.IsDefined(typeof(CompilerGeneratedAttribute), false));
            CollectionAssert.AreEqual(new[] { "midPointCallback:Action", "<>4__this:FullScreenTransitionController", "halfTotalDurationOverride:Single" },
                closure.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(field => field.Name + ":" + field.FieldType.Name));
            MethodInfo lambda = closure.GetMethod("<StartTransition>b__0", Declared);
            Assert.IsNotNull(lambda);
            Assert.IsTrue(lambda.IsAssembly);
            CollectionAssert.AreEqual(new[] { "<>c__DisplayClass25_0" }, type.GetNestedTypes(Declared)
                .Where(nested => nested.IsDefined(typeof(CompilerGeneratedAttribute), false)).Select(nested => nested.Name));
        }

        // .ctor 0x1b40290 stores the 8-byte literal at 0x29a6c30 (two 1.0f) over
        // m_transitionInDuration and m_transitionOutDuration and nothing else.
        // Awake 0x1b3fdf0 only writes HeldOut (mov dword [rdi+0x90],0).
        // Mutations caught: one-second default on only one duration, an
        // initialiser on the overrides, caches, animator or state, Awake
        // touching the animator or the caches. Trigger strings, UnityEvents and
        // an m_type = Continuous initialiser are not observable here (the
        // serializer and the enum default produce the same values).
        [Test]
        public void ConstructorDefaultsBothDurationsToOneAndAwakeOnlyResetsTheState()
        {
            var owner = new GameObject("FullScreenTransitionController constructor") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var fresh = owner.AddComponent<FullScreenTransitionController>();
                Assert.AreEqual(1f, Get(fresh, "m_transitionInDuration"));
                Assert.AreEqual(1f, Get(fresh, "m_transitionOutDuration"));
                Assert.AreEqual(FullScreenTransitionType.Continuous, fresh.Type);
                Assert.AreEqual(State.HeldOut, fresh.CurrentState);
                Assert.AreEqual(0f, Get(fresh, "m_overriddenTransitionInDuration"));
                Assert.AreEqual(0f, Get(fresh, "m_overriddenTransitionOutDuration"));
                Assert.IsNull(Get(fresh, "m_cachedMidpointCallback"));
                Assert.IsNull(Get(fresh, "m_cachedCompletionCallback"));
                // Unity null semantics: the Editor may hold a fake-null wrapper for an unassigned reference.
                Assert.IsTrue((Object)Get(fresh, "m_animator") == null);
            }
            finally { Object.DestroyImmediate(owner); }

            foreach (State state in new[] { State.TransitioningIn, State.HeldIn, State.TransitioningOut })
            {
                using (var owned = new Owned(FullScreenTransitionType.Held))
                {
                    Action mid = owned.Callback("mid"), done = owned.Callback("done");
                    owned.SetState(state);
                    Set(owned.Transition, "m_cachedMidpointCallback", mid);
                    Set(owned.Transition, "m_cachedCompletionCallback", done);
                    Set(owned.Transition, "m_overriddenTransitionInDuration", 0.75f);
                    owned.Transition.Awake();
                    Assert.AreEqual(State.HeldOut, owned.Transition.CurrentState, state.ToString());
                    Assert.AreSame(mid, owned.CachedMidpoint);
                    Assert.AreSame(done, owned.CachedCompletion);
                    Assert.AreEqual(0.75f, owned.OverriddenIn);
                    Assert.AreEqual("1 000", owned.Snapshot().Substring(2));
                    CollectionAssert.IsEmpty(owned.Log);
                }
            }
        }

        // Held cycle. StartTransition 0x1b3fe00 Held branch (0x1b3fea9) stores
        // the raw override and the caller's midpoint delegate, then the
        // completion (0x1b3ff29), state 1 (0x1b3ff35), speed 1/duration
        // (0x1b3ff68), the in trigger (0x1b3ff77) and onTransitionIn
        // (0x1b3ff82). Action_TransitionAnimationComplete 0x1b40170 at state 1
        // writes 2, raises onTransitionInComplete and then the cached midpoint;
        // a Held transition waits there. ContinueTransition 0x1b40020 at state
        // 2 writes 3, speed, out trigger and onTransitionOut; completion at
        // state 3 writes 0, raises onTransitionOutComplete, then the cached
        // completion. Neither cache is cleared afterwards.
        // Mutations caught: events after callbacks, state written after the
        // event, auto-continuing a Held transition, wrong trigger, clearing caches.
        [Test]
        public void HeldTransitionWaitsAtTheMidpointUntilContinued()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Action mid = owned.Callback("mid"), done = owned.Callback("done");
                Assert.IsTrue(owned.Transition.StartTransition(0f, mid, done));
                Assert.AreSame(mid, owned.CachedMidpoint, "Held caches the caller's delegate itself");
                Assert.AreSame(done, owned.CachedCompletion);
                Assert.AreEqual(0f, owned.OverriddenIn);
                CollectionAssert.AreEqual(new[] { "onTransitionIn:1 4 100" }, owned.Log);

                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[] { "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "mid:2 4 100" }, owned.Log);
                Assert.AreEqual(State.HeldIn, owned.Transition.CurrentState);

                owned.Transition.Action_TransitionAnimationComplete();
                Assert.AreEqual(3, owned.Log.Count, "HeldIn ignores animation completion");

                Assert.IsTrue(owned.Transition.ContinueTransition());
                Assert.AreEqual("onTransitionOut:3 5 110", owned.Log[3]);
                Assert.AreEqual(0f, owned.OverriddenOut);

                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[] { "onTransitionOutComplete:0 5 110", "done:0 5 110" }, owned.Log.Skip(4));
                Assert.AreSame(mid, owned.CachedMidpoint, "callbacks stay cached after completion");
                Assert.AreSame(done, owned.CachedCompletion);

                owned.Transition.Action_TransitionAnimationComplete();
                Assert.AreEqual(6, owned.Log.Count, "HeldOut ignores animation completion");
            }
        }

        // Duration choice in StartTransition (0x1b3ff4b ucomiss/cmova) and
        // ContinueTransition (0x1b40074): the stored override is used only when
        // it compares above 0.0 (unordered NaN, zero and negatives fall back to
        // the serialized duration), and the numerator is the 1.0f at 0x29a6be0.
        // The override is stored unconditionally first (0x1b3feae, 0x1b40037).
        // Mutations caught: >= 0, != 0, using the serialized value always,
        // multiplying instead of dividing, storing only accepted overrides.
        [Test]
        public void PositiveOverridesReplaceTheSerializedDurationsAndAreStoredRaw()
        {
            float[] overrides = { 0.5f, 0.125f, 0f, -2f, float.NaN };
            string[] inSpeeds = { "2", "8", "4", "4", "4" };
            string[] outSpeeds = { "2", "8", "5", "5", "5" };
            for (int i = 0; i < overrides.Length; i++)
            {
                using (var owned = new Owned(FullScreenTransitionType.Held))
                {
                    Assert.IsTrue(owned.Transition.StartTransition(overrides[i]));
                    Assert.AreEqual(overrides[i], owned.OverriddenIn, "stored in override " + overrides[i]);
                    owned.Transition.Action_TransitionAnimationComplete();
                    Assert.IsTrue(owned.Transition.ContinueTransition(overrides[i]));
                    Assert.AreEqual(overrides[i], owned.OverriddenOut, "stored out override " + overrides[i]);
                    CollectionAssert.AreEqual(new[]
                    {
                        "onTransitionIn:1 " + inSpeeds[i] + " 100",
                        "onTransitionInComplete:2 " + inSpeeds[i] + " 100",
                        "onTransitionOut:3 " + outSpeeds[i] + " 110",
                    }, owned.Log, "override " + overrides[i]);
                }
            }
        }

        // Continuous cycle. StartTransition's Continuous branch (0x1b3fec7)
        // multiplies the override by the 0.5f at 0x29a6c08 into the closure's
        // halfTotalDurationOverride and m_overriddenTransitionInDuration, and
        // caches a new Action over <StartTransition>b__0 (0x1b3ff03) even when
        // the caller's midpoint is null. b__0 0x1b402b0 invokes the caller's
        // midpoint if any, then (inlined ContinueTransition, null completion)
        // at state 2 stores the half as the out override and transitions out.
        // Override 1 gives half 0.5 (speed 2 both ways); override 0 falls back
        // to the serialized 0.25/0.2.
        // Mutations caught: no halving, halving only one leg, caching the raw
        // midpoint, invoking the midpoint after transitioning out, passing the
        // completion into the closure, skipping the null midpoint guard.
        [Test]
        public void ContinuousTransitionHalvesTheOverrideAndContinuesAfterTheMidpoint()
        {
            using (var owned = new Owned(FullScreenTransitionType.Continuous))
            {
                Action mid = owned.Callback("mid"), done = owned.Callback("done");
                Assert.IsTrue(owned.Transition.StartTransition(1f, mid, done));
                Assert.AreEqual(0.5f, owned.OverriddenIn);
                Assert.AreSame(done, owned.CachedCompletion);
                Action cached = owned.CachedMidpoint;
                Assert.IsNotNull(cached);
                Assert.AreNotSame(mid, cached);
                Assert.AreEqual("<StartTransition>b__0", cached.Method.Name);
                Assert.AreEqual("<>c__DisplayClass25_0", cached.Target.GetType().Name);
                Assert.AreSame(mid, Get(cached.Target, "midPointCallback"));
                Assert.AreSame(owned.Transition, Get(cached.Target, "<>4__this"));
                Assert.AreEqual(0.5f, Get(cached.Target, "halfTotalDurationOverride"));

                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 2 100", "onTransitionInComplete:2 2 100", "mid:2 2 100", "onTransitionOut:3 2 110",
                }, owned.Log);
                Assert.AreEqual(0.5f, owned.OverriddenOut);
                Assert.AreSame(done, owned.CachedCompletion, "the closure's ContinueTransition passes no completion");

                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[] { "onTransitionOutComplete:0 2 110", "done:0 2 110" }, owned.Log.Skip(4));
            }

            using (var owned = new Owned(FullScreenTransitionType.Continuous))
            {
                Assert.IsTrue(owned.Transition.StartTransition(0f, null, null));
                Assert.IsNotNull(owned.CachedMidpoint, "a closure is cached even for a null midpoint");
                Assert.IsNull(Get(owned.CachedMidpoint.Target, "midPointCallback"));
                Assert.IsNull(owned.CachedCompletion);
                owned.Transition.Action_TransitionAnimationComplete();
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "onTransitionOut:3 5 110", "onTransitionOutComplete:0 5 110",
                }, owned.Log);
                Assert.AreEqual(State.HeldOut, owned.Transition.CurrentState);
            }
        }

        // b__0 0x1b402d7 re-reads the state after the caller's midpoint and
        // continues only if it is still HeldIn (cmp [r14+0x90],2). A midpoint
        // that resets leaves the controller HeldOut; one that continues itself
        // keeps its own override and is not transitioned out a second time.
        // ContinueTransition 0x1b40041 adopts a completion only while none is
        // cached. Mutations caught: an unconditional continue, a state check
        // before the midpoint, overwriting the cached completion.
        [Test]
        public void ContinuousMidpointThatResetsOrContinuesPreemptsTheAutomaticContinue()
        {
            using (var owned = new Owned(FullScreenTransitionType.Continuous))
            {
                Action mid = () => { owned.Record("mid"); owned.Transition.ResetTransition(); };
                Set(owned.Transition, "m_overriddenTransitionOutDuration", 0.625f);
                Assert.IsTrue(owned.Transition.StartTransition(1f, mid, owned.Callback("done")));
                Assert.AreEqual(0.625f, owned.OverriddenOut, "StartTransition does not write the out override");
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 2 100", "onTransitionInComplete:2 2 100", "mid:2 2 100", "onReset:0 2 101",
                }, owned.Log);
                Assert.AreEqual(0.625f, owned.OverriddenOut, "the preempted closure does not store its half");
                owned.Transition.Action_TransitionAnimationComplete();
                Assert.AreEqual(4, owned.Log.Count);
            }

            using (var owned = new Owned(FullScreenTransitionType.Continuous))
            {
                Action other = owned.Callback("other");
                Action mid = () =>
                {
                    owned.Record("mid");
                    Assert.IsTrue(owned.Transition.ContinueTransition(0.125f, other));
                };
                Assert.IsTrue(owned.Transition.StartTransition(1f, mid, owned.Callback("done")));
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 2 100", "onTransitionInComplete:2 2 100", "mid:2 2 100", "onTransitionOut:3 8 110",
                }, owned.Log);
                Assert.AreEqual(0.125f, owned.OverriddenOut, "the closure does not overwrite the midpoint's override");
                Assert.AreEqual(0.5f, owned.OverriddenIn, "ContinueTransition does not write the in override");
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[] { "onTransitionOutComplete:0 8 110", "done:0 8 110" }, owned.Log.Skip(4));
            }
        }

        // StartTransition 0x1b3fe9a: any state but HeldOut skips every write
        // and returns false (sete on the state read at 0x1b3fe93). The closure
        // object is allocated before the check but never cached.
        // Mutations caught: restarting mid-transition, caching callbacks or the
        // override before the guard, returning true.
        [Test]
        public void StartTransitionOutsideHeldOutChangesNothingAndReturnsFalse()
        {
            foreach (FullScreenTransitionType type in new[] { FullScreenTransitionType.Continuous, FullScreenTransitionType.Held })
            foreach (State state in new[] { State.TransitioningIn, State.HeldIn, State.TransitioningOut })
            {
                using (var owned = new Owned(type))
                {
                    Action mid = owned.Callback("mid"), done = owned.Callback("done");
                    owned.SetState(state);
                    Set(owned.Transition, "m_cachedMidpointCallback", mid);
                    Set(owned.Transition, "m_cachedCompletionCallback", done);
                    Set(owned.Transition, "m_overriddenTransitionInDuration", 0.75f);
                    Assert.IsFalse(owned.Transition.StartTransition(0.5f, owned.Callback("newMid"), owned.Callback("newDone")), type + " " + state);
                    Assert.AreEqual(state, owned.Transition.CurrentState);
                    Assert.AreSame(mid, owned.CachedMidpoint);
                    Assert.AreSame(done, owned.CachedCompletion);
                    Assert.AreEqual(0.75f, owned.OverriddenIn);
                    Assert.AreEqual("1 000", owned.Snapshot().Substring(2));
                    CollectionAssert.IsEmpty(owned.Log);
                }
            }
        }

        // From HeldOut both caches are always overwritten (0x1b3febe/0x1b3ff0f
        // and 0x1b3ff29), so null arguments clear earlier callbacks.
        // Mutations caught: null-coalescing either cache.
        [Test]
        public void StartTransitionFromHeldOutOverwritesBothCachesEvenWithNull()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Set(owned.Transition, "m_cachedMidpointCallback", owned.Callback("oldMid"));
                Set(owned.Transition, "m_cachedCompletionCallback", owned.Callback("oldDone"));
                Assert.IsTrue(owned.Transition.StartTransition());
                Assert.IsNull(owned.CachedMidpoint);
                Assert.IsNull(owned.CachedCompletion);
                owned.Transition.Action_TransitionAnimationComplete();
                owned.Transition.ContinueTransition();
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "onTransitionOut:3 5 110", "onTransitionOutComplete:0 5 110",
                }, owned.Log);
            }

            using (var owned = new Owned(FullScreenTransitionType.Continuous))
            {
                Set(owned.Transition, "m_cachedCompletionCallback", owned.Callback("oldDone"));
                Assert.IsTrue(owned.Transition.StartTransition());
                Assert.IsNull(owned.CachedCompletion);
            }
        }

        // ContinueTransition 0x1b4002e: outside HeldIn it returns false before
        // storing the override or the completion. At HeldIn a non-null
        // completion is adopted only when none is cached (0x1b4003c/0x1b40041).
        // Mutations caught: storing the override before the guard, continuing
        // from TransitioningIn, replacing or clearing a cached completion.
        [Test]
        public void ContinueTransitionOnlyFromHeldInAndAdoptsACompletionOnlyWhenNoneIsCached()
        {
            foreach (State state in new[] { State.HeldOut, State.TransitioningIn, State.TransitioningOut })
            {
                using (var owned = new Owned(FullScreenTransitionType.Held))
                {
                    owned.SetState(state);
                    Set(owned.Transition, "m_overriddenTransitionOutDuration", 0.75f);
                    Assert.IsFalse(owned.Transition.ContinueTransition(0.5f, owned.Callback("done")), state.ToString());
                    Assert.AreEqual(state, owned.Transition.CurrentState);
                    Assert.AreEqual(0.75f, owned.OverriddenOut);
                    Assert.IsNull(owned.CachedCompletion);
                    Assert.AreEqual("1 000", owned.Snapshot().Substring(2));
                    CollectionAssert.IsEmpty(owned.Log);
                }
            }

            string[] cachedNames = { null, "cached", "cached" };
            string[] passedNames = { "passed", "passed", null };
            string[] invoked = { "passed", "cached", "cached" };
            for (int i = 0; i < cachedNames.Length; i++)
            {
                using (var owned = new Owned(FullScreenTransitionType.Held))
                {
                    Action cached = cachedNames[i] == null ? null : owned.Callback(cachedNames[i]);
                    Action passed = passedNames[i] == null ? null : owned.Callback(passedNames[i]);
                    owned.SetState(State.HeldIn);
                    Set(owned.Transition, "m_cachedCompletionCallback", cached);
                    Assert.IsTrue(owned.Transition.ContinueTransition(0f, passed));
                    Assert.AreSame(cached ?? passed, owned.CachedCompletion, "case " + i);
                    owned.Transition.Action_TransitionAnimationComplete();
                    CollectionAssert.AreEqual(new[]
                    {
                        "onTransitionOut:3 5 010", "onTransitionOutComplete:0 5 010", invoked[i] + ":0 5 010",
                    }, owned.Log, "case " + i);
                }
            }
        }

        // ResetTransition 0x1b40130 writes HeldOut, sets the reset trigger and
        // raises onReset (tail jump at 0x1b4015e) from every state. It leaves
        // the animator speed, both overrides and both caches untouched.
        // Mutations caught: event before the state or trigger, resetting speed,
        // clearing callbacks or overrides, a state guard.
        [Test]
        public void ResetTransitionFromAnyStateKeepsSpeedOverridesAndCaches()
        {
            foreach (State state in new[] { State.HeldOut, State.TransitioningIn, State.HeldIn, State.TransitioningOut })
            {
                using (var owned = new Owned(FullScreenTransitionType.Held))
                {
                    Action mid = owned.Callback("mid"), done = owned.Callback("done");
                    owned.SetState(state);
                    owned.Animator.speed = 3f;
                    Set(owned.Transition, "m_cachedMidpointCallback", mid);
                    Set(owned.Transition, "m_cachedCompletionCallback", done);
                    Set(owned.Transition, "m_overriddenTransitionInDuration", 0.75f);
                    Set(owned.Transition, "m_overriddenTransitionOutDuration", 0.625f);
                    owned.Transition.ResetTransition();
                    CollectionAssert.AreEqual(new[] { "onReset:0 3 001" }, owned.Log, state.ToString());
                    Assert.AreSame(mid, owned.CachedMidpoint);
                    Assert.AreSame(done, owned.CachedCompletion);
                    Assert.AreEqual(0.75f, owned.OverriddenIn);
                    Assert.AreEqual(0.625f, owned.OverriddenOut);
                }
            }
        }

        // The private step bodies do not check the state: TransitionIn
        // 0x1b3ffb0 (state 1, in), TransitionOut 0x1b400c0 (state 3, out),
        // OnMidPointReached 0x1b401f0 (state 2, event, midpoint) and
        // OnCompletion 0x1b40240 (state 0, event, completion). Both legs read
        // their own stored override (in 0.5 -> speed 2, out 0.125 -> speed 8).
        // Mutations caught: guards, swapped bodies, a leg reading the other
        // leg's override or its serialized duration.
        [Test]
        public void PrivateStepsRunUnconditionallyFromAnyState()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Set(owned.Transition, "m_cachedMidpointCallback", owned.Callback("mid"));
                Set(owned.Transition, "m_cachedCompletionCallback", owned.Callback("done"));
                Set(owned.Transition, "m_overriddenTransitionInDuration", 0.5f);
                Set(owned.Transition, "m_overriddenTransitionOutDuration", 0.125f);
                owned.SetState(State.HeldIn);
                Call(owned.Transition, "TransitionIn");
                owned.SetState(State.HeldOut);
                Call(owned.Transition, "TransitionOut");
                owned.SetState(State.HeldOut);
                Call(owned.Transition, "OnMidPointReached");
                owned.SetState(State.HeldIn);
                Call(owned.Transition, "OnCompletion");
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 2 100", "onTransitionOut:3 8 110",
                    "onTransitionInComplete:2 8 110", "mid:2 8 110",
                    "onTransitionOutComplete:0 8 110", "done:0 8 110",
                }, owned.Log);
            }
        }

        // StartTransition (0x1b3feae/0x1b3feda) writes only the in override
        // and ContinueTransition (0x1b40037) only the out override; neither
        // touches the other leg. Seeded values start away from the default 0.
        // Mutations caught: StartTransition clearing the out override,
        // ContinueTransition clearing the in override.
        [Test]
        public void EachLegWritesOnlyItsOwnOverride()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Set(owned.Transition, "m_overriddenTransitionOutDuration", 0.625f);
                Assert.IsTrue(owned.Transition.StartTransition(0.5f));
                Assert.AreEqual(0.5f, owned.OverriddenIn);
                Assert.AreEqual(0.625f, owned.OverriddenOut, "Held StartTransition keeps the out override");
                owned.Transition.Action_TransitionAnimationComplete();
                Assert.AreEqual(0.625f, owned.OverriddenOut, "the Held midpoint keeps the out override");
                Assert.IsTrue(owned.Transition.ContinueTransition(0.125f));
                Assert.AreEqual(0.5f, owned.OverriddenIn, "ContinueTransition keeps the in override");
                Assert.AreEqual(0.125f, owned.OverriddenOut);
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 2 100", "onTransitionInComplete:2 2 100", "onTransitionOut:3 8 110",
                }, owned.Log);
            }

            using (var owned = new Owned(FullScreenTransitionType.Continuous))
            {
                Set(owned.Transition, "m_overriddenTransitionOutDuration", 0.625f);
                Assert.IsTrue(owned.Transition.StartTransition(0.5f));
                Assert.AreEqual(0.25f, owned.OverriddenIn);
                Assert.AreEqual(0.625f, owned.OverriddenOut, "Continuous StartTransition keeps the out override");
            }
        }

        // Action_TransitionAnimationComplete 0x1b40199/0x1b401ce (and
        // OnMidPointReached 0x1b40209, OnCompletion 0x1b40259) load the cached
        // callback from the field only after UnityEvent.Invoke returns, so a
        // listener that re-caches changes which callback runs. Expected logs
        // follow the fixture speeds (in 4, out 5).
        // Mutations caught: reading either cache into a local before the event.
        [Test]
        public void CachedCallbacksAreReadAfterTheCompletionEvents()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Action mid1 = owned.Callback("mid1"), done1 = owned.Callback("done1");
                Action mid2 = owned.Callback("mid2"), done2 = owned.Callback("done2");
                Assert.IsTrue(owned.Transition.StartTransition(0f, mid1, done1));
                owned.Transition.Action_TransitionAnimationComplete();
                Assert.IsTrue(owned.Transition.ContinueTransition());
                bool fired = false;
                owned.EventField("m_onTransitionOutComplete").AddListener(() =>
                {
                    if (fired) return;
                    fired = true;
                    Assert.IsTrue(owned.Transition.StartTransition(0f, mid2, done2));
                });
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "mid1:2 4 100", "onTransitionOut:3 5 110",
                    "onTransitionOutComplete:0 5 110", "onTransitionIn:1 4 110", "done2:1 4 110",
                }, owned.Log);
                Assert.AreSame(done2, owned.CachedCompletion);
                Assert.AreEqual(State.TransitioningIn, owned.Transition.CurrentState);
            }

            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Action mid1 = owned.Callback("mid1"), mid2 = owned.Callback("mid2");
                Assert.IsTrue(owned.Transition.StartTransition(0f, mid1, null));
                owned.EventField("m_onTransitionInComplete").AddListener(() =>
                {
                    owned.Transition.ResetTransition();
                    Set(owned.Transition, "m_cachedMidpointCallback", mid2);
                });
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "onReset:0 4 101", "mid2:0 4 101",
                }, owned.Log);
                Assert.AreEqual(State.HeldOut, owned.Transition.CurrentState);
            }
        }

        // Neither OnMidPointReached (0x1b4020e..0x1b4022c) nor OnCompletion
        // (0x1b4025e..0x1b4027c) re-checks the state after its event: a
        // non-null cached callback always runs. Mutations caught: a HeldIn
        // guard on the midpoint, a HeldOut guard on the completion.
        [Test]
        public void CachedCallbacksRunEvenWhenAListenerChangedTheState()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Assert.IsTrue(owned.Transition.StartTransition(0f, owned.Callback("mid"), owned.Callback("done")));
                owned.EventField("m_onTransitionInComplete").AddListener(owned.Transition.ResetTransition);
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "onReset:0 4 101", "mid:0 4 101",
                }, owned.Log);
            }

            // StartTransition overwrites the completion cache, so the listener
            // passes the same completion back to keep it cached.
            using (var owned = new Owned(FullScreenTransitionType.Held))
            {
                Action done = owned.Callback("done");
                Assert.IsTrue(owned.Transition.StartTransition(0f, null, done));
                owned.Transition.Action_TransitionAnimationComplete();
                Assert.IsTrue(owned.Transition.ContinueTransition());
                bool fired = false;
                owned.EventField("m_onTransitionOutComplete").AddListener(() =>
                {
                    if (fired) return;
                    fired = true;
                    Assert.IsTrue(owned.Transition.StartTransition(0f, null, done));
                });
                owned.Transition.Action_TransitionAnimationComplete();
                CollectionAssert.AreEqual(new[]
                {
                    "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "onTransitionOut:3 5 110",
                    "onTransitionOutComplete:0 5 110", "onTransitionIn:1 4 110", "done:1 4 110",
                }, owned.Log);
            }
        }

        // StartTransition 0x1b3fea3 takes the Continuous branch only when
        // m_type == 0 (cmp dword [rbx+0x20],0; je); every other value, including
        // an undeclared 2, takes the Held branch. Override 1 gives speed 1.
        // Mutations caught: testing for Held instead of for Continuous.
        [Test]
        public void UndeclaredTransitionTypeTakesTheHeldBranch()
        {
            using (var owned = new Owned((FullScreenTransitionType)2))
            {
                Action mid = owned.Callback("mid"), done = owned.Callback("done");
                Assert.IsTrue(owned.Transition.StartTransition(1f, mid, done));
                Assert.AreSame(mid, owned.CachedMidpoint, "no closure outside Continuous");
                Assert.AreSame(done, owned.CachedCompletion);
                Assert.AreEqual(1f, owned.OverriddenIn);
                CollectionAssert.AreEqual(new[] { "onTransitionIn:1 1 100" }, owned.Log);
                owned.Transition.Action_TransitionAnimationComplete();
                Assert.AreEqual(State.HeldIn, owned.Transition.CurrentState);
                CollectionAssert.AreEqual(new[] { "onTransitionIn:1 1 100", "onTransitionInComplete:2 1 100", "mid:2 1 100" }, owned.Log);
            }
        }

        // Action_TransitionAnimationComplete 0x1b4017f/0x1b40184 matches the
        // state exactly against 3 and 1 (cmp eax,3; je / cmp eax,1; jne), so
        // undeclared values below or above the State range are no-ops.
        // Mutations caught: an ordered comparison (state >= TransitioningOut)
        // or completing from a default branch.
        [Test]
        public void AnimationCompleteIgnoresUndeclaredStates()
        {
            foreach (State state in new[] { (State)4, (State)(-1) })
            {
                using (var owned = new Owned(FullScreenTransitionType.Held))
                {
                    owned.SetState(state);
                    Set(owned.Transition, "m_cachedMidpointCallback", owned.Callback("mid"));
                    Set(owned.Transition, "m_cachedCompletionCallback", owned.Callback("done"));
                    owned.Transition.Action_TransitionAnimationComplete();
                    CollectionAssert.IsEmpty(owned.Log, ((int)state).ToString(CultureInfo.InvariantCulture));
                    Assert.AreEqual(state, owned.Transition.CurrentState);
                    Assert.AreEqual(1f, owned.Animator.speed);
                    Assert.AreEqual("000", owned.Triggers());
                }
            }
        }

        // Continuous 0x1b3fecc multiplies by 0.5f and stores the product raw
        // into the closure (0x1b3fed4) and the in override (0x1b3feda); b__0
        // later stores the same half as the out override. Negative and NaN
        // halves are kept and only fall back at the speed choice.
        // Mutations caught: clamping or guarding the half before storing it.
        [Test]
        public void ContinuousStoresNegativeAndNaNHalvesRaw()
        {
            foreach (float total in new[] { -2f, float.NaN })
            {
                using (var owned = new Owned(FullScreenTransitionType.Continuous))
                {
                    string label = total.ToString("R", CultureInfo.InvariantCulture);
                    Assert.IsTrue(owned.Transition.StartTransition(total, owned.Callback("mid"), owned.Callback("done")));
                    float closureHalf = (float)Get(owned.CachedMidpoint.Target, "halfTotalDurationOverride");
                    if (float.IsNaN(total))
                    {
                        Assert.IsTrue(float.IsNaN(owned.OverriddenIn), label);
                        Assert.IsTrue(float.IsNaN(closureHalf), label);
                    }
                    else
                    {
                        Assert.AreEqual(-1f, owned.OverriddenIn, label);
                        Assert.AreEqual(-1f, closureHalf, label);
                    }
                    owned.Transition.Action_TransitionAnimationComplete();
                    if (float.IsNaN(total))
                        Assert.IsTrue(float.IsNaN(owned.OverriddenOut), label);
                    else
                        Assert.AreEqual(-1f, owned.OverriddenOut, label);
                    CollectionAssert.AreEqual(new[]
                    {
                        "onTransitionIn:1 4 100", "onTransitionInComplete:2 4 100", "mid:2 4 100", "onTransitionOut:3 5 110",
                    }, owned.Log, label);
                }
            }
        }

        // With no Animator the first animator access (set_speed at 0x1b3ff68,
        // 0x1b40091; SetTrigger at 0x1b4014d) faults after the state and caches
        // were written and before any UnityEvent. Mutations caught: writing the
        // state after the animator calls, a null-animator guard, raising the
        // event first.
        [Test]
        public void MissingAnimatorFaultsAfterTheStateAndCachesAreWritten()
        {
            using (var owned = new Owned(FullScreenTransitionType.Held, withAnimator: false))
            {
                Action mid = owned.Callback("mid"), done = owned.Callback("done");
                Assert.Throws<NullReferenceException>(() => owned.Transition.StartTransition(0.5f, mid, done));
                Assert.AreEqual(State.TransitioningIn, owned.Transition.CurrentState);
                Assert.AreSame(mid, owned.CachedMidpoint);
                Assert.AreSame(done, owned.CachedCompletion);
                Assert.AreEqual(0.5f, owned.OverriddenIn);

                owned.SetState(State.HeldIn);
                Set(owned.Transition, "m_cachedCompletionCallback", null);
                Assert.Throws<NullReferenceException>(() => owned.Transition.ContinueTransition(0.25f, done));
                Assert.AreEqual(State.TransitioningOut, owned.Transition.CurrentState);
                Assert.AreSame(done, owned.CachedCompletion);
                Assert.AreEqual(0.25f, owned.OverriddenOut);

                Assert.Throws<NullReferenceException>(() => owned.Transition.ResetTransition());
                Assert.AreEqual(State.HeldOut, owned.Transition.CurrentState);
                CollectionAssert.IsEmpty(owned.Log);
            }
        }
    }
}
