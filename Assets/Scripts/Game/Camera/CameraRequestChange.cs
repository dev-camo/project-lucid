using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000262. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraRequestChange : MonoBehaviour
    {
        [SerializeField] private CameraType m_cameraType;
        private CinemachineCameraManager m_cinemachineCameraManger;

        // 0x06000d14: the manager is only known once its system registers.
        private void Start()
        {
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true)
                .InvokeOnValid(manager => m_cinemachineCameraManger = manager);
        }

        // 0x06000d15: switch the character camera, then snap it to the character with sticky controls.
        public void Action_RequestCameraChange()
        {
            m_cinemachineCameraManger.RequestCharacterCameraChange(m_cameraType);
            m_cinemachineCameraManger.SnapToCharacter(true, true);
        }
    }
}
