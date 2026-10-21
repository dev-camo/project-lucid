using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000296. Keeps the camera manager's proxy target in the target
    // group while enabled, joining as soon as the camera manager starts up.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineTargetGroupAssigner_CameraProxy : CinemachineTargetGroupAssigner
    {
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // 0x06000e51; x86_64 0x725300.
        private void OnEnable()
        {
            m_cameraManagerRef.OnSystemStartup += OnCameraManagerValid;
            if (m_cameraManagerRef.IsValid())
                AddMember(m_cameraManagerRef.Get().ProxyTarget.transform);
        }

        // 0x06000e52; x86_64 0x725440. The startup subscription is only removed while the manager is
        // still valid.
        private void OnDisable()
        {
            if (!m_cameraManagerRef.IsValid()) return;
            CinemachineCameraManager cameraManager = m_cameraManagerRef.Get();
            if (cameraManager.ProxyTarget != null) RemoveMember(cameraManager.ProxyTarget.transform);
            m_cameraManagerRef.OnSystemStartup -= OnCameraManagerValid;
        }

        // 0x06000e53; x86_64 0x725400.
        private void OnCameraManagerValid(CinemachineCameraManager cameraManager)
        {
            AddMember(cameraManager.ProxyTarget.transform);
        }

        // 0x06000e54; the manager reference is resolved before the base constructor.
        public CinemachineTargetGroupAssigner_CameraProxy() { }
    }
}
