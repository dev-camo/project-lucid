using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000a69. First-time-user-experience camera section: toggles the
    // camera settings the section requires on the default camera.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FTUECameraSection : CharacterTrigger
    {
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // 0x06003bd2; x86_64 0x6C43C0. Y recentering is switched together with whether a free-look
        // camera inherits the previous camera's position.
        public void ToggleRequiredSettings(bool on)
        {
            CinemachineVirtualCameraBase virtualCamera =
                m_cameraManagerRef.Get().GetCameraOfType(CameraType.Default).VirtualCamera;
            if (virtualCamera.TryGetComponent(out CinemachineCameraResetYRecenter resetYRecenter))
                resetYRecenter.enabled = on;
            if (virtualCamera is CinemachineFreeLook freeLookCamera)
                freeLookCamera.m_Transitions.m_InheritPosition = on;
        }

        // 0x06003bd3; the manager reference is resolved before the base constructor.
        public FTUECameraSection() { }
    }
}
