using Cinemachine;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using CameraType = HardlightProject.CameraType;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime AutoPlayCameraStartPoint (TypeDef 0x020001D0, x86-64 0x6F2B70..0x6F2CCF). The start point
    // is a real component on an owned inactive host. The camera manager is a real component registered into its absent
    // row, with a proxy target (which, with no target type chosen, teleports nothing) and one real virtual camera whose
    // body and aim components are removed, so the camera takes exactly the position it is forced to.
    public sealed class AutoPlayCameraStartPointTests
    {
        // Awake 0x6F2B70 asks the manager reference to run OnCameraManagerAvailable once the manager is valid, which for
        // a live manager is at once; that callback 0x6F2C50 forces every virtual camera to the start point's position
        // and rotation (ForceAllVirtualCamerasToPosition with applyFollowOffset and clearActiveBlend both true).
        [Test]
        public void AwakeForcesTheVirtualCamerasToTheStartPoint()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                Rig rig = NewRig(owned, true, false);
                // The proxy target is an authored serialized reference of the manager.
                Set(rig.Manager, "m_proxyTarget", EnemyAbilityTestSupport.AddTimeScaled<CameraProxyTarget>(owned.Host("Lucid proxy target", false)));
                owned.Register(rig.Manager);

                var camera = AddCinemachineCamera<CinemachineVirtualCamera>(owned.Host("Lucid forced camera", false));
                camera.DestroyCinemachineComponent<CinemachineTransposer>();
                camera.DestroyCinemachineComponent<CinemachineComposer>();
                rig.Manager.RegisterCameraByType(new[]
                {
                    new CinemachineCameraManager.VirtualCameraByType { Type = CameraType.Default, VirtualCamera = camera }
                });

                GameObject host = owned.Host("Lucid auto play start", false);
                host.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(0f, 90f, 0f));
                var start = host.AddComponent<AutoPlayCameraStartPoint>();

                Call(start, "Awake");

                Assert.That((camera.transform.position - new Vector3(3f, 4f, 5f)).magnitude, Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-3f));
            });
        }
    }
}
