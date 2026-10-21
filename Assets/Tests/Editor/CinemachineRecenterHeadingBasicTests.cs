using System;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.CinemachineRecenterHeadingExtension_Basic (TypeDef 0x02000289, x86-64 0x721860..0x721DA0).
    // The extension is a real component on an owned inactive host next to a real CinemachineVirtualCamera whose
    // Follow target sits at the origin with the identity rotation; the CharacterManager is built by its real
    // constructor and registered, so SnapToCharacter runs its original body. Positions are written out from the
    // geometry, not recomputed: a camera at (1, 0, -1) is 45 degrees to the left of the point behind the target
    // (Vector3.SignedAngle from the flattened direction to Vector3.back, 0x721A73), and the default
    // AnimationCurve.EaseInOut(0, 1, 1, 2) of the definition (0x06001a73) is 1.15625 at 45/180, so at dt 0.1 and
    // the default speed 150 the camera orbits 1.15625 * 0.1 * 150 = 17.34375 degrees about the target's up axis:
    // (1, 0, -1) becomes (0.656430, 0, -1.252637). Not run here: the unsnap branch with a valid character, which
    // needs the camera manager's live-camera query.
    public sealed class CinemachineRecenterHeadingBasicTests
    {
        private const float Tolerance = 1e-3f;

        private sealed class Rig
        {
            public CinemachineVirtualCamera Camera;
            public Transform Follow;
            public CinemachineRecenterHeadingExtension_Basic Extension;
            public CameraRecenterHeadingDefinition_Basic Definition;
            public CharacterManager Manager;
            public Character Character;
        }

        private static void WithRig(Action<Owned, Rig> body) => CharacterTriggerTestSupport.WithManager((owned, manager, character) =>
        {
            owned.LeaseRow<CinemachineCameraManager>();
            GameObject host = owned.Host("Lucid recenter camera", false);
            var rig = new Rig { Manager = manager, Character = character };
            rig.Camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
            rig.Follow = owned.Host("Lucid recenter follow").transform;
            rig.Camera.Follow = rig.Follow;
            rig.Extension = host.AddComponent<CinemachineRecenterHeadingExtension_Basic>();
            rig.Definition = owned.Track(ScriptableObject.CreateInstance<CameraRecenterHeadingDefinition_Basic>());
            Set(rig.Definition, "m_forwardAngleExitSnapThreshold", 5f);
            Set(rig.Extension, "m_settingsDefinition", rig.Definition);
            body(owned, rig);
        });

        // SnapToCharacter needs a valid current character; the case then makes the character invalid again so
        // that ending the snap (ClearSnapToCharacter) stops at the manager's CharacterValid test.
        private static void BeginSnap(Rig rig)
        {
            SetCurrent(rig.Manager, rig.Character, true);
            rig.Extension.SnapToCharacter();
            Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
            SetCurrent(rig.Manager, rig.Character, false);
        }

        private static CameraState StateAt(Vector3 position)
        {
            CameraState state = CameraState.Default;
            state.RawPosition = position;
            state.RawOrientation = Quaternion.identity;
            return state;
        }

        private static void Callback(Rig rig, CinemachineCore.Stage stage, ref CameraState state, float deltaTime) =>
            rig.Extension.InvokePostPipelineStageCallback(rig.Camera, stage, ref state, deltaTime);

        [Test]
        public void TheCallbackDoesNothingOutsideItsConfiguredStage()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                Set(rig.Extension, "m_executeInCameraStage", CinemachineCore.Stage.Aim);
                CameraState state = StateAt(new Vector3(1f, 0f, -1f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.RawPosition, Is.EqualTo(new Vector3(1f, 0f, -1f)));
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
            });
        }

        [Test]
        public void TheCallbackDoesNothingWhileNotSnapping()
        {
            WithRig((owned, rig) =>
            {
                CameraState state = StateAt(new Vector3(1f, 0f, -1f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.RawPosition, Is.EqualTo(new Vector3(1f, 0f, -1f)));
            });
        }

        [Test]
        public void TheCallbackDoesNothingWithoutAFollowTarget()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                rig.Camera.Follow = null;
                CameraState state = StateAt(new Vector3(1f, 0f, -1f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.RawPosition, Is.EqualTo(new Vector3(1f, 0f, -1f)));
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.True);
            });
        }

        [Test]
        public void SnappingOrbitsTheCameraTowardsTheSpotBehindTheTargetAtTheCurvedSpeed()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                CameraState state = StateAt(new Vector3(1f, 0f, -1f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(Vector3.Distance(state.RawPosition, new Vector3(0.656430f, 0f, -1.252637f)), Is.LessThan(Tolerance), state.RawPosition.ToString("F5"));
                Assert.That(Quaternion.Angle(state.RawOrientation, Quaternion.AngleAxis(17.34375f, Vector3.up)), Is.LessThan(0.01f));
            });
        }

        [Test]
        public void ACameraOnTheOtherSideOrbitsTheOtherWay()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                CameraState state = StateAt(new Vector3(-1f, 0f, -1f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(Vector3.Distance(state.RawPosition, new Vector3(-0.656430f, 0f, -1.252637f)), Is.LessThan(Tolerance), state.RawPosition.ToString("F5"));
                Assert.That(Quaternion.Angle(state.RawOrientation, Quaternion.AngleAxis(-17.34375f, Vector3.up)), Is.LessThan(0.01f));
            });
        }

        // 0x721AB2..0x721ACF: the step is clamped to [0, remaining angle], so a huge step lands exactly behind.
        [Test]
        public void TheStepNeverOvershootsTheRemainingAngle()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                Set(rig.Definition, "m_rotationSpeed", 100000f);
                CameraState state = StateAt(new Vector3(1f, 0f, -1f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(Vector3.Distance(state.RawPosition, new Vector3(0f, 0f, -1.414214f)), Is.LessThan(Tolerance), state.RawPosition.ToString("F5"));
            });
        }

        // Within the exit threshold (0x721A86 compares the absolute angle with +0x1c) the snap ends instead.
        [Test]
        public void WithinTheExitThresholdTheSnapEndsAndTheCameraStaysPut()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                CameraState state = StateAt(new Vector3(0f, 0f, -2f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.RawPosition, Is.EqualTo(new Vector3(0f, 0f, -2f)));
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.False);
            });
        }

        // The follow target's pose is used (0x721902 transforms the camera position into its frame): a target at
        // (10, 0, 0) turned to face +x has the point behind it at (8, 0, 0), where the camera is already aligned,
        // although in the world frame that position is 90 degrees off.
        [Test]
        public void TheOrbitIsMeasuredInTheFollowTargetsFrame()
        {
            WithRig((owned, rig) =>
            {
                BeginSnap(rig);
                rig.Follow.SetPositionAndRotation(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                CameraState state = StateAt(new Vector3(8f, 0f, 0f));
                Callback(rig, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.RawPosition, Is.EqualTo(new Vector3(8f, 0f, 0f)), "behind the turned target: already aligned");
                Assert.That(Get<bool>(rig.Extension, "m_snapToCharacter"), Is.False);
            });
        }

        [Test]
        public void RotateAroundPointTurnsThePositionAboutThePointAndComposesTheOrientation()
        {
            WithRig((owned, rig) =>
            {
                CameraState state = StateAt(new Vector3(2f, 0f, 0f));
                object[] arguments = { state, new Vector3(1f, 0f, 0f), Vector3.up, 90f };
                Call(rig.Extension, "RotateAroundPoint", arguments);
                state = (CameraState)arguments[0];
                Assert.That(Vector3.Distance(state.RawPosition, new Vector3(1f, 0f, -1f)), Is.LessThan(Tolerance), state.RawPosition.ToString("F5"));
                Assert.That(Quaternion.Angle(state.RawOrientation, Quaternion.AngleAxis(90f, Vector3.up)), Is.LessThan(0.01f));
            });
        }

        [Test]
        public void TheSettingsDefinitionIsTheAuthoredOne()
        {
            WithRig((owned, rig) =>
            {
                Assert.That(Call(rig.Extension, "GetSettingsDefinition"), Is.SameAs(rig.Definition));
            });
        }
    }
}
