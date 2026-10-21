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
    // aim component is removed and its transposer holds a known world-space follow offset, so the camera takes exactly the
    // position it is forced to plus that offset.
    public sealed class AutoPlayCameraStartPointTests
    {
        // Awake 0x6F2B70 asks the manager reference to run OnCameraManagerAvailable once the manager is valid, which for
        // a live manager is at once; that callback 0x6F2C50 forces every virtual camera to the start point's position
        // and rotation (ForceAllVirtualCamerasToPosition with applyFollowOffset true at 0x6f2ca1; the camera is placed at the
        // start point plus its transposer's follow offset).
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
                var transposer = camera.GetCinemachineComponent<CinemachineTransposer>() ?? camera.AddCinemachineComponent<CinemachineTransposer>();
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                transposer.m_FollowOffset = new Vector3(0f, 2f, -5f);
                camera.DestroyCinemachineComponent<CinemachineComposer>();
                rig.Manager.RegisterCameraByType(new[]
                {
                    new CinemachineCameraManager.VirtualCameraByType { Type = CameraType.Default, VirtualCamera = camera }
                });

                GameObject host = owned.Host("Lucid auto play start", false);
                host.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(0f, 90f, 0f));
                var start = host.AddComponent<AutoPlayCameraStartPoint>();

                Call(start, "Awake");

                Assert.That((camera.transform.position - new Vector3(3f, 6f, 0f)).magnitude, Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-3f));
            });
        }
    }
}
