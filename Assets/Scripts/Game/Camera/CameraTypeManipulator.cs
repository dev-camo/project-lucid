using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000263. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraTypeManipulator : MonoBehaviour
    {
        [SerializeField, Tooltip("Camera type to override on the camera manager.")]
        private CameraType m_cameraType;
        [Tooltip("Optional proxy target transform."), SerializeField]
        private Transform m_transform;
        private SystemRef<CinemachineCameraManager> m_cameraManagerRef;
        private Action m_onComplete;
        private StackableDataHandle m_stackableDataHandle;

        // 0x06000d18
        private void Awake()
        {
            m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        }

        // 0x06000d19
        public void ApplyCameraType() { ApplyCameraType(null); }

        // 0x06000d1a
        public void RemoveCameraType() { RemoveCameraType(null); }

        // 0x06000d1b: onComplete is skipped when the camera manager is gone.
        public void ApplyCameraType(Action onComplete)
        {
            CinemachineCameraManager cameraManager = m_cameraManagerRef.GetSafe();
            if (cameraManager == null) return;

            m_stackableDataHandle = cameraManager.ApplyCameraTypeOverride(m_cameraType);
            if (m_transform != null) cameraManager.SetProxyTargetTransform(m_cameraType, m_transform);
            onComplete?.Invoke();
        }

        // 0x06000d1c: the stored handle is kept after removal, so a repeat removal releases it again.
        public void RemoveCameraType(Action onComplete)
        {
            if (m_stackableDataHandle != null)
            {
                CinemachineCameraManager cameraManager = m_cameraManagerRef.GetSafe();
                if (cameraManager == null) return;

                if (m_transform != null) cameraManager.SetProxyTargetTransform(m_cameraType, null);
                cameraManager.RemoveCameraSettingOverride(m_stackableDataHandle);
            }
            onComplete?.Invoke();
        }
    }
}
