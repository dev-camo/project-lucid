using System;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.CinemachineParametersBlenderSource (TypeDef 0x0200027c, x86-64 0x71F4F0..0x71F680) and its
    // CameraInput, IsGrounded, LateralMovement, Speed and TimeSinceLive descendants (0x71F680..0x71FD40). The base
    // is exercised through a probe of the CharCore runtime test assembly; every source is a real time-scaled
    // component on an owned inactive host (Edit Mode runs no Awake or OnEnable), driven through its public
    // OnUpdate. The character manager is built by its real constructor and registered; Start (the message that
    // adopts it) is run as Unity would run it. Smoothing expectations come from the Mathf.SmoothDamp recurrence
    // (omega = 2 / smoothTime): from rest at zero towards one with smoothTime 1 and dt 0.1 the value is
    // 0.0172634, the velocity 0.3275789, and the second step lands at 0.0610558.
    public sealed class CinemachineParametersBlenderSourceTests
    {
        private const float Tolerance = 1e-4f;

        private static void WithSource<T>(Action<Owned, CharacterManager, Character, T> body, bool cameraRows = false)
            where T : CinemachineParametersBlenderSource =>
            WithManager((owned, manager, character) =>
            {
                owned.LeaseRow<TimeManager>();
                if (cameraRows)
                {
                    owned.LeaseRow<CinemachineCameraManager>();
                    owned.LeaseRow<CameraStackManager>();
                }
                OwnedTimeScaledObjects time = owned.Lease(new OwnedTimeScaledObjects("lucid-source"));
                body(owned, manager, character, AddSource<T>(time, "source"));
            });

        private static T AddSource<T>(OwnedTimeScaledObjects time, string name, GameObject host = null) where T : Component
        {
            host = host != null ? host : time.Objects.Create(name);
            host.SetActive(false);
            return host.AddComponent<T>();
        }

        // The sources read the manager's current character only through TryGetCurrentCharacter.
        private static void Start(CinemachineParametersBlenderSource source, CharacterManager manager, Character character, bool valid = true)
        {
            SetCurrent(manager, character, valid);
            Call(source, "Start");
        }

        private static void SetCharacterProperty(Character character, string name, object value) =>
            typeof(Character).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(character, new[] { value });

        private static void Seed(CinemachineParametersBlenderSource source, float blendValue) =>
            Call(source, "UpdateBlendValue", blendValue, 0.1f);

        // ---- CinemachineParametersBlenderSource (0x71F4F0..0x71F680) ----

        [Test]
        public void ASourceWithoutSmoothingTakesTheTargetValue()
        {
            WithSource<CinemachineParametersBlenderSourceProbe>((owned, manager, character, source) =>
            {
                Assert.That(source.BlendValue, Is.EqualTo(0f));
                source.RunUpdateBlendValue(0.8f, 0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.8f));
                source.RunUpdateBlendValue(-2f, 0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(-2f));
            });
        }

        [Test]
        public void PositiveSmoothingDampsTowardsTheTargetKeepingItsVelocityAcrossCalls()
        {
            WithSource<CinemachineParametersBlenderSourceProbe>((owned, manager, character, source) =>
            {
                Set(source, "m_smoothingDampTimeSeconds", 1f);
                source.RunUpdateBlendValue(1f, 0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.0172634f).Within(Tolerance));
                Assert.That(Get<float>(source, "m_blendVelocity"), Is.EqualTo(0.3275789f).Within(Tolerance));
                source.RunUpdateBlendValue(1f, 0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.0610558f).Within(Tolerance));
            });
        }

        // 0x71F600 dampens only for a smoothing time that compares greater than zero.
        [Test]
        public void ANonPositiveSmoothingTimeTakesTheTargetValue()
        {
            WithSource<CinemachineParametersBlenderSourceProbe>((owned, manager, character, source) =>
            {
                Set(source, "m_smoothingDampTimeSeconds", -1f);
                source.RunUpdateBlendValue(0.6f, 0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.6f));
            });
        }

        [Test]
        public void StartAdoptsTheRegisteredCharacterManager()
        {
            WithSource<CinemachineParametersBlenderSourceProbe>((owned, manager, character, source) =>
            {
                Assert.That(source.Manager, Is.Null);
                Call(source, "Start");
                Assert.That(source.Manager, Is.SameAs(manager));
            });
        }

        [Test]
        public void TheBaseInternalUpdateLeavesTheBlendValueAlone()
        {
            WithSource<CinemachineParametersBlenderSourceProbe>((owned, manager, character, source) =>
            {
                source.RunUpdateBlendValue(0.4f, 0.1f);
                source.RunInternalUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.4f));
            });
        }

        // ---- CinemachineParametersBlenderSource_LateralMovement (0x71F950) ----

        // 0x71F97C picks +0x290 (the raw controller movement) when the flag is set and +0x2a8 otherwise, and
        // reads only the x component of either.
        [Test]
        public void LateralMovementReadsTheHorizontalComponentOfTheRawOrProcessedMovement()
        {
            WithSource<CinemachineParametersBlenderSource_LateralMovement>((owned, manager, character, source) =>
            {
                Start(source, manager, character);
                SetCharacterProperty(character, "ControllerMovement", new Vector2(0.25f, 0.5f));
                SetCharacterProperty(character, "RawControllerMovement", new Vector2(-0.75f, 0.1f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.25f));
                Set(source, "m_useRawControllerInput", true);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(-0.75f));
            });
        }

        [Test]
        public void LateralMovementWithoutACurrentCharacterLeavesTheBlendValue()
        {
            WithSource<CinemachineParametersBlenderSource_LateralMovement>((owned, manager, character, source) =>
            {
                Start(source, manager, character, false);
                Seed(source, 0.9f);
                SetCharacterProperty(character, "ControllerMovement", new Vector2(0.25f, 0.5f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.9f));
            });
        }

        // ---- CinemachineParametersBlenderSource_Speed (0x71F9E0) ----

        // The local velocity is scaled per axis; the magnitude (or, with the absolute flag, the plain sum of the
        // scaled components) is divided by the maximum speed and clamped to [-1, 1] (0x29a6be0, 0x29a6df0).
        [Test]
        public void SpeedNormalisesTheScaledLocalVelocityByTheMaximumSpeed()
        {
            WithSource<CinemachineParametersBlenderSource_Speed>((owned, manager, character, source) =>
            {
                Start(source, manager, character);
                SetActorProperty(character, "LocalVelocity", new Vector3(3f, 4f, 0f));
                Set(source, "m_maxSpeed", 10f);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.5f).Within(Tolerance), "magnitude 5");
                Set(source, "m_useAbsoluteValues", true);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.7f).Within(Tolerance), "sum 7");
                Set(source, "m_velocityAxisMultiplier", new Vector3(1f, 0f, 1f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.3f).Within(Tolerance), "only the x component remains");
                Set(source, "m_useAbsoluteValues", false);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.3f).Within(Tolerance), "magnitude 3");

                // 0x71FA19 and 0x71FA26 read the z component and scale it by the multiplier's z.
                SetActorProperty(character, "LocalVelocity", new Vector3(1f, 2f, 2f));
                Set(source, "m_velocityAxisMultiplier", new Vector3(1f, 1f, 0.5f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.244949f).Within(Tolerance), "magnitude sqrt(6)");
                Set(source, "m_useAbsoluteValues", true);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.4f).Within(Tolerance), "sum 4");
            });
        }

        [Test]
        public void SpeedClampsTheNormalisedValueToPlusOrMinusOne()
        {
            WithSource<CinemachineParametersBlenderSource_Speed>((owned, manager, character, source) =>
            {
                Start(source, manager, character);
                Set(source, "m_maxSpeed", 2f);
                SetActorProperty(character, "LocalVelocity", new Vector3(3f, 4f, 0f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(1f), "5 / 2");
                Set(source, "m_useAbsoluteValues", true);
                SetActorProperty(character, "LocalVelocity", new Vector3(-3f, -4f, 0f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(-1f), "-7 / 2");
            });
        }

        [Test]
        public void SpeedDefaultsToUnitAxisMultipliers()
        {
            WithSource<CinemachineParametersBlenderSource_Speed>((owned, manager, character, source) =>
            {
                Assert.That(Get<Vector3>(source, "m_velocityAxisMultiplier"), Is.EqualTo(Vector3.one));
            });
        }

        [Test]
        public void SpeedWithoutACurrentCharacterLeavesTheBlendValue()
        {
            WithSource<CinemachineParametersBlenderSource_Speed>((owned, manager, character, source) =>
            {
                Start(source, manager, character, false);
                Seed(source, 0.9f);
                Set(source, "m_maxSpeed", 10f);
                SetActorProperty(character, "LocalVelocity", new Vector3(3f, 4f, 0f));
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.9f));
            });
        }

        // ---- CinemachineParametersBlenderSource_IsGrounded (0x71F840) ----

        // A tracking that reports a scripted ground contact (IsOnGround is vtable slot 5, 0x71F881).
        private sealed class GroundedTracking : CharacterTracking
        {
            public bool OnGround;
            public GroundedTracking(Character character) : base(character) { }
            public override CharacterTrackingType Type => CharacterTrackingType.ColliderFree;
            public override bool IsOnGround() => OnGround;
            public override bool WasOnGround() => throw new NotSupportedException();
            public override Vector3 GetContactNormal() => throw new NotSupportedException();
            public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime) => throw new NotSupportedException();
            public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime) => throw new NotSupportedException();
            public override void OrientateToGround() => throw new NotSupportedException();
            public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime, out Vector3 inputForward, out float inputTurn) =>
                throw new NotSupportedException();
        }

        [Test]
        public void IsGroundedBlendsToOneOnTheGroundAndZeroInTheAir()
        {
            WithSource<CinemachineParametersBlenderSource_IsGrounded>((owned, manager, character, source) =>
            {
                Start(source, manager, character);
                var tracking = new GroundedTracking(character) { OnGround = true };
                SetCharacterProperty(character, "ActiveTracking", tracking);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(1f));
                tracking.OnGround = false;
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0f));
            });
        }

        [Test]
        public void IsGroundedWithoutACurrentCharacterLeavesTheBlendValue()
        {
            WithSource<CinemachineParametersBlenderSource_IsGrounded>((owned, manager, character, source) =>
            {
                Start(source, manager, character, false);
                Seed(source, 0.9f);
                SetCharacterProperty(character, "ActiveTracking", new GroundedTracking(character) { OnGround = true });
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.9f));
            });
        }

        // ---- CinemachineParametersBlenderSource_CameraInput (0x71F680) ----

        // ControllerMovement has a private setter that the manager's InternalUpdate drives from its save settings,
        // which this fixture does not open; the case calls the setter as the manager's own code does.
        // Registered into the manager row before the source is added, so the source's field initialiser takes it.
        private static CinemachineCameraManager NewCameraManager(Owned owned, OwnedTimeScaledObjects time, Vector2 movement)
        {
            var manager = owned.Register(AddSource<CinemachineCameraManager>(time, "camera manager"));
            typeof(CinemachineCameraManager).GetProperty("ControllerMovement").GetSetMethod(true).Invoke(manager, new object[] { movement });
            return manager;
        }

        [Test]
        public void CameraInputBlendsTheMagnitudeOfTheCameraMovement()
        {
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CinemachineCameraManager>();
                owned.LeaseRow<CameraStackManager>();
                OwnedTimeScaledObjects time = owned.Lease(new OwnedTimeScaledObjects("lucid-camera-input"));
                NewCameraManager(owned, time, new Vector2(3f, 4f));
                var source = AddSource<CinemachineParametersBlenderSource_CameraInput>(time, "source");
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(5f).Within(Tolerance));
            });
        }

        [Test]
        public void CameraInputInBinaryModeBlendsOneForAnyMovementAndZeroForNone()
        {
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CinemachineCameraManager>();
                owned.LeaseRow<CameraStackManager>();
                OwnedTimeScaledObjects time = owned.Lease(new OwnedTimeScaledObjects("lucid-camera-input"));
                CinemachineCameraManager manager = NewCameraManager(owned, time, new Vector2(0f, -0.001f));
                var source = AddSource<CinemachineParametersBlenderSource_CameraInput>(time, "source");
                Set(source, "m_binaryValue", true);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(1f));
                typeof(CinemachineCameraManager).GetProperty("ControllerMovement").GetSetMethod(true).Invoke(manager, new object[] { Vector2.zero });
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0f));
            });
        }

        [Test]
        public void CameraInputWithoutTheCameraManagerLeavesTheBlendValue()
        {
            WithSource<CinemachineParametersBlenderSource_CameraInput>((owned, manager, character, source) =>
            {
                Seed(source, 0.9f);
                source.OnUpdate(0.1f);
                Assert.That(source.BlendValue, Is.EqualTo(0.9f));
            }, true);
        }

        // ---- CinemachineParametersBlenderSource_TimeSinceLive (0x71FB80..0x71FCB0) ----

        [Test]
        public void TimeSinceLiveTakesTheCameraOnItsHostWhenReset()
        {
            WithSource<CinemachineParametersBlenderSource_TimeSinceLive>((owned, manager, character, ignored) =>
            {
                OwnedTimeScaledObjects time = owned.Lease(new OwnedTimeScaledObjects("lucid-time-since-live-reset"));
                GameObject host = time.Objects.Create("camera and source");
                host.SetActive(false);
                var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var source = host.AddComponent<CinemachineParametersBlenderSource_TimeSinceLive>();
                Assert.That(Get<CinemachineVirtualCameraBase>(source, "m_virtualCamera"), Is.SameAs(camera));
                Assert.That(Get<float>(source, "m_maxTimeSeconds"), Is.EqualTo(1f));
            }, true);
        }

        [Test]
        public void TimeSinceLiveWithoutTheCameraManagerLeavesTheBlendValue()
        {
            WithSource<CinemachineParametersBlenderSource_TimeSinceLive>((owned, manager, character, source) =>
            {
                Seed(source, 0.9f);
                source.OnUpdate(0.25f);
                Assert.That(source.BlendValue, Is.EqualTo(0.9f));
            }, true);
        }

        // While the camera is live (the brain's IsLive, 0x71FBEC) the timer accumulates the frame time and the
        // blend value is the timer over the maximum time, unclamped; going off air resets the timer to zero
        // (0x71FC41) without touching the blend value, so the next live frame starts again.
        [Test]
        public void TimeSinceLiveCountsWhileLiveAndRestartsAfterGoingOffAir()
        {
            WithSource<CinemachineParametersBlenderSource_TimeSinceLive>((owned, manager, character, ignored) =>
            {
                OwnedTimeScaledObjects time = owned.Lease(new OwnedTimeScaledObjects("lucid-time-since-live"));
                GameObject host = time.Objects.Create("camera and source");
                host.SetActive(false);
                var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                CinemachineCameraManager cameraManager = owned.Register(AddSource<CinemachineCameraManager>(time, "camera manager"));
                Set(cameraManager, "m_cinemachineBrain", AddSource<CinemachineBrain>(time, "brain"));
                var source = host.AddComponent<CinemachineParametersBlenderSource_TimeSinceLive>();
                Set(source, "m_maxTimeSeconds", 2f);

                using (CinemachineCameraTestRig.MakeLive(camera))
                {
                    source.OnUpdate(0.25f);
                    Assert.That(source.BlendValue, Is.EqualTo(0.125f).Within(Tolerance));
                    source.OnUpdate(0.25f);
                    Assert.That(source.BlendValue, Is.EqualTo(0.25f).Within(Tolerance));
                }
                source.OnUpdate(0.25f);
                Assert.That(source.BlendValue, Is.EqualTo(0.25f).Within(Tolerance), "off air: the blend value is kept");
                Assert.That(Get<float>(source, "m_timeSecondsSinceActive"), Is.EqualTo(0f));
                using (CinemachineCameraTestRig.MakeLive(camera))
                {
                    source.OnUpdate(0.25f);
                    Assert.That(source.BlendValue, Is.EqualTo(0.125f).Within(Tolerance), "the timer restarted");
                }

                // 0x71FC08 is a bare division by the maximum time: the value runs past one.
                source.OnUpdate(0.25f);
                Set(source, "m_maxTimeSeconds", 0.2f);
                using (CinemachineCameraTestRig.MakeLive(camera))
                {
                    source.OnUpdate(0.25f);
                    Assert.That(source.BlendValue, Is.EqualTo(1.25f).Within(Tolerance));
                    source.OnUpdate(0.25f);
                    Assert.That(source.BlendValue, Is.EqualTo(2.5f).Within(Tolerance));
                }
            }, true);
        }
    }
}
