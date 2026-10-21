using System;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CinemachineRelativeOffsetExtension (TypeDef 0x02000290, x86-64 0x723FE0..0x7240DC) and
    // CinemachineSlopeAngleClamp (0x02000293, 0x724890..0x725253). The extensions are real components on owned inactive
    // hosts (the Editor sends no Awake), their pipeline callbacks are called by name with a real virtual camera, and
    // the slope clamp runs against a real CharacterManager, a Character whose tracking is a scripted ground contact,
    // and a TimeManager whose Camera category has scale 0. With a zero delta time the axis value is not moved by the
    // correction curve, so the cases check the clamp bookkeeping and the ordering of every comparison, not the speed
    // of the easing.
    public sealed class CinemachineCameraExtensionTests
    {
        private const float Tolerance = 1e-5f;

        // ---- CinemachineRelativeOffsetExtension ----

        private static CinemachineRelativeOffsetExtension NewOffsetExtension(Owned owned, CinemachineCore.Stage stage, Vector3 offset)
        {
            var extension = owned.Host("Lucid relative offset extension", false).AddComponent<CinemachineRelativeOffsetExtension>();
            Set(extension, "m_applyAfterStage", stage);
            Set(extension, "m_followRelativeOffset", offset);
            return extension;
        }

        private static CinemachineMixingCamera NewFollowingCamera(Owned owned, Transform follow)
        {
            var camera = owned.Host("Lucid following camera", false).AddComponent<CinemachineMixingCamera>();
            camera.Follow = follow;
            return camera;
        }

        private static Vector3 CorrectionAfter(CinemachineRelativeOffsetExtension extension, CinemachineVirtualCameraBase camera,
            CinemachineCore.Stage stage, Vector3 correction)
        {
            CameraState state = CameraState.Default;
            state.PositionCorrection = correction;
            object[] arguments = { camera, stage, state, 0.02f };
            Call(extension, "PostPipelineStageCallback", arguments);
            return ((CameraState)arguments[2]).PositionCorrection;
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string why) =>
            Assert.That((actual - expected).magnitude, Is.LessThan(Tolerance), why + ": expected " + expected + " but was " + actual);

        // PostPipelineStageCallback 0x723FE0 adds follow.TransformPoint(offset) - follow.position to the state's
        // position correction (the subps/addps pair at 0x72409a..0x72409d on the field at +0x78). The follow target is at
        // (1, 2, 3) turned 90 degrees about Y, so its local +Z offset of 2 lies along world +X: the displacement is
        // (2, 0, 0) and a correction of (1, 1, 1) becomes (3, 1, 1).
        [Test]
        public void RelativeOffsetAddsTheFollowTargetsWorldDisplacementToThePositionCorrection()
        {
            Owned.Run(owned =>
            {
                Transform follow = owned.Host("Lucid follow", false).transform;
                follow.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
                CinemachineRelativeOffsetExtension extension = NewOffsetExtension(owned, CinemachineCore.Stage.Aim, new Vector3(0f, 0f, 2f));
                CinemachineMixingCamera camera = NewFollowingCamera(owned, follow);

                AssertNear(CorrectionAfter(extension, camera, CinemachineCore.Stage.Aim, Vector3.one), new Vector3(3f, 1f, 1f), "aim stage");
            });
        }

        // TransformPoint includes the follow target's scale: at scale 2 the offset (0, 0, 2) is (0, 0, 4) before the
        // rotation, so the displacement is (4, 0, 0).
        [Test]
        public void RelativeOffsetIncludesTheFollowTargetsScale()
        {
            Owned.Run(owned =>
            {
                Transform follow = owned.Host("Lucid follow", false).transform;
                follow.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
                follow.localScale = new Vector3(2f, 2f, 2f);
                CinemachineRelativeOffsetExtension extension = NewOffsetExtension(owned, CinemachineCore.Stage.Body, new Vector3(0f, 0f, 2f));
                CinemachineMixingCamera camera = NewFollowingCamera(owned, follow);

                AssertNear(CorrectionAfter(extension, camera, CinemachineCore.Stage.Body, Vector3.zero), new Vector3(4f, 0f, 0f), "scaled");
            });
        }

        // Each call adds the displacement again; only the configured stage does anything (cmp [+0x30], stage at 0x724017).
        [Test]
        public void RelativeOffsetAccumulatesAndIgnoresOtherStages()
        {
            Owned.Run(owned =>
            {
                Transform follow = owned.Host("Lucid follow", false).transform;
                follow.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));
                CinemachineRelativeOffsetExtension extension = NewOffsetExtension(owned, CinemachineCore.Stage.Aim, new Vector3(0f, 0f, 2f));
                CinemachineMixingCamera camera = NewFollowingCamera(owned, follow);

                AssertNear(CorrectionAfter(extension, camera, CinemachineCore.Stage.Body, Vector3.zero), Vector3.zero, "body stage");
                AssertNear(CorrectionAfter(extension, camera, CinemachineCore.Stage.Noise, Vector3.zero), Vector3.zero, "noise stage");
                Vector3 once = CorrectionAfter(extension, camera, CinemachineCore.Stage.Aim, Vector3.zero);
                AssertNear(once, new Vector3(2f, 0f, 0f), "first");
                AssertNear(CorrectionAfter(extension, camera, CinemachineCore.Stage.Aim, once), new Vector3(4f, 0f, 0f), "second");
            });
        }

        // A camera that follows nothing is left alone (op_Equality(Follow, null) at 0x724056 branches to the return).
        [Test]
        public void RelativeOffsetIgnoresACameraWithoutAFollowTarget()
        {
            Owned.Run(owned =>
            {
                CinemachineRelativeOffsetExtension extension = NewOffsetExtension(owned, CinemachineCore.Stage.Aim, new Vector3(0f, 0f, 2f));
                CinemachineMixingCamera camera = NewFollowingCamera(owned, null);

                AssertNear(CorrectionAfter(extension, camera, CinemachineCore.Stage.Aim, Vector3.one), Vector3.one, "no follow target");
            });
        }

        // ---- CinemachineSlopeAngleClamp ----

        private static readonly MethodInfo TryGetFreeLookCamMethod =
            typeof(CinemachineSlopeAngleClamp).GetMethod("TryGetFreeLookCam", BindingFlags.Static | BindingFlags.NonPublic);

        private sealed class Scene
        {
            internal Owned Owned;
            internal CharacterManager Characters;
            internal CharacterGravityVectorProbe Character;
            internal GroundTracking Tracking;
            internal CinemachineSlopeAngleClamp Clamp;
            internal CinemachineFreeLook Camera;
            internal TimeManager Time;
        }

        // A tracking that reports a scripted ground contact; IsOnGround is vtable slot 5 and Type slot 4 (0x724968, 0x72498a).
        private sealed class GroundTracking : CharacterTracking
        {
            public bool OnGround = true;
            public CharacterTrackingType Kind = CharacterTrackingType.ColliderFree;
            public GroundTracking(Character character) : base(character) { }
            public override CharacterTrackingType Type => Kind;
            public override bool IsOnGround() => OnGround;
            public override bool WasOnGround() => throw new NotSupportedException();
            public override Vector3 GetContactNormal() => throw new NotSupportedException();
            public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime) =>
                throw new NotSupportedException();
            public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime) =>
                throw new NotSupportedException();
            public override void OrientateToGround() => throw new NotSupportedException();
            public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime,
                out Vector3 inputForward, out float inputTurn) => throw new NotSupportedException();
        }

        // The clamp reads a TimeManager whose Camera category is scale 0. The manager is registered before the clamp
        // caches its reference; the character is current, grounded on a free-collider tracking and facing 30 degrees
        // uphill (forward (0, sin 30, cos 30), right +X, gravity down), which is a slope angle of 30.
        private static void WithClamp(Action<Scene> body, float cameraTimeScale = 0f) =>
            WithManager((owned, characters, ignored) =>
            {
                TimeManager time = NewTimeManager(owned, TimeCategory.Camera, cameraTimeScale);
                owned.Register(time);
                var scene = new Scene { Owned = owned, Characters = characters, Time = time };
                GameObject characterHost = owned.Host("Lucid slope character", false);
                characterHost.AddComponent<BoxCollider>();
                scene.Character = characterHost.AddComponent<CharacterGravityVectorProbe>();
                SetActorProperty(scene.Character, "Storage", new FSMStorage(0));
                float radians = 30f * Mathf.Deg2Rad;
                SetActorProperty(scene.Character, "ForwardDirection", new Vector3(0f, Mathf.Sin(radians), Mathf.Cos(radians)));
                SetActorProperty(scene.Character, "RightDirection", Vector3.right);
                scene.Tracking = new GroundTracking(scene.Character);
                typeof(Character).GetProperty("ActiveTracking").GetSetMethod(true).Invoke(scene.Character, new object[] { scene.Tracking });
                SetCurrent(characters, scene.Character, true);
                scene.Clamp = owned.Host("Lucid slope clamp", false).AddComponent<CinemachineSlopeAngleClamp>();
                Set(scene.Clamp, "m_angleCorrectionCurve", AnimationCurve.Constant(-10f, 10f, 1f));
                scene.Camera = NewFreeLook(owned, 0.5f);
                scene.Camera.Follow = owned.Host("Lucid slope follow", false).transform;
                body(scene);
            });

        private static void Configure(CinemachineSlopeAngleClamp clamp, float lower, float clamped)
        {
            Set(clamp, "m_lowerThreshold", lower);
            Set(clamp, "m_clampedYAxisValue", clamped);
        }

        private static void SetState(CinemachineSlopeAngleClamp clamp, bool applied, float unclamped)
        {
            Set(clamp, "m_clampApplied", applied);
            Set(clamp, "m_unclampedYAxisValue", unclamped);
        }

        private static bool Applied(CinemachineSlopeAngleClamp clamp) => Get<bool>(clamp, "m_clampApplied");

        private static float Unclamped(CinemachineSlopeAngleClamp clamp) => Get<float>(clamp, "m_unclampedYAxisValue");

        private static float Apply(CinemachineSlopeAngleClamp clamp, float value) =>
            ((AxisState)Call(clamp, "ApplyCameraAxisClamp", new AxisState { Value = value })).Value;

        private static float Remove(CinemachineSlopeAngleClamp clamp, float value) =>
            ((AxisState)Call(clamp, "RemoveCameraAxisClamp", new AxisState { Value = value })).Value;

        // ApplyCameraAxisClamp 0x724F20: below the lower threshold (ucomiss lower, value; jbe at 0x724f5e) the unclamped
        // value is forgotten (the store of 0 at 0x724f60) and nothing else changes. At the threshold itself the clamp
        // starts, so the threshold test is strict.
        [Test]
        public void ApplyingBelowTheLowerThresholdOnlyForgetsTheUnclampedValue()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, false, 0.33f);

                Assert.That(Apply(scene.Clamp, 0.1f), Is.EqualTo(0.1f));

                Assert.That(Unclamped(scene.Clamp), Is.Zero);
                Assert.That(Applied(scene.Clamp), Is.False);
            });
        }

        [Test]
        public void ApplyingAtTheLowerThresholdStartsTheClamp()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, false, 0f);

                Assert.That(Apply(scene.Clamp, 0.2f), Is.EqualTo(0.2f));

                Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.2f));
                Assert.That(Applied(scene.Clamp), Is.True);
            });
        }

        // Starting a clamp below the clamp value (0x72501e..0x72502e) stores the axis value as the unclamped value and
        // marks the clamp applied; the axis becomes Min(clamp, value + deltaTime * curve), which for a zero delta time is
        // the value itself, not the clamp (a Max would answer 0.8).
        [Test]
        public void StartingTheClampRemembersTheUnclampedValueAndNeverRaisesTheAxisPastTheClamp()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, false, 0f);

                Assert.That(Apply(scene.Clamp, 0.5f), Is.EqualTo(0.5f));

                Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.5f));
                Assert.That(Applied(scene.Clamp), Is.True);
            });
        }

        // An axis value at or above the clamp starts the clamp without remembering anything (the store at 0x725023 sits
        // behind ucomiss clamp > value) and is returned unchanged.
        [Test]
        public void StartingTheClampAtOrAboveTheClampValueRemembersNothing()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                foreach (float value in new[] { 0.8f, 0.9f })
                {
                    SetState(scene.Clamp, false, 0.33f);

                    Assert.That(Apply(scene.Clamp, value), Is.EqualTo(value), "value " + value);

                    Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.33f), "value " + value);
                    Assert.That(Applied(scene.Clamp), Is.True, "value " + value);
                }
            });
        }

        // Once applied, the remembered value is kept (the store is only on the first application) and the axis keeps
        // easing toward the clamp.
        [Test]
        public void AnAppliedClampKeepsItsRememberedValue()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);

                Assert.That(Apply(scene.Clamp, 0.6f), Is.EqualTo(0.6f));

                Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.5f));
                Assert.That(Applied(scene.Clamp), Is.True);
            });
        }

        // RemoveCameraAxisClamp 0x725040 does nothing while no clamp is applied (cmp [+0x68], 0; je at 0x725075).
        [Test]
        public void RemovingWithoutAClampChangesNothing()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, false, 0.5f);

                Assert.That(Remove(scene.Clamp, 0.7f), Is.EqualTo(0.7f));

                Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.5f));
                Assert.That(Applied(scene.Clamp), Is.False);
            });
        }

        // The axis still above the remembered value keeps the clamp applied (0x7250e8 jb skips the release). The compare
        // against the clamp value is strict: a value equal to the clamp (0.8) is still eased, so the clamp is kept.
        [Test]
        public void RemovingKeepsTheClampWhileTheAxisIsAboveTheRememberedValue()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                foreach (float value in new[] { 0.7f, 0.8f })
                {
                    SetState(scene.Clamp, true, 0.5f);

                    Assert.That(Remove(scene.Clamp, value), Is.EqualTo(value), "value " + value);

                    Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.5f), "value " + value);
                    Assert.That(Applied(scene.Clamp), Is.True, "value " + value);
                }
            });
        }

        // An axis that has come back down to the remembered value releases the clamp (ucomiss unclamped, value; the
        // jb at 0x7250eb is not taken for equality, so the release test is >=).
        [Test]
        public void RemovingReleasesTheClampWhenTheAxisReachesTheRememberedValue()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);

                Assert.That(Remove(scene.Clamp, 0.5f), Is.EqualTo(0.5f));

                Assert.That(Unclamped(scene.Clamp), Is.Zero);
                Assert.That(Applied(scene.Clamp), Is.False);
            });
        }

        // An axis that has dropped below the remembered value is put back to it (the store at 0x7250ed) before the
        // release.
        [Test]
        public void RemovingRaisesAnAxisBelowTheRememberedValueBackToIt()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);

                Assert.That(Remove(scene.Clamp, 0.3f), Is.EqualTo(0.5f));

                Assert.That(Unclamped(scene.Clamp), Is.Zero);
                Assert.That(Applied(scene.Clamp), Is.False);
            });
        }

        // The clamp is released without touching the axis when the remembered value is not above zero (0x725084 jae),
        // and when the player has pushed the axis past the clamp value (0x725093 ja).
        [Test]
        public void RemovingReleasesAtOnceForANonPositiveRememberedValueOrAnAxisPastTheClamp()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                foreach ((float unclamped, float value) in new[] { (0f, 0.4f), (-0.1f, 0.4f), (0.5f, 0.9f) })
                {
                    SetState(scene.Clamp, true, unclamped);

                    Assert.That(Remove(scene.Clamp, value), Is.EqualTo(value), "unclamped " + unclamped + " value " + value);

                    Assert.That(Unclamped(scene.Clamp), Is.Zero, "unclamped " + unclamped + " value " + value);
                    Assert.That(Applied(scene.Clamp), Is.False, "unclamped " + unclamped + " value " + value);
                }
            });
        }

        // Time.deltaTime cannot be set in Edit Mode, so the eased cases read the frame time the Camera category reports
        // and use a curve that is linear in its argument (value = slope * argument). The easing step is then
        // frameTime * slope * argument; the argument is clamp - value for Apply (0x724f9d..0x724fc5, an addss) and
        // value - unclamped for Remove (0x72509a..0x7250d9, a subss).
        private static float CameraFrameTime(Scene scene) => scene.Time.GetDeltaTime(TimeCategory.Camera);

        private static void UseLinearCurve(Scene scene, float slope) =>
            Set(scene.Clamp, "m_angleCorrectionCurve", AnimationCurve.Linear(-10f, -10f * slope, 10f, 10f * slope));

        [Test]
        public void ApplyingEasesTheAxisUpTowardTheClampByTheScaledCurveValue()
        {
            WithClamp(scene =>
            {
                float frame = CameraFrameTime(scene);
                Assume.That(frame, Is.GreaterThan(0f).And.LessThan(0.3f), "needs a positive frame time");
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);
                UseLinearCurve(scene, 2f);

                // argument 0.8 - 0.5 = 0.3, curve value 0.6
                Assert.That(Apply(scene.Clamp, 0.5f), Is.EqualTo(0.5f + frame * 0.6f).Within(1e-6f));
            }, 1f);
        }

        [Test]
        public void ApplyingNeverEasesPastTheClamp()
        {
            WithClamp(scene =>
            {
                Assume.That(CameraFrameTime(scene), Is.GreaterThan(0f), "needs a positive frame time");
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);
                UseLinearCurve(scene, 1e7f);

                Assert.That(Apply(scene.Clamp, 0.5f), Is.EqualTo(0.8f));
            }, 1f);
        }

        [Test]
        public void RemovingEasesTheAxisDownTowardTheRememberedValueByTheScaledCurveValue()
        {
            WithClamp(scene =>
            {
                float frame = CameraFrameTime(scene);
                Assume.That(frame, Is.GreaterThan(0f).And.LessThan(0.3f), "needs a positive frame time");
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);
                UseLinearCurve(scene, 2f);

                // argument 0.7 - 0.5 = 0.2, curve value 0.4
                Assert.That(Remove(scene.Clamp, 0.7f), Is.EqualTo(0.7f - frame * 0.4f).Within(1e-6f));
                Assert.That(Applied(scene.Clamp), Is.True);
            }, 1f);
        }

        [Test]
        public void RemovingSnapsAnOvershootingStepBackToTheRememberedValueAndReleases()
        {
            WithClamp(scene =>
            {
                Assume.That(CameraFrameTime(scene), Is.GreaterThan(0f), "needs a positive frame time");
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);
                UseLinearCurve(scene, 1e7f);

                Assert.That(Remove(scene.Clamp, 0.6f), Is.EqualTo(0.5f));
                Assert.That(Unclamped(scene.Clamp), Is.Zero);
                Assert.That(Applied(scene.Clamp), Is.False);
            }, 1f);
        }

        // An unordered axis takes the compute path in RemoveCameraAxisClamp: ucomiss value, clamped; ja at 0x72508e is
        // not taken, and the NaN result then makes ucomiss unclamped, new; jb at 0x7250e8 return before the release.
        [Test]
        public void RemovingANaNAxisStaysNaNAndKeepsTheClamp()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);

                Assert.That(float.IsNaN(Remove(scene.Clamp, float.NaN)), Is.True);

                Assert.That(Applied(scene.Clamp), Is.True);
                Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.5f));
            });
        }

        // ApplyCameraAxisClamp ends in minss clamped, new (0x724fca), which returns the second operand (the eased value)
        // when unordered, so a NaN frame time gives NaN rather than the clamp.
        [Test]
        public void ApplyingWithANaNFrameTimeYieldsNaNInsteadOfTheClamp()
        {
            WithClamp(scene =>
            {
                Configure(scene.Clamp, 0.2f, 0.8f);
                SetState(scene.Clamp, true, 0.5f);

                Assert.That(float.IsNaN(Apply(scene.Clamp, 0.5f)), Is.True);
            }, float.NaN);
        }

        // ResetClampingValues 0x725150 forgets the clamp and returns the axis untouched.
        [Test]
        public void ResettingForgetsTheClampAndKeepsTheAxis()
        {
            WithClamp(scene =>
            {
                SetState(scene.Clamp, true, 0.5f);

                var result = (AxisState)Call(scene.Clamp, "ResetClampingValues", new AxisState { Value = 0.7f });

                Assert.That(result.Value, Is.EqualTo(0.7f));
                Assert.That(Unclamped(scene.Clamp), Is.Zero);
                Assert.That(Applied(scene.Clamp), Is.False);
            });
        }

        // TryGetFreeLookCam 0x724D50 resolves a camera through its parent camera when it has one, and answers a
        // free-look camera only.
        private static (bool found, CinemachineFreeLook camera) TryGetFreeLook(ICinemachineCamera camera)
        {
            object[] arguments = { camera, null };
            bool found = (bool)TryGetFreeLookCamMethod.Invoke(null, arguments);
            return (found, (CinemachineFreeLook)arguments[1]);
        }

        [Test]
        public void AFreeLookCameraIsItsOwnFreeLookCamera()
        {
            Owned.Run(owned =>
            {
                CinemachineFreeLook freeLook = NewFreeLook(owned, 0.5f);

                Assert.That(TryGetFreeLook(freeLook), Is.EqualTo((true, freeLook)));
            });
        }

        [Test]
        public void ACameraOfAnotherKindHasNoFreeLookCamera()
        {
            Owned.Run(owned =>
            {
                var mixing = owned.Host("Lucid mixing camera", false).AddComponent<CinemachineMixingCamera>();

                Assert.That(TryGetFreeLook(mixing), Is.EqualTo((false, (CinemachineFreeLook)null)));
            });
        }

        // The parent is taken first: a camera under a free-look camera answers that free-look camera, and a free-look
        // camera under another kind of camera answers nothing.
        [Test]
        public void ACameraIsResolvedThroughItsParentCamera()
        {
            Owned.Run(owned =>
            {
                CinemachineFreeLook parentFreeLook = NewFreeLook(owned, 0.5f);
                var child = owned.Host("Lucid child camera", false, parentFreeLook.transform).AddComponent<CinemachineMixingCamera>();
                var mixingParent = owned.Host("Lucid mixing parent", false).AddComponent<CinemachineMixingCamera>();
                var freeLookChild = AddCinemachineCamera<CinemachineFreeLook>(owned.Host("Lucid free look child", false, mixingParent.transform));

                Assert.That(TryGetFreeLook(child), Is.EqualTo((true, parentFreeLook)));
                Assert.That(TryGetFreeLook(freeLookChild), Is.EqualTo((false, (CinemachineFreeLook)null)));
            });
        }

        // PostPipelineStageCallback 0x724890. Chooses the clamp when the slope angle lies in
        // [upward, downward]: `slope >= upward` and `downward >= slope` (ucomiss jb at 0x724a24 and jae at 0x724a2e),
        // with an unordered (NaN) bound taking the remove path. Starting a clamp marks it applied, which the removal of
        // a clamp that was never started cannot do, so the flag tells the two paths apart.
        private static bool ClampStartedForSlope(Scene scene, float upward, float downward)
        {
            Set(scene.Clamp, "m_upwardSlopeAngle", upward);
            Set(scene.Clamp, "m_downwardSlopeAngle", downward);
            Configure(scene.Clamp, 0f, 0.8f);
            SetState(scene.Clamp, false, 0f);
            scene.Camera.m_YAxis.Value = 0.5f;
            Run(scene, CinemachineCore.Stage.Body);
            return Applied(scene.Clamp);
        }

        private static void Run(Scene scene, CinemachineCore.Stage stage, CinemachineVirtualCameraBase camera = null)
        {
            object[] arguments = { camera ?? scene.Camera, stage, CameraState.Default, 0.02f };
            Call(scene.Clamp, "PostPipelineStageCallback", arguments);
        }

        [Test]
        public void ASlopeInsideTheBandStartsTheClampAndOneOutsideDoesNot()
        {
            WithClamp(scene =>
            {
                float slope = ActorMovementUtilities.GetSlopeAngle(scene.Character);
                Assert.That(slope, Is.EqualTo(30f).Within(1e-3f), "the fixture's slope");

                Assert.That(ClampStartedForSlope(scene, 20f, 40f), Is.True, "inside");
                Assert.That(Unclamped(scene.Clamp), Is.EqualTo(0.5f), "the axis value is remembered");
                Assert.That(scene.Camera.m_YAxis.Value, Is.EqualTo(0.5f));
                Assert.That(ClampStartedForSlope(scene, 35f, 40f), Is.False, "below the band");
                Assert.That(ClampStartedForSlope(scene, 20f, 25f), Is.False, "above the band");
            });
        }

        [Test]
        public void BothSlopeBoundsAreInclusive()
        {
            WithClamp(scene =>
            {
                float slope = ActorMovementUtilities.GetSlopeAngle(scene.Character);

                Assert.That(ClampStartedForSlope(scene, slope, 40f), Is.True, "slope equals the upward bound");
                Assert.That(ClampStartedForSlope(scene, 20f, slope), Is.True, "slope equals the downward bound");
                Assert.That(ClampStartedForSlope(scene, slope + 0.01f, 40f), Is.False, "just below the upward bound");
                Assert.That(ClampStartedForSlope(scene, 20f, slope - 0.01f), Is.False, "just above the downward bound");
            });
        }

        [Test]
        public void AnUnorderedSlopeBoundTakesTheRemovePath()
        {
            WithClamp(scene =>
            {
                Assert.That(ClampStartedForSlope(scene, float.NaN, 40f), Is.False, "NaN upward bound");
                Assert.That(ClampStartedForSlope(scene, 20f, float.NaN), Is.False, "NaN downward bound");
            });
        }

        // Outside the band the clamp is removed instead: a started clamp whose axis is back at the remembered value is
        // released through the free-look camera's own axis.
        [Test]
        public void ASlopeOutsideTheBandReleasesAStartedClamp()
        {
            WithClamp(scene =>
            {
                Set(scene.Clamp, "m_upwardSlopeAngle", 35f);
                Set(scene.Clamp, "m_downwardSlopeAngle", 40f);
                Configure(scene.Clamp, 0f, 0.8f);
                SetState(scene.Clamp, true, 0.3f);
                scene.Camera.m_YAxis.Value = 0.3f;

                Run(scene, CinemachineCore.Stage.Body);

                Assert.That(Applied(scene.Clamp), Is.False);
                Assert.That(Unclamped(scene.Clamp), Is.Zero);
                Assert.That(scene.Camera.m_YAxis.Value, Is.EqualTo(0.3f));
            });
        }

        // Every guard returns before the clamp is touched: another stage, no follow target, no valid current character,
        // another tracking type, a character in the air, and a camera that is not a free-look camera.
        [Test]
        public void EveryGuardLeavesTheClampUntouched()
        {
            WithClamp(scene =>
            {
                Set(scene.Clamp, "m_upwardSlopeAngle", 20f);
                Set(scene.Clamp, "m_downwardSlopeAngle", 40f);
                Configure(scene.Clamp, 0f, 0.8f);
                scene.Camera.m_YAxis.Value = 0.5f;

                Run(scene, CinemachineCore.Stage.Aim);
                Assert.That(Applied(scene.Clamp), Is.False, "another stage");

                Transform follow = scene.Camera.Follow;
                scene.Camera.Follow = null;
                Run(scene, CinemachineCore.Stage.Body);
                Assert.That(Applied(scene.Clamp), Is.False, "no follow target");
                scene.Camera.Follow = follow;

                SetCurrent(scene.Characters, scene.Character, false);
                Run(scene, CinemachineCore.Stage.Body);
                Assert.That(Applied(scene.Clamp), Is.False, "no valid character");
                SetCurrent(scene.Characters, scene.Character, true);

                scene.Tracking.Kind = CharacterTrackingType.Active;
                Run(scene, CinemachineCore.Stage.Body);
                Assert.That(Applied(scene.Clamp), Is.False, "another tracking type");
                scene.Tracking.Kind = CharacterTrackingType.ColliderFree;

                scene.Tracking.OnGround = false;
                Run(scene, CinemachineCore.Stage.Body);
                Assert.That(Applied(scene.Clamp), Is.False, "in the air");
                scene.Tracking.OnGround = true;

                var mixing = scene.Owned.Host("Lucid other camera", false).AddComponent<CinemachineMixingCamera>();
                mixing.Follow = follow;
                Run(scene, CinemachineCore.Stage.Body, mixing);
                Assert.That(Applied(scene.Clamp), Is.False, "not a free-look camera");

                Run(scene, CinemachineCore.Stage.Body);
                Assert.That(Applied(scene.Clamp), Is.True, "all guards satisfied");
            });
        }
    }
}
