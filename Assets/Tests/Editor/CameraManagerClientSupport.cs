using System.Collections.Generic;
using Cinemachine;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // A real CinemachineCameraManager for the components that talk to it (camera type volumes and manipulators,
    // target assignment, y-axis recentering and the camera heading volume). The manager is a component on an owned
    // inactive host, so no Awake runs; it has a brain on a real Camera and an empty camera list, to which cameras are
    // added through its own RegisterCameraByType. The camera list is installed through the public
    // ResetAndPopulateCameraTypeLookup, from the configuration the manager holds for CameraConfigurationType.None.
    internal static class CameraManagerClientSupport
    {
        internal sealed class Rig
        {
            internal CinemachineCameraManager Manager;
            internal Camera Camera;
            internal CinemachineBrain Brain;
            internal List<CinemachineCameraManager.VirtualCameraByType> Cameras;
        }

        // The manager's field initialisers take the SaveManager and CameraStackManager references, so those rows are
        // leased. A case that already registered a SaveManager (CharacterTriggerTestSupport.WithManager) passes
        // leaseSaveRow false. The manager is a TimeScaledComponent_SDT, whose type initialiser takes the TimeManager
        // row, so that row is leased first unless the case handled it itself (leaseTimeRow false, for a case that
        // registers a TimeManager). A registered manager goes into its absent row before any component caches the
        // reference.
        internal static Rig NewRig(Owned owned, bool leaseSaveRow, bool registerManager, bool leaseTimeRow = true)
        {
            if (leaseTimeRow) owned.LeaseRow<TimeManager>();
            if (leaseSaveRow) owned.LeaseRow<SaveManager>();
            owned.LeaseRow<CameraStackManager>();
            var rig = new Rig();
            // AddComponent sends the Editor Reset to this TimeScaledComponent_SDT, which needs an owned empty lookup.
            rig.Manager = EnemyAbilityTestSupport.AddTimeScaled<CinemachineCameraManager>(owned.Host("Lucid camera manager", false));
            GameObject cameraHost = owned.Host("Lucid output camera", false);
            rig.Camera = cameraHost.AddComponent<Camera>();
            rig.Brain = cameraHost.AddComponent<CinemachineBrain>();
            // The brain is an authored, serialized reference with no setter.
            Set(rig.Manager, "m_cinemachineBrain", rig.Brain);
            rig.Cameras = new List<CinemachineCameraManager.VirtualCameraByType>();
            Get<SerializableDictionary<CameraConfigurationType, CinemachineCameraManager.VirtualCamerasBySetting>>(
                    rig.Manager, "m_virtualCamerasBySetting")
                .Add(CameraConfigurationType.None, new CinemachineCameraManager.VirtualCamerasBySetting { VirtualCamerasByType = rig.Cameras });
            rig.Manager.ResetAndPopulateCameraTypeLookup();
            if (registerManager) owned.Register(rig.Manager);
            return rig;
        }

        // A virtual camera of the given type and authored priority. Registering it records the priority as its starting
        // priority and, with nothing on the manager's camera stack yet, drops the camera to -1.
        internal static CinemachineMixingCamera AddCamera(Owned owned, Rig rig, HardlightProject.CameraType type, int priority)
        {
            var camera = owned.Host("Lucid " + type + " camera", false).AddComponent<CinemachineMixingCamera>();
            camera.Priority = priority;
            rig.Manager.RegisterCameraByType(new[]
            {
                new CinemachineCameraManager.VirtualCameraByType { Type = type, VirtualCamera = camera }
            });
            return camera;
        }
    }
}
