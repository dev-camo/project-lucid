using System.Collections;
using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200028d. Registers its virtual camera with the Y axis proxy
    // manager for gimbal lock handling once that manager is available.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineRegisterForGimbalLock : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCameraBase m_virtualCamera;

        // 0x06000e28; the iterator is 0x0200028f with its display class 0x0200028e.
        private IEnumerator Start()
        {
            SystemRef<CameraYAxisProxyManager> cameraYAxisProxyManagerRef =
                ProcessManager.GetSystemRef<CameraYAxisProxyManager>(null, true);
            yield return new WaitUntil(() => cameraYAxisProxyManagerRef.IsValid());
            cameraYAxisProxyManagerRef.Get().RegisterGimbalLockCamera(m_virtualCamera);
        }

        // 0x06000e29
        public CinemachineRegisterForGimbalLock() { }
    }
}
