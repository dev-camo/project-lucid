using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x020001d0.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AutoPlayCameraStartPoint : MonoBehaviour
    {
        // 0x060009c1: run once the camera manager system is registered.
        private void Awake()
        {
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true).InvokeOnValid(OnCameraManagerAvailable);
        }

        // 0x060009c2: snap every virtual camera to this transform's pose, keeping the follow offset.
        private void OnCameraManagerAvailable(CinemachineCameraManager cameraManager)
        {
            cameraManager.ForceAllVirtualCamerasToPosition(transform.position, transform.rotation, true, true);
        }
    }
}
