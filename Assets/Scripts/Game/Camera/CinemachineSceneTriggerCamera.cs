using System.Collections;
using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000291. Raises a virtual camera's priority while the character
    // is inside the trigger, optionally leaving after a timeout and destroying itself on exit.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineSceneTriggerCamera : CharacterTrigger
    {
        private const int DisabledPriority = -1;

        [FormerlySerializedAs("virtualCam")]
        [SerializeField]
        private CinemachineVirtualCamera m_virtualCammera;
        [FormerlySerializedAs("destroyOnExit")]
        [SerializeField]
        private bool m_destroyOnExit;
        [SerializeField]
        [FormerlySerializedAs("exitAfterSeconds")]
        private float m_exitAfterSeconds = -1f;
        [SerializeField]
        private bool m_lookAtCharacter;
        [SerializeField]
        private int m_onTriggerCameraPriority = 100;
        private readonly SystemRef<LevelManager> m_levelManagerRef =
            ProcessManager.GetSystemRef<LevelManager>(null, true);

        // 0x06000e34; x86_64 0x7240E0. The camera starts disabled and is disabled again on every
        // level activation, including the one already current.
        private void Start()
        {
            DisableCamera();
            m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated));
        }

        // 0x06000e35; x86_64 0x724230.
        private void OnDestroy()
        {
            m_levelManagerRef.InvokeOnValid(levelManager => levelManager.RemoveLevelActivatedAction(OnLevelActivated));
        }

        // 0x06000e36; x86_64 0x7242C0.
        private void OnLevelActivated(LevelManagerLevel level)
        {
            DisableCamera();
        }

        // 0x06000e37; x86_64 0x7241C0.
        private void DisableCamera()
        {
            if (m_virtualCammera == null) return;
            m_virtualCammera.Priority = DisabledPriority;
        }

        // 0x06000e38; x86_64 0x724330. An exit-after of exactly -1 means the camera stays until exit.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            m_virtualCammera.Priority = m_onTriggerCameraPriority;
            if (m_lookAtCharacter) m_virtualCammera.LookAt = character.transform;
            if (m_exitAfterSeconds == -1f) return;
            StartCoroutine(OnTriggerTimeout());
        }

        // 0x06000e39; x86_64 0x724470. With a timeout configured, the timeout performs the exit instead.
        protected override void DoTriggerExit(Character character)
        {
            if (m_exitAfterSeconds != -1f) return;
            ExitCamera();
        }

        // 0x06000e3a; iterator 0x02000292.
        private IEnumerator OnTriggerTimeout()
        {
            yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(m_exitAfterSeconds, TimeCategory.Camera);
            ExitCamera();
        }

        // 0x06000e3b; x86_64 0x724500. Only a camera that is not yet disabled is disabled and destroyed.
        public void ExitCamera()
        {
            if (m_virtualCammera.Priority == DisabledPriority) return;
            m_virtualCammera.Priority = DisabledPriority;
            if (m_destroyOnExit) Destroy(gameObject);
        }

        // 0x06000e3c; the field initializers run before the base constructor.
        public CinemachineSceneTriggerCamera() { }
    }
}
