using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.SurfaceAnimatedKnot (TypeDef 0x02000a9b, x86-64 0x6D23F0..0x6D2A00) and
    // SurfaceAnimator (0x02000a9d, 0x6D2A10..0x6D2EA0) on owned, inactive GameObjects with a
    // genuine InertSpline behind a Spline facade. The animator's field initializer takes the
    // typed TimeManager reference, so those cases lease that row; only the subscription case
    // registers an owned TimeManager into it, and unregisters it before the lease is released.
    public sealed class OriginalSurfaceAnimatorTests
    {
        private sealed class InertFixture : IDisposable
        {
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-surface-animator");
            public readonly Spline Facade;
            public readonly InertSpline Inert;
            public readonly InertSplineKnot[] Knots;
            public readonly Transform Target;
            public int KnotsUpdated;

            public InertFixture(int knotCount)
            {
                try
                {
                    GameObject splineObject = Objects.Create("spline");
                    splineObject.SetActive(false);
                    Inert = splineObject.AddComponent<InertSpline>();
                    Knots = Enumerable.Range(0, knotCount).Select(_ => new InertSplineKnot()).ToArray();
                    Set(Inert, "m_knots", Knots);
                    Facade = splineObject.AddComponent<Spline>();
                    Set(Facade, "m_inertSpline", Inert);
                    Set(Facade, "m_activeSplineType", SplineVariantType.Inert);
                    Inert.OnSplineKnotsUpdated += () => KnotsUpdated++;
                    Target = Objects.Create("target").transform;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public SurfaceAnimatedKnot Knot(bool allKnots, int index = 0, int axis = 0)
            {
                var knot = new SurfaceAnimatedKnot();
                Set(knot, "m_spline", Facade);
                Set(knot, "m_allKnots", allKnots);
                Set(knot, "m_index", index);
                Set(knot, "m_axis", Enum.ToObject(typeof(SurfaceAnimatedKnot).GetNestedType("Axis", BindingFlags.NonPublic), axis));
                Set(knot, "m_transform", Target);
                return knot;
            }

            public void Dispose() => Objects.Dispose();
        }

        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(SurfaceAnimatedKnot).Attributes, Is.EqualTo(0x00102001));
            Type axis = typeof(SurfaceAnimatedKnot).GetNestedType("Axis", BindingFlags.NonPublic);
            Assert.That((int)axis.Attributes, Is.EqualTo(0x00000103));
            Assert.That(Enum.GetNames(axis), Is.EqualTo(new[] { "X", "Y", "Z" }));
            Assert.That(Fields(typeof(SurfaceAnimatedKnot)), Is.EqualTo(new[]
            {
                "m_spline:Spline:0001", "m_allKnots:Boolean:0001", "m_index:Int32:0001", "m_axis:Axis:0001",
                "m_transform:Transform:0001", "m_isValid:Boolean:0001", "m_inertSpline:IInertSplineRuntimeHandle:0001",
                "m_inertKnot:IInertSplineKnotRuntimeHandle:0001"
            }));
            Assert.That(Methods(typeof(SurfaceAnimatedKnot)), Is.EqualTo(new[]
                { "CalculateCachedValues:0086", "DoUpdate:0086", "TransformKnot:0081", "TransformSpline:0081", ".ctor:1886" }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(SurfaceAnimatedKnot).GetField("m_index", field)),
                Is.EqualTo("ShowIfAttribute(m_allKnots,False) MinAttribute(0) SerializeField() TooltipAttribute(The single knot index that is animated.)"));
            Assert.That(AttributeRow(typeof(SurfaceAnimatedKnot).GetField("m_axis", field)),
                Is.EqualTo("SerializeField() ShowIfAttribute(m_allKnots,null) TooltipAttribute(The axis to align spline to.)"));

            Assert.That((int)typeof(SurfaceAnimator).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(SurfaceAnimator).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(typeof(SurfaceAnimator).GetInterfaces(), Does.Contain(typeof(ITimeScaled)));
            Assert.That(Fields(typeof(SurfaceAnimator)), Is.EqualTo(new[]
            {
                "m_knots:List`1:0001", "m_timeCategory:TimeCategory:0001", "m_updateOn:UpdateOn:0001",
                "m_timeManagerRef:SystemRef`1:0021", "<IsPaused>k__BackingField:Boolean:0001"
            }));
            Assert.That(Methods(typeof(SurfaceAnimator)), Is.EqualTo(new[]
            {
                "get_IsPaused:09e6", "set_IsPaused:09e6", "Awake:0081", "OnEnable:0081", "OnDisable:0081", "OnUpdate:01e6",
                "OnFixedUpdate:01e6", "OnLateUpdate:01e6", "DoUpdate:0081", ".ctor:1886"
            }));
            // 06003d1e is the OnEnable lambda <OnEnable>b__9_0(TimeManager) on the animator itself.
            MethodInfo lambda = typeof(SurfaceAnimator).GetMethod("<OnEnable>b__9_0", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lambda?.GetParameters().Single().ParameterType, Is.EqualTo(typeof(TimeManager)));
        }

        // CalculateCachedValues 0x6D23F0: IsInst IInertSplineRuntimeHandle (0x6D244E), m_isValid =
        // non-null (0x6D2479); for a single knot, interface slot 14 GetKnotRuntimeHandle(m_index) and
        // IsInst IInertSplineKnotRuntimeHandle decide it again (0x6D253B).
        [Test]
        public void CachedValuesNeedAnInertSplineAndForOneKnotAnInertKnot()
        {
            using (var fixture = new InertFixture(2))
            {
                SurfaceAnimatedKnot single = fixture.Knot(false, 1);
                single.CalculateCachedValues();
                Assert.That(Get(single, "m_isValid"), Is.True);
                Assert.That(Get(single, "m_inertSpline"), Is.SameAs(fixture.Inert));
                Assert.That(Get(single, "m_inertKnot"), Is.SameAs(fixture.Knots[1]));

                SurfaceAnimatedKnot all = fixture.Knot(true, 1);
                all.CalculateCachedValues();
                Assert.That(Get(all, "m_isValid"), Is.True);
                Assert.That(Get(all, "m_inertKnot"), Is.Null, "all knots never look up a knot");

                Set(fixture.Inert, "m_knots", new InertSplineKnot[0]);
                single.CalculateCachedValues();
                Assert.That(Get(single, "m_isValid"), Is.False, "an empty inert spline has no knot handle");

                Set(fixture.Facade, "m_activeSplineType", SplineVariantType.Editable);
                all.CalculateCachedValues();
                Assert.That(Get(all, "m_isValid"), Is.False, "a missing or non-inert spline is invalid");
                Assert.That(Get(all, "m_inertSpline"), Is.Null);
            }
        }

        // TransformKnot 0x6D2620: localPosition = spline.InverseTransformPoint(target.position);
        // WithinTolerance(knot location, localPosition, 0.0001 at 0x29a6dfc) returns false before any
        // rotation is read; otherwise SetTransform(localPosition, Inverse(spline.rotation) *
        // target.rotation) and DoUpdate 0x6D2550 calls KnotsUpdated.
        [Test]
        public void SingleKnotTakesTheTargetPoseInSplineSpaceOutsideTheTolerance()
        {
            using (var fixture = new InertFixture(2))
            {
                Transform splineTransform = fixture.Facade.transform;
                splineTransform.SetPositionAndRotation(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                fixture.Target.SetPositionAndRotation(new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(30f, 0f, 0f));
                SurfaceAnimatedKnot knot = fixture.Knot(false, 1);
                knot.CalculateCachedValues();

                knot.DoUpdate(0f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(1));
                AreClose(new Vector3(-2f, 0f, 0f), fixture.Knots[1].Transform.Location, "spline-space location");
                Assert.That(Quaternion.Angle(Quaternion.Euler(30f, 0f, 0f), fixture.Knots[1].Transform.Orientation), Is.LessThan(0.01f));
                Assert.That(fixture.Knots[0].Transform.Location, Is.EqualTo(Vector3.zero), "other knots are untouched");

                fixture.Target.rotation = Quaternion.identity;
                fixture.Target.position += new Vector3(0f, 0f, 0.00005f);
                knot.DoUpdate(0f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(1), "a location inside the tolerance skips the update");
                Assert.That(Quaternion.Angle(Quaternion.Euler(30f, 0f, 0f), fixture.Knots[1].Transform.Orientation), Is.LessThan(0.01f),
                    "the rotation is not refreshed either");

                fixture.Target.position += new Vector3(0f, 1f, 0f);
                knot.DoUpdate(0f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(2));
                Assert.That(Quaternion.Angle(Quaternion.Euler(0f, -90f, 0f), fixture.Knots[1].Transform.Orientation), Is.LessThan(0.01f));

                Set(knot, "m_isValid", false);
                fixture.Target.position += Vector3.one;
                knot.DoUpdate(0f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(2), "an invalid knot does nothing");
            }
        }

        // TransformSpline 0x6D28A0: axis right/up/forward for 0/1/2, else zero (0x6D2927); offset =
        // axis * Dot(spline.position - target.position, axis); a squared length below 0.0001
        // (ucomiss/ja at 0x6D29C5) returns false, otherwise the spline moves by -offset.
        [TestCase(0, 3f, 0f, 0f)]
        [TestCase(1, 0f, 2f, 0f)]
        [TestCase(2, 0f, 0f, 1f)]
        [TestCase(3, 0f, 0f, 0f)]
        public void AllKnotsMoveTheSplineAlongOneOfItsAxes(int axis, float x, float y, float z)
        {
            using (var fixture = new InertFixture(1))
            {
                fixture.Target.position = new Vector3(3f, 2f, 1f);
                SurfaceAnimatedKnot knot = fixture.Knot(true, 0, axis);
                knot.CalculateCachedValues();
                knot.DoUpdate(0f);
                AreClose(new Vector3(x, y, z), fixture.Facade.transform.position, "aligned spline");
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(axis == 3 ? 0 : 1));
                knot.DoUpdate(0f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(axis == 3 ? 0 : 1), "an aligned spline is left alone");
            }
        }

        // The axis comes from the spline's own transform (get_right 0x6D28DB). Turned 90 degrees
        // about y, its right is (0, 0, -1), so the offset is (0, 0, -1) * Dot((0, 0, 0) - (3, 2, 1),
        // (0, 0, -1)) = (0, 0, -1) and the spline moves to (0, 0, 1); world right would give (3, 0, 0).
        [Test]
        public void AllKnotsTakeTheAxisFromTheSplineTransform()
        {
            using (var fixture = new InertFixture(1))
            {
                fixture.Facade.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                fixture.Target.position = new Vector3(3f, 2f, 1f);
                SurfaceAnimatedKnot knot = fixture.Knot(true, 0, 0);
                knot.CalculateCachedValues();
                knot.DoUpdate(0f);
                AreClose(new Vector3(0f, 0f, 1f), fixture.Facade.transform.position, "moved along the turned right");
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(1));
            }
        }

        // The 0.0001 bound is on the squared offset: 0.009 (0.000081) stays, 0.011 (0.000121) moves.
        [TestCase(0.009f, false)]
        [TestCase(0.011f, true)]
        public void AllKnotsCompareTheSquaredOffsetWithTheTolerance(float distance, bool moves)
        {
            using (var fixture = new InertFixture(1))
            {
                fixture.Target.position = new Vector3(distance, 0f, 0f);
                SurfaceAnimatedKnot knot = fixture.Knot(true);
                knot.CalculateCachedValues();
                knot.DoUpdate(0f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(moves ? 1 : 0));
                Assert.That(fixture.Facade.transform.position.x, Is.EqualTo(moves ? distance : 0f).Within(1e-6f));
            }
        }

        // OnEnable 0x6D2B50 hands <OnEnable>b__9_0 to InvokeOnValid, which runs it at once for a
        // registered TimeManager; the lambda tail-calls TimeExtensions.Subscribe with m_timeCategory
        // (edx from this+0x28 at 0x6D2EC9), m_updateOn (ecx from this+0x2c at 0x6D2ECC) and "".
        // OnDisable 0x6D2BE0 gets the live system and tail-calls TimeExtensions.Unsubscribe with
        // the same two fields (0x6D2C42..0x6D2C5C). Both fields are set away from their defaults.
        // The owned TimeManager stays inactive: the Editor's Reset on AddComponent faults on its
        // null configuration (logged, as the maintained TimeManager verification expects), and
        // Awake, which needs the shared TimeCategoryConfiguration, never runs, so its three empty
        // phase dictionaries are added here. The TimeExtensions category lookup is leased.
        [Test]
        public void AnimatorSubscribesAndUnsubscribesWithItsOwnCategoryAndPhase()
        {
            FieldInfo lookupField = typeof(TimeExtensions).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object priorLookup = lookupField.GetValue(null);
            using (var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()))
            using (var objects = new OwnedWorldObjects("lucid-surface-animator-time"))
            {
                TimeManager manager = null;
                bool registered = false;
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    var environment = objects.Own(ScriptableObject.CreateInstance<TimeCategoryObject>());
                    var playerMovement = objects.Own(ScriptableObject.CreateInstance<TimeCategoryObject>());
                    Set(environment, "m_guid", "lucid-surface-animator-environment");
                    Set(playerMovement, "m_guid", "lucid-surface-animator-player-movement");
                    var lookup = objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>());
                    lookup.Dictionary.Add(TimeCategory.Environment, environment);
                    lookup.Dictionary.Add(TimeCategory.PlayerMovement, playerMovement);
                    lookupField.SetValue(null, lookup);

                    GameObject timeHost = objects.Create("time");
                    timeHost.SetActive(false);
                    LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
                    manager = timeHost.AddComponent<TimeManager>();
                    var phases = (IDictionary)Get(manager, "m_updateObjects");
                    Type phaseType = phases.GetType().GetGenericArguments()[1];
                    foreach (UpdateOn updateOn in new[] { UpdateOn.FixedUpdate, UpdateOn.LateUpdate, UpdateOn.Update })
                        phases.Add(updateOn, Activator.CreateInstance(phaseType));

                    GameObject host = objects.Create("animator");
                    host.SetActive(false);
                    var animator = host.AddComponent<SurfaceAnimator>();
                    Set(animator, "m_timeCategory", TimeCategory.PlayerMovement);
                    Set(animator, "m_updateOn", UpdateOn.LateUpdate);
                    ProcessManager.RegisterSystem(manager);
                    registered = true;

                    Call(animator, "OnEnable");
                    var late = (IDictionary)phases[UpdateOn.LateUpdate];
                    Assert.That(late.Count, Is.EqualTo(1));
                    Assert.That(late.Contains(playerMovement), Is.True, "the animator's own category");
                    var subscriptions = (IList)late[playerMovement];
                    Assert.That(subscriptions.Count, Is.EqualTo(1));
                    object subscription = subscriptions[0];
                    Assert.That(Get(subscription, "TimeScaledObject"), Is.SameAs(animator));
                    Assert.That(Get(subscription, "IsSubscribed"), Is.True);
                    Assert.That(Get(subscription, "TransformPath"), Is.EqualTo(""));
                    Assert.That(((IDictionary)phases[UpdateOn.FixedUpdate]).Count, Is.Zero, "not the field's FixedUpdate default");
                    Assert.That(((IDictionary)phases[UpdateOn.Update]).Count, Is.Zero, "not Subscribe's Update default");

                    Call(animator, "OnDisable");
                    Assert.That(Get(subscription, "IsSubscribed"), Is.False, "unsubscribed from the same category and phase");
                    Assert.That(subscriptions.Count, Is.EqualTo(1), "Unsubscribe only marks the entry");
                },
                () => { if (registered) ProcessManager.UnregisterSystem(manager); },
                objects.Dispose,
                () => lookupField.SetValue(null, priorLookup));
            }
        }

        // SurfaceAnimator..ctor 0x6D2DC0: a new list, the 64-bit store of Environment (0x34dab335)
        // and FixedUpdate (0x09869c05) at 0x6D2E48, then GetSystemRef<TimeManager>(null, true).
        // Awake 0x6D2A30 caches every knot; OnUpdate/OnFixedUpdate/OnLateUpdate all jump to DoUpdate
        // 0x6D2C80. OnDisable 0x6D2BE0 returns while the reference's system IsNull() and, through
        // the separate reference test (test rdi / je at 0x6D2C15), while the reference itself is null.
        [Test]
        public void AnimatorCachesAndUpdatesEveryKnotFromEachCallback()
        {
            using (var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()))
            using (var fixture = new InertFixture(1))
            {
                GameObject host = fixture.Objects.Create("animator");
                host.SetActive(false);
                var animator = host.AddComponent<SurfaceAnimator>();
                Assert.That(Get(animator, "m_knots"), Is.Empty);
                Assert.That(Get(animator, "m_timeCategory"), Is.EqualTo(TimeCategory.Environment));
                Assert.That(Get(animator, "m_updateOn"), Is.EqualTo(UpdateOn.FixedUpdate));
                var timeManagerRef = (SystemRef<TimeManager>)Get(animator, "m_timeManagerRef");
                Assert.That(timeManagerRef.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<TimeManager>()));

                SurfaceAnimatedKnot first = fixture.Knot(true, 0, 0);
                SurfaceAnimatedKnot second = fixture.Knot(true, 0, 1);
                Set(animator, "m_knots", new List<SurfaceAnimatedKnot> { first, second });
                Call(animator, "Awake");
                Assert.That(Get(first, "m_isValid"), Is.True);
                Assert.That(Get(second, "m_isValid"), Is.True);

                fixture.Target.position = new Vector3(1f, 2f, 0f);
                animator.OnUpdate(0.5f);
                AreClose(new Vector3(1f, 2f, 0f), fixture.Facade.transform.position, "both knots ran in order");
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(2));
                fixture.Target.position = new Vector3(4f, 2f, 0f);
                animator.OnFixedUpdate(0.5f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(3));
                fixture.Target.position = new Vector3(4f, 5f, 0f);
                animator.OnLateUpdate(0.5f);
                Assert.That(fixture.KnotsUpdated, Is.EqualTo(4));
                AreClose(new Vector3(4f, 5f, 0f), fixture.Facade.transform.position, "late update");

                Assert.That(timeManagerRef.IsNull(), Is.True);
                Assert.DoesNotThrow(() => Call(animator, "OnDisable"));
                Set(animator, "m_timeManagerRef", null);
                Assert.DoesNotThrow(() => Call(animator, "OnDisable"), "a null reference is not dereferenced");
                animator.IsPaused = true;
                Assert.That(animator.IsPaused, Is.True);
            }
        }
    }
}
