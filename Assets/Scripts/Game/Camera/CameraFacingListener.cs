using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000257. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraFacingListener : MonoBehaviour
    {
        [SerializeField] private Transform m_lookTarget;
        [Delayed, Range(-1f, 1f), Tooltip("Minimum alignment the camera must be to trigger, where 1 is perfect alignment towards, -1 is perfect alignment away, and 0 is perpendicular."), SerializeField]
        private float m_alignmentThresholdEnter;
        [Tooltip("Camera alignment less than this will exit the trigger. Must be less than or equal to the enter threshold."), SerializeField, Range(-1f, 1f), Delayed]
        private float m_alignmentThresholdLeave;
        [SerializeField] private UnityEvent m_onCameraFacingEnter;
        [SerializeField] private UnityEvent m_onCameraFacingStay;
        [SerializeField] private UnityEvent m_onCameraFacingLeave;
        private bool m_entered;
        private SystemRef<CharacterManager> m_characterManagerRef;
        private SystemRef<CinemachineCameraManager> m_cameraManagerRef;

        // 0x06000cb9
        private void Awake()
        {
            m_entered = false;
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        }

        // 0x06000cba
        private void OnEnable() { m_entered = false; }

        // 0x06000cbb: compares the camera's forward with the direction from the character to the look target.
        private void Update()
        {
            if (m_characterManagerRef.IsNull() || m_cameraManagerRef.IsNull()
                || !m_characterManagerRef.Get().CharacterValid || m_lookTarget == null)
            {
                SetEntered(false);
                return;
            }

            bool wasEntered = m_entered;
            CinemachineCameraManager cameraManager = m_cameraManagerRef.Get();
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            Vector3 toTarget = m_lookTarget.position - character.WorldPosition;
            Vector3 cameraForward = cameraManager.CinemachineBrain.OutputCamera.transform.forward;
            float alignment = Vector3.Dot(cameraForward, toTarget.normalized);

            if (alignment >= m_alignmentThresholdEnter) SetEntered(true);
            else if (alignment < m_alignmentThresholdLeave) SetEntered(false);

            if (wasEntered && m_entered) m_onCameraFacingStay.Invoke();
        }

        // 0x06000cbc: raise enter or leave only on a change of state.
        private void SetEntered(bool entered)
        {
            if (m_entered == entered) return;
            m_entered = entered;
            if (entered) m_onCameraFacingEnter.Invoke();
            else m_onCameraFacingLeave.Invoke();
        }

        // 0x06000cbd: the native body only queries CanValidate and keeps no result.
        private void OnValidate() => gameObject.CanValidate();

        // 0x06000cbe
        private void Reset()
        {
            if (m_lookTarget == null) m_lookTarget = transform;
        }
    }
}
