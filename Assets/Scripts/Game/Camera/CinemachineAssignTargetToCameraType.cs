using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000269. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineAssignTargetToCameraType : MonoBehaviour
    {
        [SerializeField] private CameraType m_cameraType;
        [SerializeField] private Transform m_target;
        [SerializeField] private bool m_setAsLookAtTarget;
        [SerializeField] private bool m_setAsFollowTarget;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // 0x06000d49: follow the manager's startup, and handle a manager that already started.
        private void OnEnable()
        {
            m_cameraManagerRef.OnSystemStartup += OnCameraManagerValid;
            if (m_cameraManagerRef.IsValid()) OnCameraManagerValid(m_cameraManagerRef.Get());
        }

        // 0x06000d4a
        private void OnDisable()
        {
            if (m_cameraManagerRef.IsValid()) m_cameraManagerRef.OnSystemStartup -= OnCameraManagerValid;
        }

        // 0x06000d4b: targets can only be assigned once the main camera exists.
        private void OnCameraManagerValid(CinemachineCameraManager cameraManager)
        {
            cameraManager.OnMainCameraReady += OnMainCameraReady;
            if (cameraManager.MainCameraIsReady) OnMainCameraReady(cameraManager.MainCamera);
        }

        // 0x06000d4c: a one-shot assignment; the unused flag simply leaves that target empty.
        private void OnMainCameraReady(Camera _)
        {
            CinemachineCameraManager cameraManager = m_cameraManagerRef.Get();
            cameraManager.OnMainCameraReady -= OnMainCameraReady;
            Transform followTarget = m_setAsFollowTarget ? m_target : null;
            Transform lookAtTarget = m_setAsLookAtTarget ? m_target : null;
            cameraManager.SetCameraTargetsForType(m_cameraType, followTarget, lookAtTarget);
        }
    }
}
