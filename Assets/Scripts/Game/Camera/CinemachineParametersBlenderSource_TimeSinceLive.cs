using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000281.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineParametersBlenderSource_TimeSinceLive : CinemachineParametersBlenderSource
    {
        [Min(0.01f)]
        [SerializeField]
        private float m_maxTimeSeconds = 1f;
        [SerializeField]
        private CinemachineVirtualCameraBase m_virtualCamera;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
        private float m_timeSecondsSinceActive;

        // 0x06000de4: the timer runs only while the camera is live and restarts from zero otherwise.
        protected override void InternalUpdate(float deltaTime)
        {
            if (m_cameraManagerRef.IsNull() ||
                !m_cameraManagerRef.Get().CinemachineBrain.IsLive(m_virtualCamera, false))
            {
                m_timeSecondsSinceActive = 0f;
                return;
            }
            m_timeSecondsSinceActive += deltaTime;
            UpdateBlendValue(m_timeSecondsSinceActive / m_maxTimeSeconds, deltaTime);
        }

        // 0x06000de5
        protected override void Reset()
        {
            base.Reset();
            m_virtualCamera = GetComponent<CinemachineVirtualCameraBase>();
        }

        // 0x06000de6
        public CinemachineParametersBlenderSource_TimeSinceLive() { }
    }
}
