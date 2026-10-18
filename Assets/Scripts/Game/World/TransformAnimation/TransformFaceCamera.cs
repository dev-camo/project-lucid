using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200080e: looks at the main camera every LateUpdate,
    // and stays disabled while there is no camera manager or main camera.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransformFaceCamera : MonoBehaviour
    {
        private Transform m_mainCameraTransform;
        private SystemRef<CinemachineCameraManager> m_cameraManagerRef;

        private void OnEnable()
        {
            m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
            m_cameraManagerRef.InvokeOnValid(OnCameraInitialised);
            if (m_cameraManagerRef.IsNull())
                enabled = false;
        }

        private void OnDisable()
        {
            RemoveCameraShutdownReference();
        }

        // A camera that is not ready yet is taken from OnMainCameraReady.
        private void OnCameraInitialised(CinemachineCameraManager cameraManager)
        {
            cameraManager.OnSystemShutdown += OnCameraManagerShutdown;
            if (cameraManager.MainCameraIsReady)
                SetMainCamera(cameraManager.MainCamera);
            else
                cameraManager.OnMainCameraReady += SetMainCamera;
        }

        private void SetMainCamera(Camera mainCamera)
        {
            enabled = true;
            m_mainCameraTransform = mainCamera.transform;
            m_cameraManagerRef.Get().OnMainCameraReady -= SetMainCamera;
        }

        private void OnCameraManagerShutdown()
        {
            if (this != null)
                enabled = false;
            RemoveCameraShutdownReference();
        }

        private void RemoveCameraShutdownReference()
        {
            if (m_cameraManagerRef.IsNull())
                return;
            m_cameraManagerRef.Get().OnSystemShutdown -= OnCameraManagerShutdown;
        }

        private void LateUpdate()
        {
            transform.LookAt(m_mainCameraTransform);
        }
    }
}
