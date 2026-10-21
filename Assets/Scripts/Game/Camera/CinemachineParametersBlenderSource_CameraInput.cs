using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200027d.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineParametersBlenderSource_CameraInput : CinemachineParametersBlenderSource
    {
        [SerializeField]
        [Tooltip("If true, the blend value will be either 1 or 0 based on whether there is or is not camera input.")]
        private bool m_binaryValue;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();

        // 0x06000ddc: without the camera manager the blend value is left untouched.
        protected override void InternalUpdate(float deltaTime)
        {
            if (m_cameraManagerRef.IsNull()) return;
            Vector2 input = m_cameraManagerRef.Get().ControllerMovement;
            UpdateBlendValue(m_binaryValue ? (input.sqrMagnitude > 0f ? 1f : 0f) : input.magnitude, deltaTime);
        }

        // 0x06000ddd
        public CinemachineParametersBlenderSource_CameraInput() { }
    }
}
