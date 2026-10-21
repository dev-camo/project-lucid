using System.Collections.Generic;
using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200026f. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineCameraResetYRecenter : MonoBehaviour
    {
        [SerializeField] private CinemachineFreeLook m_targetCamera;
        [SerializeField] private float m_resetValue = 0.5f;
        [SerializeField] private List<CameraType> m_cameraTypesToAllowsResetFrom;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private bool m_allowResetOnCameraLive = true;
        private List<ICinemachineCamera> m_virtualCamerasToAlwaysResetFrom = new List<ICinemachineCamera>();

        // 0x06000da4
        private void OnEnable()
        {
            m_cameraManagerRef.InvokeOnValid(CameraManagerReady);
            m_levelManagerRef.InvokeOnValid(LevelManagerReady);
        }

        // 0x06000da5
        private void LevelManagerReady(LevelManager levelManager)
        {
            levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        // 0x06000da6: every activated level starts the free-look Y axis from the reset value.
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            m_targetCamera.m_YAxis.Value = m_resetValue;
        }

        // 0x06000da7
        private void OnDisable()
        {
            if (m_cameraManagerRef.IsValid())
            {
                CinemachineCameraManager cameraManager = m_cameraManagerRef.Get();
                cameraManager.OnCameraLive -= OnCameraLive;
                cameraManager.OnResetYAxisRecenter -= ResetYAxisValue;
            }
            if (m_levelManagerRef.IsValid())
                m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
        }

        // 0x06000da8: also remembers the virtual cameras of the listed types as always-reset sources.
        private void CameraManagerReady(CinemachineCameraManager cameraManager)
        {
            cameraManager.OnCameraLive += OnCameraLive;
            cameraManager.OnResetYAxisRecenter += ResetYAxisValue;
            foreach (CameraType cameraType in m_cameraTypesToAllowsResetFrom)
                m_virtualCamerasToAlwaysResetFrom.Add(cameraManager.GetCameraOfType(cameraType).VirtualCamera);
        }

        // 0x06000da9: reset when the target camera goes live, if allowed or coming from a listed camera.
        public void OnCameraLive(ICinemachineCamera activeCamera, ICinemachineCamera previousCamera)
        {
            if ((ICinemachineCamera)m_targetCamera != activeCamera) return;
            if (m_allowResetOnCameraLive || m_virtualCamerasToAlwaysResetFrom.Contains(previousCamera))
                m_targetCamera.m_YAxis.Value = m_resetValue;
        }

        // 0x06000daa
        public void Action_SetAllowResetOnCameraLive(bool value) { m_allowResetOnCameraLive = value; }

        // 0x06000dab
        private void ResetYAxisValue() { m_targetCamera.m_YAxis.Value = m_resetValue; }
    }
}
