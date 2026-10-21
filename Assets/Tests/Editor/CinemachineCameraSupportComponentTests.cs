using System;
using System.Reflection;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering.Universal;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.CinemachineControllerMovementProvider (TypeDef 0x02000273, x86-64 0x71DFB0..0x71E120),
    // CinemachineClipPlaneCollisionExtension (0x02000271, 0x71D430..0x71DE50) with its Character variant (0x02000272,
    // 0x71DE50..0x71DFB0) and CinemachinePlayerCharacterAssign (0x02000287, 0x720FE0..0x721320). Components are real
    // components on owned inactive hosts; the CharacterManager is built by its real constructor and registered; the
    // camera manager, its brain (on the host of a real Camera) and the virtual cameras are real Cinemachine
    // components, and the brain answers IsLive for the camera made its solo camera (CinemachineCameraTestRig.MakeLive).
    // The clip-plane cases use real box colliders and Physics.Raycast. Not run here: the Development-only
    // DebugDrawRay (the retail call sites are compiled out) and the camera pipeline itself.
    public sealed class CinemachineCameraSupportComponentTests
    {
        private const float Tolerance = 1e-3f;

        private static void WithCameras(Action<Owned, CharacterManager, Character, OwnedTimeScaledObjects> body) =>
            WithManager((owned, characters, character) =>
            {
                owned.LeaseRow<TimeManager>();
                owned.LeaseRow<CinemachineCameraManager>();
                owned.LeaseRow<CameraStackManager>();
                body(owned, characters, character, owned.Lease(new OwnedTimeScaledObjects("lucid-camera-support")));
            });

        private static T Inactive<T>(OwnedTimeScaledObjects time, string name) where T : Component
        {
            GameObject host = time.Objects.Create(name);
            host.SetActive(false);
            return host.AddComponent<T>();
        }

        private static void SetCameraMovement(CinemachineCameraManager manager, Vector2 movement) =>
            typeof(CinemachineCameraManager).GetProperty("ControllerMovement").GetSetMethod(true).Invoke(manager, new object[] { movement });

        // ---- CinemachineControllerMovementProvider (0x71DFB0) ----

        [Test]
        public void BothAxesAreEnabledByDefault()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var provider = Inactive<CinemachineControllerMovementProvider>(time, "provider");
                Assert.That(Get<bool>(provider, "m_xAxisEnabled"), Is.True);
                Assert.That(Get<bool>(provider, "m_yAxisEnabled"), Is.True);
            });
        }

        [Test]
        public void EnabledAxesReadTheCameraManagersControllerMovement()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var manager = owned.Register(Inactive<CinemachineCameraManager>(time, "camera manager"));
                SetCameraMovement(manager, new Vector2(0.3f, -0.6f));
                var provider = Inactive<CinemachineControllerMovementProvider>(time, "provider");
                Assert.That(provider.GetAxisValue(0), Is.EqualTo(0.3f));
                Assert.That(provider.GetAxisValue(1), Is.EqualTo(-0.6f));
            });
        }

        // 0x71DFEA and 0x71E00C: a disabled axis reads zero, and so does the third (Z) axis the provider is
        // also asked for.
        [Test]
        public void ADisabledAxisAndTheThirdAxisReadZero()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var manager = owned.Register(Inactive<CinemachineCameraManager>(time, "camera manager"));
                SetCameraMovement(manager, new Vector2(0.3f, -0.6f));
                var provider = Inactive<CinemachineControllerMovementProvider>(time, "provider");
                Set(provider, "m_xAxisEnabled", false);
                Assert.That(provider.GetAxisValue(0), Is.EqualTo(0f));
                Assert.That(provider.GetAxisValue(1), Is.EqualTo(-0.6f));
                Set(provider, "m_yAxisEnabled", false);
                Assert.That(provider.GetAxisValue(1), Is.EqualTo(0f));
                Set(provider, "m_xAxisEnabled", true);
                Set(provider, "m_yAxisEnabled", true);
                Assert.That(provider.GetAxisValue(2), Is.EqualTo(0f));
                Assert.That(provider.GetAxisValue(7), Is.EqualTo(0f));
            });
        }

        // A negative axis reaches the Vector2 indexer (0x71E034 builds the "Invalid Vector2 index!" exception).
        [Test]
        public void ANegativeAxisFaultsInTheVectorIndexer()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var manager = owned.Register(Inactive<CinemachineCameraManager>(time, "camera manager"));
                var provider = Inactive<CinemachineControllerMovementProvider>(time, "provider");
                Assert.Throws<IndexOutOfRangeException>(() => provider.GetAxisValue(-1));
            });
        }

        // ---- CinemachineClipPlaneCollisionExtension (0x71D430..0x71DE50) ----

        [Test]
        public void TheClipPlaneExtensionStartsWithItsAuthoredDefaults()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var host = time.Objects.Create("camera");
                host.SetActive(false);
                CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var extension = host.AddComponent<CinemachineClipPlaneCollisionExtension>();
                Assert.That(Get<bool>(extension, "m_toggleOcclusionRenderFeatures"), Is.True);
                Assert.That(Get<float>(extension, "m_nearClipPlaneScale"), Is.EqualTo(1.3f));
                Assert.That(Get<Vector3[]>(extension, "m_localFrustumPositions").Length, Is.EqualTo(5));
                Assert.That(Get<bool>(extension, "m_showDebugRays"), Is.False);
            });
        }

        [Test]
        public void EnablingTheExtensionRecordsWhetherItsCameraIsAFreeLookCamera()
        {
            WithCameras((owned, characters, character, time) =>
            {
                GameObject plain = time.Objects.Create("virtual camera");
                plain.SetActive(false);
                CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(plain);
                var plainExtension = plain.AddComponent<CinemachineClipPlaneCollisionExtension>();
                Call(plainExtension, "OnEnable");
                Assert.That(Get<bool>(plainExtension, "m_isFreeLookCamera"), Is.False);

                GameObject freeLook = time.Objects.Create("free look camera");
                freeLook.SetActive(false);
                CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineFreeLook>(freeLook);
                var freeLookExtension = freeLook.AddComponent<CinemachineClipPlaneCollisionExtension>();
                Call(freeLookExtension, "OnEnable");
                Assert.That(Get<bool>(freeLookExtension, "m_isFreeLookCamera"), Is.True);
            });
        }

        [Test]
        public void TheLookAtTransformIsTheVirtualCamerasLookAtTarget()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var host = time.Objects.Create("camera");
                host.SetActive(false);
                var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var extension = host.AddComponent<CinemachineClipPlaneCollisionExtension>();
                Transform target = owned.Host("Lucid look at target").transform;
                camera.LookAt = target;
                Assert.That(Call(extension, "GetLookAtTransform"), Is.SameAs(target));
            });
        }

        // 0x71DE50: the current character's transform wins; with no manager value or no current character the
        // virtual camera's own look-at target is used.
        [Test]
        public void TheCharacterVariantLooksAtTheCurrentCharacterElseTheCamerasTarget()
        {
            WithCameras((owned, characters, character, time) =>
            {
                var host = time.Objects.Create("camera");
                host.SetActive(false);
                var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
                var extension = host.AddComponent<CinemachineClipPlaneCollisionExtension_Character>();
                Transform target = owned.Host("Lucid look at target").transform;
                camera.LookAt = target;
                SetCurrent(characters, character, false);
                Assert.That(Call(extension, "GetLookAtTransform"), Is.SameAs(target), "no current character");
                SetCurrent(characters, character, true);
                Assert.That(Call(extension, "GetLookAtTransform"), Is.SameAs(character.transform));
            });
        }

        private sealed class ClipRig
        {
            public CinemachineVirtualCameraBase Camera;
            public CinemachineClipPlaneCollisionExtension Extension;
            public CinemachineCameraManager Manager;
            public Camera OutputCamera;
            public Transform LookAt;
            public RenderObjects Feature;
            public BoxCollider Wall;
        }

        // A camera 10 units behind the look-at target (the origin) looking along +z, a 0.3 near plane and a wall
        // (a 20 x 20 x 1 box centred at z = -5, front face at z = -4.5) between them.
        private static ClipRig NewClipRig(Owned owned, OwnedTimeScaledObjects time, bool freeLook = false)
        {
            var rig = new ClipRig();
            GameObject brainHost = time.Objects.Create("brain");
            brainHost.SetActive(false);
            rig.OutputCamera = brainHost.AddComponent<Camera>();
            rig.OutputCamera.nearClipPlane = 0.3f;
            rig.OutputCamera.fieldOfView = 60f;
            var brain = brainHost.AddComponent<CinemachineBrain>();
            rig.Manager = owned.Register(Inactive<CinemachineCameraManager>(time, "camera manager"));
            Set(rig.Manager, "m_cinemachineBrain", brain);
            rig.Feature = time.Objects.Own(ScriptableObject.CreateInstance<RenderObjects>());
            Set(rig.Manager, "m_occlusionRenderFeatures", new ScriptableRendererFeature[] { rig.Feature });

            GameObject host = time.Objects.Create("clip plane camera");
            host.SetActive(false);
            rig.Camera = freeLook
                ? (CinemachineVirtualCameraBase)CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineFreeLook>(host)
                : CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(host);
            rig.Extension = host.AddComponent<CinemachineClipPlaneCollisionExtension>();
            Set(rig.Extension, "m_collisionMask", new LayerMask { value = -1 });
            Set(rig.Extension, "m_collisionIgnoreTag", "EditorOnly");
            rig.LookAt = owned.Host("Lucid look at target").transform;
            rig.Camera.LookAt = rig.LookAt;

            GameObject wallHost = owned.Host("Lucid wall");
            wallHost.transform.position = new Vector3(0f, 0f, -5f);
            rig.Wall = wallHost.AddComponent<BoxCollider>();
            rig.Wall.size = new Vector3(20f, 20f, 1f);
            Physics.SyncTransforms();
            return rig;
        }

        private static CameraState StateAtTen()
        {
            CameraState state = CameraState.Default;
            state.RawPosition = new Vector3(0f, 0f, -10f);
            state.RawOrientation = Quaternion.identity;
            return state;
        }

        // The nearest hit among the five rays (the centre one, 0x71D9A3..0x71DC15) is the wall's front face
        // at z = -4.5, so the camera is pushed from its near-plane centre (0, 0, -9.7) to the hit point:
        // a correction of (0, 0, 5.2).
        [Test]
        public void AWallBetweenTheCameraAndTheTargetPushesTheCameraInFrontOfIt()
        {
            WithCameras((owned, characters, character, time) =>
            {
                ClipRig rig = NewClipRig(owned, time);
                CameraState state = StateAtTen();
                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(Vector3.Distance(state.PositionCorrection, new Vector3(0f, 0f, 5.2f)), Is.LessThan(Tolerance), state.PositionCorrection.ToString("F4"));
                Assert.That(Get<float>(rig.Extension, "m_previousNearClipPlaneValue"), Is.EqualTo(0.3f).Within(1e-5f));
                Assert.That(Get<float>(rig.Extension, "m_previousFieldOfView"), Is.EqualTo(60f));
                Assert.That(Get<Vector3[]>(rig.Extension, "m_localFrustumPositions")[4], Is.EqualTo(new Vector3(0f, 0f, 0.3f)));
                Assert.That(rig.Feature.isActive, Is.False, "no ignored collider was hit: the occlusion features go off");
            });
        }

        // 0x71D98B..0x71D995 scale the near-plane corner x and y by the near-clip-plane scale (1.3). A strip spanning
        // y 0.22..0.30 is reached only by the scaled top corners (0.3 * tan 30 degrees = 0.173205, scaled 0.225167);
        // the unscaled corners and the centre ray pass beneath it.
        [Test]
        public void TheNearPlaneCornersAreSpreadByTheNearClipPlaneScale()
        {
            WithCameras((owned, characters, character, time) =>
            {
                ClipRig rig = NewClipRig(owned, time);
                rig.Wall.enabled = false;
                GameObject stripHost = owned.Host("Lucid strip");
                stripHost.transform.position = new Vector3(0f, 0.26f, -5f);
                BoxCollider strip = stripHost.AddComponent<BoxCollider>();
                strip.size = new Vector3(20f, 0.08f, 1f);
                Physics.SyncTransforms();
                CameraState state = StateAtTen();
                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(Vector3.Distance(state.PositionCorrection, new Vector3(0f, 0f, 5.2f)), Is.LessThan(Tolerance), state.PositionCorrection.ToString("F4"));
            });
        }

        // Rays that hit an ignore-tagged collider (0x71DB7D) do not push the camera; they switch the occlusion
        // features on instead (0x71DC75), but only for the extension that toggles them.
        [Test]
        public void AnIgnoredColliderOnlySwitchesTheOcclusionFeaturesOn()
        {
            WithCameras((owned, characters, character, time) =>
            {
                ClipRig rig = NewClipRig(owned, time);
                rig.Wall.gameObject.tag = "EditorOnly";
                CameraState state = StateAtTen();
                rig.Feature.SetActive(false);
                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.PositionCorrection, Is.EqualTo(Vector3.zero));
                Assert.That(rig.Feature.isActive, Is.True);

                rig.Feature.SetActive(false);
                Set(rig.Extension, "m_toggleOcclusionRenderFeatures", false);
                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(rig.Feature.isActive, Is.False, "the toggle is off");
            });
        }

        [Test]
        public void ThePostPipelineCallbackIgnoresOtherStagesCamerasThatAreNotLiveAndMissingTargets()
        {
            WithCameras((owned, characters, character, time) =>
            {
                ClipRig rig = NewClipRig(owned, time);
                CameraState state = StateAtTen();
                rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                Assert.That(state.PositionCorrection, Is.EqualTo(Vector3.zero), "not live");

                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                {
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Aim, ref state, 0.1f);
                    Assert.That(state.PositionCorrection, Is.EqualTo(Vector3.zero), "another stage");

                    rig.Camera.LookAt = null;
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                    Assert.That(state.PositionCorrection, Is.EqualTo(Vector3.zero), "no look-at target");
                    Assert.That(rig.Feature.isActive, Is.True, "the occlusion features are untouched");
                }
            });
        }

        // 0x71D55E: a free-look camera runs the callback for its own camera only, not for its rigs.
        [Test]
        public void AFreeLookExtensionIgnoresTheCallbackOfItsRigs()
        {
            WithCameras((owned, characters, character, time) =>
            {
                ClipRig rig = NewClipRig(owned, time, true);
                Call(rig.Extension, "OnEnable");
                Assert.That(Get<bool>(rig.Extension, "m_isFreeLookCamera"), Is.True);
                CinemachineVirtualCameraBase rigCamera = ((CinemachineFreeLook)rig.Camera).GetRig(0);
                CameraState state = StateAtTen();
                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                {
                    rig.Extension.InvokePostPipelineStageCallback(rigCamera, CinemachineCore.Stage.Body, ref state, 0.1f);
                    Assert.That(state.PositionCorrection, Is.EqualTo(Vector3.zero));
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                    Assert.That(state.PositionCorrection.z, Is.EqualTo(5.2f).Within(Tolerance));
                }
            });
        }

        // 0x71D6D1..0x71D724: the frustum corners are recomputed only when the near plane or the field of view
        // changed (Mathf.Approximately), and the extra centre entry follows the near plane.
        [Test]
        public void TheFrustumCornersAreRecomputedOnlyWhenTheLensChanges()
        {
            WithCameras((owned, characters, character, time) =>
            {
                ClipRig rig = NewClipRig(owned, time);
                var corners = Get<Vector3[]>(rig.Extension, "m_localFrustumPositions");
                CameraState state = StateAtTen();
                using (CinemachineCameraTestRig.MakeLive(rig.Camera))
                {
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                    corners[4] = new Vector3(0f, 0f, 99f);
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                    Assert.That(corners[4].z, Is.EqualTo(99f), "unchanged lens: not recomputed");
                    rig.OutputCamera.nearClipPlane = 0.5f;
                    rig.Extension.InvokePostPipelineStageCallback(rig.Camera, CinemachineCore.Stage.Body, ref state, 0.1f);
                    Assert.That(corners[4], Is.EqualTo(new Vector3(0f, 0f, 0.5f)));
                    Assert.That(corners[0].z, Is.EqualTo(0.5f).Within(1e-5f), "the corners lie on the near plane");
                }
            });
        }

        // ---- CinemachinePlayerCharacterAssign (0x720FE0) ----

        private static SmoothedTransformProxy NewVisualProxy(Owned owned, Character character)
        {
            var proxy = owned.Host("Lucid visual proxy").AddComponent<SmoothedTransformProxy>();
            SetActorProperty(character, "VisualProxy", proxy);
            return proxy;
        }

        private static (CinemachinePlayerCharacterAssign assign, CinemachineVirtualCamera camera) NewAssign(Owned owned)
        {
            GameObject cameraHost = owned.Host("Lucid assign camera", false);
            var camera = CinemachineCameraTestRig.AddWithoutPipelineHooks<CinemachineVirtualCamera>(cameraHost);
            var assign = owned.Host("Lucid assign", false).AddComponent<CinemachinePlayerCharacterAssign>();
            Set(assign, "m_virtualCamera", camera);
            return (assign, camera);
        }

        [Test]
        public void TheAssignerFollowsNothingAndLooksAtTheCharacterByDefault()
        {
            WithManager((owned, characters, character) =>
            {
                (CinemachinePlayerCharacterAssign assign, _) = NewAssign(owned);
                Assert.That(Get<bool>(assign, "m_assignPlayerAsFollow"), Is.False);
                Assert.That(Get<bool>(assign, "m_assignPlayerAsLookAt"), Is.True);
            });
        }

        // Start publishes the manager reference and registers for character changes; a character that is
        // already current is assigned at once (RegisterOnCharacterChange invokes immediately, 0x721137).
        [Test]
        public void StartAssignsTheCurrentCharactersVisualProxyAsTheLookAtTarget()
        {
            WithManager((owned, characters, character) =>
            {
                SmoothedTransformProxy proxy = NewVisualProxy(owned, character);
                SetCurrent(characters, character, true);
                (CinemachinePlayerCharacterAssign assign, CinemachineVirtualCamera camera) = NewAssign(owned);
                Call(assign, "Start");
                Assert.That(camera.LookAt, Is.SameAs(proxy.transform));
                Assert.That(camera.Follow, Is.Null, "follow is off by default");
            });
        }

        [Test]
        public void AssignPlayerAsTargetFollowsOnlyWhenFollowIsEnabled()
        {
            WithManager((owned, characters, character) =>
            {
                SmoothedTransformProxy proxy = NewVisualProxy(owned, character);
                SetCurrent(characters, character, true);
                (CinemachinePlayerCharacterAssign assign, CinemachineVirtualCamera camera) = NewAssign(owned);
                Call(assign, "Start");
                Set(assign, "m_assignPlayerAsFollow", true);
                Set(assign, "m_assignPlayerAsLookAt", false);
                camera.LookAt = null;
                Call(assign, "AssignPlayerAsTarget");
                Assert.That(camera.Follow, Is.SameAs(proxy.transform));
                Assert.That(camera.LookAt, Is.Null);
            });
        }

        // Without a current character nothing is assigned at Start; the registered callback assigns the
        // character the manager announces, as CharacterManager does through its change delegate.
        [Test]
        public void ACharacterThatBecomesCurrentLaterIsAssignedThroughTheRegisteredCallback()
        {
            WithManager((owned, characters, character) =>
            {
                SmoothedTransformProxy proxy = NewVisualProxy(owned, character);
                (CinemachinePlayerCharacterAssign assign, CinemachineVirtualCamera camera) = NewAssign(owned);
                Call(assign, "Start");
                Assert.That(camera.LookAt, Is.Null);
                SetCurrent(characters, character, true);
                Get<Action<Character>>(characters, "m_onCharacterChange")(character);
                Assert.That(camera.LookAt, Is.SameAs(proxy.transform));
            });
        }

        [Test]
        public void DestroyingTheAssignerReleasesItsCharacterChangeCallback()
        {
            WithManager((owned, characters, character) =>
            {
                (CinemachinePlayerCharacterAssign assign, _) = NewAssign(owned);
                Call(assign, "Start");
                Assert.That(Get<Action<Character>>(characters, "m_onCharacterChange").GetInvocationList(),
                    Has.Some.Matches<Delegate>(handler => ReferenceEquals(handler.Target, assign)));
                Call(assign, "OnDestroy");
                Action<Character> remaining = Get<Action<Character>>(characters, "m_onCharacterChange");
                Assert.That(remaining == null || Array.TrueForAll(remaining.GetInvocationList(), handler => !ReferenceEquals(handler.Target, assign)), Is.True);
            });
        }
    }
}
