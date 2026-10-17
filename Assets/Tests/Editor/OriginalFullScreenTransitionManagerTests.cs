using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.InOutTestSupport;
using Object = UnityEngine.Object;
using State = Hardlight.FullScreenTransitionController.State;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.FullScreenTransitionManager (TypeDef 0x02000023, dump.cs
    // 11859) against the x86-64 bodies 0x1B40350..0x1B40918. The manager lives on an inactive host
    // with its required Canvas (sorting layer Default, order 5). Controllers are genuine
    // FullScreenTransitionController components, each with its own Animator and in-memory
    // AnimatorController (triggers In/Out/Reset, in 0.25 s, out 0.2 s), as in the controller's own
    // tests. Only the Awake case touches the system registry, through an owned registry row, and it
    // restores the singleton Instance.
    public sealed class OriginalFullScreenTransitionManagerTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly Owned Owned = new Owned();
            readonly List<AnimatorController> m_controllers = new List<AnimatorController>();
            public readonly FullScreenTransitionManager Manager;
            public readonly Canvas Canvas;

            public Fixture(string name)
            {
                try
                {
                    Manager = Owned.Host(name, false).AddComponent<FullScreenTransitionManager>();
                    Canvas = Manager.GetComponent<Canvas>();
                    Canvas.sortingOrder = 5;
                    Set(Manager, "m_transitionCanvas", Canvas);
                }
                catch { Dispose(); throw; }
            }

            public FullScreenTransitionController Controller(string name, FullScreenTransitionType type)
            {
                var animatorController = new AnimatorController { name = name, hideFlags = HideFlags.HideAndDontSave };
                m_controllers.Add(animatorController);
                animatorController.AddLayer("Base");
                foreach (string trigger in new[] { "In", "Out", "Reset" })
                    animatorController.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                Undo.ClearUndo(animatorController);
                GameObject host = Owned.Host(name, true);
                Animator animator = host.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                animator.Rebind();
                Assert.That(animator.isInitialized, Is.True, "fixture Animator binds its controller");

                var controller = host.AddComponent<FullScreenTransitionController>();
                Set(controller, "m_type", type);
                Set(controller, "m_animator", animator);
                Set(controller, "m_animateInTrigger", "In");
                Set(controller, "m_animateOutTrigger", "Out");
                Set(controller, "m_animatorResetTrigger", "Reset");
                Set(controller, "m_transitionInDuration", 0.25f);
                Set(controller, "m_transitionOutDuration", 0.2f);
                foreach (string field in new[] { "onTransitionIn", "onTransitionInComplete", "onTransitionOut", "onTransitionOutComplete", "onReset" })
                    Set(controller, "m_" + field, Owned.Event(name + "." + field));
                return controller;
            }

            public void Register(params FullScreenTransitionController[] controllers)
            {
                var table = Table(Manager);
                foreach (FullScreenTransitionController controller in controllers)
                    table.Add(controller.Type, controller);
            }

            public void Dispose()
            {
                try { Owned.Dispose(); }
                finally
                {
                    foreach (AnimatorController controller in m_controllers)
                    {
                        foreach (AnimatorControllerLayer layer in controller.layers)
                            if (layer.stateMachine != null) Object.DestroyImmediate(layer.stateMachine);
                        Undo.ClearUndo(controller);
                        Object.DestroyImmediate(controller);
                    }
                    m_controllers.Clear();
                }
            }
        }

        static Dictionary<FullScreenTransitionType, FullScreenTransitionController> Table(FullScreenTransitionManager manager) =>
            Get<Dictionary<FullScreenTransitionType, FullScreenTransitionController>>(manager, "m_transitionControllers");

        static void SetState(FullScreenTransitionController controller, State state) => Call(controller, "set_CurrentState", state);

        // dump.cs 11859: sealed MonoSingleton<FullScreenTransitionManager>, NullChecks then
        // ArrayBoundsChecks then RequireComponent(Canvas); the field order and the parameter names and
        // defaults of the public API ("Default", 0, 0, null, null).
        [Test]
        public void ManagerRetainsWholeDeclaration()
        {
            Type type = typeof(FullScreenTransitionManager);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoSingleton<FullScreenTransitionManager>)));
            Assert.That((int)type.Attributes, Is.EqualTo(0x100101));
            OriginalHLUIAnimationInOutTests.CheckOptions(type, 1, 2);
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Length, Is.EqualTo(3));
            Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(RequireComponent)));
            Assert.That(attributes[2].ConstructorArguments.Single().Value, Is.EqualTo(typeof(Canvas)));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "transitionsController", "m_transitionCanvas", "m_transitionControllers" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(FullScreenTransitionController[]), typeof(Canvas), typeof(Dictionary<FullScreenTransitionType, FullScreenTransitionController>),
            }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            Assert.That(fields.Select(f => f.GetCustomAttributesData().Count).ToArray(), Is.EqualTo(new[] { 1, 1, 0 }));

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "Awake", "Start", "IsTransitionActive", "StartTransition", "ContinueTransition", "ResetTransitions", "SetSortingLayerAndOrder",
            }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 0xC4, 0x81, 0x86, 0x86, 0x86, 0x86, 0x81 }));
            Assert.That(methods.Select(m => m.ReturnType).ToArray(), Is.EqualTo(new[] { typeof(void), typeof(void), typeof(bool), typeof(bool), typeof(bool), typeof(void), typeof(void) }));

            ParameterInfo[] start = methods[3].GetParameters();
            Assert.That(start.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "type", "sortingLayer", "sortingOrder", "overrideDuration", "midpointCallback", "completionCallback" }));
            Assert.That(start.Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(FullScreenTransitionType), typeof(string), typeof(int), typeof(float), typeof(Action), typeof(Action) }));
            Assert.That(start.Skip(1).Select(p => p.DefaultValue).ToArray(), Is.EqualTo(new object[] { "Default", 0, 0f, null, null }));
            Assert.That(start[0].HasDefaultValue, Is.False);

            ParameterInfo[] resume = methods[4].GetParameters();
            Assert.That(resume.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "type", "overrideDuration", "completionCallback" }));
            Assert.That(resume.Skip(1).Select(p => p.DefaultValue).ToArray(), Is.EqualTo(new object[] { 0f, null }));
            Assert.That(methods[2].GetParameters().Single().Name, Is.EqualTo("type"));
            ParameterInfo[] sorting = methods[6].GetParameters();
            Assert.That(sorting.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "sortingLayer", "sortingOrder" }));
            Assert.That(sorting.Select(p => p.DefaultValue).ToArray(), Is.EqualTo(new object[] { "Default", 0 }));
            Assert.That(methods.SelectMany(m => m.GetCustomAttributesData()), Is.Empty);
        }

        // .ctor 0x060000f7 @0x1B40890: a new empty dictionary, then the MonoSingleton constructor.
        [Test]
        public void ConstructionCreatesAnEmptyControllerTable()
        {
            using (var fixture = new Fixture("TransitionManager-ctor"))
            {
                Assert.That(Table(fixture.Manager), Is.Not.Null);
                Assert.That(Table(fixture.Manager), Is.Empty);
            }
        }

        // Awake 0x060000f0 @0x1B40350: MonoSingleton Awake (registers this as the system and Instance),
        // then each array element in order is added under its Type unless that type is already present.
        [Test]
        public void AwakeRegistersTheSingletonAndKeepsTheFirstControllerOfEachType()
        {
            using (var fixture = new Fixture("TransitionManager-awake"))
            {
                FullScreenTransitionController first = fixture.Controller("held-1", FullScreenTransitionType.Held);
                FullScreenTransitionController continuous = fixture.Controller("continuous", FullScreenTransitionType.Continuous);
                FullScreenTransitionController second = fixture.Controller("held-2", FullScreenTransitionType.Held);
                Set(fixture.Manager, "transitionsController", new[] { first, continuous, second });
                MethodInfo setInstance = typeof(MonoSingleton<FullScreenTransitionManager>).GetProperty("Instance").GetSetMethod(true);
                var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<FullScreenTransitionManager>(), FullScreenTransitionManager.Instance);
                try
                {
                    row.RegisterUsing(fixture.Manager, () => Call(fixture.Manager, "Awake"));
                    Assert.That(FullScreenTransitionManager.Instance, Is.SameAs(fixture.Manager));
                    var table = Table(fixture.Manager);
                    Assert.That(table.Count, Is.EqualTo(2));
                    Assert.That(table[FullScreenTransitionType.Held], Is.SameAs(first));
                    Assert.That(table[FullScreenTransitionType.Continuous], Is.SameAs(continuous));
                }
                finally
                {
                    try { row.Dispose(); }
                    finally { setInstance.Invoke(null, new object[] { null }); }
                }
            }
        }

        // Start 0x060000f1 @0x1B40420: an assigned canvas returns at once; a missing one logs the
        // original message and falls back to GetComponent<Canvas>().
        [Test]
        public void StartFallsBackToTheOwnCanvasAndReportsIt()
        {
            using (var log = new OwnedCoreErrorLog())
            using (var fixture = new Fixture("TransitionManager-start"))
            {
                Canvas other = fixture.Owned.Host("TransitionManager-other-canvas", false, typeof(Canvas)).GetComponent<Canvas>();
                Set(fixture.Manager, "m_transitionCanvas", other);
                Call(fixture.Manager, "Start");
                Assert.That(Get(fixture.Manager, "m_transitionCanvas"), Is.SameAs(other));
                Assert.That(log.Errors, Is.Empty);

                Set(fixture.Manager, "m_transitionCanvas", null);
                Call(fixture.Manager, "Start");
                Assert.That(Get(fixture.Manager, "m_transitionCanvas"), Is.SameAs(fixture.Canvas));
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    "FullScreenTransitionManager::Start - transition canvas is not set, please serialize a canvas and save the project.:0",
                }));
            }
        }

        // IsTransitionActive 0x060000f2 @0x1B404F0: false for an unregistered type, otherwise
        // controller state (+0x90) != HeldOut.
        [Test]
        public void IsTransitionActiveIsAnyStateButHeldOut()
        {
            using (var fixture = new Fixture("TransitionManager-active"))
            {
                FullScreenTransitionController held = fixture.Controller("held", FullScreenTransitionType.Held);
                fixture.Register(held);
                Assert.That(fixture.Manager.IsTransitionActive(FullScreenTransitionType.Continuous), Is.False);
                Assert.That(fixture.Manager.IsTransitionActive(FullScreenTransitionType.Held), Is.False);
                foreach (State state in new[] { State.TransitioningIn, State.HeldIn, State.TransitioningOut })
                {
                    SetState(held, state);
                    Assert.That(fixture.Manager.IsTransitionActive(FullScreenTransitionType.Held), Is.True, state.ToString());
                }
            }
        }

        // StartTransition 0x060000f3 @0x1B40560: SetSortingLayerAndOrder runs before the lookup, so the
        // order changes even when no controller is registered or the controller refuses; otherwise the
        // controller's StartTransition result is returned (Held: speed 1 / 0.5 = 2, TransitioningIn).
        [Test]
        public void StartTransitionAppliesTheSortingFirstAndDelegates()
        {
            using (var fixture = new Fixture("TransitionManager-start-transition"))
            {
                FullScreenTransitionController held = fixture.Controller("held", FullScreenTransitionType.Held);
                fixture.Register(held);
                Assert.That(fixture.Manager.StartTransition(FullScreenTransitionType.Continuous, "Default", 3), Is.False);
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(3));

                var calls = new List<string>();
                Assert.That(fixture.Manager.StartTransition(FullScreenTransitionType.Held, "Default", 4, 0.5f, () => calls.Add("mid"), () => calls.Add("done")), Is.True);
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(4));
                Assert.That(held.CurrentState, Is.EqualTo(State.TransitioningIn));
                Assert.That(held.GetComponent<Animator>().speed, Is.EqualTo(2f));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "held.onTransitionIn" }));

                Assert.That(fixture.Manager.StartTransition(FullScreenTransitionType.Held, "Default", 6), Is.False, "the controller refuses while transitioning");
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(6));
                Assert.That(calls, Is.Empty);
            }
        }

        // ContinueTransition 0x060000f4 @0x1B406A0 (controller body inlined): only a HeldIn controller
        // continues; it stores the override, adopts the completion when none is cached, becomes
        // TransitioningOut at speed 1 / 0.25 = 4 and fires onTransitionOut. Sorting is never touched.
        [Test]
        public void ContinueTransitionOnlyContinuesAHeldInController()
        {
            using (var fixture = new Fixture("TransitionManager-continue"))
            {
                FullScreenTransitionController held = fixture.Controller("held", FullScreenTransitionType.Held);
                fixture.Register(held);
                Assert.That(fixture.Manager.ContinueTransition(FullScreenTransitionType.Continuous), Is.False);
                Assert.That(fixture.Manager.ContinueTransition(FullScreenTransitionType.Held), Is.False, "HeldOut");

                SetState(held, State.HeldIn);
                Action done = () => { };
                Assert.That(fixture.Manager.ContinueTransition(FullScreenTransitionType.Held, 0.25f, done), Is.True);
                Assert.That(held.CurrentState, Is.EqualTo(State.TransitioningOut));
                Assert.That(held.GetComponent<Animator>().speed, Is.EqualTo(4f));
                Assert.That(Get(held, "m_overriddenTransitionOutDuration"), Is.EqualTo(0.25f));
                Assert.That(Get(held, "m_cachedCompletionCallback"), Is.SameAs(done));
                Assert.That(fixture.Owned.Log, Is.EqualTo(new[] { "held.onTransitionOut" }));
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(5));
                Assert.That(fixture.Manager.ContinueTransition(FullScreenTransitionType.Held), Is.False, "already continuing");
            }
        }

        // ResetTransitions 0x060000f5 @0x1B407B0: every registered controller is reset (HeldOut, reset
        // trigger, onReset); a controller only present in the serialized array is not.
        [Test]
        public void ResetTransitionsResetsOnlyRegisteredControllers()
        {
            using (var fixture = new Fixture("TransitionManager-reset"))
            {
                FullScreenTransitionController held = fixture.Controller("held", FullScreenTransitionType.Held);
                FullScreenTransitionController continuous = fixture.Controller("continuous", FullScreenTransitionType.Continuous);
                FullScreenTransitionController spare = fixture.Controller("spare", FullScreenTransitionType.Held);
                Set(fixture.Manager, "transitionsController", new[] { held, continuous, spare });
                fixture.Register(held, continuous);
                SetState(held, State.TransitioningIn);
                SetState(continuous, State.HeldIn);
                SetState(spare, State.HeldIn);
                fixture.Manager.ResetTransitions();
                Assert.That(held.CurrentState, Is.EqualTo(State.HeldOut));
                Assert.That(continuous.CurrentState, Is.EqualTo(State.HeldOut));
                Assert.That(spare.CurrentState, Is.EqualTo(State.HeldIn));
                CollectionAssert.AreEquivalent(new[] { "held.onReset", "continuous.onReset" }, fixture.Owned.Log);
                Assert.That(held.GetComponent<Animator>().GetBool("Reset"), Is.True);
                Assert.That(spare.GetComponent<Animator>().GetBool("Reset"), Is.False);
            }
        }

        // SetSortingLayerAndOrder 0x060000f6 @0x1B40610: an equal layer name is left alone; the order is
        // only written when it differs and is not zero, so 0 can never reset an order.
        [Test]
        public void SortingOrderZeroNeverOverridesTheCanvas()
        {
            using (var fixture = new Fixture("TransitionManager-sorting"))
            {
                Assert.That(fixture.Canvas.sortingLayerName, Is.EqualTo("Default"));
                Call(fixture.Manager, "SetSortingLayerAndOrder", "Default", 0);
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(5));
                Call(fixture.Manager, "SetSortingLayerAndOrder", "Default", -2);
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(-2));
                Call(fixture.Manager, "SetSortingLayerAndOrder", "Default", 0);
                Assert.That(fixture.Canvas.sortingOrder, Is.EqualTo(-2));
                Assert.That(fixture.Canvas.sortingLayerName, Is.EqualTo("Default"));
            }
        }
    }
}
