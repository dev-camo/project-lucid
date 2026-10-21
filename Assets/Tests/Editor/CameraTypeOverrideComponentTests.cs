using System;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using CameraType = HardlightProject.CameraType;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CameraRequestChange (TypeDef 0x02000262, x86-64 0x714D30..0x715142), CameraTypeManipulator
    // (0x02000263, 0x715150..0x7155FF) and CameraTypeTriggerVolume (0x02000264, 0x715600..0x715B1F). Each component is
    // real, on an owned inactive host, beside a real CinemachineCameraManager registered into its absent row. The
    // manager keeps a virtual camera per type: registering a camera records its authored priority as its starting
    // priority and drops it to -1, and a camera type on the manager's stack gets its starting priority back, so a
    // camera's priority shows whether its type is the active override. The delayed (coroutine) parts of the type
    // volume need a CoroutineUtils host and real time and are not covered.
    public sealed class CameraTypeOverrideComponentTests
    {
        private const int DefaultStart = 10, RailStart = 20;

        private sealed class Cameras
        {
            internal Rig Rig;
            internal CinemachineMixingCamera Default;
            internal CinemachineMixingCamera Rail;

            internal void AssertPriorities(int expectedDefault, int expectedRail, string label)
            {
                Assert.That((Default.Priority, Rail.Priority), Is.EqualTo((expectedDefault, expectedRail)), label);
            }
        }

        private static void WithCameras(Action<Owned, Cameras> body) => Owned.Run(owned =>
        {
            var cameras = new Cameras { Rig = NewRig(owned, true, true) };
            cameras.Default = AddCamera(owned, cameras.Rig, CameraType.Default, DefaultStart);
            cameras.Rail = AddCamera(owned, cameras.Rig, CameraType.Rail, RailStart);
            cameras.AssertPriorities(-1, -1, "registered with nothing on the camera stack");
            body(owned, cameras);
        });

        // ---- CameraRequestChange ----

        // Start 0x714D30 hands the manager to its lambda through InvokeOnValid, which runs at once for a live manager.
        [Test]
        public void RequestChangeStartTakesTheRegisteredCameraManager()
        {
            WithCameras((owned, cameras) =>
            {
                var request = owned.Host("Lucid camera request", false).AddComponent<CameraRequestChange>();

                Call(request, "Start");

                Assert.That(Get<CinemachineCameraManager>(request, "m_cinemachineCameraManger"), Is.SameAs(cameras.Rig.Manager));
            });
        }

        // Action_RequestCameraChange 0x714E10 calls RequestCharacterCameraChange with the stored type and then
        // SnapToCharacter(true, true). The first request creates the character's override, a later one replaces its
        // type, and the None type removes it.
        [Test]
        public void RequestingAChangeSwitchesTheCharacterCamera()
        {
            WithCameras((owned, cameras) =>
            {
                var request = owned.Host("Lucid camera request", false).AddComponent<CameraRequestChange>();
                Call(request, "Start");

                Set(request, "m_cameraType", CameraType.Rail);
                request.Action_RequestCameraChange();
                cameras.AssertPriorities(-1, RailStart, "rail requested");

                Set(request, "m_cameraType", CameraType.Default);
                request.Action_RequestCameraChange();
                cameras.AssertPriorities(DefaultStart, -1, "default replaces rail");

                Set(request, "m_cameraType", CameraType.None);
                request.Action_RequestCameraChange();
                cameras.AssertPriorities(-1, -1, "none removes the override");

                Set(request, "m_cameraType", CameraType.Rail);
                request.Action_RequestCameraChange();
                cameras.AssertPriorities(-1, RailStart, "rail requested again after none");
            });
        }

        // ---- CameraTypeManipulator ----

        private static CameraTypeManipulator NewManipulator(Owned owned, CameraType type)
        {
            var manipulator = owned.Host("Lucid camera type manipulator", false).AddComponent<CameraTypeManipulator>();
            Set(manipulator, "m_cameraType", type);
            Call(manipulator, "Awake");
            return manipulator;
        }

        // ApplyCameraType 0x7151E0 pushes the camera type override and then calls onComplete; RemoveCameraType 0x715320
        // removes the kept handle and then calls onComplete. The handle stays stored after removal (it is only read).
        [Test]
        public void ApplyingAndRemovingTheCameraTypeMovesThatCamerasPriorityAndCompletes()
        {
            WithCameras((owned, cameras) =>
            {
                CameraTypeManipulator manipulator = NewManipulator(owned, CameraType.Rail);
                int completed = 0;

                manipulator.ApplyCameraType(() => completed++);
                cameras.AssertPriorities(-1, RailStart, "applied");
                Assert.That(completed, Is.EqualTo(1));
                var handle = Get<StackableDataHandle>(manipulator, "m_stackableDataHandle");
                Assert.That(handle, Is.Not.Null);

                manipulator.RemoveCameraType(() => completed++);
                cameras.AssertPriorities(-1, -1, "removed");
                Assert.That(completed, Is.EqualTo(2));
                Assert.That(Get<StackableDataHandle>(manipulator, "m_stackableDataHandle"), Is.SameAs(handle));

                Assert.DoesNotThrow(() => manipulator.ApplyCameraType(), "the overloads without a callback pass null");
                cameras.AssertPriorities(-1, RailStart, "applied again");
                Assert.DoesNotThrow(() => manipulator.RemoveCameraType());
                cameras.AssertPriorities(-1, -1, "removed again");
            });
        }

        // Without a handle, RemoveCameraType skips the manager altogether and only completes (0x715361 jumps to 0x7153eb),
        // so it completes even when the manager is gone. ApplyCameraType and a removal with a handle both return
        // before onComplete when the manager reference has no live manager (0x715249, 0x715396).
        [Test]
        public void WithoutTheCameraManagerOnlyAHandlelessRemovalCompletes()
        {
            // The manager is registered through the scope, which can also take it out of its row again.
            GameInputScope.Run(scope => Owned.Run(owned =>
            {
                scope.Row<CinemachineCameraManager>();
                Rig rig = NewRig(owned, true, false);
                scope.Register(rig.Manager);
                CameraTypeManipulator manipulator = NewManipulator(owned, CameraType.Rail);
                int completed = 0;
                manipulator.ApplyCameraType();
                scope.Unregister(rig.Manager);

                manipulator.RemoveCameraType(() => completed++);
                Assert.That(completed, Is.Zero, "the handle needs the manager to release it");
                manipulator.ApplyCameraType(() => completed++);
                Assert.That(completed, Is.Zero, "nothing is applied without the manager");

                CameraTypeManipulator fresh = NewManipulator(owned, CameraType.Rail);
                fresh.RemoveCameraType(() => completed++);
                Assert.That(completed, Is.EqualTo(1), "no handle: complete without asking the manager");
            }));
        }

        // ---- CameraTypeTriggerVolume ----

        private static CameraTypeTriggerVolume NewVolume(Owned owned, CameraType type, Transform follow, Transform lookAt)
        {
            GameObject host = owned.Host("Lucid camera type volume", false);
            host.AddComponent<BoxCollider>();
            var volume = host.AddComponent<CameraTypeTriggerVolume>();
            Set(volume, "m_cameraType", type);
            Set(volume, "m_followOverride", follow);
            Set(volume, "m_lookAtOverride", lookAt);
            return volume;
        }

        // DoTriggerEnter 0x715600 with no delays applies the camera type override at once (ApplyOverride 0x715820)
        // and then sets the follow and look-at targets of that type's camera (SetCameraTargets 0x7158D0).
        // DoTriggerExit 0x715990 and OnDisable 0x7159D0 release the override again (TryRemoveOverride 0x715780).
        [Test]
        public void EnteringWithoutDelaysOverridesTheCameraTypeAndItsTargetsAndLeavingReleasesIt()
        {
            WithCameras((owned, cameras) =>
            {
                Transform follow = owned.Host("Lucid follow", false).transform;
                Transform lookAt = owned.Host("Lucid look at", false).transform;
                CameraTypeTriggerVolume volume = NewVolume(owned, CameraType.Rail, follow, lookAt);
                Assert.That(cameras.Rail.Follow, Is.Null);

                Call(volume, "DoTriggerEnter", (object)null);
                cameras.AssertPriorities(-1, RailStart, "entered");
                Assert.That((cameras.Rail.Follow, cameras.Rail.LookAt), Is.EqualTo((follow, lookAt)));
                Assert.That((cameras.Default.Follow, cameras.Default.LookAt), Is.EqualTo(((Transform)null, (Transform)null)),
                    "only the volume's type gets targets");

                Call(volume, "DoTriggerExit", (object)null);
                cameras.AssertPriorities(-1, -1, "left");
                Assert.That(Get<StackableDataHandle>(volume, "m_handle"), Is.Not.Null, "the handle is kept");

                Call(volume, "DoTriggerEnter", (object)null);
                cameras.AssertPriorities(-1, RailStart, "entered again");
                Call(volume, "OnDisable");
                cameras.AssertPriorities(-1, -1, "disabled");
            });
        }

        // Entering first releases the override of the previous visit (TryRemoveOverride before the new apply, 0x715671),
        // so a volume whose type changed between visits leaves only the new type active.
        [Test]
        public void EnteringAgainReleasesTheEarlierOverrideBeforeApplyingTheNewOne()
        {
            WithCameras((owned, cameras) =>
            {
                CameraTypeTriggerVolume volume = NewVolume(owned, CameraType.Rail, null, null);
                Call(volume, "DoTriggerEnter", (object)null);
                cameras.AssertPriorities(-1, RailStart, "rail entered");

                Set(volume, "m_cameraType", CameraType.Default);
                Call(volume, "DoTriggerEnter", (object)null);

                cameras.AssertPriorities(DefaultStart, -1, "only the default camera is active");
            });
        }
    }
}
