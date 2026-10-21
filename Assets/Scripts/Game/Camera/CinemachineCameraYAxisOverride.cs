using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000270. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineCameraYAxisOverride : MonoBehaviour
    {
        [Tooltip("The target camera to apply the y-axis override to."), SerializeField]
        private CinemachineFreeLook m_targetCamera;
        [Tooltip("Settings for overriding the camera y-axis responsiveness state."), SerializeField]
        private CameraAxisStateOverrides m_yAxisStateOverrides;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);

        // 0x06000dad
        private void Awake() { m_cameraManagerRef.InvokeOnValid(OnCameraManagerReady); }

        // 0x06000dae: subscribing also applies the current sensitivity right away.
        private void OnCameraManagerReady(CinemachineCameraManager cameraManager)
        {
            cameraManager.InvokeOnSensitivityChanged(OnCameraSensitivityChanged);
        }

        // 0x06000daf: the override result is not used.
        private void OnCameraSensitivityChanged(CameraSensitivityType cameraSensitivity)
        {
            m_yAxisStateOverrides.SetOverride(cameraSensitivity, ref m_targetCamera.m_YAxis);
        }

        // 0x06000db0
        private void OnDestroy()
        {
            if (m_cameraManagerRef.IsValid())
                m_cameraManagerRef.Get().RemoveSensitivityChangedAction(OnCameraSensitivityChanged);
        }
    }
}
