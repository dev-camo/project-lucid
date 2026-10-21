using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200073e. Snaps the gameplay camera to the character's heading
    // when the character enters and/or exits, optionally after a delay.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SnapCameraToCharacterHeadingTrigger : CharacterTrigger
    {
        [SerializeField]
        private float m_snapDelaySeconds = 0.2f;
        [SerializeField]
        private bool m_allowStickyControls;
        [SerializeField]
        private bool m_triggerOnEnter = true;
        [SerializeField]
        private bool m_triggerOnExit;
        private Coroutine m_delayRoutine;

        // 0x060028c9; x86_64 0x5CB180.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            if (m_triggerOnEnter) DoTrigger();
        }

        // 0x060028ca; x86_64 0x5CB280.
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            if (m_triggerOnExit) DoTrigger();
        }

        // 0x060028cb; x86_64 0x5CB1B0. A new request replaces a pending delayed snap.
        private void DoTrigger()
        {
            if (m_snapDelaySeconds > 0f)
            {
                this.SafeStopCoroutine(ref m_delayRoutine);
                m_delayRoutine = CoroutineUtils.Delay(SnapToCharacterHeading, m_snapDelaySeconds);
            }
            else SnapToCharacterHeading();
        }

        // 0x060028cc; x86_64 0x5CB2B0. m_allowStickyControls is passed as triggerTurnCamera; forceStickyControls stays false.
        private void SnapToCharacterHeading()
        {
            SystemRef<CinemachineCameraManager> cameraManagerRef =
                ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
            if (cameraManagerRef.IsNull()) return;
            cameraManagerRef.Get().SnapToCharacter(m_allowStickyControls, false);
        }

        // 0x060028cd
        public SnapCameraToCharacterHeadingTrigger() { }
    }
}
