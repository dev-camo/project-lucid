using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 02000ac0 with iterator 02000ac1; bodies translated from the 1.10.1 x86-64
    // binary. A trigger volume that hands the entering character to its CatchRelease ability, either at
    // once or, when the attach needs input, once the input has been held for the frames after entry.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    [SelectionBase]
    public class CatchReleaseTrigger : CharacterTrigger
    {
        [Tooltip("Definition data sent to the ability to control state logic.")]
        [SerializeField]
        [FormerlySerializedAs("Data")]
        private CatchReleaseDefinition m_data = new CatchReleaseDefinition();
        [SerializeField]
        [Tooltip("Events invoked on level activated.")]
        private UnityEvent m_onLevelReset = new UnityEvent();
        private CharacterAbility_CatchRelease m_ability;
        private Coroutine m_stayCoroutine;
        private readonly WaitForFixedUpdate m_waitForFixedUpdate = new WaitForFixedUpdate();
        private const int WaitNumberOfFramesBeforeTrigger = 2;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);

        // 06003dfd (1.10.1 RVA 0x6E5500)
        protected override void Awake()
        {
            base.Awake();
            m_levelManagerRef.Get().InvokeOnLevelActivated(OnLevelActivated, true);
        }

        // 06003dfe (1.10.1 RVA 0x6E55B0)
        private void OnDestroy()
        {
            if (m_levelManagerRef.IsValid())
                m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
        }

        // 06003dff (1.10.1 RVA 0x6E5680). The ability is dropped after it is told to reset.
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            if (m_ability != null)
                m_ability.DoTriggerReset(m_data, m_collider);
            m_ability = null;
            m_onLevelReset.Invoke();
        }

        // 06003e00 (1.10.1 RVA 0x6E5710)
        private static bool CharacterIsValid(Character character)
        {
            return character.IsActive() && !character.SwapOutSequenceIsInProgress();
        }

        // 06003e01 (1.10.1 RVA 0x6E5750). The native body inlines CharacterIsValid. The ability is kept in
        // m_ability even when it is already triggered.
        protected override void DoTriggerEnter(Character character)
        {
            base.DoTriggerEnter(character);
            if (!CharacterIsValid(character))
                return;
            if (!character.TryGetAbility(ActorAbilityType.Character_CatchRelease, out m_ability))
                return;
            if (m_ability.IsTriggered)
                return;
            if (m_data.Attach.InputRequired)
            {
                this.SafeStopCoroutine(ref m_stayCoroutine);
                m_stayCoroutine = StartCoroutine(ProcessStayEvents(character));
            }
            else
            {
                m_ability.DoTriggerEnter(m_data, m_collider);
            }
        }

        // 06003e02 (1.10.1 RVA 0x6E5920): a character that stays in the volume re-enters it, unless the
        // input wait is already running.
        protected override void DoTriggerStay(Character character)
        {
            base.DoTriggerStay(character);
            if (m_stayCoroutine != null || !CharacterIsValid(character))
                return;
            DoTriggerEnter(character);
        }

        // 06003e03 (1.10.1 RVA 0x6E58A0) with iterator 02000ac1 (MoveNext 0x6E5B80). Waits out the frames
        // before the trigger, then for the attach input, attaches, and (for a continuous input) releases
        // when the input ends. A character that disappears stops the wait without releasing.
        private IEnumerator ProcessStayEvents(Character character)
        {
            for (int frameCount = 0; frameCount < WaitNumberOfFramesBeforeTrigger; frameCount++)
                yield return null;
            while (character != null && !character.GetBrainAppliedState(m_data.Attach.InputGameAction))
                yield return m_waitForFixedUpdate;
            if (character != null)
            {
                m_ability.DoTriggerEnter(m_data, m_collider);
                yield return m_waitForFixedUpdate;
            }
            if (character == null || !m_data.Attach.InputRequiredIsContinuous)
            {
                this.SafeStopCoroutine(ref m_stayCoroutine);
                yield break;
            }
            while (character != null && character.GetBrainAppliedState(m_data.Attach.InputGameAction))
                yield return m_waitForFixedUpdate;
            this.SafeStopCoroutine(ref m_stayCoroutine);
            if (m_ability != null)
                m_ability.DoTriggerExit();
        }

        // 06003e04 (1.10.1 RVA 0x6E59A0)
        protected override void DoTriggerExit(Character character)
        {
            base.DoTriggerExit(character);
            this.SafeStopCoroutine(ref m_stayCoroutine);
            if (m_ability != null)
                m_ability.DoTriggerExit();
        }

        // 06003e05 (1.10.1 RVA 0x6E59E0)
        public void OnEndReached()
        {
            this.SafeStopCoroutine(ref m_stayCoroutine);
            if (m_ability != null)
                m_ability.DoTriggerExit();
        }

        // 06003e06 (1.10.1 RVA 0x6E56D0)
        public void OnReset()
        {
            if (m_ability != null)
                m_ability.DoTriggerReset(m_data, m_collider);
            m_ability = null;
        }

        // 06003e07 (1.10.1 RVA 0x6E5A20)
        public void SnapToAttachPosition()
        {
            if (m_ability != null)
                m_ability.SnapToAttachPosition();
        }

        // 06003e08 (1.10.1 RVA 0x6E5A40): field initialisers (data, reset event, fixed-update wait, level
        // manager reference) run before the base constructor.
        public CatchReleaseTrigger() { }
    }
}
