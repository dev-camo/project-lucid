using System.Collections;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 02000962; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameActionButtonClick : MonoBehaviour
    {
        [SerializeField] private Button m_buttonToClick;
        [SerializeField] private GameAction m_actionToListenFor;
        [SerializeField] private GameAction m_actionComboToListenFor;
        [SerializeField] private float m_minimumTime;
        [Tooltip("When game action is triggered, inputs are locked for this period of time to ensure game logic also receives them.")]
        [SerializeField]
        private float m_inputLockTime = 0.1f;
        public readonly Bindable<GameAction> Action = new Bindable<GameAction>(GameAction.None);
        public readonly Bindable<GameAction> ActionCombo = new Bindable<GameAction>(GameAction.None);
        public readonly Bindable<float> MinimumTime = new Bindable<float>();
        private bool m_actionOverridden;
        private Coroutine m_updateCoroutine;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 060035ed (1.10.1 RVA 0x689020): StartUpdateCoroutine is inlined.
        private void OnEnable()
        {
            if (!m_actionOverridden)
            {
                Action.Value = m_actionToListenFor;
                ActionCombo.Value = m_actionComboToListenFor;
                MinimumTime.Value = m_minimumTime;
            }
            if (Action.Value != GameAction.None) StartUpdateCoroutine();
        }

        // 060035ee (1.10.1 RVA 0x6891C0)
        private void OnDisable()
        {
            StopUpdateCoroutine();
        }

        // 060035ef (1.10.1 RVA 0x689240): None clears the override and restores the serialized action.
        public void SetAction(GameAction action)
        {
            if (Action.Value != action)
            {
                m_actionOverridden = action != GameAction.None;
                if (!m_actionOverridden) action = m_actionToListenFor;
                Action.Value = action;
            }
            if (Action.Value != GameAction.None) StartUpdateCoroutine();
            else StopUpdateCoroutine();
        }

        // 060035f0 (1.10.1 RVA 0x689350): neither marks an override nor restarts the coroutine.
        public void SetActionCombo(GameAction actionCombo)
        {
            if (ActionCombo.Value == actionCombo) return;
            if (actionCombo == GameAction.None) actionCombo = m_actionComboToListenFor;
            ActionCombo.Value = actionCombo;
        }

        // 060035f1 (1.10.1 RVA 0x6893A0)
        public void SetMinimumTime(float minimumTime)
        {
            MinimumTime.Value = minimumTime;
        }

        // 060035f2 (1.10.1 RVA 0x689120)
        private void StartUpdateCoroutine()
        {
            if (m_updateCoroutine == null) m_updateCoroutine = StartCoroutine(UpdateCoroutine());
        }

        // 060035f3 (1.10.1 RVA 0x6893C0); MoveNext 060035f9 (0x689660) inlines GetBrainState.
        // Waits for the action to be released, holds for MinimumTime, then clicks on the next press.
        private IEnumerator UpdateCoroutine()
        {
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) yield break;
            yield return null;
            while (GetBrainState(character)) yield return null;
            if (MinimumTime.Value > 0f) yield return new WaitForSeconds(MinimumTime.Value);
            while (!GetBrainState(character)) yield return null;
            if (m_inputLockTime > 0f) character.LockBrain(m_inputLockTime);
            m_updateCoroutine = null;
            m_buttonToClick.onClick.Invoke();
        }

        // 060035f4 (1.10.1 RVA 0x689450): with a combo set, both raw states must be held.
        private bool GetBrainState(Character character)
        {
            character.CacheBrainMovement();
            bool hasCombo = ActionCombo.Value != GameAction.None;
            bool actionHeld = character.GetBrainRawState(Action.Value);
            if (!hasCombo) return actionHeld;
            return actionHeld && character.GetBrainRawState(ActionCombo.Value);
        }

        // 060035f5 (1.10.1 RVA 0x689200)
        private void StopUpdateCoroutine()
        {
            if (m_updateCoroutine == null) return;
            StopCoroutine(m_updateCoroutine);
            m_updateCoroutine = null;
        }

        // 060035f6 (1.10.1 RVA 0x6894F0)
        public GameActionButtonClick()
        {
        }
    }
}
