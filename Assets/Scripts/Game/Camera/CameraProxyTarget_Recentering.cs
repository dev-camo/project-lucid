using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000261. A proxy target that follows the live free-look camera's
    // manual X axis heading, tracked whenever the live camera follows this proxy.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraProxyTarget_Recentering : CameraProxyTarget
    {
        private CinemachineRecenterHeadingExtension_FreeLookManualXAxis m_headingExtensionFreeLook;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private bool m_hasHeadingExtension;

        // 0x06000d0c; x86_64 0x7146D0.
        protected override void Awake()
        {
            base.Awake();
            m_cameraManagerRef.InvokeOnValid(OnCameraManagerValid);
        }

        // 0x06000d0d; x86_64 0x714790.
        public override void OnDestroy()
        {
            if (m_cameraManagerRef.IsValid())
                m_cameraManagerRef.Get().OnCameraLive -= OnCameraLive;
            base.OnDestroy();
        }

        // 0x06000d0e; x86_64 0x7148D0. Subscribe first, then adopt the camera that is already live.
        private void OnCameraManagerValid(CinemachineCameraManager cameraManager)
        {
            cameraManager.OnCameraLive += OnCameraLive;
            TrySetHeadingExtension(cameraManager.CinemachineBrain.ActiveVirtualCamera);
        }

        // 0x06000d0f; x86_64 0x714B90.
        private void OnCameraLive(ICinemachineCamera incomingCamera, ICinemachineCamera previousCamera)
        {
            TrySetHeadingExtension(incomingCamera);
        }

        // 0x06000d10; x86_64 0x7149E0. Only a camera that follows this proxy supplies the extension,
        // and a previously found extension is kept when the new camera has none.
        private void TrySetHeadingExtension(ICinemachineCamera cinemachineCamera)
        {
            if (cinemachineCamera == null) return;
            if (cinemachineCamera.Follow != transform) return;
            if (cinemachineCamera.VirtualCameraGameObject.TryGetComponent(
                out CinemachineRecenterHeadingExtension_FreeLookManualXAxis extension))
            {
                m_headingExtensionFreeLook = extension;
                m_hasHeadingExtension = true;
            }
        }

        // 0x06000d11; x86_64 0x714BA0. Without a heading extension nothing is updated at all.
        protected override void UpdateSmoothedTransformProxy(Character character, float deltaTime)
        {
            if (!m_hasHeadingExtension) return;
            Quaternion targetRotation = m_settings.GetWorldRotation(character);
            UpdateSmoothedTransformProxy(m_currentTargetPosition, targetRotation, deltaTime);
        }

        // 0x06000d12; x86_64 0x714C00. The extension's X axis value is the proxy's forward angle.
        protected override void UpdateSmoothedTransformProxy(Vector3 targetPosition, Quaternion targetRotation,
            float deltaTime)
        {
            m_smoothedTransformProxy.UpdatePositionAndRotation(targetPosition, targetRotation,
                m_headingExtensionFreeLook.XAxisState.Value, deltaTime);
        }

        // 0x06000d13; the manager reference is resolved before the base constructor (whose CharacterManager initializer is inlined).
        public CameraProxyTarget_Recentering() { }
    }
}
