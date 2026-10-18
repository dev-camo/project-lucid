using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.TransformSurfaceDescription (TypeDef 0x02000812, x86-64 0x5FE480..0x5FF7B0) with
    // its nested FollowModeType and SurfaceEvents, TransformSurfaceCircleDescription (0x02000811,
    // 0x5FE280..0x5FEFD0), TransformSurfaceFollower (0x02000819, 0x6013D0..0x603CC0),
    // TransformSurfaceTarget (0x0200081c, 0x601BE0..0x6048A9) and TransformSurfaceTargetStop
    // (0x0200081e, 0x603FA0..0x604930), and the line (0x02000815, 0x5FF7B0..0x5FFF20), point (0x02000816,
    // 0x5FFF20..0x600560), spline (0x02000817, 0x600560..0x600E80) and transform (0x02000818,
    // 0x600E80..0x6013D0) descriptions. The surfaces, targets and stops are plain serializable
    // classes built with their own constructors. A follower is a TimeScaledComponent_SDT, so it is added
    // to an inactive owned object with an owned, empty TimeCategoryLookup standing in the cache the
    // Editor's Reset reads, and the TimeManager row the TimeScaledComponent static reference may create
    // is owned too. Awake never runs, so the tests call the private InternalReset the way Awake does.
    // Not covered: the active-object branches of Play, PlayWithDirection and Stop, AdvanceInDirection
    // (it reads the registered TimeManager through GetDeltaTime), SnapToNearest (it steps in the
    // unscaled frame time), and the spline tracker the target builds in OnAwake (no surface here has
    // a Spline).
    public sealed class OriginalTransformSurfaceTests
    {
        private const float Tolerance = 1e-4f;
        private const float Pi = 3.14159265f;

        // Concrete surface for the base behaviour. It answers the abstract members the way the tests
        // need: a settable distance, a settable local rotation and an end position that is stored as given.
        private sealed class DescriptionProbe : TransformSurfaceDescription
        {
            public float ProbeDistance;
            public Quaternion LocalRotation = Quaternion.identity;
            public readonly List<(Vector3 end, Vector3 prev, Quaternion rotation)> Initialised =
                new List<(Vector3, Vector3, Quaternion)>();

            public override Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null) =>
                Quaternion.identity;

            public override Quaternion GetRotationLocal(Transform transform) => LocalRotation;

            protected override void CalculateDistance()
            {
                m_distance = ProbeDistance;
            }

            public override void SetPositionEnd(Vector3 worldPosition, Transform transform)
            {
                m_positionEndWorld = worldPosition;
                m_positionEndLocal = transform.InverseTransformPoint(worldPosition);
            }

            public override void Initialise(TransformSurfaceFollower follower, Vector3 localPositionEnd, Vector3 localPositionPrev,
                Quaternion localRotationEnd)
            {
                Initialised.Add((localPositionEnd, localPositionPrev, localRotationEnd));
                base.Initialise(follower, localPositionEnd, localPositionPrev, localRotationEnd);
            }

            public float Progress(float time, int direction) => GetProgressByTime(time, direction);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-transform-surface");
            public readonly TransformSurfaceFollower Follower;
            public readonly List<TransformSurfaceDescription> Surfaces = new List<TransformSurfaceDescription>();
            private readonly VerificationFixtureOwnership.LifecycleRow m_timeRow;
            private readonly object m_priorConfig;

            public Fixture() : this(Vector3.zero, Quaternion.identity) { }

            public Fixture(Vector3 position, Quaternion rotation)
            {
                m_timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
                m_priorConfig = GetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal");
                try
                {
                    SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", Objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                    GameObject host = Objects.Create("follower", position, rotation);
                    host.SetActive(false);
                    Follower = host.AddComponent<TransformSurfaceFollower>();
                }
                catch
                {
                    // The using statement never receives a fixture whose constructor throws.
                    Dispose();
                    throw;
                }
            }

            public void Add(TransformSurfaceDescription surface)
            {
                Surfaces.Add(surface);
                Follower.AddSurface(surface);
            }

            // Two quarter arcs of radius 2 around the follower's origin that each take one second:
            // 0 to 90 degrees and 90 to 180 degrees.
            public void AddQuarterArcs()
            {
                Add(Circle(0f, 90f, 1f));
                Add(Circle(90f, 180f, 1f));
            }

            public TransformSurfaceTarget AddTarget()
            {
                var target = new TransformSurfaceTarget();
                Set(target, "m_transform", Objects.Create("target").transform);
                ((List<TransformSurfaceTarget>)Get(Follower, "m_targets")).Add(target);
                return target;
            }

            // The part of Awake that measures every surface and initialises the targets.
            public void Reset() => Call(Follower, "InternalReset");

            public void Dispose()
            {
                try { Objects.Dispose(); }
                finally
                {
                    try { SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", m_priorConfig); }
                    finally { m_timeRow.Dispose(); }
                }
            }
        }

        private static TransformSurfaceCircleDescription Circle(float angleStart, float angleEnd, float followTime)
        {
            var circle = new TransformSurfaceCircleDescription();
            Set(circle, "m_radius", 2f);
            Set(circle, "m_angleStart", angleStart);
            Set(circle, "m_angleEnd", angleEnd);
            circle.SetFollowMode(TransformSurfaceDescription.FollowModeType.Time, 0f, followTime);
            return circle;
        }

        private static DescriptionProbe Probe(TransformSurfaceDescription.FollowModeType mode, float distance, float speed = 1f,
            float time = 1f, AnimationCurve curve = null)
        {
            var probe = new DescriptionProbe { ProbeDistance = distance };
            probe.SetFollowMode(mode, speed, time);
            if (curve != null) Set(probe, "m_followAnim", curve);
            probe.SetExtentsMultiplier(1f);
            return probe;
        }

        private static AnimationCurve Curve(float timeA, float valueA, float timeB, float valueB) =>
            new AnimationCurve(new Keyframe(timeA, valueA), new Keyframe(timeB, valueB));

        // Begin and end event counts of a surface or of a target's own events.
        private static int[] Count(object owner)
        {
            var events = (TransformSurfaceDescription.SurfaceEvents)Get(owner, "m_events");
            var counts = new int[2];
            ((UnityEvent)Get(events, "m_onBeginningReached")).AddListener(() => counts[0]++);
            ((UnityEvent)Get(events, "m_onEndReached")).AddListener(() => counts[1]++);
            return counts;
        }

        private static void Near(float expected, float actual, string label = "", float tolerance = Tolerance) =>
            Assert.That(actual, Is.EqualTo(expected).Within(tolerance), label);

        private static void Near(Vector3 expected, Vector3 actual, string label = "", float tolerance = 1e-3f) =>
            WorldMemberAccess.AreClose(expected, actual, label, tolerance);

        private static TransformSurfaceTargetStop StopOf(TransformSurfaceTarget target) => (TransformSurfaceTargetStop)Get(target, "m_stop");

        // A target on a one-second-per-unit surface of the given length of time, with its stop initialised.
        private static TransformSurfaceTarget StopRig(float timeTotal, float time, int direction)
        {
            DescriptionProbe surface = Probe(TransformSurfaceDescription.FollowModeType.Time, 10f, time: timeTotal);
            var target = new TransformSurfaceTarget();
            Set(target, "m_surface", surface);
            Set(target, "m_time", time);
            Set(target, "m_direction", direction);
            StopOf(target).Initialise(target);
            return target;
        }

        // ---- declarations ----

        [Test]
        public void DeclarationsOfTheStopAndTheTarget()
        {
            Assert.That((int)typeof(TransformSurfaceTargetStop).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfaceTargetStop).BaseType, Is.EqualTo(typeof(object)));
            Assert.That(Fields(typeof(TransformSurfaceTargetStop)), Is.EqualTo(new[]
            {
                "m_stopModeType:StopModeType:0001", "m_stopThreshold:Single:0001", "m_stopEaseType:EaseType:0001",
                "m_stopEaseTime:Single:0001", "m_fireEndEvents:Boolean:0001", "m_onStopReached:UnityEvent:0001",
                "m_target:TransformSurfaceTarget:0001", "m_stopRequested:Boolean:0001", "m_stopActive:Boolean:0001",
                "m_stopTime:Single:0001", "m_stopSurfaceTime:Single:0001"
            }));
            Assert.That(Methods(typeof(TransformSurfaceTargetStop)), Is.EqualTo(new[]
            {
                "get_FireEndEvents:0886", "Initialise:0086", "Reset:0086", "Activate:0086", "Update:0086", "OnStopReached:0086", ".ctor:1886"
            }));
            Assert.That((int)typeof(TransformSurfaceTargetStop.StopModeType).Attributes, Is.EqualTo(0x102));
            Assert.That(Enum.GetNames(typeof(TransformSurfaceTargetStop.StopModeType)),
                Is.EqualTo(new[] { "EaseToPaused", "EaseToNext", "EaseToLast", "EaseToNearest" }));
            Assert.That(Enum.GetValues(typeof(TransformSurfaceTargetStop.StopModeType)), Is.EqualTo(new[]
            {
                TransformSurfaceTargetStop.StopModeType.EaseToPaused, TransformSurfaceTargetStop.StopModeType.EaseToNext,
                TransformSurfaceTargetStop.StopModeType.EaseToLast, TransformSurfaceTargetStop.StopModeType.EaseToNearest
            }));
            Assert.That(AttributeRow(typeof(TransformSurfaceTargetStop).GetField("m_stopThreshold", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("TooltipAttribute(If current surface's progress exceeds threshold then apply stop logic from start of next surface.) SerializeField() RangeAttribute(0,1)"));

            Assert.That((int)typeof(TransformSurfaceTarget).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfaceTarget).BaseType, Is.EqualTo(typeof(TransformSurfaceProperty)));
            Assert.That(Fields(typeof(TransformSurfaceTarget)), Is.EqualTo(new[]
            {
                "m_transform:Transform:0001", "m_offset:Vector3:0001", "m_orientate:Boolean:0001", "m_orientateToDirection:Boolean:0001",
                "m_rotationLocalForward:Vector3:0001", "m_rotationLocalBackward:Vector3:0001", "m_startDirectionReversed:Boolean:0001",
                "m_startModeType:StartModeType:0001", "m_startDistance:Single:0001", "m_startIndex:Int32:0001", "m_startAngle:Int32:0001",
                "m_normalisedValue:Single:0001", "m_events:SurfaceEvents:0001", "m_stop:TransformSurfaceTargetStop:0001",
                "m_surfaceIndex:Int32:0001", "m_surface:TransformSurfaceDescription:0001", "m_time:Single:0001", "m_direction:Int32:0001",
                "m_worldOffset:Vector3:0001", "m_rotationWorldToLocalForward:Quaternion:0001", "m_rotationWorldToLocalBackward:Quaternion:0001",
                "m_ignoreEndEvents:Boolean:0001", "m_tracker:Tracker2D:0001", "m_collisionData:CharacterCollisionData[]:0001"
            }));
            Assert.That(Methods(typeof(TransformSurfaceTarget)), Is.EqualTo(new[]
            {
                "get_Surface:0886", "GetInitialState:0081", "OnAwake:0086", "Initialise:0086", "SetCollisionData:0081",
                "UpdateCollisionData:0081", "Reset:0086", "RefreshTransform:0081", "SnapToNearest:0086", "Play:0086", "SetDirection:0086",
                "Stop:0086", "FireEndEvents:0081", "Update:0086", "SetTransform:0081", "OnValidate:0086", "SetNextSurface:0081",
                "SetPrevSurface:0081", ".ctor:1886"
            }));
            Assert.That((int)typeof(TransformSurfaceTarget.StartModeType).Attributes, Is.EqualTo(0x102));
            Assert.That(Enum.GetNames(typeof(TransformSurfaceTarget.StartModeType)), Is.EqualTo(new[] { "Distance", "Index", "Angle", "Normalised" }));
            Assert.That(AttributeRow(typeof(TransformSurfaceTarget).GetField("m_rotationLocalForward", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("SerializeField() TooltipAttribute(If orientate checked, applies an offset rotation when travelling forward.) FormerlySerializedAsAttribute(m_rotationLocal)"));
        }

        [Test]
        public void DeclarationsOfTheDescriptionAndTheCircle()
        {
            Assert.That((int)typeof(TransformSurfaceDescription).Attributes, Is.EqualTo(0x00102081));
            Assert.That(typeof(TransformSurfaceDescription).BaseType, Is.EqualTo(typeof(TransformSurfaceProperty)));
            Assert.That(Fields(typeof(TransformSurfaceDescription)), Is.EqualTo(new[]
            {
                "FloatPrecision:Single:8051", "m_inverseRotation:Quaternion:0034", "m_followMode:FollowModeType:0001",
                "m_followSpeed:Single:0001", "m_followTime:Single:0001", "m_followAnim:AnimationCurve:0001", "m_events:SurfaceEvents:0001",
                "m_position:Vector3:0001", "m_positionWorld:Vector3:0004", "m_positionStartWorld:Vector3:0004",
                "m_positionEndWorld:Vector3:0004", "m_positionEndLocal:Vector3:0004", "m_distance:Single:0004", "m_timeTotal:Single:0004",
                "m_speedStart:Single:0001", "m_speedEnd:Single:0001", "m_extentsMultiplier:Single:0004"
            }));
            Assert.That(Methods(typeof(TransformSurfaceDescription)), Is.EqualTo(new[]
            {
                "get_LocalPosition:0886", "get_Distance:0886", "get_FollowTime:0886", "get_FollowSpeed:0886", "get_FollowMode:0886",
                "get_TimeTotal:0886", "get_PositionWorld:0886", "get_PositionStartWorld:0886", "get_PositionEndWorld:0886",
                "get_PositionEndLocal:0886", "get_SpeedStart:0886", "get_SpeedEnd:0886", "get_Orientate:09c6", "OnStart:01c6",
                "Teleport:01c6", "Update:01c6", "OnEnd:01c6", "GetRotation:05c6", "GetRotationLocal:05c6", "CalculateDistance:05c4",
                "CalculateSpeedLimits:0081", "SetSpeedLimits:01c6", "CalculateTime:0081", "GetTimeByDistance:0086", "GetTimeByAngle:01c6",
                "GetDistanceByTime:0086", "GetProgressByTime:0084", "Initialise:01c6", "OnValidate:01c6", "OnValidateTracker:01c6",
                "GetSpline:01c6", "SetPosition:01c6", "SetPositionEnd:05c6", "SetFollowMode:0086", "SetExtentsMultiplier:01c6",
                ".ctor:1884", ".cctor:1891"
            }));
            Assert.That(Enum.GetNames(typeof(TransformSurfaceDescription.FollowModeType)), Is.EqualTo(new[]
                { "Speed", "Time", "ProgressAnimated", "TimeSmoothed", "SpeedLerp", "SpeedAnimated" }));
            Assert.That((int)typeof(TransformSurfaceDescription.SurfaceEvents).Attributes, Is.EqualTo(0x00102002));
            Assert.That(Fields(typeof(TransformSurfaceDescription.SurfaceEvents)), Is.EqualTo(new[]
                { "m_onBeginningReached:UnityEvent:0001", "m_onEndReached:UnityEvent:0001" }));
            Assert.That(Methods(typeof(TransformSurfaceDescription.SurfaceEvents)), Is.EqualTo(new[]
                { "OnBeginningReached:0086", "OnEndReached:0086", ".ctor:1886" }));
            foreach (string name in new[] { "Teleport", "Update", "GetRotation" })
            {
                ParameterInfo[] parameters = typeof(TransformSurfaceDescription).GetMethod(name).GetParameters();
                ParameterInfo tracker = parameters[parameters.Length - 1];
                Assert.That(tracker.IsOptional && tracker.HasDefaultValue && tracker.DefaultValue == null, Is.True, name);
            }
            ParameterInfo[] followMode = typeof(TransformSurfaceDescription).GetMethod("SetFollowMode").GetParameters();
            Assert.That(followMode[1].DefaultValue, Is.EqualTo(0f));
            Assert.That(followMode[2].DefaultValue, Is.EqualTo(0f));

            Assert.That((int)typeof(TransformSurfaceCircleDescription).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfaceCircleDescription).BaseType, Is.EqualTo(typeof(TransformSurfaceDescription)));
            Assert.That(Fields(typeof(TransformSurfaceCircleDescription)), Is.EqualTo(new[]
            {
                "m_radius:Single:0001", "m_angleStart:Single:0001", "m_angleEnd:Single:0001", "m_clockwise:Boolean:0001",
                "m_axis:Vector3:0001", "m_forward:Vector3:0001", "m_right:Vector3:0001", "m_up:Vector3:0001", "m_angle:Single:0001"
            }));
            Assert.That(Methods(typeof(TransformSurfaceCircleDescription)), Is.EqualTo(new[]
            {
                "get_Radius:0886", "get_AngleStart:0886", "get_AngleEnd:0886", "get_Clockwise:0886", "get_Forward:0886",
                "get_Right:0886", "get_Up:0886", "Update:00c6", "Teleport:00c6", "GetTimeByAngle:00c6", "GetRotation:00c6",
                "GetRotationLocal:00c6", "SetPosition:00c6", "SetPositionEnd:00c6", "CalculatePosition:0081", "CalculateDistance:00c4",
                "SetExtentsMultiplier:00c6", ".ctor:1886"
            }));
        }

        [Test]
        public void DeclarationsOfTheFollower()
        {
            Assert.That((int)typeof(TransformSurfaceFollower).Attributes, Is.EqualTo(0x00100101));
            Assert.That(typeof(TransformSurfaceFollower).BaseType, Is.EqualTo(typeof(TimeScaledComponent_SDT)));
            Assert.That(Fields(typeof(TransformSurfaceFollower)), Is.EqualTo(new[]
            {
                "m_static:Boolean:0001", "m_wrapMode:WrapModeType:0001", "m_closed:Boolean:0001", "m_targets:List`1:0001",
                "m_surfaces:List`1:0001", "<IsRuntime>k__BackingField:Boolean:0001", "<TimeMultiplier>k__BackingField:Single:0001",
                "OnCompleteAction:Action`1:0006"
            }));
            Assert.That(Methods(typeof(TransformSurfaceFollower)), Is.EqualTo(new[]
            {
                "get_IsRuntime:0886", "set_IsRuntime:0881", "get_TimeMultiplier:0886", "set_TimeMultiplier:0881", "get_Static:0886",
                "get_WrapMode:0886", "get_Closed:0886", "get_NumSurfaces:0886", "Awake:00c4", "SetEnabled:0086", "OnDisable:00c4",
                "InternalAwake:0081", "InternalReset:0081", "InternalUpdate:00c4", "ResetTargets:0086", "SnapTargetsToNearest:0086",
                "SnapTargetsToNearest:0086", "Play:0086", "PlayWithDirection:0086", "AdvanceInDirection:0086", "SetExtentsMultiplier:0086",
                "Stop:0086", "AddSurface:0086", "GetSurface:0086", "GetSurfaceSpline:0086", "OnValidate:00c4", "GetSurfaceByDistance:0086",
                "GetSurfaceByAngle:0086", "GetSurfaceByNormalisedValue:0086", "GetTotalDistance:0081", ".ctor:1886"
            }));
            Assert.That((int)typeof(TransformSurfaceFollower.WrapModeType).Attributes, Is.EqualTo(0x102));
            Assert.That(Enum.GetNames(typeof(TransformSurfaceFollower.WrapModeType)), Is.EqualTo(new[] { "Once", "Loop", "PingPong" }));
        }

        // ---- the stop ----

        // .ctor 0x6048B0 stores threshold 1, ease type 1 (OutQuad), ease time 1 and fire-end-events true,
        // and creates the UnityEvent.
        [Test]
        public void StopConstructorStoresTheNativeDefaults()
        {
            var stop = new TransformSurfaceTargetStop();
            Assert.That(Get(stop, "m_stopModeType"), Is.EqualTo(TransformSurfaceTargetStop.StopModeType.EaseToPaused));
            Assert.That(Get(stop, "m_stopThreshold"), Is.EqualTo(1f));
            Assert.That(Get(stop, "m_stopEaseType"), Is.EqualTo(EaseUtilities.EaseType.OutQuad));
            Assert.That(Get(stop, "m_stopEaseTime"), Is.EqualTo(1f));
            Assert.That(stop.FireEndEvents, Is.True);
            Assert.That(Get(stop, "m_onStopReached"), Is.InstanceOf<UnityEvent>());
            Assert.That(Get(stop, "m_stopRequested"), Is.False);
            Assert.That(Get(stop, "m_stopActive"), Is.False);
        }

        // Initialise 0x603FA0 stores the target and clears both flags (one 16-bit store at +0x38); Reset
        // 0x604020 clears the same two flags; OnStopReached 0x6044B0 clears them and raises the event
        // only while a stop is active.
        [Test]
        public void StopInitialiseResetAndStopReachedHandleBothFlags()
        {
            TransformSurfaceTarget target = StopRig(10f, 4f, 1);
            TransformSurfaceTargetStop stop = StopOf(target);
            int fired = 0;
            ((UnityEvent)Get(stop, "m_onStopReached")).AddListener(() => fired++);

            stop.OnStopReached();
            Assert.That(fired, Is.Zero, "no stop active");

            stop.Activate(4f, 1);
            Assert.That(Get(stop, "m_stopActive"), Is.True);
            stop.Reset();
            Assert.That(Get(stop, "m_stopRequested"), Is.False);
            Assert.That(Get(stop, "m_stopActive"), Is.False);
            Assert.That(Get(stop, "m_target"), Is.SameAs(target));

            stop.Activate(4f, 1);
            stop.OnStopReached();
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(Get(stop, "m_stopActive"), Is.False);

            Set(stop, "m_stopRequested", true);
            Set(stop, "m_stopActive", true);
            TransformSurfaceTarget other = StopRig(10f, 0f, 1);
            stop.Initialise(other);
            Assert.That(Get(stop, "m_target"), Is.SameAs(other));
            Assert.That(Get(stop, "m_stopRequested"), Is.False);
            Assert.That(Get(stop, "m_stopActive"), Is.False);
        }

        // Activate 0x6043C0: while no stop is active, a time past threshold * the surface's total time only
        // records the request (ucomiss at 0x6043E0). Otherwise the stop becomes active with its time at 0 and the
        // surface time left = total - time (0x60442F..0x60443A). An active stop restarts regardless of the threshold.
        [Test]
        public void StopActivateWaitsPastTheThresholdThenStartsEasing()
        {
            TransformSurfaceTarget target = StopRig(10f, 4f, 1);
            TransformSurfaceTargetStop stop = StopOf(target);
            Set(stop, "m_stopThreshold", 0.5f);

            stop.Activate(6f, 1);
            Assert.That(Get(stop, "m_stopRequested"), Is.True);
            Assert.That(Get(stop, "m_stopActive"), Is.False);

            stop.Activate(5f, 1);
            Assert.That(Get(stop, "m_stopRequested"), Is.False, "exactly at the threshold starts");
            Assert.That(Get(stop, "m_stopActive"), Is.True);
            Assert.That(Get(stop, "m_stopTime"), Is.EqualTo(0f));
            Near(5f, (float)Get(stop, "m_stopSurfaceTime"), "surface time left");

            Set(stop, "m_stopTime", 3f);
            stop.Activate(8f, 1);
            Assert.That(Get(stop, "m_stopActive"), Is.True, "an active stop ignores the threshold");
            Assert.That(Get(stop, "m_stopTime"), Is.EqualTo(0f));
            Near(2f, (float)Get(stop, "m_stopSurfaceTime"), "restarted");
        }

        // Activate 0x6043C0 turns the target round (SetDirection(-direction)) for EaseToLast, and for
        // EaseToNearest only while half the total time exceeds the time (0x6043FA..0x604409).
        [Test]
        public void StopActivateTurnsTheTargetBackForEaseToLastAndNearest()
        {
            var flipped = new[]
            {
                (TransformSurfaceTargetStop.StopModeType.EaseToLast, 4f, true),
                (TransformSurfaceTargetStop.StopModeType.EaseToNearest, 4f, true),
                (TransformSurfaceTargetStop.StopModeType.EaseToNearest, 5f, false),
                (TransformSurfaceTargetStop.StopModeType.EaseToNearest, 6f, false),
                (TransformSurfaceTargetStop.StopModeType.EaseToNext, 4f, false),
                (TransformSurfaceTargetStop.StopModeType.EaseToPaused, 4f, false)
            };
            foreach (var (mode, time, turns) in flipped)
            {
                TransformSurfaceTarget target = StopRig(10f, time, 1);
                Set(StopOf(target), "m_stopModeType", mode);
                StopOf(target).Activate(time, 1);
                string label = mode + " at " + time;
                Assert.That(Get(StopOf(target), "m_stopActive"), Is.True, label);
                Assert.That(Get(target, "m_direction"), Is.EqualTo(turns ? -1 : 1), label);
                Near(turns ? 10f - time : time, (float)Get(target, "m_time"), label);
                Assert.That(Get(target, "m_ignoreEndEvents"), Is.EqualTo(turns), label);
                Near(10f - time, (float)Get(StopOf(target), "m_stopSurfaceTime"), label);
            }

            // Already facing the way the stop would turn it: nothing changes.
            TransformSurfaceTarget facing = StopRig(10f, 4f, -1);
            Set(StopOf(facing), "m_stopModeType", TransformSurfaceTargetStop.StopModeType.EaseToLast);
            StopOf(facing).Activate(4f, 1);
            Assert.That(Get(facing, "m_direction"), Is.EqualTo(-1));
            Near(4f, (float)Get(facing, "m_time"));
            Assert.That(Get(facing, "m_ignoreEndEvents"), Is.False);

            // Past the threshold the request waits and the target is left alone.
            TransformSurfaceTarget waiting = StopRig(10f, 6f, 1);
            Set(StopOf(waiting), "m_stopModeType", TransformSurfaceTargetStop.StopModeType.EaseToLast);
            Set(StopOf(waiting), "m_stopThreshold", 0.5f);
            StopOf(waiting).Activate(6f, 1);
            Assert.That(Get(StopOf(waiting), "m_stopRequested"), Is.True);
            Assert.That(Get(waiting, "m_direction"), Is.EqualTo(1));
        }

        // Update 0x6044D0 with EaseToPaused (0x604646): the stop time advances by delta / multiplier, the delta
        // time is scaled by Ease(type, 1 - previous stop time / ease time) while that is below the ease time and the
        // call reports false; once reached the delta is zero, the flags clear, the event fires and the call reports
        // true. InQuad is t * t. A stop that is not active leaves the delta alone.
        [Test]
        public void StopUpdateEasesToPausedThenRaisesTheEvent()
        {
            TransformSurfaceTarget target = StopRig(10f, 4f, 1);
            TransformSurfaceTargetStop stop = StopOf(target);
            Set(stop, "m_stopEaseType", EaseUtilities.EaseType.InQuad);
            int fired = 0;
            ((UnityEvent)Get(stop, "m_onStopReached")).AddListener(() => fired++);

            float inactive = 0.7f;
            Assert.That(stop.Update(ref inactive, 1f, 4f, 1), Is.False);
            Assert.That(inactive, Is.EqualTo(0.7f));

            stop.Activate(4f, 1);
            float delta = 0.5f;
            Assert.That(stop.Update(ref delta, 1f, 4f, 1), Is.False);
            Near(0.5f, delta, "InQuad(1) * 0.5");
            Near(0.5f, (float)Get(stop, "m_stopTime"), "stop time");

            delta = 0.4f;
            Assert.That(stop.Update(ref delta, 1f, 4f, 1), Is.False);
            Near(0.1f, delta, "InQuad(0.5) * 0.4");
            Near(0.9f, (float)Get(stop, "m_stopTime"), "stop time");

            delta = 0.5f;
            Assert.That(stop.Update(ref delta, 2f, 4f, 1), Is.True, "0.9 + 0.5 / 2 reaches the ease time");
            Assert.That(delta, Is.EqualTo(0f));
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(Get(stop, "m_stopRequested"), Is.False);
            Assert.That(Get(stop, "m_stopActive"), Is.False);

            delta = 0.7f;
            Assert.That(stop.Update(ref delta, 1f, 4f, 1), Is.False);
            Assert.That(delta, Is.EqualTo(0.7f));
            Assert.That(fired, Is.EqualTo(1));
        }

        // Update 0x6044D0 with EaseToNext (0x60459C..0x604644): the delta is Max(delta * Ease(type, (1 - stop time /
        // ease time) * remaining), 0.001) and then Max(that * ratio, (delta / multiplier) * Min(ratio * remaining, 1))
        // with ratio = surface time left / ease time and remaining = (total - time) / surface time left; the stop time
        // grows by delta / multiplier and the call reports true. Total 10, activated at 4, ease time 2, InQuad.
        [Test]
        public void StopUpdateEasesToTheSurfaceEnd()
        {
            TransformSurfaceTarget target = StopRig(10f, 4f, 1);
            TransformSurfaceTargetStop stop = StopOf(target);
            Set(stop, "m_stopModeType", TransformSurfaceTargetStop.StopModeType.EaseToNext);
            Set(stop, "m_stopEaseType", EaseUtilities.EaseType.InQuad);
            Set(stop, "m_stopEaseTime", 2f);
            stop.Activate(4f, 1);
            Near(6f, (float)Get(stop, "m_stopSurfaceTime"));

            float delta = 0.5f;
            Assert.That(stop.Update(ref delta, 1f, 6f, 1), Is.True);
            Near(0.6666667f, delta, "first delta");
            Near(0.6666667f, (float)Get(stop, "m_stopTime"), "stop time");

            delta = 0.3f;
            Assert.That(stop.Update(ref delta, 1f, 8f, 1), Is.True);
            Near(0.3f, delta, "second delta keeps the scaled step");
            Near(0.9666667f, (float)Get(stop, "m_stopTime"), "stop time");

            // A tiny step is first raised to 0.001 (maxss at 0x6045F9), and that floor is what the ratio of 3 scales;
            // without the floor the scaled step 0.0001 * Min(0.5, 1) would win.
            delta = 0.0001f;
            Assert.That(stop.Update(ref delta, 1f, 9f, 1), Is.True);
            Near(0.003f, delta, "floored then scaled", 1e-6f);
            Near(0.9696667f, (float)Get(stop, "m_stopTime"), "stop time");

            // The scaled step is capped at its own size by Min(ratio * remaining, 1) (minss at 0x60461A): 0.9, not 1.8.
            delta = 0.9f;
            Assert.That(stop.Update(ref delta, 1f, 6f, 1), Is.True);
            Near(0.9f, delta, "capped");
            Near(1.8696667f, (float)Get(stop, "m_stopTime"), "stop time");
        }

        // Update 0x6044D0 first activates a pending request (0x6044E4..0x604512) and reports false if the time is
        // still past the threshold.
        [Test]
        public void StopUpdateActivatesAPendingRequestOnlyBeforeTheThreshold()
        {
            TransformSurfaceTarget target = StopRig(10f, 4f, 1);
            TransformSurfaceTargetStop stop = StopOf(target);
            Set(stop, "m_stopThreshold", 0.5f);
            Set(stop, "m_stopEaseType", EaseUtilities.EaseType.InQuad);
            Set(stop, "m_stopRequested", true);

            float delta = 0.5f;
            Assert.That(stop.Update(ref delta, 1f, 6f, 1), Is.False, "still past the threshold");
            Assert.That(delta, Is.EqualTo(0.5f));
            Assert.That(Get(stop, "m_stopRequested"), Is.True);
            Assert.That(Get(stop, "m_stopActive"), Is.False);

            Assert.That(stop.Update(ref delta, 1f, 4f, 1), Is.False, "activated and easing");
            Assert.That(Get(stop, "m_stopRequested"), Is.False);
            Assert.That(Get(stop, "m_stopActive"), Is.True);
            Near(0.5f, delta);
            Near(0.5f, (float)Get(stop, "m_stopTime"));
            Near(6f, (float)Get(stop, "m_stopSurfaceTime"));
        }

        // ---- the description (through a probe) ----

        [Test]
        public void DescriptionConstructorStoresTheNativeDefaults()
        {
            var probe = new DescriptionProbe();
            Assert.That(probe.FollowMode, Is.EqualTo(TransformSurfaceDescription.FollowModeType.Speed));
            Assert.That(probe.FollowSpeed, Is.EqualTo(1f));
            Assert.That(probe.FollowTime, Is.EqualTo(1f));
            Assert.That(probe.Orientate, Is.True);
            Assert.That(Get(probe, "m_extentsMultiplier"), Is.EqualTo(1f));
            Assert.That(((AnimationCurve)Get(probe, "m_followAnim")).length, Is.Zero);
            Assert.That(Get(probe, "m_events"), Is.InstanceOf<TransformSurfaceDescription.SurfaceEvents>());
            Assert.That(probe.Distance, Is.Zero);
            Assert.That(probe.TimeTotal, Is.Zero);
            Assert.That(probe.SpeedStart, Is.Zero);
            Assert.That(probe.SpeedEnd, Is.Zero);
            Assert.That(probe.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(probe.GetSpline(), Is.Null);
            probe.OnValidateTracker(null);
            var inverse = (Quaternion)GetStatic(typeof(TransformSurfaceDescription), "m_inverseRotation");
            Near(Vector3.back, inverse * Vector3.forward, "the inverse rotation is half a turn about up");
            Near(Vector3.right, inverse * Vector3.left, "left becomes right");
        }

        // CalculateTime 0x5FF350 and CalculateSpeedLimits 0x5FF210, reached through SetExtentsMultiplier (0x5FEED0):
        // Speed: distance / speed (0 for a speed of 0), speeds = follow speed; Time and TimeSmoothed: follow time,
        // speeds = distance / time; ProgressAnimated and SpeedAnimated: the last key's time; ProgressAnimated speeds
        // are distance / first key time and distance / total time, SpeedAnimated speeds are the curve at 0 and at
        // the total time.
        [Test]
        public void TimeAndSpeedLimitsFollowTheFollowMode()
        {
            TransformSurfaceDescription.FollowModeType speedMode = TransformSurfaceDescription.FollowModeType.Speed;
            DescriptionProbe speed = Probe(speedMode, 10f, speed: 4f);
            Near(2.5f, speed.TimeTotal, "speed time");
            Near(4f, speed.SpeedStart, "speed start");
            Near(4f, speed.SpeedEnd, "speed end");
            Assert.That(Probe(speedMode, 10f, speed: 0f).TimeTotal, Is.Zero, "zero speed takes no time");
            Assert.That(Probe(speedMode, 10f, speed: -2f).TimeTotal, Is.Zero, "negative speed takes no time");

            foreach (TransformSurfaceDescription.FollowModeType mode in new[]
                { TransformSurfaceDescription.FollowModeType.Time, TransformSurfaceDescription.FollowModeType.TimeSmoothed })
            {
                DescriptionProbe timed = Probe(mode, 10f, time: 4f);
                Near(4f, timed.TimeTotal, mode + " time");
                Near(2.5f, timed.SpeedStart, mode + " start");
                Near(2.5f, timed.SpeedEnd, mode + " end");
                DescriptionProbe instant = Probe(mode, 10f, time: 0f);
                Assert.That(instant.TimeTotal, Is.Zero, mode + " instant");
                Assert.That(instant.SpeedStart, Is.Zero, mode + " instant start");
            }

            DescriptionProbe progress = Probe(TransformSurfaceDescription.FollowModeType.ProgressAnimated, 10f, curve: Curve(0.5f, 0f, 5f, 1f));
            Near(5f, progress.TimeTotal, "progress time");
            Near(20f, progress.SpeedStart, "progress start is distance / first key time");
            Near(2f, progress.SpeedEnd, "progress end is distance / total");
            DescriptionProbe noKeys = Probe(TransformSurfaceDescription.FollowModeType.ProgressAnimated, 10f, curve: new AnimationCurve());
            Assert.That(noKeys.TimeTotal, Is.Zero);
            Assert.That(noKeys.SpeedStart, Is.Zero);
            Assert.That(noKeys.SpeedEnd, Is.Zero);

            DescriptionProbe animated = Probe(TransformSurfaceDescription.FollowModeType.SpeedAnimated, 10f, curve: Curve(0f, 1f, 5f, 3f));
            Near(5f, animated.TimeTotal, "animated time");
            Near(1f, animated.SpeedStart, "animated start is the curve at 0");
            Near(3f, animated.SpeedEnd, "animated end is the curve at the total time");
        }

        // SetSpeedLimits 0x5FF330 only acts for SpeedLerp (cmp 4 at 0x5FF334): it stores both limits and
        // recomputes the time as 2 * distance / (start + end) (0x5FF3E1..0x5FF3F4). The other limits are kept
        // by CalculateSpeedLimits, which has no SpeedLerp case.
        [Test]
        public void SpeedLerpKeepsItsLimitsAndTimesTheCrossing()
        {
            DescriptionProbe lerp = Probe(TransformSurfaceDescription.FollowModeType.SpeedLerp, 10f);
            lerp.SetSpeedLimits(2f, 3f);
            Near(2f, lerp.SpeedStart);
            Near(3f, lerp.SpeedEnd);
            Near(4f, lerp.TimeTotal, "2 * 10 / 5");
            lerp.SetExtentsMultiplier(1f);
            Near(2f, lerp.SpeedStart, "kept by the extents update");
            Near(3f, lerp.SpeedEnd);
            Near(4f, lerp.TimeTotal);

            DescriptionProbe fixedSpeed = Probe(TransformSurfaceDescription.FollowModeType.Speed, 10f, speed: 4f);
            fixedSpeed.SetSpeedLimits(2f, 3f);
            Near(4f, fixedSpeed.SpeedStart, "other modes ignore the limits");
            Near(4f, fixedSpeed.SpeedEnd);
            Near(2.5f, fixedSpeed.TimeTotal);
        }

        // GetProgressByTime 0x5FE7F0: a negative direction counts from the far end (total - time). Speed and Time
        // give time / total (0x5FE88A); TimeSmoothed gives SmoothStep(0, total, t / total) / total, i.e.
        // 3p^2 - 2p^3; SpeedLerp gives time * Lerp(start, end, 0.5 * t / total) / distance (0x5FE8FC..0x5FE93A);
        // ProgressAnimated evaluates the curve at the time (tail call at 0x5FE828); SpeedAnimated divides the area under the
        // curve up to the time by its total area (0x5FE85B..0x5FE881). Without a curve both fall back to time / total.
        [Test]
        public void ProgressByTimeForEachFollowMode()
        {
            DescriptionProbe timed = Probe(TransformSurfaceDescription.FollowModeType.Time, 8f, time: 4f);
            Near(0.25f, timed.Progress(1f, 1));
            Near(0.75f, timed.Progress(1f, -1));
            Near(0f, timed.Progress(0f, 1));
            Near(1f, timed.Progress(4f, 1));

            DescriptionProbe smooth = Probe(TransformSurfaceDescription.FollowModeType.TimeSmoothed, 8f, time: 4f);
            Near(0.15625f, smooth.Progress(1f, 1), "3p^2 - 2p^3 at 0.25");
            Near(0.84375f, smooth.Progress(3f, 1), "at 0.75");
            Near(0.84375f, smooth.Progress(1f, -1), "counted from the far end");

            DescriptionProbe lerp = Probe(TransformSurfaceDescription.FollowModeType.SpeedLerp, 8f);
            lerp.SetSpeedLimits(2f, 6f);
            Near(2f, lerp.TimeTotal);
            Near(0.375f, lerp.Progress(1f, 1), "1 * Lerp(2, 6, 0.25) / 8");
            Near(1f, lerp.Progress(2f, 1), "the end of the surface");

            DescriptionProbe progress = Probe(TransformSurfaceDescription.FollowModeType.ProgressAnimated, 8f, curve: Curve(0f, 0f, 5f, 1f));
            Near(0.104f, progress.Progress(1f, 1), "curve at 1");
            Near(0.896f, progress.Progress(1f, -1), "curve at 5 - 1");

            DescriptionProbe area = Probe(TransformSurfaceDescription.FollowModeType.SpeedAnimated, 8f, curve: Curve(0f, 2f, 5f, 2f));
            Near(0.5f, area.Progress(2.5f, 1), "5 of 10", 1e-3f);

            foreach (TransformSurfaceDescription.FollowModeType mode in new[]
                { TransformSurfaceDescription.FollowModeType.ProgressAnimated, TransformSurfaceDescription.FollowModeType.SpeedAnimated })
            {
                DescriptionProbe missing = Probe(TransformSurfaceDescription.FollowModeType.Time, 8f, time: 4f);
                missing.SetFollowMode(mode);
                Set(missing, "m_followAnim", null);
                Near(0.25f, missing.Progress(1f, 1), mode + " without a curve");
            }
        }

        // GetTimeByDistance 0x5FF430 (jae at 0x5FF444: a distance at or past the length gives the whole time, NaN
        // takes the proportion), GetDistanceByTime 0x5FF460 (progress * distance) and GetTimeByAngle 0x5FF450 (0).
        [Test]
        public void TimeDistanceAndAngleLookups()
        {
            DescriptionProbe timed = Probe(TransformSurfaceDescription.FollowModeType.Time, 10f, time: 4f);
            Near(2f, timed.GetTimeByDistance(5f));
            Near(4f, timed.GetTimeByDistance(10f));
            Near(4f, timed.GetTimeByDistance(20f));
            Assert.That(timed.GetTimeByDistance(float.NaN), Is.NaN);
            Near(2.5f, timed.GetDistanceByTime(1f, 1));
            Near(7.5f, timed.GetDistanceByTime(1f, -1));
            Assert.That(timed.GetTimeByAngle(30f), Is.Zero);
        }

        // Update 0x5FE480 on the base surface: the world position (or the local position through the transform)
        // and a result of timeTotal > 0, leaving time and delta alone. Teleport 0x5FF180 picks the start for a
        // positive direction and the end otherwise, OnStart and OnEnd raise the begin and end events.
        [Test]
        public void BaseSurfaceUpdateTeleportAndEvents()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-base"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                DescriptionProbe probe = Probe(TransformSurfaceDescription.FollowModeType.Time, 10f, time: 4f);
                Set(probe, "m_position", new Vector3(1f, 0f, 0f));
                Set(probe, "m_positionWorld", new Vector3(4f, 5f, 6f));
                Set(probe, "m_positionStartWorld", new Vector3(7f, 0f, 0f));
                Set(probe, "m_positionEndWorld", new Vector3(8f, 0f, 0f));
                Set(probe, "m_positionEndLocal", new Vector3(0f, 9f, 0f));

                float time = 0.5f, delta = 0.25f;
                Assert.That(probe.Update(out Vector3 position, ref time, ref delta, 1, false, null), Is.True);
                Near(new Vector3(4f, 5f, 6f), position, "world position");
                Assert.That(time, Is.EqualTo(0.5f));
                Assert.That(delta, Is.EqualTo(0.25f));
                Assert.That(probe.Update(out position, ref time, ref delta, 1, true, transform), Is.True);
                Near(new Vector3(11f, 0f, 0f), position, "local position through the transform");
                Assert.That(Probe(TransformSurfaceDescription.FollowModeType.Speed, 10f, speed: 0f)
                    .Update(out position, ref time, ref delta, 1, false, null), Is.False, "no time to cross");

                Near(new Vector3(7f, 0f, 0f), probe.Teleport(1, false, null), "start");
                Near(new Vector3(8f, 0f, 0f), probe.Teleport(0, false, null), "end at zero");
                Near(new Vector3(8f, 0f, 0f), probe.Teleport(-1, false, null), "end");
                Near(new Vector3(11f, 0f, 0f), probe.Teleport(1, true, transform), "start through the transform");
                Near(new Vector3(10f, 9f, 0f), probe.Teleport(0, true, transform), "end through the transform");

                int[] counts = Count(probe);
                probe.OnStart();
                probe.OnStart();
                probe.OnEnd();
                Assert.That(counts, Is.EqualTo(new[] { 2, 1 }));
            }
        }

        // SetPosition 0x5FED60 stores the world and local positions only when the new position is not within
        // 0.001 (sqrMagnitude < tolerance^2), and returns whether it moved.
        [Test]
        public void SetPositionStoresTheLocalPositionOnlyWhenMoved()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-position"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                DescriptionProbe probe = Probe(TransformSurfaceDescription.FollowModeType.Time, 10f, time: 4f);
                Assert.That(probe.SetPosition(new Vector3(12f, 0f, 0f), transform), Is.True);
                Near(new Vector3(12f, 0f, 0f), probe.PositionWorld, "world");
                Near(new Vector3(2f, 0f, 0f), probe.LocalPosition, "local");
                Assert.That(probe.SetPosition(new Vector3(12.0005f, 0f, 0f), transform), Is.False);
                Near(new Vector3(12f, 0f, 0f), probe.PositionWorld, "inside the tolerance");
                Assert.That(probe.SetPosition(new Vector3(12.0015f, 0f, 0f), transform), Is.True);
                Near(new Vector3(2.0015f, 0f, 0f), probe.LocalPosition, "outside the tolerance");
            }
        }

        // OnValidate 0x5FF540 measures the surface, then snaps the positions and speeds that are within 0.01 of
        // a whole number (Vector3Extensions.ClampToInt / MathUtilities.ClampToInt with 0.01).
        [Test]
        public void OnValidateMeasuresAndSnapsPositionsAndSpeeds()
        {
            DescriptionProbe probe = Probe(TransformSurfaceDescription.FollowModeType.Time, 10f, time: 3f);
            probe.ProbeDistance = 12f;
            Set(probe, "m_position", new Vector3(0.004f, 1.996f, 3.2f));
            Set(probe, "m_positionWorld", new Vector3(5.005f, 0f, 0f));
            Set(probe, "m_speedStart", 1.003f);
            Set(probe, "m_speedEnd", 2.5f);
            probe.OnValidate();
            Near(12f, probe.Distance, "measured again");
            Near(3f, probe.TimeTotal);
            Near(new Vector3(0f, 2f, 3.2f), probe.LocalPosition, "local position", 1e-5f);
            Near(new Vector3(5f, 0f, 0f), probe.PositionWorld, "world position", 1e-5f);
            Near(1f, probe.SpeedStart, "snapped speed", 1e-5f);
            Near(2.5f, probe.SpeedEnd, "kept speed", 1e-5f);
        }

        // ---- the circle ----

        // .ctor 0x5FEF20 stores radius 10, end angle 360, clockwise true and axis up.
        [Test]
        public void CircleConstructorStoresTheNativeDefaults()
        {
            var circle = new TransformSurfaceCircleDescription();
            Assert.That(circle.Radius, Is.EqualTo(10f));
            Assert.That(circle.AngleStart, Is.Zero);
            Assert.That(circle.AngleEnd, Is.EqualTo(360f));
            Assert.That(circle.Clockwise, Is.True);
            Assert.That(Get(circle, "m_axis"), Is.EqualTo(Vector3.up));
            Assert.That(circle.Forward, Is.EqualTo(Vector3.zero));
            Assert.That(circle.FollowSpeed, Is.EqualTo(1f));
        }

        // CalculateDistance 0x5FEE30: (end - start) * (radius * 2 pi) / 360. SetExtentsMultiplier 0x5FEE70 scales the
        // radius by the change in multiplier before the base measures again.
        [Test]
        public void CircleDistanceFollowsTheRadiusAndTheExtentsMultiplier()
        {
            var circle = new TransformSurfaceCircleDescription();
            circle.SetExtentsMultiplier(1f);
            Near(62.831856f, circle.Distance, "10 * 2 pi", 1e-3f);
            Near(62.831856f, circle.TimeTotal, "follow speed 1");
            circle.SetExtentsMultiplier(2f);
            Near(20f, circle.Radius);
            Near(125.66371f, circle.Distance, "20 * 2 pi", 1e-3f);
            Near(125.66371f, circle.TimeTotal, "the time scales with the distance");
            Near(2f, (float)Get(circle, "m_extentsMultiplier"));
        }

        // GetTimeByAngle 0x5FE980: the angle is compared with the range (jae at 0x5FE99F) and scales the total time.
        [Test]
        public void CircleTimeByAngleIsRelativeToTheRange()
        {
            TransformSurfaceCircleDescription circle = Circle(30f, 120f, 2f);
            circle.SetExtentsMultiplier(1f);
            Near(2f, circle.TimeTotal);
            Near(1f, circle.GetTimeByAngle(45f), "half of the 90 degree range");
            Near(2f, circle.GetTimeByAngle(90f), "the whole range");
            Near(2f, circle.GetTimeByAngle(100f), "past the range");
        }

        // Initialise through the base: the start and end are rebuilt in the follower's space. The follower is
        // at (10, 0, 0), so the circle's centre is (10, 0, 0) and its basis is forward z, right x, up y
        // (CalculatePosition 0x5FE4D0: centre + (right * sin + forward * cos) * radius, clockwise).
        [Test]
        public void CirclePositionsAreBuiltInTheFollowersSpace()
        {
            using (var fixture = new Fixture(new Vector3(10f, 0f, 0f), Quaternion.identity))
            {
                TransformSurfaceCircleDescription circle = Circle(0f, 90f, 2f);
                circle.Initialise(fixture.Follower, Vector3.zero, Vector3.zero, Quaternion.identity);
                Near(new Vector3(10f, 0f, 0f), circle.PositionWorld, "centre");
                Near(new Vector3(10f, 0f, 2f), circle.PositionStartWorld, "start at 0 degrees");
                Near(new Vector3(12f, 0f, 0f), circle.PositionEndWorld, "end at 90 degrees");
                Near(new Vector3(2f, 0f, 0f), circle.PositionEndLocal, "end in the follower's space");
                Near(Pi, circle.Distance, "a quarter of a circle of radius 2", 1e-4f);
                Near(2f, circle.TimeTotal);
                Near(Vector3.forward, circle.Forward, "forward");
                Near(Vector3.right, circle.Right, "right");
                Near(Vector3.up, circle.Up, "up");

                Transform follower = fixture.Follower.transform;
                Near(new Vector3(10f, 0f, 2f), circle.Teleport(1, true, follower), "teleport forwards");
                Near(new Vector3(12f, 0f, 0f), circle.Teleport(0, true, follower), "teleport at zero");
                Near(new Vector3(12f, 0f, 0f), circle.Teleport(-1, true, follower), "teleport backwards");
                Assert.That(Get(circle, "m_angle"), Is.EqualTo(90f));
                Near(new Vector3(10f, 0f, 2f), circle.Teleport(1, false, null), "without a transform the stored basis is used");
                Assert.That(Get(circle, "m_angle"), Is.EqualTo(0f));
            }
        }

        // Update 0x5FE320 (time 2, radius 2, 0 to 90 degrees, clockwise): inside the surface the delta is used up
        // and the angle is start + progress * (end - start); at or past the end the angle is the end angle (the
        // start angle for a negative direction), the overshoot is handed back and the call reports false.
        [Test]
        public void CircleUpdateAdvancesAlongTheArcAndReportsTheOvershoot()
        {
            using (var fixture = new Fixture())
            {
                TransformSurfaceCircleDescription circle = Circle(0f, 90f, 2f);
                Transform follower = fixture.Follower.transform;
                float time = 0.5f, delta = 0.5f;

                Assert.That(circle.Update(out Vector3 untimed, ref time, ref delta, 1, true, follower), Is.False, "not measured yet");
                Near(Vector3.zero, untimed, "the local position");
                Assert.That(time, Is.EqualTo(0.5f));

                circle.Initialise(fixture.Follower, Vector3.zero, Vector3.zero, Quaternion.identity);
                Assert.That(circle.Update(out Vector3 position, ref time, ref delta, 1, true, follower), Is.True);
                Near(1f, time, "time advanced by the delta");
                Assert.That(delta, Is.Zero, "the delta is used up");
                Near(45f, (float)Get(circle, "m_angle"));
                Near(new Vector3(1.4142135f, 0f, 1.4142135f), position, "45 degrees");

                time = 0.25f; delta = 0.25f;
                Assert.That(circle.Update(out position, ref time, ref delta, -1, true, follower), Is.True);
                Near(67.5f, (float)Get(circle, "m_angle"), "a negative direction counts from the far end");
                Near(new Vector3(1.847759f, 0f, 0.7653669f), position, "67.5 degrees");

                time = 1.5f; delta = 1f;
                Assert.That(circle.Update(out position, ref time, ref delta, 1, true, follower), Is.False);
                Near(new Vector3(2f, 0f, 0f), position, "the end");
                Near(0.5f, delta, "overshoot");
                Near(2f, time, "clamped to the total time");
                Near(90f, (float)Get(circle, "m_angle"));

                time = 1.5f; delta = 1f;
                Assert.That(circle.Update(out position, ref time, ref delta, -1, true, follower), Is.False);
                Near(new Vector3(0f, 0f, 2f), position, "backwards ends at the start");
                Near(0f, (float)Get(circle, "m_angle"));

                time = 1.5f; delta = 1f;
                circle.Update(out position, ref time, ref delta, 0, true, follower);
                Near(90f, (float)Get(circle, "m_angle"), "zero counts as forwards (setns at 0x5FE419)");
            }
        }

        // GetRotationLocal 0x5FEBF0 is AngleAxis(angle or -angle when anticlockwise, axis); GetRotation 0x5FE9B0 is
        // the transform's rotation times that, then times half a turn about up (the static inverse rotation) when
        // orientating to the direction, travelling backwards clockwise or forwards anticlockwise.
        [Test]
        public void CircleRotationTurnsWithTheAngleAndTheDirection()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-rotation"))
            {
                Transform transform = objects.Create("transform", Vector3.zero, Quaternion.Euler(0f, 90f, 0f)).transform;
                var circle = new TransformSurfaceCircleDescription();
                Set(circle, "m_angle", 45f);
                float root = 0.70710678f;

                Near(new Vector3(root, 0f, root), circle.GetRotationLocal(transform) * Vector3.forward, "local, clockwise");
                Set(circle, "m_clockwise", false);
                Near(new Vector3(-root, 0f, root), circle.GetRotationLocal(transform) * Vector3.forward, "local, anticlockwise");
                Set(circle, "m_clockwise", true);

                Near(new Vector3(root, 0f, -root), circle.GetRotation(transform, 1, true) * Vector3.forward, "forwards, clockwise");
                Near(new Vector3(-root, 0f, root), circle.GetRotation(transform, -1, true) * Vector3.forward, "backwards, clockwise flips");
                Near(new Vector3(root, 0f, -root), circle.GetRotation(transform, -1, false) * Vector3.forward, "not orientating to the direction");
                Near(new Vector3(root, 0f, -root), circle.GetRotation(transform, 0, true) * Vector3.forward, "zero never flips");

                Set(circle, "m_clockwise", false);
                Near(new Vector3(-root, 0f, -root), circle.GetRotation(transform, 1, true) * Vector3.forward, "forwards, anticlockwise flips");
                Near(new Vector3(root, 0f, root), circle.GetRotation(transform, -1, true) * Vector3.forward, "backwards, anticlockwise");
            }
        }

        // SetPosition 0x5FEC30: a moved position rebuilds the start (and returns true); an unmoved one only does so
        // while the transform's forward or right still matches the stored basis, and otherwise returns false.
        [Test]
        public void CircleSetPositionNeedsAMovedPositionOrAMatchingAxis()
        {
            using (var fixture = new Fixture())
            {
                TransformSurfaceCircleDescription circle = Circle(0f, 90f, 2f);
                Transform transform = fixture.Objects.Create("transform").transform;

                Assert.That(circle.SetPosition(new Vector3(1f, 0f, 0f), transform), Is.True, "moved");
                Near(new Vector3(1f, 0f, 2f), circle.PositionStartWorld, "start rebuilt");
                Near(Vector3.forward, circle.Forward);
                Near(Vector3.right, circle.Right);

                Assert.That(circle.SetPosition(new Vector3(1f, 0f, 0f), transform), Is.True, "unmoved with both axes matching");

                transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Assert.That(circle.SetPosition(new Vector3(1f, 0f, 0f), transform), Is.True, "unmoved, only forward matches");
                Near(new Vector3(0f, 1f, 2f), circle.PositionStartWorld, "start rebuilt from the turned basis");
                Near(Vector3.up, circle.Right, "right follows the transform");

                transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Assert.That(circle.SetPosition(new Vector3(1f, 0f, 0f), transform), Is.False, "unmoved with neither axis matching");
                Near(new Vector3(0f, 1f, 2f), circle.PositionStartWorld, "start kept");

                Assert.That(circle.SetPosition(new Vector3(2f, 0f, 0f), transform), Is.True, "moved again");
            }
        }

        // ---- the follower ----

        [Test]
        public void FollowerConstructorStoresTheNativeDefaults()
        {
            using (var fixture = new Fixture())
            {
                TransformSurfaceFollower follower = fixture.Follower;
                Assert.That(follower.IsRuntime, Is.True);
                Assert.That(follower.TimeMultiplier, Is.EqualTo(1f));
                Assert.That(follower.Static, Is.True);
                Assert.That(follower.WrapMode, Is.EqualTo(TransformSurfaceFollower.WrapModeType.PingPong));
                Assert.That(follower.Closed, Is.False);
                Assert.That(follower.NumSurfaces, Is.Zero);
                Assert.That(follower.OnCompleteAction, Is.Null);
                Assert.That(Get(follower, "m_targets"), Is.InstanceOf<List<TransformSurfaceTarget>>().And.Empty);
                Assert.That(follower.GetSurfaceSpline(), Is.Null);
            }
        }

        // GetSurface 0x601EA0 answers null outside the list; AddSurface 0x6033A0 appends; GetSurfaceSpline
        // 0x603430 skips null entries and surfaces without a spline; GetTotalDistance 0x6039B0 counts a null entry
        // as 0 (the test at 0x603A76); SetExtentsMultiplier 0x603010 forwards to every
        // surface (the circle's radius is scaled; its follow time is not).
        [Test]
        public void FollowerSurfaceAccessAndExtents()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceFollower follower = fixture.Follower;
                Assert.That(follower.NumSurfaces, Is.EqualTo(2));
                Assert.That(follower.GetSurface(0), Is.SameAs(fixture.Surfaces[0]));
                Assert.That(follower.GetSurface(1), Is.SameAs(fixture.Surfaces[1]));
                Assert.That(follower.GetSurface(2), Is.Null);
                Assert.That(follower.GetSurface(-1), Is.Null);
                fixture.Reset();
                ((List<TransformSurfaceDescription>)Get(follower, "m_surfaces")).Add(null);
                Assert.That(follower.GetSurfaceSpline(), Is.Null, "no surface offers a spline, and a null entry is skipped");
                Near(2f * Pi, (float)Call(follower, "GetTotalDistance"), "a null entry adds no distance", 1e-3f);

                ((List<TransformSurfaceDescription>)Get(follower, "m_surfaces")).RemoveAt(2);
                follower.SetExtentsMultiplier(2f);
                foreach (TransformSurfaceDescription surface in fixture.Surfaces)
                {
                    Near(4f, ((TransformSurfaceCircleDescription)surface).Radius, "radius");
                    Near(2f * Pi, surface.Distance, "distance", 1e-3f);
                    Near(1f, surface.TimeTotal, "the follow time is fixed");
                }
            }
        }

        // GetSurfaceByDistance 0x603830 (two quarter arcs of length pi, one second each): distance 0 answers
        // surface 0, time 0, direction 1; otherwise whole surfaces are taken off while the distance is at least
        // the surface length, stopping at the last. The direction is 1 only while the total length of all the surfaces
        // exceeds the distance left in the final surface (seta at 0x603971), so a remainder past the total gives -1.
        [Test]
        public void FollowerFindsTheSurfaceByDistance()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                fixture.Reset();
                TransformSurfaceFollower follower = fixture.Follower;
                int index; float time; int direction;

                follower.GetSurfaceByDistance(0f, out index, out time, out direction);
                Assert.That((index, time, direction), Is.EqualTo((0, 0f, 1)));

                follower.GetSurfaceByDistance(0.5f * Pi, out index, out time, out direction);
                Assert.That(index, Is.Zero);
                Near(0.5f, time, "half of the first surface");
                Assert.That(direction, Is.EqualTo(1));

                follower.GetSurfaceByDistance(1.5f * Pi, out index, out time, out direction);
                Assert.That(index, Is.EqualTo(1));
                Near(0.5f, time, "half of the second surface");
                Assert.That(direction, Is.EqualTo(1));

                follower.GetSurfaceByDistance(2.5f * Pi, out index, out time, out direction);
                Assert.That(index, Is.EqualTo(1), "past the end stays on the last surface");
                Near(0.5f, time, "the remainder after taking off the first surface and one more length");
                Assert.That(direction, Is.EqualTo(1));

                follower.GetSurfaceByDistance(3.5f * Pi, out index, out time, out direction);
                Assert.That(index, Is.EqualTo(1));
                Near(1f, time, "the remainder exceeds the last surface");
                Assert.That(direction, Is.EqualTo(1));

                follower.GetSurfaceByDistance(5f * Pi, out index, out time, out direction);
                Assert.That(index, Is.EqualTo(1));
                Near(1f, time);
                Assert.That(direction, Is.EqualTo(-1), "a remainder of 3 pi exceeds the total of 2 pi");

                follower.GetSurfaceByNormalisedValue(0.75f, out index, out time, out direction);
                Assert.That(index, Is.EqualTo(1));
                Near(0.5f, time, "0.75 of the total length");
                Assert.That(direction, Is.EqualTo(1));

                follower.GetSurfaceByAngle(45f, out index, out time, out direction);
                Assert.That(index, Is.Zero);
                Near(0.5f, time, "45 of 90 degrees on the first surface");
                Assert.That(direction, Is.EqualTo(1));
                follower.GetSurfaceByAngle(100f, out index, out time, out direction);
                Near(1f, time, "past the range");
            }
        }

        // InternalReset 0x6015E0 hands each surface the end point of its segment, the previous point and the end
        // rotation: the last surface leads into the first when closed and ends at its own point otherwise.
        [Test]
        public void FollowerResetGivesEverySurfaceItsEndAndPreviousPoint()
        {
            foreach (bool closed in new[] { false, true })
            {
                using (var fixture = new Fixture())
                {
                    var probes = new List<DescriptionProbe>();
                    for (int i = 0; i < 3; i++)
                    {
                        DescriptionProbe probe = Probe(TransformSurfaceDescription.FollowModeType.Speed, 8f, speed: 3f);
                        Set(probe, "m_position", new Vector3(1f + 4f * i, 0f, 0f));
                        probe.LocalRotation = Quaternion.Euler(0f, 10f * (i + 1), 0f);
                        probes.Add(probe);
                        fixture.Add(probe);
                    }
                    Set(fixture.Follower, "m_closed", closed);
                    fixture.Reset();

                    foreach (DescriptionProbe probe in probes) Assert.That(probe.Initialised.Count, Is.EqualTo(1));
                    string label = closed ? "closed" : "open";
                    Near(new Vector3(5f, 0f, 0f), probes[0].Initialised[0].end, label + " first end");
                    Near(new Vector3(9f, 0f, 0f), probes[0].Initialised[0].prev, label + " first previous is the last point");
                    Assert.That(probes[0].Initialised[0].rotation, Is.EqualTo(probes[1].LocalRotation), label + " first rotation");
                    Near(new Vector3(9f, 0f, 0f), probes[1].Initialised[0].end, label + " second end");
                    Near(new Vector3(1f, 0f, 0f), probes[1].Initialised[0].prev, label + " second previous");
                    Assert.That(probes[1].Initialised[0].rotation, Is.EqualTo(probes[2].LocalRotation), label + " second rotation");
                    Near(new Vector3(9f, 0f, 0f), probes[2].Initialised[0].prev, label + " last previous");
                    Near(closed ? new Vector3(1f, 0f, 0f) : new Vector3(9f, 0f, 0f), probes[2].Initialised[0].end, label + " last end");
                    Assert.That(probes[2].Initialised[0].rotation,
                        Is.EqualTo(closed ? probes[0].LocalRotation : probes[2].LocalRotation), label + " last rotation");

                    Near(new Vector3(5f, 0f, 0f), probes[0].PositionEndWorld, label + " stored end");
                    Near(new Vector3(1f, 0f, 0f), probes[0].PositionWorld, label + " stored position");
                    Near(closed ? new Vector3(1f, 0f, 0f) : new Vector3(9f, 0f, 0f), probes[2].PositionEndWorld, label + " last stored end");
                    Near(8f, probes[2].Distance, label + " measured");
                }
            }
        }

        // InternalReset 0x6015E0 then gives each SpeedLerp surface the previous surface's end speed and the next one's
        // start speed (0 at an open end, the first surface's start speed at a closed one). Fixed-speed surfaces
        // ignore the limits. Distance 8 throughout.
        [Test]
        public void FollowerResetChainsTheSpeedLimitsAcrossTheSurfaces()
        {
            foreach (bool closed in new[] { false, true })
            {
                using (var fixture = new Fixture())
                {
                    float[] speeds = { 3f, 4f, 6f };
                    foreach (float speed in speeds)
                        fixture.Add(Probe(TransformSurfaceDescription.FollowModeType.Speed, 8f, speed: speed));
                    DescriptionProbe lerp = Probe(TransformSurfaceDescription.FollowModeType.SpeedLerp, 8f);
                    fixture.Add(lerp);
                    Set(fixture.Follower, "m_closed", closed);
                    fixture.Reset();

                    string label = closed ? "closed" : "open";
                    Near(6f, lerp.SpeedStart, label + " takes the previous end speed");
                    Near(closed ? 3f : 0f, lerp.SpeedEnd, label + " end speed");
                    Near(closed ? 16f / 9f : 16f / 6f, lerp.TimeTotal, label + " time");
                    Near(3f, fixture.Surfaces[0].SpeedStart, label + " fixed surface");
                    Near(6f, fixture.Surfaces[2].SpeedEnd, label + " fixed surface end");
                }

                using (var fixture = new Fixture())
                {
                    DescriptionProbe first = Probe(TransformSurfaceDescription.FollowModeType.SpeedLerp, 8f);
                    fixture.Add(first);
                    fixture.Add(Probe(TransformSurfaceDescription.FollowModeType.Speed, 8f, speed: 4f));
                    Set(fixture.Follower, "m_closed", closed);
                    fixture.Reset();

                    string label = "two surfaces " + (closed ? "closed" : "open");
                    Near(closed ? 4f : 0f, first.SpeedStart, label + " start");
                    Near(4f, first.SpeedEnd, label + " end is the next start");
                    Near(closed ? 2f : 4f, first.TimeTotal, label + " time");
                }
            }
        }

        // Reset ends with TimeMultiplier = 1 (0x601A36) after initialising every target.
        [Test]
        public void FollowerResetRestoresTheTimeMultiplier()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                Call(fixture.Follower, "set_TimeMultiplier", 3f);
                Near(3f, fixture.Follower.TimeMultiplier);
                fixture.Reset();
                Near(1f, fixture.Follower.TimeMultiplier);
                Assert.That(target.Surface, Is.SameAs(fixture.Surfaces[0]), "the target was initialised");
            }
        }

        // Play 0x602BE0 and Stop 0x603140 test activeInHierarchy first, and PlayWithDirection 0x602C30 does too: on an
        // inactive object nothing changes. SetEnabled 0x601B90 is the enabled setter.
        [Test]
        public void FollowerIgnoresPlaybackOnAnInactiveObjectAndSetsEnabled()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                Set(target, "m_direction", 1);
                TransformSurfaceFollower follower = fixture.Follower;

                follower.Play(2f);
                follower.PlayWithDirection(-1f);
                follower.Stop();
                Near(1f, follower.TimeMultiplier);
                Assert.That(Get(target, "m_direction"), Is.EqualTo(1));
                Assert.That(Get(StopOf(target), "m_stopActive"), Is.False);

                follower.SetEnabled(false);
                Assert.That(follower.enabled, Is.False);
                follower.SetEnabled(true);
                Assert.That(follower.enabled, Is.True);
            }
        }

        // OnDisable 0x601BA0 runs the base, then invokes OnCompleteAction with the follower, if set.
        [Test]
        public void FollowerOnDisableInvokesTheCompleteAction()
        {
            using (var fixture = new Fixture())
            {
                Call(fixture.Follower, "OnDisable");
                TransformSurfaceFollower received = null;
                int calls = 0;
                fixture.Follower.OnCompleteAction = follower => { received = follower; calls++; };
                Call(fixture.Follower, "OnDisable");
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(received, Is.SameAs(fixture.Follower));
            }
        }

        // ---- the target ----

        // GetInitialState 0x603CD0 by start mode (jump table at 0x603EA8): Distance asks the follower, Index uses the
        // index at time 0 and direction 1, Angle uses the first surface, Normalised scales the total length; the
        // surface is then looked up (null outside the list) and a reversed start turns the target round.
        [Test]
        public void TargetStartModesPickTheInitialSurface()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                fixture.Reset();

                var distance = new TransformSurfaceTarget();
                Set(distance, "m_startDistance", 1.5f * Pi);
                Call(distance, "GetInitialState", fixture.Follower);
                Assert.That(Get(distance, "m_surfaceIndex"), Is.EqualTo(1));
                Near(0.5f, (float)Get(distance, "m_time"));
                Assert.That(Get(distance, "m_direction"), Is.EqualTo(1));
                Assert.That(distance.Surface, Is.SameAs(fixture.Surfaces[1]));

                var index = new TransformSurfaceTarget();
                Set(index, "m_startModeType", TransformSurfaceTarget.StartModeType.Index);
                Set(index, "m_startIndex", 1);
                Call(index, "GetInitialState", fixture.Follower);
                Assert.That(Get(index, "m_surfaceIndex"), Is.EqualTo(1));
                Assert.That(Get(index, "m_time"), Is.EqualTo(0f));
                Assert.That(Get(index, "m_direction"), Is.EqualTo(1));
                Assert.That(index.Surface, Is.SameAs(fixture.Surfaces[1]));

                Set(index, "m_startIndex", 5);
                Call(index, "GetInitialState", fixture.Follower);
                Assert.That(Get(index, "m_surfaceIndex"), Is.EqualTo(5));
                Assert.That(index.Surface, Is.Null, "outside the list");

                var angle = new TransformSurfaceTarget();
                Set(angle, "m_startModeType", TransformSurfaceTarget.StartModeType.Angle);
                Set(angle, "m_startAngle", 45);
                Call(angle, "GetInitialState", fixture.Follower);
                Assert.That(Get(angle, "m_surfaceIndex"), Is.Zero);
                Near(0.5f, (float)Get(angle, "m_time"));
                Assert.That(angle.Surface, Is.SameAs(fixture.Surfaces[0]));

                var normalised = new TransformSurfaceTarget();
                Set(normalised, "m_startModeType", TransformSurfaceTarget.StartModeType.Normalised);
                Set(normalised, "m_normalisedValue", 0.75f);
                Call(normalised, "GetInitialState", fixture.Follower);
                Assert.That(Get(normalised, "m_surfaceIndex"), Is.EqualTo(1));
                Near(0.5f, (float)Get(normalised, "m_time"));

                var unknown = new TransformSurfaceTarget();
                Set(unknown, "m_startModeType", (TransformSurfaceTarget.StartModeType)9);
                Set(unknown, "m_surfaceIndex", 1);
                Call(unknown, "GetInitialState", fixture.Follower);
                Assert.That(Get(unknown, "m_surfaceIndex"), Is.Zero);
                Assert.That(Get(unknown, "m_time"), Is.EqualTo(0f));
                Assert.That(Get(unknown, "m_direction"), Is.EqualTo(1));
            }
        }

        [Test]
        public void TargetStartingReversedTurnsRoundOnce()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                fixture.Reset();

                var reversed = new TransformSurfaceTarget();
                Set(reversed, "m_startModeType", TransformSurfaceTarget.StartModeType.Index);
                Set(reversed, "m_startIndex", 1);
                Set(reversed, "m_startDirectionReversed", true);
                Call(reversed, "GetInitialState", fixture.Follower);
                Assert.That(Get(reversed, "m_direction"), Is.EqualTo(-1));
                Near(1f, (float)Get(reversed, "m_time"), "total time - 0");
                Assert.That(Get(reversed, "m_ignoreEndEvents"), Is.True);

                // Already travelling backwards: a distance past the total gives direction -1 and nothing more.
                var backwards = new TransformSurfaceTarget();
                Set(backwards, "m_startDistance", 5f * Pi);
                Set(backwards, "m_startDirectionReversed", true);
                Call(backwards, "GetInitialState", fixture.Follower);
                Assert.That(Get(backwards, "m_direction"), Is.EqualTo(-1));
                Near(1f, (float)Get(backwards, "m_time"));
                Assert.That(Get(backwards, "m_ignoreEndEvents"), Is.False);
            }
        }

        // SetDirection 0x602DF0 turning round mirrors the time (total - time) and suppresses the next end events;
        // the same direction changes nothing. Play 0x602E20 resets the stop; Stop 0x603310 activates it with the
        // target's time and direction.
        [Test]
        public void TargetSetDirectionPlayAndStop()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                Set(target, "m_time", 0.25f);

                target.SetDirection(1);
                Near(0.25f, (float)Get(target, "m_time"));
                Assert.That(Get(target, "m_ignoreEndEvents"), Is.False);
                target.SetDirection(-1);
                Assert.That(Get(target, "m_direction"), Is.EqualTo(-1));
                Near(0.75f, (float)Get(target, "m_time"));
                Assert.That(Get(target, "m_ignoreEndEvents"), Is.True);

                target.Stop();
                TransformSurfaceTargetStop stop = StopOf(target);
                Assert.That(Get(stop, "m_stopActive"), Is.True);
                Near(0.25f, (float)Get(stop, "m_stopSurfaceTime"), "total - time");
                target.Play();
                Assert.That(Get(stop, "m_stopActive"), Is.False);
            }
        }

        // OnAwake 0x601BE0 finds the CharacterCollisionData below the transform (inactive ones too) and gives each a
        // zero movement (SetMovement(true, zero) at 0x603F5D); SetTransform 0x6040B0 reports (new position - current
        // position) / deltaTime to them when deltaTime is not below 0.001 and the target has a surface (ucomiss at 0x60411A,
        // ja at 0x604122), and a zero movement otherwise.
        [Test]
        public void TargetReportsItsMovementToTheCollisionDataBelowIt()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                var transform = (Transform)Get(target, "m_transform");
                GameObject child = fixture.Objects.Create("collision data", Vector3.zero, Quaternion.identity, transform);
                child.SetActive(false);
                child.AddComponent<BoxCollider>();
                var data = child.AddComponent<CharacterCollisionData>();
                data.SetMovement(false, new Vector3(5f, 5f, 5f));

                target.OnAwake(fixture.Follower);
                Assert.That(data.HasMovement, Is.True);
                Assert.That(data.MovementVelocity, Is.EqualTo(Vector3.zero));

                fixture.Reset();
                var cases = new[]
                {
                    (0.5f, new Vector3(4f, 0f, 0f)),
                    (0.001f, new Vector3(2000f, 0f, 0f)),
                    (0.0009f, Vector3.zero),
                    (0f, Vector3.zero)
                };
                foreach (var (deltaTime, velocity) in cases)
                {
                    transform.position = Vector3.zero;
                    data.SetMovement(false, new Vector3(9f, 9f, 9f));
                    Call(target, "SetTransform", fixture.Follower, new Vector3(2f, 0f, 0f), deltaTime);
                    Assert.That(data.HasMovement, Is.True, "delta time " + deltaTime);
                    Near(velocity, data.MovementVelocity, "delta time " + deltaTime, 0.5f);
                    Near(new Vector3(2f, 0f, 0f), transform.position, "position " + deltaTime);
                }

                Set(target, "m_surface", null);
                transform.position = Vector3.zero;
                Call(target, "SetTransform", fixture.Follower, new Vector3(2f, 0f, 0f), 0.5f);
                Near(Vector3.zero, data.MovementVelocity, "no surface");
            }
        }

        // Update 0x602210 with the default PingPong wrap, a static follower and two one-second quarter arcs of radius
        // 2 (the circle's stored basis is used because the follower is static). Half a second moves to 45 degrees;
        // three quarters more finishes the first arc (end events, the next surface starts with its begin event)
        // and carries the 0.25 s overshoot onto the second arc; 1.5 s more finishes the chain, which raises the
        // target's end event and turns it round, so the 0.75 s left travel the second arc backwards.
        [Test]
        public void TargetUpdateCarriesTheOvershootAcrossSurfacesAndPingPongs()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                var first = Count(fixture.Surfaces[0]);
                var second = Count(fixture.Surfaces[1]);
                var own = Count(target);
                fixture.Reset();
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 0.5f);
                Near(new Vector3(1.4142135f, 0f, 1.4142135f), transform.position, "45 degrees");
                Near(0.5f, (float)Get(target, "m_time"));
                Assert.That(Get(target, "m_surfaceIndex"), Is.Zero);

                target.Update(fixture.Follower, 0.75f);
                Near(new Vector3(1.847759f, 0f, -0.7653669f), transform.position, "112.5 degrees on the second arc");
                Near(0.25f, (float)Get(target, "m_time"));
                Assert.That(Get(target, "m_surfaceIndex"), Is.EqualTo(1));
                Assert.That(target.Surface, Is.SameAs(fixture.Surfaces[1]));
                Assert.That(first, Is.EqualTo(new[] { 0, 1 }), "the first arc ended");
                Assert.That(second, Is.EqualTo(new[] { 1, 0 }), "the second arc started");
                Assert.That(own, Is.EqualTo(new[] { 0, 0 }));

                target.Update(fixture.Follower, 1.5f);
                Near(new Vector3(1.847759f, 0f, -0.7653669f), transform.position, "112.5 degrees again, travelling backwards");
                Near(0.75f, (float)Get(target, "m_time"));
                Assert.That(Get(target, "m_direction"), Is.EqualTo(-1));
                Assert.That(Get(target, "m_surfaceIndex"), Is.EqualTo(1));
                Assert.That(second, Is.EqualTo(new[] { 2, 1 }), "the second arc ended and started again");
                Assert.That(own, Is.EqualTo(new[] { 0, 1 }), "the chain ended");
            }
        }

        // Update 0x602210 past the last surface by wrap mode: Loop fires the target's end and beginning events and
        // continues from the first surface; Once leaves the surface unset at index -1 and holds the end position.
        [Test]
        public void TargetUpdateLoopsOrFinishesAtTheEndOfTheChain()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                Set(fixture.Follower, "m_wrapMode", TransformSurfaceFollower.WrapModeType.Loop);
                TransformSurfaceTarget target = fixture.AddTarget();
                Set(target, "m_startModeType", TransformSurfaceTarget.StartModeType.Index);
                Set(target, "m_startIndex", 1);
                var own = Count(target);
                var first = Count(fixture.Surfaces[0]);
                fixture.Reset();
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 1.25f);
                Near(new Vector3(0.7653669f, 0f, 1.847759f), transform.position, "22.5 degrees on the first arc");
                Assert.That(Get(target, "m_surfaceIndex"), Is.Zero);
                Near(0.25f, (float)Get(target, "m_time"));
                Assert.That(own, Is.EqualTo(new[] { 1, 1 }), "end then beginning of the chain");
                Assert.That(first, Is.EqualTo(new[] { 1, 0 }));
            }

            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                Set(fixture.Follower, "m_wrapMode", TransformSurfaceFollower.WrapModeType.Once);
                TransformSurfaceTarget target = fixture.AddTarget();
                Set(target, "m_startModeType", TransformSurfaceTarget.StartModeType.Index);
                Set(target, "m_startIndex", 1);
                var own = Count(target);
                fixture.Reset();
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 1.25f);
                Near(new Vector3(0f, 0f, -2f), transform.position, "the end of the chain");
                Assert.That(target.Surface, Is.Null);
                Assert.That(Get(target, "m_surfaceIndex"), Is.EqualTo(-1));
                Assert.That(own, Is.EqualTo(new[] { 0, 1 }));

                transform.position = new Vector3(5f, 5f, 5f);
                target.Update(fixture.Follower, 1f);
                Near(new Vector3(5f, 5f, 5f), transform.position, "a finished target does not move");
            }
        }

        // SetTransform 0x6040B0 orientates the transform only when the surface and the target both ask for it:
        // the surface rotation times the forward offset rotation, or the backward one when the direction is not
        // positive (cmp 0 / setle at 0x6042C3). The offsets are the Euler rotations OnAwake builds (0x601CAD..0x601CFA).
        [Test]
        public void TargetOrientatesWithTheOffsetRotationForTheDirection()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                Set(target, "m_orientate", true);
                Set(target, "m_orientateToDirection", false);
                Set(target, "m_rotationLocalForward", new Vector3(0f, 90f, 0f));
                Set(target, "m_rotationLocalBackward", new Vector3(0f, -90f, 0f));
                target.OnAwake(fixture.Follower);
                Near(Vector3.right, (Quaternion)Get(target, "m_rotationWorldToLocalForward") * Vector3.forward, "forward offset");
                Near(Vector3.left, (Quaternion)Get(target, "m_rotationWorldToLocalBackward") * Vector3.forward, "backward offset");
                fixture.Reset();
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 0.5f);
                float root = 0.70710678f;
                Near(new Vector3(root, 0f, -root), transform.rotation * Vector3.forward, "135 degrees about up");

                Set(target, "m_direction", -1);
                Call(target, "SetTransform", fixture.Follower, Vector3.zero, 1f);
                Near(new Vector3(-root, 0f, root), transform.rotation * Vector3.forward, "-45 degrees about up");
                Near(Vector3.zero, transform.position, "the position is the surface position");

                Set(target, "m_orientate", false);
                transform.rotation = Quaternion.identity;
                Call(target, "SetTransform", fixture.Follower, new Vector3(1f, 2f, 3f), 1f);
                Near(new Vector3(1f, 2f, 3f), transform.position, "position only");
                Near(Vector3.forward, transform.rotation * Vector3.forward, "rotation untouched");
            }
        }

        // ---- axis, product order and boundary cases ----

        // CalculatePosition 0x5FE4D0 multiplies the transform's rotation by FromToRotation(up, axis) in that order
        // (the lanes at 0x5FE526..0x5FE61D). The follower is turned 90 degrees about up and the axis is right, so the
        // basis only comes out as forward +x, right -y, up -z when the two rotations do not commute.
        [Test]
        public void CircleBasisTurnsFromUpToTheAxisInTheFollowersSpace()
        {
            using (var fixture = new Fixture(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)))
            {
                TransformSurfaceCircleDescription circle = Circle(0f, 90f, 2f);
                Set(circle, "m_axis", Vector3.right);
                circle.Initialise(fixture.Follower, Vector3.zero, Vector3.zero, Quaternion.identity);
                Near(Vector3.right, circle.Forward, "forward");
                Near(Vector3.down, circle.Right, "right");
                Near(Vector3.back, circle.Up, "up");
                Near(new Vector3(12f, 0f, 0f), circle.PositionStartWorld, "start at 0 degrees");
                Near(new Vector3(10f, -2f, 0f), circle.PositionEndWorld, "end at 90 degrees");
            }
        }

        // GetRotationLocal 0x5FEBF0 turns about the axis field (read at 0x5FEC0C); GetRotation 0x5FE9B0 is
        // transform.rotation * local, then * the inverse rotation. With the transform turned about up and the axis
        // right, swapping either product changes the result.
        [Test]
        public void CircleRotationTurnsAboutTheAxisAndMultipliesInOrder()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-axis"))
            {
                Transform transform = objects.Create("transform", Vector3.zero, Quaternion.Euler(0f, 90f, 0f)).transform;
                var circle = new TransformSurfaceCircleDescription();
                Set(circle, "m_axis", Vector3.right);
                Set(circle, "m_angle", 45f);
                float root = 0.70710678f;

                Near(new Vector3(0f, -root, root), circle.GetRotationLocal(transform) * Vector3.forward, "local about right");
                Near(new Vector3(root, -root, 0f), circle.GetRotation(transform, 1, false) * Vector3.forward, "transform times local");
                Near(new Vector3(-root, root, 0f), circle.GetRotation(transform, -1, true) * Vector3.forward, "then times the inverse rotation");
            }
        }

        // SetTransform 0x6040B0 multiplies the surface rotation by the offset rotation, in that order (the lhs is the
        // GetRotation result, 0x6042E0..0x60437A). A quarter turn about right is the offset, so the order shows.
        [Test]
        public void TargetOffsetRotationFollowsTheSurfaceRotation()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                Set(target, "m_orientate", true);
                Set(target, "m_orientateToDirection", false);
                Set(target, "m_rotationLocalForward", new Vector3(90f, 0f, 0f));
                target.OnAwake(fixture.Follower);
                fixture.Reset();
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 0.5f);
                float root = 0.70710678f;
                Near(Vector3.down, transform.rotation * Vector3.forward, "forward");
                Near(new Vector3(root, 0f, root), transform.rotation * Vector3.up, "up");
            }
        }

        // Update 0x6024F7..0x602518 leaves the loop after a surface change when the follower is disabled (or the stop
        // was reached) and runtime; the 0.5 s overshoot is dropped.
        [Test]
        public void TargetUpdateStopsAtTheJoinWhenTheFollowerIsDisabled()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                fixture.Follower.SetEnabled(false);
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 1.5f);
                Assert.That(Get(target, "m_surfaceIndex"), Is.EqualTo(1));
                Assert.That(Get(target, "m_time"), Is.EqualTo(0f));
                Near(new Vector3(2f, 0f, 0f), transform.position, "the start of the second arc");
            }
        }

        // The stop's Update reports true for the surface-relative modes (here EaseToNext, InQuad, ease time 0.1, 0.2 s of
        // surface left at time 0.9): remaining 0.5, step max(0.5 * 0.25, 0.001), ratio 2, delta max(0.25, 0.5 * min(1, 1))
        // = 0.5, which crosses the end. The stop then breaks the loop (0x6024F7..0x602518) and FireEndEvents
        // (0x604468..0x6044AB) raises the stop event and, as m_fireEndEvents is true, the surface end.
        [Test]
        public void TargetUpdateEndsAnEasedStopAtTheSurfaceEnd()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                TransformSurfaceTargetStop stop = StopOf(target);
                Set(stop, "m_stopModeType", TransformSurfaceTargetStop.StopModeType.EaseToNext);
                Set(stop, "m_stopEaseType", EaseUtilities.EaseType.InQuad);
                Set(stop, "m_stopEaseTime", 0.1f);
                Set(stop, "m_stopTime", 0f);
                Set(stop, "m_stopSurfaceTime", 0.2f);
                Set(stop, "m_stopActive", true);
                Set(target, "m_time", 0.9f);
                Set(target, "m_direction", 1);
                int stopEvents = 0;
                ((UnityEvent)Get(stop, "m_onStopReached")).AddListener(() => stopEvents++);
                var first = Count(fixture.Surfaces[0]);
                Transform transform = (Transform)Get(target, "m_transform");

                target.Update(fixture.Follower, 0.5f);
                Assert.That(Get(target, "m_surfaceIndex"), Is.EqualTo(1));
                Assert.That(Get(target, "m_time"), Is.EqualTo(0f));
                Near(new Vector3(2f, 0f, 0f), transform.position, "the start of the second arc");
                Assert.That(stopEvents, Is.EqualTo(1));
                Assert.That(first[1], Is.EqualTo(1), "the first arc ended");
                Assert.That(Get(stop, "m_stopActive"), Is.False);
            }
        }

        // FireEndEvents 0x604450 reads m_fireEndEvents before OnStopReached (0x604468..0x6044AB); the surface's end
        // follows only when it is true.
        [Test]
        public void TargetFireEndEventsRaisesTheSurfaceEndOnlyWhenTheStopAllowsIt()
        {
            foreach (bool fireEndEvents in new[] { false, true })
            {
                using (var fixture = new Fixture())
                {
                    fixture.AddQuarterArcs();
                    TransformSurfaceTarget target = fixture.AddTarget();
                    fixture.Reset();
                    TransformSurfaceTargetStop stop = StopOf(target);
                    Set(stop, "m_fireEndEvents", fireEndEvents);
                    Set(stop, "m_stopActive", true);
                    int stopEvents = 0;
                    ((UnityEvent)Get(stop, "m_onStopReached")).AddListener(() => stopEvents++);
                    var first = Count(fixture.Surfaces[0]);

                    Call(target, "FireEndEvents", true);
                    Assert.That(stopEvents, Is.EqualTo(1), "fire end events " + fireEndEvents);
                    Assert.That(first[1], Is.EqualTo(fireEndEvents ? 1 : 0), "fire end events " + fireEndEvents);
                }
            }
        }

        // FireEndEvents 0x604450 returns while m_ignoreEndEvents is set (0x604457), which SetDirection sets when it
        // turns the target round. Update clears it afterwards.
        [Test]
        public void TargetUpdateSuppressesTheSurfaceEndAfterTurningRound()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                var first = Count(fixture.Surfaces[0]);
                var own = Count(target);
                Transform transform = (Transform)Get(target, "m_transform");

                target.SetDirection(-1);
                Assert.That(Get(target, "m_ignoreEndEvents"), Is.True);
                Near(1f, (float)Get(target, "m_time"));
                target.Update(fixture.Follower, 0.5f);

                Assert.That(first, Is.EqualTo(new[] { 1, 0 }), "begun again, not ended");
                Assert.That(own, Is.EqualTo(new[] { 1, 0 }), "the chain's beginning");
                Near(0.5f, (float)Get(target, "m_time"));
                Assert.That(Get(target, "m_direction"), Is.EqualTo(1));
                Assert.That(Get(target, "m_ignoreEndEvents"), Is.False);
                Near(new Vector3(1.4142135f, 0f, 1.4142135f), transform.position, "45 degrees");
            }
        }

        // GetProgressByTime 0x5FE7F0 clamps the interpolant of TimeSmoothed's SmoothStep (0x5FE8A6..0x5FE8B2) and of
        // SpeedLerp's Lerp (0x5FE90B..0x5FE927). A time of 6 of 4 would give 0 from the unclamped polynomial, and a
        // time of -1 would give 0.21875.
        [Test]
        public void ProgressByTimeClampsItsInterpolants()
        {
            DescriptionProbe smooth = Probe(TransformSurfaceDescription.FollowModeType.TimeSmoothed, 8f, time: 4f);
            Near(1f, smooth.Progress(6f, 1), "past the end");
            Near(0f, smooth.Progress(5f, -1), "before the start");

            DescriptionProbe lerp = Probe(TransformSurfaceDescription.FollowModeType.SpeedLerp, 8f);
            lerp.SetSpeedLimits(2f, 6f);
            Near(3.75f, lerp.Progress(5f, 1), "5 * Lerp(2, 6, 1.25 clamped to 1) / 8");
        }

        // CalculateSpeedLimits 0x5FF210 only divides by the first key's time when it is positive (ucomiss/jbe at
        // 0x5FF2AB..0x5FF2AE); a first key at 0 gives a start speed of 0.
        [Test]
        public void ProgressAnimatedStartSpeedNeedsAPositiveFirstKeyTime()
        {
            DescriptionProbe progress = Probe(TransformSurfaceDescription.FollowModeType.ProgressAnimated, 10f, curve: Curve(0f, 0f, 5f, 1f));
            Near(0f, progress.SpeedStart, "start");
            Near(2f, progress.SpeedEnd, "end");
            Near(5f, progress.TimeTotal, "time");
        }

        // InternalReset 0x6015E0 with one surface passes speedEndPrev as the next speed as well (cmp count, 1 at
        // 0x60194D, [rbp-0x40] = speedEndPrev at 0x601958). Closed, that is the surface's own end speed: limits (5, 7)
        // become (7, 7) and take 16 / 14 s; the first surface's start speed or 0 as the next speed would give 16 / 12
        // or 16 / 7. Open, both are 0 and the crossing never ends.
        [Test]
        public void FollowerResetGivesASingleSurfaceItsOwnEndSpeedAsTheNextSpeed()
        {
            foreach (bool closed in new[] { false, true })
            {
                using (var fixture = new Fixture())
                {
                    DescriptionProbe lerp = Probe(TransformSurfaceDescription.FollowModeType.SpeedLerp, 8f);
                    lerp.SetSpeedLimits(5f, 7f);
                    fixture.Add(lerp);
                    Set(fixture.Follower, "m_closed", closed);
                    fixture.Reset();

                    string label = closed ? "closed" : "open";
                    Near(closed ? 7f : 0f, lerp.SpeedStart, label + " start");
                    Near(closed ? 7f : 0f, lerp.SpeedEnd, label + " end");
                    if (closed) Near(8f / 7f, lerp.TimeTotal, label + " time");
                    else Assert.That(float.IsPositiveInfinity(lerp.TimeTotal), Is.True, label + " time");
                }
            }
        }

        // Reset 0x602780 clears both stop flags (one 16-bit store), resolves the start surface, places the transform at
        // the surface's start with a delta of 0 (so the collision data are told a zero movement) and raises the
        // surface's begin event.
        [Test]
        public void TargetResetRestartsFromTheStartSurface()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                Set(target, "m_startModeType", TransformSurfaceTarget.StartModeType.Index);
                Set(target, "m_startIndex", 1);
                var transform = (Transform)Get(target, "m_transform");
                GameObject child = fixture.Objects.Create("collision data", Vector3.zero, Quaternion.identity, transform);
                child.SetActive(false);
                child.AddComponent<BoxCollider>();
                var data = child.AddComponent<CharacterCollisionData>();
                target.OnAwake(fixture.Follower);
                fixture.Reset();
                TransformSurfaceTargetStop stop = StopOf(target);
                Set(stop, "m_stopRequested", true);
                Set(stop, "m_stopActive", true);
                data.SetMovement(false, new Vector3(9f, 9f, 9f));
                transform.position = new Vector3(5f, 5f, 5f);
                var second = Count(fixture.Surfaces[1]);

                target.Reset(fixture.Follower);

                Assert.That(Get(stop, "m_stopRequested"), Is.False);
                Assert.That(Get(stop, "m_stopActive"), Is.False);
                Near(new Vector3(2f, 0f, 0f), transform.position, "90 degrees");
                Assert.That(second, Is.EqualTo(new[] { 1, 0 }), "the second arc began");
                Assert.That(data.HasMovement, Is.True);
                Assert.That(data.MovementVelocity, Is.EqualTo(Vector3.zero));
            }
        }

        // ResetTargets 0x602630 ends with the virtual InternalUpdate(0) after resetting the follower and each target.
        // Update clears m_ignoreEndEvents at its end, which Reset leaves alone, so the flag shows the final update.
        [Test]
        public void FollowerResetTargetsEndsWithAnUpdateOfZero()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                Call(fixture.Follower, "set_TimeMultiplier", 3f);
                Set(StopOf(target), "m_stopActive", true);
                Set(target, "m_ignoreEndEvents", true);
                Set(target, "m_time", 0.5f);

                fixture.Follower.ResetTargets();

                Near(1f, fixture.Follower.TimeMultiplier);
                Assert.That(Get(StopOf(target), "m_stopActive"), Is.False);
                Assert.That(Get(target, "m_ignoreEndEvents"), Is.False);
                Assert.That(Get(target, "m_time"), Is.EqualTo(0f));
                Near(new Vector3(0f, 0f, 2f), ((Transform)Get(target, "m_transform")).position, "0 degrees");
            }
        }

        // InternalUpdate 0x6020D0 hands every target TimeMultiplier * deltaTime (mulss at 0x602188): 0.25 s at a
        // multiplier of 2 is 45 degrees of the one-second quarter arc, where 0.25 s alone would be 22.5.
        [Test]
        public void FollowerInternalUpdateScalesTheDeltaTimeByTheMultiplier()
        {
            using (var fixture = new Fixture())
            {
                fixture.AddQuarterArcs();
                TransformSurfaceTarget target = fixture.AddTarget();
                fixture.Reset();
                Call(fixture.Follower, "set_TimeMultiplier", 2f);

                Call(fixture.Follower, "InternalUpdate", 0.25f);

                Near(0.5f, (float)Get(target, "m_time"));
                Near(new Vector3(1.4142135f, 0f, 1.4142135f), ((Transform)Get(target, "m_transform")).position, "45 degrees");
            }
        }

        // ---- the line, point, spline and transform descriptions ----

        // Stands in for the collision resolver a Tracker2D is built on: a teleport answers the requested world
        // position with the configured rotation, a local move adds the offset to the current world position,
        // and every call is recorded. Tracker2D itself is the real class.
        private sealed class RecordingResolver : ICollisionResolver2D
        {
            public readonly List<string> Calls = new List<string>();
            public Quaternion Rotation = Quaternion.identity;

            // "kind (x, y, z)" with negative zero printed as zero.
            public static string Row(string kind, Vector3 v) =>
                kind + " (" + Z(v.x) + ", " + Z(v.y) + ", " + Z(v.z) + ")";

            public static string Row(string kind, float x, float y, float z) => Row(kind, new Vector3(x, y, z));

            private static string Z(float value) => (value + 0f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            public Collision2DOutput GetNearestLocationFromWorld(Collision2DInput input, Vector3 worldPosition)
            {
                Calls.Add(Row("teleport", worldPosition));
                SurfaceLocation location = input.m_currentLocation;
                location.m_worldPosition = worldPosition;
                location.m_worldRotation = Rotation;
                return new Collision2DOutput { m_newLocation = location };
            }

            public Collision2DOutput MoveTowardsLocalLocation(Collision2DInput input, Vector3 localOffset)
            {
                Calls.Add(Row("move", localOffset));
                SurfaceLocation location = input.m_currentLocation;
                location.m_worldPosition += localOffset;
                return new Collision2DOutput { m_newLocation = location };
            }

            public RayHit2DOutput GetNearestLocationHitFromWorld(RayHit2DInput input) => throw new NotSupportedException();

            public Collision2DOutput MoveTowardsWorldLocation(Collision2DInput input, Vector3 worldOffset) => throw new NotSupportedException();
        }

        private static void RotationIs(Quaternion expected, Quaternion actual, string label) =>
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThan(0.01f), label + ": expected " + expected + " but was " + actual);

        private static void RotationIsNot(Quaternion unexpected, Quaternion actual, string label) =>
            Assert.That(Quaternion.Angle(unexpected, actual), Is.GreaterThan(1f), label);

        // A surface whose total time is the given one and whose length is the given one, measured the way the
        // follower does (SetExtentsMultiplier measures the surface, then the time).
        private static T Timed<T>(T surface, float distance, float timeTotal) where T : TransformSurfaceDescription
        {
            Set(surface, "m_distance", distance);
            surface.SetFollowMode(TransformSurfaceDescription.FollowModeType.Time, 0f, timeTotal);
            surface.SetExtentsMultiplier(1f);
            return surface;
        }

        [Test]
        public void DeclarationsOfTheLinePointSplineAndTransformDescriptions()
        {
            Assert.That((int)typeof(TransformSurfaceLineDescription).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfaceLineDescription).BaseType, Is.EqualTo(typeof(TransformSurfaceDescription)));
            Assert.That(Fields(typeof(TransformSurfaceLineDescription)), Is.EqualTo(new[]
            {
                "m_lerpOrientationToTarget:Boolean:0001", "m_rotationLocal:Quaternion:0001", "m_rotationLocalStart:Quaternion:0001",
                "m_rotationLocalEnd:Quaternion:0001"
            }));
            Assert.That(Methods(typeof(TransformSurfaceLineDescription)), Is.EqualTo(new[]
            {
                "Initialise:00c6", "Update:00c6", "GetRotation:00c6", "GetRotationLocal:00c6", "SetPosition:00c6", "SetPositionEnd:00c6",
                "CalculateDistance:00c4", ".ctor:1886"
            }));
            Assert.That(AttributeRow(typeof(TransformSurfaceLineDescription).GetField("m_lerpOrientationToTarget", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("TooltipAttribute(Lerps the transform orientation linearly to the target over its length.) SerializeField()"));

            Assert.That((int)typeof(TransformSurfacePointDescription).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfacePointDescription).BaseType, Is.EqualTo(typeof(TransformSurfaceDescription)));
            Assert.That(Fields(typeof(TransformSurfacePointDescription)), Is.EqualTo(new[] { "m_rotationLocal:Quaternion:0001" }));
            Assert.That(Methods(typeof(TransformSurfacePointDescription)), Is.EqualTo(new[]
            {
                "get_Orientate:08c6", "Initialise:00c6", "OnValidate:00c6", "Update:00c6", "GetRotation:00c6", "GetRotationLocal:00c6",
                "SetPosition:00c6", "SetPositionEnd:00c6", "CalculateDistance:00c4", ".ctor:1886"
            }));

            Assert.That((int)typeof(TransformSurfaceSplineDescription).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfaceSplineDescription).BaseType, Is.EqualTo(typeof(TransformSurfaceDescription)));
            Assert.That(Fields(typeof(TransformSurfaceSplineDescription)), Is.EqualTo(new[] { "m_spline:Spline:0001" }));
            Assert.That(Methods(typeof(TransformSurfaceSplineDescription)), Is.EqualTo(new[]
            {
                ".ctor:1886", "Teleport:00c6", "Update:00c6", "GetRotation:00c6", "GetRotationLocal:00c6", "SetPosition:00c6",
                "SetPositionEnd:00c6", "CalculateDistance:00c4", "OnValidateTracker:00c6", "GetSpline:00c6"
            }));
            Assert.That(AttributeRow(typeof(TransformSurfaceSplineDescription).GetField("m_spline", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("SerializeField()"));
            ParameterInfo splineParameter = typeof(TransformSurfaceSplineDescription).GetConstructors()[0].GetParameters()[0];
            Assert.That(splineParameter.IsOptional && splineParameter.HasDefaultValue && splineParameter.DefaultValue == null, Is.True);

            Assert.That((int)typeof(TransformSurfaceTransformDescription).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(TransformSurfaceTransformDescription).BaseType, Is.EqualTo(typeof(TransformSurfaceDescription)));
            Assert.That(Fields(typeof(TransformSurfaceTransformDescription)), Is.EqualTo(new[] { "m_transform:Transform:0001" }));
            Assert.That(Methods(typeof(TransformSurfaceTransformDescription)), Is.EqualTo(new[]
            {
                "OnValidate:00c6", "Update:00c6", "GetRotation:00c6", "GetRotationLocal:00c6", "SetPosition:00c6", "SetPositionEnd:00c6",
                "CalculateDistance:00c4", ".ctor:1886"
            }));
            Assert.That(AttributeRow(typeof(TransformSurfaceTransformDescription).GetField("m_transform", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("TooltipAttribute(Transform to follow.) SerializeField()"));

            foreach (Type type in new[]
                { typeof(TransformSurfaceLineDescription), typeof(TransformSurfacePointDescription), typeof(TransformSurfaceSplineDescription),
                  typeof(TransformSurfaceTransformDescription) })
            {
                foreach (string name in new[] { "Update", "GetRotation" })
                {
                    ParameterInfo[] parameters = type.GetMethod(name).GetParameters();
                    ParameterInfo tracker = parameters[parameters.Length - 1];
                    // The DummyDll row for TransformSurfaceSplineDescription.Update (0x06002ef7) has no default on tracker.
                    if (type == typeof(TransformSurfaceSplineDescription) && name == "Update")
                        Assert.That(tracker.IsOptional || tracker.HasDefaultValue, Is.False, type.Name + "." + name);
                    else
                        Assert.That(tracker.IsOptional && tracker.HasDefaultValue && tracker.DefaultValue == null, Is.True, type.Name + "." + name);
                }
            }
        }

        // ---- the point ----

        // get_Orientate 0x5FFF20 is xor eax, eax: the point never orientates (the base answers true).
        [Test]
        public void PointNeverOrientatesAndHasNoLength()
        {
            var point = new TransformSurfacePointDescription();
            Assert.That(point.Orientate, Is.False);
            Assert.That(new DescriptionProbe().Orientate, Is.True);
            Set(point, "m_distance", 5f);
            Call(point, "CalculateDistance");
            Assert.That(point.Distance, Is.EqualTo(0f));
        }

        // Initialise 0x5FFF30 places the point and measures it like the base, then stores LookRotation(m_position -
        // localPositionPrev, up) at +0x80: the given end is not used (SetPositionEnd 0x6004C0 copies the start).
        [Test]
        public void PointInitialiseLooksFromThePreviousPointAndEndsWhereItStarts()
        {
            using (var fixture = new Fixture(new Vector3(10f, 0f, 0f), Quaternion.identity))
            {
                var point = new TransformSurfacePointDescription();
                Set(point, "m_position", new Vector3(1f, 2f, 3f));

                point.Initialise(fixture.Follower, new Vector3(50f, 60f, 70f), new Vector3(0f, 2f, 3f), Quaternion.identity);

                Near(new Vector3(11f, 2f, 3f), point.PositionStartWorld, "start");
                Near(new Vector3(11f, 2f, 3f), point.PositionEndWorld, "the end is the start");
                Near(new Vector3(1f, 2f, 3f), point.PositionEndLocal, "end in the follower's space");
                Near(0f, point.Distance);
                RotationIs(Quaternion.Euler(0f, 90f, 0f), point.GetRotationLocal(null), "looking along +x");

                point.Initialise(fixture.Follower, Vector3.zero, new Vector3(1f, 2f, 4f), Quaternion.identity);
                RotationIs(Quaternion.Euler(0f, 180f, 0f), point.GetRotationLocal(null), "looking along -z");
            }
        }

        // Update 0x600160 and 0x600F90: the position is the local position through the transform or the stored
        // world position; with a positive total time the delta is added, and past the total time the overshoot
        // is handed back and the time pinned. Without a total time nothing is touched.
        [Test]
        public void PointAndTransformUpdateWaitForTheTotalTime()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-wait"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                foreach (TransformSurfaceDescription surface in new TransformSurfaceDescription[]
                    { new TransformSurfacePointDescription(), new TransformSurfaceTransformDescription() })
                {
                    string name = surface.GetType().Name;
                    Set(surface, "m_position", new Vector3(1f, 0f, 0f));
                    Set(surface, "m_positionWorld", new Vector3(4f, 5f, 6f));

                    float time = 0.5f, delta = 0.25f;
                    Assert.That(surface.Update(out Vector3 position, ref time, ref delta, 1, false, null), Is.False, name + " without a total time");
                    Near(new Vector3(4f, 5f, 6f), position, name + " position");
                    Assert.That(time, Is.EqualTo(0.5f), name);
                    Assert.That(delta, Is.EqualTo(0.25f), name);

                    Timed(surface, 0f, 2f);
                    Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null), Is.True, name);
                    Near(0.75f, time, name + " time");
                    Assert.That(delta, Is.EqualTo(0f), name);
                    Near(new Vector3(4f, 5f, 6f), position, name + " world position");

                    Assert.That(surface.Update(out position, ref time, ref delta, -1, true, transform), Is.True, name);
                    Near(new Vector3(11f, 0f, 0f), position, name + " local position through the transform");

                    // 0x6001cf ucomiss/jae: an unordered compare waits instead of overshooting.
                    time = 0f;
                    delta = float.NaN;
                    Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null), Is.True, name + " NaN delta");
                    Assert.That(time, Is.NaN, name);
                    Assert.That(delta, Is.EqualTo(0f), name);

                    time = 1.9f;
                    delta = 0.3f;
                    Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null), Is.False, name + " past the total time");
                    Near(2f, time, name + " pinned time");
                    Near(0.2f, delta, name + " overshoot");

                    time = 2f;
                    delta = 0f;
                    Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null), Is.False, name + " exactly at the total time");
                    Near(0f, delta, name + " no overshoot");
                }
            }
        }

        // GetRotation 0x600200: transform.rotation * m_rotationLocal (+0x80), then * the inverse rotation (180
        // degrees about up) only for a negative direction while orientating to the direction.
        [Test]
        public void PointRotationMultipliesInOrderAndTurnsRoundOnlyBackwards()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-point-rotation"))
            {
                Quaternion transformRotation = Quaternion.Euler(0f, 0f, 90f);
                Quaternion local = Quaternion.Euler(90f, 0f, 0f);
                Transform transform = objects.Create("transform", Vector3.zero, transformRotation).transform;
                var point = new TransformSurfacePointDescription();
                Set(point, "m_rotationLocal", local);
                Quaternion inverse = Quaternion.AngleAxis(180f, Vector3.up);

                RotationIs(local, point.GetRotationLocal(transform), "local rotation");
                RotationIs(transformRotation * local, point.GetRotation(transform, 1, true), "forwards");
                RotationIsNot(local * transformRotation, point.GetRotation(transform, 1, true), "the product is not reversed");
                RotationIs(transformRotation * local, point.GetRotation(transform, 0, true), "standing still");
                RotationIs(transformRotation * local, point.GetRotation(transform, -1, false), "backwards without orientating");
                RotationIs(transformRotation * local * inverse, point.GetRotation(transform, -1, true), "backwards orientating");
                RotationIsNot(inverse * transformRotation * local, point.GetRotation(transform, -1, true), "the turn is applied last");
            }
        }

        // SetPosition 0x600430 stores the world and local positions like the base and, only when it moved, the
        // start world position; SetPositionEnd 0x6004C0 ignores its position and ends where the start is.
        [Test]
        public void PointSetPositionStoresTheStartAndTheEndFollowsIt()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-point-position"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                var point = new TransformSurfacePointDescription();

                Assert.That(point.SetPosition(new Vector3(12f, 0f, 0f), transform), Is.True);
                Near(new Vector3(12f, 0f, 0f), point.PositionStartWorld, "start");
                Near(new Vector3(2f, 0f, 0f), point.LocalPosition, "local");
                Assert.That(point.SetPosition(new Vector3(12.0005f, 0f, 0f), transform), Is.False, "inside the tolerance");
                Near(new Vector3(12f, 0f, 0f), point.PositionStartWorld, "start kept");

                // The start is stored only when the position moved (0x60046a jumps over the store at 0x600491).
                Set(point, "m_positionStartWorld", Vector3.zero);
                Assert.That(point.SetPosition(new Vector3(12.0005f, 0f, 0f), transform), Is.False);
                Assert.That(point.PositionStartWorld, Is.EqualTo(Vector3.zero), "not stored when it did not move");

                point.SetPositionEnd(new Vector3(99f, 99f, 99f), transform);
                Near(new Vector3(12f, 0f, 0f), point.PositionEndWorld, "end world");
                Near(new Vector3(2f, 0f, 0f), point.PositionEndLocal, "end local");
            }
        }

        // OnValidate 0x600050 and 0x600E80: the base validation (the time is measured from the follow mode as it
        // was), then the follow mode becomes Time with a follow speed of zero; the follow time is untouched.
        [Test]
        public void PointAndTransformOnValidateSwitchToTheTimeFollowMode()
        {
            foreach (TransformSurfaceDescription surface in new TransformSurfaceDescription[]
                { new TransformSurfacePointDescription(), new TransformSurfaceTransformDescription() })
            {
                string name = surface.GetType().Name;
                surface.SetFollowMode(TransformSurfaceDescription.FollowModeType.Speed, 3f, 4f);
                Set(surface, "m_distance", 9f);
                Set(surface, "m_position", new Vector3(0.004f, 1.996f, 3.2f));
                Set(surface, "m_speedStart", 1.003f);

                surface.OnValidate();

                Assert.That(surface.FollowMode, Is.EqualTo(TransformSurfaceDescription.FollowModeType.Time), name);
                Assert.That(surface.FollowSpeed, Is.EqualTo(0f), name);
                Assert.That(surface.FollowTime, Is.EqualTo(4f), name);
                Assert.That(surface.Distance, Is.EqualTo(0f), name + " measured again");
                Near(0f, surface.TimeTotal, name + " time measured before the mode changed (0 / 3)");
                Near(new Vector3(0f, 2f, 3.2f), surface.LocalPosition, name + " snapped position", 1e-5f);
                Near(1f, surface.SpeedStart, name + " snapped speed", 1e-5f);
            }
        }

        // ---- the transform ----

        // GetRotation 0x601030: the followed transform's rotation, else the given transform's; GetRotationLocal
        // 0x6010B0: Inverse(transform.rotation) * followed.rotation, else identity.
        [Test]
        public void TransformRotationsComeFromTheFollowedTransform()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-transform-rotation"))
            {
                Quaternion given = Quaternion.Euler(0f, 0f, 90f);
                Quaternion followed = Quaternion.Euler(90f, 0f, 0f);
                Transform transform = objects.Create("transform", Vector3.zero, given).transform;
                Transform target = objects.Create("target", Vector3.zero, followed).transform;
                var surface = new TransformSurfaceTransformDescription();

                RotationIs(given, surface.GetRotation(transform, -1, true), "without a followed transform");
                RotationIs(Quaternion.identity, surface.GetRotationLocal(transform), "identity without one");

                Set(surface, "m_transform", target);
                RotationIs(followed, surface.GetRotation(transform, -1, true), "the followed transform's, whatever the direction");
                RotationIs(Quaternion.Inverse(given) * followed, surface.GetRotationLocal(transform), "relative to the given transform");
                RotationIsNot(followed * Quaternion.Inverse(given), surface.GetRotationLocal(transform), "the product is not reversed");
            }
        }

        // SetPosition 0x601240 takes the followed transform's position over the given one and stores it as the
        // start before the tolerance check, so even a position that did not move updates the start.
        [Test]
        public void TransformSetPositionFollowsTheTransformAndAlwaysStoresTheStart()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-transform-position"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                Transform target = objects.Create("target", new Vector3(3f, 4f, 5f), Quaternion.identity).transform;
                var surface = new TransformSurfaceTransformDescription();

                Assert.That(surface.SetPosition(new Vector3(1f, 1f, 1f), transform), Is.True, "without a followed transform");
                Near(new Vector3(1f, 1f, 1f), surface.PositionStartWorld, "given position");
                Near(new Vector3(-9f, 1f, 1f), surface.LocalPosition, "given local position");

                Set(surface, "m_transform", target);
                Assert.That(surface.SetPosition(new Vector3(1f, 1f, 1f), transform), Is.True);
                Near(new Vector3(3f, 4f, 5f), surface.PositionStartWorld, "followed start");
                Near(new Vector3(3f, 4f, 5f), surface.PositionWorld, "followed world position");
                Near(new Vector3(-7f, 4f, 5f), surface.LocalPosition, "followed local position");

                Set(surface, "m_positionStartWorld", Vector3.zero);
                Assert.That(surface.SetPosition(new Vector3(8f, 8f, 8f), transform), Is.False, "it did not move");
                Near(new Vector3(3f, 4f, 5f), surface.PositionStartWorld, "the start is stored anyway");

                surface.SetPositionEnd(new Vector3(99f, 99f, 99f), transform);
                Near(new Vector3(3f, 4f, 5f), surface.PositionEndWorld, "end world");
                Near(new Vector3(-7f, 4f, 5f), surface.PositionEndLocal, "end local");
                Set(surface, "m_distance", 3f);
                Call(surface, "CalculateDistance");
                Assert.That(surface.Distance, Is.EqualTo(0f));
            }
        }

        // ---- the line ----

        // Initialise 0x5FF7B0 places and measures the line like the base; when the length is not zero the start
        // rotation is LookRotation(endLocal - position, up) and the end rotation is the one given.
        [Test]
        public void LineInitialiseStoresTheLookRotationsOnlyForALineWithALength()
        {
            using (var fixture = new Fixture(new Vector3(10f, 0f, 0f), Quaternion.identity))
            {
                var line = new TransformSurfaceLineDescription();
                Quaternion end = Quaternion.Euler(0f, 0f, 45f);

                line.Initialise(fixture.Follower, new Vector3(4f, 0f, 0f), Vector3.zero, end);

                Near(new Vector3(10f, 0f, 0f), line.PositionStartWorld, "start");
                Near(new Vector3(14f, 0f, 0f), line.PositionEndWorld, "end");
                Near(new Vector3(4f, 0f, 0f), line.PositionEndLocal, "end local");
                Near(4f, line.Distance);
                Near(4f, line.TimeTotal, "follow speed 1");
                RotationIs(Quaternion.Euler(0f, 90f, 0f), (Quaternion)Get(line, "m_rotationLocalStart"), "looking along the line");
                RotationIs(end, (Quaternion)Get(line, "m_rotationLocalEnd"), "end rotation");

                var point = new TransformSurfaceLineDescription();
                point.Initialise(fixture.Follower, Vector3.zero, Vector3.zero, end);
                Near(0f, point.Distance);
                foreach (string name in new[] { "m_rotationLocalStart", "m_rotationLocalEnd" })
                {
                    var stored = (Quaternion)Get(point, name);
                    Assert.That(stored.x == 0f && stored.y == 0f && stored.z == 0f && stored.w == 0f, Is.True, name + " left alone without a length");
                }
            }
        }

        // The start rotation looks along the end minus the position in the follower's space (0x5ff865..0x5ff8c7), not
        // along the world delta.
        [Test]
        public void LineInitialiseLooksAlongTheLocalDeltaOfARotatedFollower()
        {
            using (var fixture = new Fixture(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)))
            {
                var line = new TransformSurfaceLineDescription();

                line.Initialise(fixture.Follower, new Vector3(4f, 0f, 0f), Vector3.zero, Quaternion.identity);

                RotationIs(Quaternion.Euler(0f, 90f, 0f), (Quaternion)Get(line, "m_rotationLocalStart"), "local +x, not the world delta (10,0,-4)");
            }
        }

        // CalculateDistance 0x5FFDD0 is the distance between the start and end world positions; SetPositionEnd
        // 0x5FFDA0 stores the given end, SetPosition 0x5FFD10 also the start when it moved.
        [Test]
        public void LineDistanceAndPositionsFollowTheStartAndEnd()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-line-position"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                var line = new TransformSurfaceLineDescription();

                Assert.That(line.SetPosition(new Vector3(12f, 0f, 0f), transform), Is.True);
                Near(new Vector3(12f, 0f, 0f), line.PositionStartWorld, "start");
                Assert.That(line.SetPosition(new Vector3(12f, 0f, 0f), transform), Is.False);
                // The start is stored only when the position moved (0x5ffd4a jumps over the store at 0x5ffd71).
                Set(line, "m_positionStartWorld", Vector3.zero);
                Assert.That(line.SetPosition(new Vector3(12.0005f, 0f, 0f), transform), Is.False);
                Assert.That(line.PositionStartWorld, Is.EqualTo(Vector3.zero), "not stored when it did not move");
                line.SetPositionEnd(new Vector3(15f, 4f, 0f), transform);
                Near(new Vector3(15f, 4f, 0f), line.PositionEndWorld, "end world");
                Near(new Vector3(5f, 4f, 0f), line.PositionEndLocal, "end local");
                Call(line, "CalculateDistance");
                Near(5f, line.Distance, "3-4-5");
            }
        }

        // Update 0x5FF8F0: the position is a clamped lerp from the start to the end by the progress (the end
        // for a negative direction counts the time back from the far end); past the total time the position is
        // the start for a negative direction and the end otherwise, with the overshoot handed back.
        [Test]
        public void LineUpdateInterpolatesAndHoldsTheEndItRanTo()
        {
            var line = Timed(new TransformSurfaceLineDescription(), 0f, 4f);
            Set(line, "m_positionStartWorld", Vector3.zero);
            Set(line, "m_positionEndWorld", new Vector3(8f, 0f, 0f));
            Set(line, "m_positionWorld", new Vector3(1f, 2f, 3f));
            line.SetExtentsMultiplier(1f);
            Near(8f, line.Distance, "measured from the start and end");

            float time = 0f, delta = 1f;
            Assert.That(line.Update(out Vector3 position, ref time, ref delta, 1, false, null), Is.True);
            Near(new Vector3(2f, 0f, 0f), position, "a quarter of the way");
            Near(1f, time);
            Near(0f, delta);

            time = 0f;
            delta = 1f;
            Assert.That(line.Update(out position, ref time, ref delta, -1, false, null), Is.True);
            Near(new Vector3(6f, 0f, 0f), position, "backwards counts from the far end");

            time = 3.5f;
            delta = 1f;
            Assert.That(line.Update(out position, ref time, ref delta, 1, false, null), Is.False);
            Near(new Vector3(8f, 0f, 0f), position, "the end");
            Near(4f, time, "pinned");
            Near(0.5f, delta, "overshoot");

            time = 3.5f;
            delta = 1f;
            Assert.That(line.Update(out position, ref time, ref delta, -1, false, null), Is.False);
            Near(Vector3.zero, position, "the start going backwards");

            time = 3.5f;
            delta = 1f;
            Assert.That(line.Update(out position, ref time, ref delta, 0, false, null), Is.False);
            Near(new Vector3(8f, 0f, 0f), position, "the end when standing still");

            var idle = new TransformSurfaceLineDescription();
            time = 1f;
            delta = 1f;
            Assert.That(idle.Update(out position, ref time, ref delta, 1, false, null), Is.False, "no total time");
            Near(1f, time);
            Near(1f, delta);

            // A NaN time fails the unordered compare (0x5ff9c3 jb with a transform, 0x5ffa25 jae without), so the line
            // counts as still moving: it keeps the NaN time and zeroes the delta (0x5ffa27).
            var timed = Timed(new TransformSurfaceLineDescription(), 0f, 4f);
            Set(timed, "m_positionEndWorld", new Vector3(8f, 0f, 0f));
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-line-nan"))
            {
                Transform transform = objects.Create("transform", Vector3.zero, Quaternion.identity).transform;
                foreach (bool usesTransform in new[] { false, true })
                {
                    time = float.NaN;
                    delta = 1f;
                    Assert.That(timed.Update(out position, ref time, ref delta, 1, usesTransform, transform), Is.True, "NaN time, transform " + usesTransform);
                    Assert.That(float.IsNaN(time), Is.True, "time kept, transform " + usesTransform);
                    Near(0f, delta, "delta zeroed, transform " + usesTransform);
                }
            }
        }

        // With a transform the line runs from the transform of m_position to the transform of the end in the
        // follower's space, not from the stored world positions.
        [Test]
        public void LineUpdateThroughATransformUsesTheLocalPositions()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-line-transform"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                var line = Timed(new TransformSurfaceLineDescription(), 0f, 4f);
                Set(line, "m_position", Vector3.zero);
                Set(line, "m_positionEndLocal", new Vector3(8f, 0f, 0f));
                Set(line, "m_positionStartWorld", new Vector3(100f, 0f, 0f));
                Set(line, "m_positionEndWorld", new Vector3(200f, 0f, 0f));

                float time = 0f, delta = 1f;
                Assert.That(line.Update(out Vector3 position, ref time, ref delta, 1, true, transform), Is.True);
                Near(new Vector3(12f, 0f, 0f), position);

                time = 3.5f;
                delta = 1f;
                Assert.That(line.Update(out position, ref time, ref delta, 1, true, transform), Is.False);
                Near(new Vector3(18f, 0f, 0f), position, "end through the transform");
                time = 3.5f;
                delta = 1f;
                Assert.That(line.Update(out position, ref time, ref delta, -1, true, transform), Is.False);
                Near(new Vector3(10f, 0f, 0f), position, "start through the transform");
            }
        }

        // The progress is clamped to 0..1 for the position (Vector3.Lerp); the line's rotation is the start
        // rotation, or with the lerp flag Quaternion.Lerp (which clamps) of the start and end by the raw progress. Past the total time
        // the rotation is left alone.
        [Test]
        public void LineUpdateClampsTheProgressAndLerpsTheRotationOnRequest()
        {
            Quaternion start = Quaternion.Euler(0f, 90f, 0f);
            Quaternion end = Quaternion.Euler(0f, 0f, 90f);
            var line = new TransformSurfaceLineDescription();
            line.SetFollowMode(TransformSurfaceDescription.FollowModeType.ProgressAnimated);
            Set(line, "m_followAnim", AnimationCurve.Linear(0f, -1f, 4f, 2f));
            Set(line, "m_positionEndWorld", new Vector3(8f, 0f, 0f));
            Set(line, "m_rotationLocalStart", start);
            Set(line, "m_rotationLocalEnd", end);
            line.SetExtentsMultiplier(1f);
            Near(4f, line.TimeTotal, "last key time");

            float time = 0f, delta = 0.5f;
            Assert.That(line.Update(out Vector3 position, ref time, ref delta, 1, false, null), Is.True);
            Near(Vector3.zero, position, "progress -0.625 is clamped to the start");
            RotationIs(start, line.GetRotationLocal(null), "start rotation without the lerp flag");

            time = 2.5f;
            delta = 0.5f;
            Assert.That(line.Update(out position, ref time, ref delta, 1, false, null), Is.True);
            Near(new Vector3(8f, 0f, 0f), position, "progress 1.25 is clamped to the end");

            Set(line, "m_lerpOrientationToTarget", true);
            time = 2f;
            delta = 0f;
            Assert.That(line.Update(out position, ref time, ref delta, 1, false, null), Is.True);
            Near(new Vector3(4f, 0f, 0f), position, "progress 0.5");
            RotationIs(Quaternion.Lerp(start, end, 0.5f), line.GetRotationLocal(null), "halfway between the rotations");
            RotationIsNot(start, line.GetRotationLocal(null), "no longer the start rotation");

            time = 2.5f;
            delta = 0.5f;
            Assert.That(line.Update(out position, ref time, ref delta, 1, false, null), Is.True);
            RotationIs(end, line.GetRotationLocal(null), "progress 1.25 is clamped by Quaternion.Lerp (0x5ffac0), not extrapolated");

            Set(line, "m_rotationLocal", start);
            time = 3.5f;
            delta = 1f;
            Assert.That(line.Update(out position, ref time, ref delta, 1, false, null), Is.False);
            RotationIs(start, line.GetRotationLocal(null), "left alone past the total time");
        }

        // GetRotation 0x5FFAE0 multiplies the transform's rotation by m_rotationLocal (+0x84) and turns round
        // only for a negative direction while orientating to the direction.
        [Test]
        public void LineRotationMultipliesInOrderAndTurnsRoundOnlyBackwards()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-line-rotation"))
            {
                Quaternion transformRotation = Quaternion.Euler(0f, 0f, 90f);
                Quaternion local = Quaternion.Euler(90f, 0f, 0f);
                Transform transform = objects.Create("transform", Vector3.zero, transformRotation).transform;
                var line = new TransformSurfaceLineDescription();
                Set(line, "m_rotationLocal", local);
                Quaternion inverse = Quaternion.AngleAxis(180f, Vector3.up);

                RotationIs(transformRotation * local, line.GetRotation(transform, 1, true), "forwards");
                RotationIsNot(local * transformRotation, line.GetRotation(transform, 1, true), "the product is not reversed");
                RotationIs(transformRotation * local, line.GetRotation(transform, -1, false), "backwards without orientating");
                RotationIs(transformRotation * local * inverse, line.GetRotation(transform, -1, true), "backwards orientating");
                RotationIs(local, line.GetRotationLocal(transform), "local rotation");
            }
        }

        // ---- the spline ----

        // A Spline facade over an InertSpline of the given length, under an inactive owner so nothing wakes.
        private static Spline NewSpline(OwnedWorldObjects objects, float length, Quaternion rotation)
        {
            GameObject host = objects.Create("spline", Vector3.zero, rotation);
            host.SetActive(false);
            var inert = host.AddComponent<InertSpline>();
            Set(inert, "m_length", length);
            var facade = host.AddComponent<Spline>();
            Set(facade, "m_inertSpline", inert);
            Set(facade, "m_activeSplineType", SplineVariantType.Inert);
            return facade;
        }

        // .ctor 0x600560 stores the spline; GetSpline 0x600E70 returns it.
        [Test]
        public void SplineConstructorStoresTheSplineAndGetSplineReturnsIt()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-spline-ctor"))
            {
                Spline spline = NewSpline(objects, 5f, Quaternion.identity);
                Assert.That(new TransformSurfaceSplineDescription(spline).GetSpline(), Is.SameAs(spline));
                Assert.That(new TransformSurfaceSplineDescription().GetSpline(), Is.Null);
                Assert.That(new DescriptionProbe().GetSpline(), Is.Null, "the base answers null");
            }
        }

        // CalculateDistance 0x600C60 takes the length of the spline's surface; without a spline, or without an
        // active spline behind the facade, the distance is left as it was.
        [Test]
        public void SplineDistanceIsTheLengthOfTheSplineOrKeepsItsValue()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-spline-distance"))
            {
                var surface = new TransformSurfaceSplineDescription(NewSpline(objects, 12.5f, Quaternion.identity));
                Set(surface, "m_distance", 7f);
                Call(surface, "CalculateDistance");
                Near(12.5f, surface.Distance, "the spline's length");

                var without = new TransformSurfaceSplineDescription();
                Set(without, "m_distance", 7f);
                Call(without, "CalculateDistance");
                Near(7f, without.Distance, "no spline");

                Spline empty = NewSpline(objects, 3f, Quaternion.identity);
                Set(empty, "m_inertSpline", null);
                var hollow = new TransformSurfaceSplineDescription(empty);
                Set(hollow, "m_distance", 7f);
                Call(hollow, "CalculateDistance");
                Near(7f, hollow.Distance, "the facade has no spline");
            }
        }

        // GetRotationLocal 0x600A00: Inverse(transform.rotation) * spline.transform.rotation, else identity.
        [Test]
        public void SplineRotationLocalIsRelativeToTheGivenTransform()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-spline-rotation"))
            {
                Quaternion given = Quaternion.Euler(0f, 0f, 90f);
                Quaternion splineRotation = Quaternion.Euler(90f, 0f, 0f);
                Transform transform = objects.Create("transform", Vector3.zero, given).transform;
                var surface = new TransformSurfaceSplineDescription(NewSpline(objects, 1f, splineRotation));

                RotationIs(Quaternion.Inverse(given) * splineRotation, surface.GetRotationLocal(transform), "relative");
                RotationIsNot(splineRotation * Quaternion.Inverse(given), surface.GetRotationLocal(transform), "the product is not reversed");
                RotationIs(Quaternion.identity, new TransformSurfaceSplineDescription().GetRotationLocal(transform), "identity without a spline");
            }
        }

        // GetRotation 0x600890: the tracker's world rotation (the transform is not touched) or the transform's,
        // then turned round for a negative direction while orientating.
        [Test]
        public void SplineRotationComesFromTheTrackerOrTheTransform()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-spline-tracker-rotation"))
            {
                Quaternion trackerRotation = Quaternion.Euler(90f, 0f, 0f);
                Quaternion transformRotation = Quaternion.Euler(0f, 0f, 90f);
                Quaternion inverse = Quaternion.AngleAxis(180f, Vector3.up);
                Transform transform = objects.Create("transform", Vector3.zero, transformRotation).transform;
                var resolver = new RecordingResolver { Rotation = trackerRotation };
                var tracker = new Tracker2D(resolver);
                tracker.Teleport(Vector3.zero);
                var surface = new TransformSurfaceSplineDescription();

                RotationIs(trackerRotation, surface.GetRotation(null, 1, true, tracker), "tracker forwards");
                RotationIs(trackerRotation * inverse, surface.GetRotation(null, -1, true, tracker), "tracker backwards orientating");
                RotationIsNot(inverse * trackerRotation, surface.GetRotation(null, -1, true, tracker), "the turn is applied last");
                RotationIs(trackerRotation, surface.GetRotation(null, -1, false, tracker), "tracker backwards without orientating");
                RotationIs(transformRotation, surface.GetRotation(transform, 0, true), "transform");
                RotationIs(transformRotation * inverse, surface.GetRotation(transform, -1, true), "transform backwards orientating");
            }
        }

        // Teleport 0x6005D0 without a tracker is the base answer; with one, the tracker is teleported there and
        // moved forward by distance * -direction, and its world position is the answer.
        [Test]
        public void SplineTeleportPlacesTheTrackerAndMovesItAgainstTheDirection()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-spline-teleport"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                var surface = new TransformSurfaceSplineDescription();
                Set(surface, "m_position", new Vector3(1f, 0f, 0f));
                Set(surface, "m_positionEndLocal", new Vector3(0f, 2f, 0f));
                Set(surface, "m_positionStartWorld", new Vector3(1f, 2f, 3f));
                Set(surface, "m_positionEndWorld", new Vector3(5f, 5f, 5f));
                Set(surface, "m_distance", 6f);

                Near(new Vector3(1f, 2f, 3f), surface.Teleport(1, false, null), "start");
                Near(new Vector3(5f, 5f, 5f), surface.Teleport(-1, false, null), "end");
                Near(new Vector3(11f, 0f, 0f), surface.Teleport(1, true, transform), "start through the transform");
                Near(new Vector3(10f, 2f, 0f), surface.Teleport(0, true, transform), "end through the transform");

                var resolver = new RecordingResolver();
                var tracker = new Tracker2D(resolver);
                Near(new Vector3(1f, 2f, -3f), surface.Teleport(1, false, null, tracker), "forwards");
                Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("teleport", 1.0f, 2.0f, 3.0f), RecordingResolver.Row("move", 0.0f, 0.0f, -6.0f) }));

                resolver.Calls.Clear();
                Near(new Vector3(5f, 5f, 11f), surface.Teleport(-1, false, null, tracker), "backwards");
                Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("teleport", 5.0f, 5.0f, 5.0f), RecordingResolver.Row("move", 0.0f, 0.0f, 6.0f) }));

                resolver.Calls.Clear();
                Near(new Vector3(10f, 2f, 0f), surface.Teleport(0, true, transform, tracker), "standing still through the transform");
                Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("teleport", 10.0f, 2.0f, 0.0f), RecordingResolver.Row("move", 0.0f, 0.0f, 0.0f) }));
            }
        }

        // Update 0x6006E0 moves the tracker forward by direction * distance * deltaTime / totalTime (the whole
        // delta, even when it runs past the end) and reports its world position; without a tracker the position
        // is the surface's. The time handling is the point's.
        [Test]
        public void SplineUpdateMovesTheTrackerByTheShareOfTheLengthCovered()
        {
            var surface = Timed(new TransformSurfaceSplineDescription(), 8f, 4f);
            Set(surface, "m_positionWorld", new Vector3(4f, 5f, 6f));
            var resolver = new RecordingResolver();
            var tracker = new Tracker2D(resolver);

            float time = 0f, delta = 0.5f;
            Assert.That(surface.Update(out Vector3 position, ref time, ref delta, 1, false, null, tracker), Is.True);
            Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("move", 0.0f, 0.0f, 1.0f) }));
            Near(new Vector3(0f, 0f, 1f), position, "the tracker's position");
            Near(0.5f, time);
            Near(0f, delta);

            resolver.Calls.Clear();
            delta = 1f;
            Assert.That(surface.Update(out position, ref time, ref delta, -1, false, null, tracker), Is.True);
            Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("move", 0.0f, 0.0f, -2.0f) }));
            Near(new Vector3(0f, 0f, -1f), position);

            resolver.Calls.Clear();
            time = 3.5f;
            delta = 1f;
            Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null, tracker), Is.False);
            Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("move", 0.0f, 0.0f, 2.0f) }), "the whole delta is used");
            Near(4f, time, "pinned");
            Near(0.5f, delta, "overshoot");

            // 0x600850 ucomiss/jae: an unordered compare waits instead of overshooting.
            resolver.Calls.Clear();
            time = 0f;
            delta = float.NaN;
            Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null, tracker), Is.True, "NaN delta with a tracker");
            Assert.That(time, Is.NaN);
            Assert.That(delta, Is.EqualTo(0f));
            time = 0f;
            delta = float.NaN;
            Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null, null), Is.True, "NaN delta without a tracker");
            Assert.That(delta, Is.EqualTo(0f));

            resolver.Calls.Clear();
            time = 0f;
            delta = 1f;
            Assert.That(surface.Update(out position, ref time, ref delta, 1, false, null, null), Is.True, "without a tracker");
            Near(new Vector3(4f, 5f, 6f), position, "the surface's position");
            Assert.That(resolver.Calls, Is.Empty);

            var idle = new TransformSurfaceSplineDescription();
            time = 1f;
            delta = 1f;
            Assert.That(idle.Update(out position, ref time, ref delta, 1, false, null, tracker), Is.False, "no total time");
            Assert.That(resolver.Calls, Is.Empty, "the tracker is not moved");
            Near(1f, time);
            Near(1f, delta);
        }

        // OnValidateTracker 0x600D60: the tracker is teleported to the start, moved a whole length forward, the
        // location reached becomes the end world position, and the tracker goes back to the start.
        [Test]
        public void SplineOnValidateTrackerMeasuresTheEndAndRewinds()
        {
            var surface = new TransformSurfaceSplineDescription();
            Set(surface, "m_positionStartWorld", new Vector3(1f, 2f, 3f));
            Set(surface, "m_distance", 6f);
            surface.OnValidateTracker(null);
            Near(Vector3.zero, surface.PositionEndWorld, "nothing happens without a tracker");

            var resolver = new RecordingResolver();
            var tracker = new Tracker2D(resolver);
            surface.OnValidateTracker(tracker);

            Assert.That(resolver.Calls, Is.EqualTo(new[] { RecordingResolver.Row("teleport", 1.0f, 2.0f, 3.0f), RecordingResolver.Row("move", 0.0f, 0.0f, 6.0f), RecordingResolver.Row("teleport", 1.0f, 2.0f, 3.0f) }));
            Near(new Vector3(1f, 2f, 9f), surface.PositionEndWorld, "end");
            Near(new Vector3(1f, 2f, 3f), tracker.Location.m_worldPosition, "the tracker is back at the start");
        }

        // SetPosition 0x600BA0 and SetPositionEnd 0x600C30 are the line's.
        [Test]
        public void SplineSetPositionAndSetPositionEndStoreTheGivenPositions()
        {
            using (var objects = new OwnedWorldObjects("lucid-transform-surface-spline-position"))
            {
                Transform transform = objects.Create("transform", new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                var surface = new TransformSurfaceSplineDescription();

                Assert.That(surface.SetPosition(new Vector3(12f, 0f, 0f), transform), Is.True);
                Near(new Vector3(12f, 0f, 0f), surface.PositionStartWorld, "start");
                Near(new Vector3(2f, 0f, 0f), surface.LocalPosition, "local");
                Assert.That(surface.SetPosition(new Vector3(12f, 0f, 0f), transform), Is.False);
                // The start is stored only when the position moved (the jump at 0x600bda skips the store).
                Set(surface, "m_positionStartWorld", Vector3.zero);
                Assert.That(surface.SetPosition(new Vector3(12.0005f, 0f, 0f), transform), Is.False);
                Assert.That(surface.PositionStartWorld, Is.EqualTo(Vector3.zero), "not stored when it did not move");
                surface.SetPositionEnd(new Vector3(15f, 4f, 0f), transform);
                Near(new Vector3(15f, 4f, 0f), surface.PositionEndWorld, "end world");
                Near(new Vector3(5f, 4f, 0f), surface.PositionEndLocal, "end local");
            }
        }
    }
}
