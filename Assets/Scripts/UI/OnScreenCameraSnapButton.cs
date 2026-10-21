using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200096e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OnScreenCameraSnapButton : MonoBehaviour
    {
        [SerializeField] private UIButton m_button;
        private SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
        // Declared by the original but never read or written by any of its methods.
        private bool m_wasInteractable;

        // 0600364c (1.10.1 RVA 0x68C8C0)
        private void OnEnable()
        {
            m_cameraManagerRef.InvokeOnValid(OnCameraManagerReady);
        }

        // 0600364d (RVA 0x68C950): listens on the brain's camera-activated event (field 0x68 of the brain).
        private void OnCameraManagerReady(CinemachineCameraManager cameraManager)
        {
            cameraManager.CinemachineBrain.m_CameraActivatedEvent.AddListener(OnCameraTypeChanged);
        }

        // 0600364e (RVA 0x68C9F0)
        private void OnDisable()
        {
            if (m_cameraManagerRef.IsNull()) return;
            m_cameraManagerRef.Get().CinemachineBrain.m_CameraActivatedEvent.RemoveListener(OnCameraTypeChanged);
        }

        // 0600364f (RVA 0x68CAD0)
        private void OnCameraTypeChanged(ICinemachineCamera incomingCamera, ICinemachineCamera outgoingCamera)
        {
            m_button.Interactable = m_cameraManagerRef.Get().LiveCameraIsUsingSnapExtension();
        }

        // 06003650 (RVA 0x68CB30)
        public OnScreenCameraSnapButton()
        {
        }
    }
}
