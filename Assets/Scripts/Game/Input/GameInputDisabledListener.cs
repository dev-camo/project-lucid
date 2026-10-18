using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 020006a1; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameInputDisabledListener : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("GameInputs to listen for the disabling of.")]
        private GameInput[] m_gameInputs;
        [SerializeField]
        [Tooltip("Triggered when all the inputs on the above list become disabled, and if the inputs are already all disabled when this object is enabled.")]
        private UnityEvent m_onInputsDisabled;
        [Tooltip("Triggered when all the inputs on the above list were disabled and one becomes enabled, and if any of the inputs are already enabled when this object is enabled.")]
        [SerializeField]
        private UnityEvent m_onInputsEnabled;
        private ControlMapping m_controlMapping;
        private bool m_inputsAllDisabled;

        // 06002388 (1.10.1 RVA 0x5884A0)
        private void Awake()
        {
            m_controlMapping = ProcessManager.GetSystem<InputSystem>().ControlMapping;
        }

        // 06002389 (1.10.1 RVA 0x588520): subscribes without an immediate callback, then reports the current state.
        private void OnEnable()
        {
            foreach (GameInput gameInput in m_gameInputs)
                m_controlMapping.SubscribeInputDisabled(gameInput, OnInputDisabled, false);

            m_inputsAllDisabled = AllInputsDisabled();
            if (m_inputsAllDisabled) m_onInputsDisabled.Invoke();
            else m_onInputsEnabled.Invoke();
        }

        // 0600238a (1.10.1 RVA 0x588690)
        private void OnDisable()
        {
            foreach (GameInput gameInput in m_gameInputs)
                m_controlMapping.UnsubscribeInputDisabled(gameInput, OnInputDisabled);

            if (m_inputsAllDisabled) m_onInputsEnabled.Invoke();
            m_inputsAllDisabled = false;
        }

        // 0600238b (1.10.1 RVA 0x588640): an empty list counts as all disabled.
        private bool AllInputsDisabled()
        {
            foreach (GameInput gameInput in m_gameInputs)
            {
                if (!m_controlMapping.IsGameInputDisabled(gameInput)) return false;
            }
            return true;
        }

        // 0600238c (1.10.1 RVA 0x588760): both arguments are ignored; the whole list is re-read.
        // Each event fires only on a change, before the stored state is updated.
        private void OnInputDisabled(GameInput gameInput, bool disabled)
        {
            if (AllInputsDisabled())
            {
                if (m_inputsAllDisabled) return;
                m_onInputsDisabled.Invoke();
                m_inputsAllDisabled = true;
            }
            else
            {
                if (!m_inputsAllDisabled) return;
                m_onInputsEnabled.Invoke();
                m_inputsAllDisabled = false;
            }
        }

        // 0600238d (1.10.1 RVA 0x5887E0)
        public GameInputDisabledListener()
        {
        }
    }
}
