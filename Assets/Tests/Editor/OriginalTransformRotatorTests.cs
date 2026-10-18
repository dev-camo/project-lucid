using System;
using System.Collections;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.TransformAnimationAccess;
using static ProjectLucid.Tests.WorldMemberAccess;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // HardlightProject.TransformRotator, TransformRotatorLimits, TransformRotatorRandom,
    // TransformRotatorSingleCurve and TransformRotatorSnapped (TypeDefs 0x02000824..0x02000829,
    // x86-64 0x605160..0x6063F0) on owned GameObjects. Awake, InternalUpdate, OnSynchronise and
    // the private helpers are invoked directly because EditMode sends no messages; the OnEnable
    // and OnDisable bodies reach the shared TimeManager reference or start coroutines and are
    // not run here.
    public sealed class OriginalTransformRotatorTests
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            Assert.That((int)typeof(TransformRotator).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(TransformRotator).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(AttributeRow(typeof(TransformRotator)), Is.EqualTo("Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)"));
            Assert.That(Fields(typeof(TransformRotator)), Is.EqualTo(new[]
            {
                "m_targetTransform:Transform:0001", "m_randomiseRotationSpeedOnAwake:Boolean:0001", "m_rotationSpeed:Vector3:0001",
                "m_rotationSpeedMin:Vector3:0001", "m_rotationSpeedMax:Vector3:0001", "m_transform:Transform:0001",
                "m_rotationSpeedInUse:Vector3:0001", "m_startRotation:Quaternion:0001", "m_multiplier:Single:0001"
            }));
            Assert.That(AttributeRow(typeof(TransformRotator).GetField("m_targetTransform", All)), Is.EqualTo(
                "HeaderAttribute(Rotates objects in any axis at a given speed (degrees p/s) (Loops)) SpaceAttribute(10) " +
                "TooltipAttribute(Optional target transform else this transform is used.) SerializeField()"));
            Assert.That(AttributeRow(typeof(TransformRotator).GetField("m_rotationSpeed", All)),
                Is.EqualTo("SerializeField() HideIfAttribute(m_randomiseRotationSpeedOnAwake,null)"));
            Assert.That(AttributeRow(typeof(TransformRotator).GetField("m_rotationSpeedMax", All)),
                Is.EqualTo("ShowIfAttribute(m_randomiseRotationSpeedOnAwake,null) SerializeField()"));
            Assert.That(Methods(typeof(TransformRotator)), Is.EqualTo(new[]
            {
                "get_RotationSpeed:0886", "set_RotationSpeed:0886", "Awake:00c4", "OnSynchronise:00c4", "InternalUpdate:00c4",
                "ResetRotation:0086", "Rotate:0081", "ApplyRotations:01c4", "SetRotationSpeedMultiplier:0086", ".ctor:1886"
            }));

            Assert.That(typeof(TransformRotatorLimits).BaseType, Is.EqualTo(typeof(TransformRotator)));
            Assert.That(Fields(typeof(TransformRotatorLimits)), Is.EqualTo(new[]
                { "m_limits:Vector3:0001", "m_onLimitReached:UnityEvent:0001", "m_rotationsAccumulated:Vector3:0001" }));
            Assert.That(AttributeRow(typeof(TransformRotatorLimits).GetField("m_onLimitReached", All)),
                Is.EqualTo("TooltipAttribute(Callback when a limit is reached.) SerializeField()"));
            Assert.That(Methods(typeof(TransformRotatorLimits)), Is.EqualTo(new[] { "OnEnable:00c4", "ClampToLimit:0081", "ApplyRotations:00c4", ".ctor:1886" }));

            Assert.That(typeof(TransformRotatorRandom).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(AttributeRow(typeof(TransformRotatorRandom)), Is.EqualTo("Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(Fields(typeof(TransformRotatorRandom)), Is.EqualTo(new[]
            {
                "m_target:Transform:0001", "m_anglesMin:Vector3:0001", "m_anglesMax:Vector3:0001", "m_smoothTime:Single:0001",
                "m_maxSpeed:Single:0001", "m_transform:Transform:0001", "m_startRotation:Quaternion:0001", "m_velocity:Quaternion:0001"
            }));
            Assert.That(AttributeRow(typeof(TransformRotatorRandom).GetField("m_maxSpeed", All)),
                Is.EqualTo("TooltipAttribute(Optionally clamp the max smooth speed.  0 will leave unclamped.) SerializeField()"));
            Assert.That(Methods(typeof(TransformRotatorRandom)), Is.EqualTo(new[] { "Awake:00c4", "OnSynchronise:00c4", "InternalUpdate:00c4", ".ctor:1886" }));

            Assert.That(Fields(typeof(TransformRotatorSingleCurve)), Is.EqualTo(new[]
            {
                "m_rotationCurve:AnimationCurve:0001", "m_rotationSpeed:Single:0001", "m_activateOnEnable:Boolean:0001",
                "RotationComplete:FastAction:0006", "m_timerSeconds:Single:0001", "m_shouldRotate:Boolean:0001",
                "m_originalRotation:Quaternion:0001", "m_curveLength:Single:0001", "m_reverseRotation:Boolean:0001"
            }));
            Assert.That(Methods(typeof(TransformRotatorSingleCurve)), Is.EqualTo(new[]
                { "OnEnable:00c4", "OnDisable:00c4", "Activate:0081", "InternalUpdate:00c4", "Evaluate:0081", ".ctor:1886" }));
            ParameterInfo reverse = typeof(TransformRotatorSingleCurve).GetMethod("Activate", All).GetParameters()[0];
            Assert.That((int)reverse.Attributes, Is.EqualTo(0x1010));
            Assert.That(reverse.DefaultValue, Is.EqualTo(false));

            Assert.That(typeof(TransformRotatorSnapped).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(Fields(typeof(TransformRotatorSnapped)), Is.EqualTo(new[]
            {
                "m_rotationPerInterval:Vector3:0001", "m_timePerIntervalInSeconds:Vector3:0001", "m_timeCategory:TimeCategory:0001",
                "m_transform:Transform:0001"
            }));
            Assert.That(Methods(typeof(TransformRotatorSnapped)), Is.EqualTo(new[] { "Awake:0081", "OnEnable:0081", "ProcessRotation:0081", ".ctor:1886" }));
            Type iterator = typeof(TransformRotatorSnapped).GetNestedType("<ProcessRotation>d__6", BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Assert.That(Fields(iterator), Is.EqualTo(new[]
            {
                "<>1__state:Int32:0001", "<>2__current:Object:0001", "timeSeconds:Single:0006",
                "<>4__this:TransformRotatorSnapped:0006", "rotation:Quaternion:0006"
            }));
        }

        // .ctor 0x605460 stores Vector3.zero in the three authored speeds and 1.0 in the
        // multiplier; TransformRotatorLimits 0x605770 adds a new UnityEvent; SingleCurve
        // 0x605D70 stores speed 1.0; Snapped 0x606150 stores zero vectors and TimeCategory
        // 0x34dab335 (Environment).
        [Test]
        public void ConstructorsStoreTheNativeDefaults()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-defaults"))
            {
                var rotator = owned.Add<TransformRotator>("rotator");
                Assert.That(Get(rotator, "m_multiplier"), Is.EqualTo(1f));
                Assert.That(Get(rotator, "m_rotationSpeed"), Is.EqualTo(Vector3.zero));
                Assert.That(Get(rotator, "m_rotationSpeedMax"), Is.EqualTo(Vector3.zero));
                var limits = owned.Add<TransformRotatorLimits>("limits");
                Assert.That(Get(limits, "m_onLimitReached"), Is.InstanceOf<UnityEvent>());
                Assert.That(Get(limits, "m_multiplier"), Is.EqualTo(1f));
                Assert.That(Get(owned.Add<TransformRotatorSingleCurve>("curve"), "m_rotationSpeed"), Is.EqualTo(1f));
                var snapped = owned.Add<TransformRotatorSnapped>("snapped");
                Assert.That(Get(snapped, "m_timeCategory"), Is.EqualTo(TimeCategory.Environment));
                Assert.That((int)TimeCategory.Environment, Is.EqualTo(0x34dab335));
                Assert.That(Get(snapped, "m_rotationPerInterval"), Is.EqualTo(Vector3.zero));
            }
        }

        // Awake 0x6051A0: the optional target (op_Inequality 0x6051f7) or this transform, its
        // world rotation (get_rotation 0x60522f), then either three Random.Range draws in x, y,
        // z order (0x605250, 0x605266, 0x60527c) or a copy of m_rotationSpeed.
        [Test]
        public void AwakeTakesTheTargetsWorldRotationAndOptionallyRandomSpeeds()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-awake"))
            {
                Transform parent = owned.Objects.Create("parent", Vector3.zero, Quaternion.Euler(0f, 0f, 90f)).transform;
                var rotator = owned.Add<TransformRotator>("rotator", Vector3.zero, Quaternion.Euler(0f, 30f, 0f));
                Transform target = owned.Objects.Create("target", Vector3.zero, Quaternion.identity, parent).transform;
                target.localRotation = Quaternion.Euler(30f, 0f, 0f);
                Set(rotator, "m_targetTransform", target);
                Set(rotator, "m_rotationSpeed", new Vector3(7f, 8f, 9f));
                Set(rotator, "m_rotationSpeedMin", new Vector3(1f, 2f, 3f));
                Set(rotator, "m_rotationSpeedMax", new Vector3(4f, 5f, 6f));

                Random.InitState(91);
                Random.State before = Random.state;
                Call(rotator, "Awake");
                Assert.That(Get(rotator, "m_transform"), Is.SameAs(target));
                AreClose(target.rotation, (Quaternion)Get(rotator, "m_startRotation"), "world rotation of the target");
                Assert.That(rotator.RotationSpeed, Is.EqualTo(new Vector3(7f, 8f, 9f)));
                Assert.That(Random.state, Is.EqualTo(before), "no draw without randomising");

                Set(rotator, "m_targetTransform", null);
                Set(rotator, "m_randomiseRotationSpeedOnAwake", true);
                Random.InitState(1234);
                var expected = new Vector3(Random.Range(1f, 4f), Random.Range(2f, 5f), Random.Range(3f, 6f));
                Random.InitState(1234);
                Call(rotator, "Awake");
                Assert.That(Get(rotator, "m_transform"), Is.SameAs(rotator.transform));
                AreClose(Quaternion.Euler(0f, 30f, 0f), (Quaternion)Get(rotator, "m_startRotation"), "own rotation");
                Assert.That(rotator.RotationSpeed, Is.EqualTo(expected));
                Assert.That(Get(rotator, "m_rotationSpeed"), Is.EqualTo(new Vector3(7f, 8f, 9f)), "the authored speed is kept");
            }
        }

        // InternalUpdate 0x6053E0 dispatches slot 31 ApplyRotations with
        // m_rotationSpeedInUse * m_multiplier * deltaTime; ApplyRotations 0x605430 is
        // Transform.Rotate(Vector3) (0x26DB8E0), a local-space rotation. The RotationSpeed
        // accessors 0x605160/0x605180 read and write the speed in use only.
        [Test]
        public void InternalUpdateRotatesLocallyBySpeedTimesMultiplierTimesDelta()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-update"))
            {
                var rotator = owned.Add<TransformRotator>("rotator", Vector3.zero, Quaternion.Euler(0f, 45f, 0f));
                Call(rotator, "Awake");
                rotator.RotationSpeed = new Vector3(10f, 20f, 30f);
                Assert.That(Get(rotator, "m_rotationSpeed"), Is.EqualTo(Vector3.zero));
                rotator.SetRotationSpeedMultiplier(2f);
                Call(rotator, "InternalUpdate", 0.5f);
                AreClose(Quaternion.Euler(0f, 45f, 0f) * Quaternion.Euler(10f, 20f, 30f), rotator.transform.rotation, "one step");
                Assert.That(rotator.RotationSpeed, Is.EqualTo(new Vector3(10f, 20f, 30f)), "the multiplier is not folded into the speed");
            }
        }

        // OnSynchronise 0x6052D0 keeps the base result (SDT 0x06002e41: the time for OnAwake,
        // 0 for None), restores m_startRotation with set_rotation (0x605300), applies the speed
        // for that whole time and returns it. ResetRotation 0x605360 is the restore alone.
        [Test]
        public void OnSynchroniseRestartsFromTheStartRotationAndAppliesTheWholeTime()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-sync"))
            {
                var rotator = owned.Add<TransformRotator>("rotator", Vector3.zero, Quaternion.Euler(0f, 45f, 0f));
                Set(rotator, "m_rotationSpeed", new Vector3(0f, 10f, 0f));
                Call(rotator, "Awake");
                rotator.transform.rotation = Quaternion.Euler(80f, 0f, 0f);
                SetSynchronise(rotator, 1);
                Assert.That(Call(rotator, "OnSynchronise", 3f), Is.EqualTo(3f));
                AreClose(Quaternion.Euler(0f, 75f, 0f), rotator.transform.rotation, "start plus three seconds");

                SetSynchronise(rotator, 0);
                Assert.That(Call(rotator, "OnSynchronise", 3f), Is.EqualTo(0f));
                AreClose(Quaternion.Euler(0f, 45f, 0f), rotator.transform.rotation, "no synchronisation applies nothing");

                rotator.transform.rotation = Quaternion.Euler(0f, 0f, 10f);
                rotator.ResetRotation();
                AreClose(Quaternion.Euler(0f, 45f, 0f), rotator.transform.rotation, "reset");
            }
        }

        // ClampToLimit 0x605560: rotationAccumulated += rotation; the clamp is taken unless
        // limit == 0 (cmpneqps) or |acc| < limit (cmpnltps keeps NaN). The overshoot
        // |acc| - limit is negated for a negative rotation (cmpltss 0x6055a8) and subtracted
        // from both. Rows: accumulated, rotation, limit -> returned, accumulated, rotation.
        [TestCase(0f, 5f, 0f, false, 5f, 5f, TestName = "ClampToLimitWithAZeroLimitOnlyAccumulates")]
        [TestCase(0f, 5f, 10f, false, 5f, 5f, TestName = "ClampToLimitBelowTheLimitOnlyAccumulates")]
        [TestCase(8f, 2f, 10f, true, 10f, 2f, TestName = "ClampToLimitExactlyAtTheLimitIsReached")]
        [TestCase(8f, 5f, 10f, true, 10f, 2f, TestName = "ClampToLimitTakesBackAPositiveOvershoot")]
        [TestCase(-8f, -5f, 10f, true, -10f, -2f, TestName = "ClampToLimitTakesBackANegativeOvershoot")]
        [TestCase(8f, -20f, 10f, true, -10f, -18f, TestName = "ClampToLimitCrossingZeroHoldsTheNegativeLimit")]
        [TestCase(0f, 5f, -10f, true, -10f, -10f, TestName = "ClampToLimitWithANegativeLimitIsAlwaysReached")]
        [TestCase(0f, 5f, float.NaN, true, float.NaN, float.NaN, TestName = "ClampToLimitWithANaNLimitIsReached")]
        [TestCase(float.NaN, 1f, 10f, true, float.NaN, float.NaN, TestName = "ClampToLimitWithANaNAccumulationIsReached")]
        public void ClampToLimitTruthTable(float accumulated, float rotation, float limit, bool reached, float accumulatedAfter, float rotationAfter)
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-clamp"))
            {
                var limits = owned.Add<TransformRotatorLimits>("limits");
                object[] args = CallWithRefs(limits, "ClampToLimit", out object result, accumulated, rotation, limit);
                Assert.That(result, Is.EqualTo(reached));
                Assert.That(args[0], Is.EqualTo(accumulatedAfter));
                Assert.That(args[1], Is.EqualTo(rotationAfter));
            }
        }

        // ApplyRotations 0x6055D0 tests x, then y, then z, and the first clamp jumps to the
        // rotate-and-invoke tail (0x60572b): later axes are neither accumulated nor clamped.
        // The event (UnityEvent.Invoke 0x605748) follows Transform.Rotate on every reaching
        // step; a step that reaches no limit only rotates (0x60574d).
        [Test]
        public void LimitsClampTheFirstReachedAxisAndRaiseTheEventEachStep()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-limits"))
            {
                var limits = owned.Add<TransformRotatorLimits>("limits");
                Call(limits, "Awake");
                int raised = 0;
                ((UnityEvent)Get(limits, "m_onLimitReached")).AddListener(() => raised++);
                Set(limits, "m_limits", new Vector3(10f, 10f, 0f));
                limits.RotationSpeed = new Vector3(12f, 4f, 7f);

                Call(limits, "InternalUpdate", 1f);
                Assert.That(Get(limits, "m_rotationsAccumulated"), Is.EqualTo(new Vector3(10f, 0f, 0f)));
                Assert.That(raised, Is.EqualTo(1));
                Quaternion first = Quaternion.Euler(10f, 4f, 7f);
                AreClose(first, limits.transform.localRotation, "x clamped, y and z unclamped");

                Call(limits, "InternalUpdate", 1f);
                Assert.That(Get(limits, "m_rotationsAccumulated"), Is.EqualTo(new Vector3(10f, 0f, 0f)), "y never accumulates while x is held");
                Assert.That(raised, Is.EqualTo(2));
                AreClose(first * Quaternion.Euler(0f, 4f, 7f), limits.transform.localRotation, "x held at its limit");

                Set(limits, "m_limits", new Vector3(0f, 10f, 0f));
                Set(limits, "m_rotationsAccumulated", Vector3.zero);
                limits.transform.localRotation = Quaternion.identity;
                limits.RotationSpeed = new Vector3(3f, 12f, 5f);
                Call(limits, "InternalUpdate", 1f);
                Assert.That(Get(limits, "m_rotationsAccumulated"), Is.EqualTo(new Vector3(3f, 10f, 0f)), "a free x accumulates, z is skipped");
                Assert.That(raised, Is.EqualTo(3));
                AreClose(Quaternion.Euler(3f, 10f, 5f), limits.transform.localRotation, "y clamped");

                Set(limits, "m_limits", new Vector3(100f, 100f, 100f));
                Set(limits, "m_rotationsAccumulated", Vector3.zero);
                limits.transform.localRotation = Quaternion.identity;
                limits.RotationSpeed = new Vector3(1f, 2f, 3f);
                Call(limits, "InternalUpdate", 1f);
                Assert.That(Get(limits, "m_rotationsAccumulated"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(raised, Is.EqualTo(3), "no limit reached");
                AreClose(Quaternion.Euler(1f, 2f, 3f), limits.transform.localRotation, "free rotation");
            }
        }

        // Random Awake 0x605860 keeps the target or this transform and its local rotation
        // (get_localRotation 0x6058e2). InternalUpdate 0x605940 draws x, y, z (0x60595d,
        // 0x605972, 0x605987) before the smoothing test, forms m_startRotation * Euler(angles)
        // and snaps through set_localRotation unless m_smoothTime > 0 (jbe 0x605a8d, so NaN
        // and negative times snap).
        [TestCase(0f, TestName = "RandomRotatorSnapsWithAZeroSmoothTime")]
        [TestCase(-1f, TestName = "RandomRotatorSnapsWithANegativeSmoothTime")]
        [TestCase(float.NaN, TestName = "RandomRotatorSnapsWithANaNSmoothTime")]
        public void RandomRotatorSnapsToTheStartTimesARandomEuler(float smoothTime)
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-random"))
            {
                Transform parent = owned.Objects.Create("parent", Vector3.zero, Quaternion.Euler(0f, 90f, 0f)).transform;
                var random = owned.Add<TransformRotatorRandom>("random", Vector3.zero, Quaternion.identity, parent);
                random.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                Set(random, "m_anglesMin", new Vector3(0f, 10f, 20f));
                Set(random, "m_anglesMax", new Vector3(30f, 40f, 50f));
                Set(random, "m_smoothTime", smoothTime);
                Call(random, "Awake");
                Assert.That(Get(random, "m_transform"), Is.SameAs(random.transform));
                AreClose(Quaternion.Euler(10f, 0f, 0f), (Quaternion)Get(random, "m_startRotation"), "local start");

                Random.InitState(77);
                var angles = new Vector3(Random.Range(0f, 30f), Random.Range(10f, 40f), Random.Range(20f, 50f));
                Random.State afterDraws = Random.state;
                Random.InitState(77);
                Call(random, "InternalUpdate", 0.1f);
                AreClose(Quaternion.Euler(10f, 0f, 0f) * Quaternion.Euler(angles), random.transform.localRotation, "snapped");
                Assert.That(Random.state, Is.EqualTo(afterDraws));
            }
        }

        // With m_smoothTime > 0 the target goes through QuaternionExtensions.SmoothDamp
        // (0x605ae1) from the current local rotation; with no elapsed time that leaves the
        // rotation where it was, while the three draws still happen.
        [Test]
        public void RandomRotatorSmoothsFromTheCurrentRotationAfterDrawing()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-random-smooth"))
            {
                var random = owned.Add<TransformRotatorRandom>("random", Vector3.zero, Quaternion.Euler(10f, 0f, 0f));
                Set(random, "m_anglesMin", new Vector3(0f, 10f, 20f));
                Set(random, "m_anglesMax", new Vector3(30f, 40f, 50f));
                Set(random, "m_smoothTime", 1f);
                Call(random, "Awake");
                Random.InitState(5);
                Random.Range(0f, 1f);
                Random.Range(0f, 1f);
                Random.Range(0f, 1f);
                Random.State afterDraws = Random.state;
                Random.InitState(5);
                Call(random, "InternalUpdate", 0f);
                AreClose(Quaternion.Euler(10f, 0f, 0f), random.transform.localRotation, "no time, no movement");
                Assert.That(Random.state, Is.EqualTo(afterDraws));
            }
        }

        // Random OnSynchronise 0x605900 writes the Awake local rotation back with set_rotation
        // (0x60592b), a world rotation, and returns the base result.
        [Test]
        public void RandomRotatorSynchronisesTheLocalStartAsAWorldRotation()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-random-sync"))
            {
                Transform parent = owned.Objects.Create("parent", Vector3.zero, Quaternion.Euler(0f, 90f, 0f)).transform;
                var random = owned.Add<TransformRotatorRandom>("random", Vector3.zero, Quaternion.identity, parent);
                random.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
                Call(random, "Awake");
                SetSynchronise(random, 1);
                Assert.That(Call(random, "OnSynchronise", 2f), Is.EqualTo(2f));
                AreClose(Quaternion.Euler(10f, 0f, 0f), random.transform.rotation, "world rotation is the local start");
                AreClose(Quaternion.Inverse(parent.rotation) * Quaternion.Euler(10f, 0f, 0f), random.transform.localRotation, "local rotation");
            }
        }

        // SingleCurve InternalUpdate 0x605C10 returns unless m_shouldRotate; it adds
        // deltaTime * m_rotationSpeed to the timer, applies the inlined Evaluate (0x605CE0:
        // the curve value, negated when reversing, as the local euler Y with X and Z kept),
        // and only when the timer is strictly past m_curveLength (jbe 0x605cbc) raises
        // RotationComplete and stops. Activate 0x605BB0 resets the timer and sets the direction.
        [Test]
        public void SingleCurveSetsTheLocalYawAndCompletesStrictlyPastTheCurve()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-curve"))
            {
                var curve = owned.Add<TransformRotatorSingleCurve>("curve", Vector3.zero, Quaternion.Euler(10f, 0f, 20f));
                Set(curve, "m_rotationCurve", AnimationCurve.Linear(0f, 0f, 2f, 90f));
                Set(curve, "m_curveLength", 2f);
                Set(curve, "m_rotationSpeed", 2f);
                int completed = 0;
                curve.RotationComplete += () => completed++;

                Call(curve, "InternalUpdate", 0.25f);
                Assert.That(Get(curve, "m_timerSeconds"), Is.EqualTo(0f), "not rotating");
                AreClose(Quaternion.Euler(10f, 0f, 20f), curve.transform.localRotation, "untouched");

                Call(curve, "Activate", true);
                Assert.That(Get(curve, "m_shouldRotate"), Is.True);
                Call(curve, "InternalUpdate", 0.25f);
                Assert.That(Get(curve, "m_timerSeconds"), Is.EqualTo(0.5f));
                AreClose(Quaternion.Euler(10f, -22.5f, 20f), curve.transform.localRotation, "reversed");

                Call(curve, "Activate", false);
                Assert.That(Get(curve, "m_timerSeconds"), Is.EqualTo(0f));
                Call(curve, "InternalUpdate", 0.25f);
                AreClose(Quaternion.Euler(10f, 22.5f, 20f), curve.transform.localRotation, "forward");
                Call(curve, "InternalUpdate", 0.75f);
                Assert.That(Get(curve, "m_timerSeconds"), Is.EqualTo(2f));
                Assert.That(completed, Is.EqualTo(0), "the curve length itself is not past it");
                Assert.That(Get(curve, "m_shouldRotate"), Is.True);

                curve.transform.localRotation = Quaternion.Euler(10f, 0f, 20f);
                Call(curve, "InternalUpdate", 0.5f);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(Get(curve, "m_shouldRotate"), Is.False);
                AreClose(Quaternion.Euler(10f, 90f, 20f), curve.transform.localRotation, "the completing step still applies its angle");

                Call(curve, "InternalUpdate", 0.5f);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(Get(curve, "m_timerSeconds"), Is.EqualTo(3f), "stopped");
            }
        }

        // FastActionExtensions.Invoke (0x605cc4) returns for a null action, so completing
        // without a listener only stops.
        [Test]
        public void SingleCurveCompletesWithoutAListener()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-curve-null"))
            {
                var curve = owned.Add<TransformRotatorSingleCurve>("curve");
                Set(curve, "m_rotationCurve", AnimationCurve.Linear(0f, 0f, 1f, 45f));
                Set(curve, "m_curveLength", 1f);
                Call(curve, "Activate", false);
                Assert.DoesNotThrow(() => Call(curve, "InternalUpdate", 2f));
                Assert.That(Get(curve, "m_shouldRotate"), Is.False);
            }
        }

        // Snapped Awake 0x605D90 caches transform. ProcessRotation 0x6060A0 only builds
        // <ProcessRotation>d__6; its MoveNext 0x6061E0 yields
        // TimeScaledUtilities_SDT.WaitForFixedSeconds(timeSeconds, m_timeCategory) (inlined
        // iterator construction 0x60632b, no lookup yet) first, and on each resume sets
        // m_transform.localRotation = localRotation * rotation (0x6062f2) before yielding again.
        [Test]
        public void SnappedRotationWaitsThenTurnsTheLocalRotationEachInterval()
        {
            using (var owned = new OwnedTimeScaledObjects("lucid-rotator-snapped"))
            {
                var snapped = owned.Add<TransformRotatorSnapped>("snapped", Vector3.zero, Quaternion.Euler(0f, 10f, 0f));
                Call(snapped, "Awake");
                Assert.That(Get(snapped, "m_transform"), Is.SameAs(snapped.transform));
                Set(snapped, "m_timeCategory", TimeCategory.System);
                Quaternion step = Quaternion.AngleAxis(30f, Vector3.right);
                var routine = (IEnumerator)Call(snapped, "ProcessRotation", 0.5f, step);

                Assert.That(routine.MoveNext(), Is.True);
                AreClose(Quaternion.Euler(0f, 10f, 0f), snapped.transform.localRotation, "the first interval is waited");
                object wait = routine.Current;
                Assert.That(wait.GetType().DeclaringType, Is.EqualTo(typeof(TimeScaledUtilities_SDT)));
                Assert.That(Get(wait, "waitSeconds"), Is.EqualTo(0.5f));
                Assert.That(Get(wait, "timeCategory"), Is.EqualTo(TimeCategory.System));

                Assert.That(routine.MoveNext(), Is.True);
                AreClose(Quaternion.Euler(0f, 10f, 0f) * step, snapped.transform.localRotation, "one step");
                Assert.That(routine.Current, Is.Not.SameAs(wait), "a new wait per interval");
                Assert.That(routine.MoveNext(), Is.True);
                AreClose(Quaternion.Euler(0f, 10f, 0f) * step * step, snapped.transform.localRotation, "two steps");
            }
        }
    }
}
