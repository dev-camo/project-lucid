using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000273.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineControllerMovementProvider : MonoBehaviour, AxisState.IInputAxisProvider
    {
        [SerializeField]
        private bool m_xAxisEnabled = true;
        [SerializeField]
        private bool m_yAxisEnabled = true;
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();

        // 0x06000db9: a disabled axis and the unused third axis read zero. A negative axis reaches the
        // Vector2 indexer, which throws, after the camera manager has been fetched.
        public float GetAxisValue(int axis)
        {
            if ((axis == 0 && !m_xAxisEnabled) || (axis == 1 && !m_yAxisEnabled) || axis > 1) return 0f;
            return m_cameraManagerRef.Get().ControllerMovement[axis];
        }

        // 0x06000dba
        public CinemachineControllerMovementProvider() { }
    }
}
