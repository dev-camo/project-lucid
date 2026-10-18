using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 02000965; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameInputButtonClick : MonoBehaviour
    {
        [SerializeField] private Button m_buttonToClick;
        [SerializeField] private GameInput m_inputToListenFor;
        [SerializeField] private InputTrigger m_inputTrigger = InputTrigger.Down;
        public readonly Bindable<GameInput> Input = new Bindable<GameInput>();
        private bool m_inputOverridden;

        // 06003601 (1.10.1 RVA 0x689BC0): an override set before enabling survives it.
        private void OnEnable()
        {
            if (!m_inputOverridden) Input.Value = m_inputToListenFor;
            ControlMapping.Subscribe(Input.Value, OnInput, m_inputTrigger, -1);
        }

        // 06003602 (1.10.1 RVA 0x689CA0)
        private void OnDisable()
        {
            ControlMapping.Unsubscribe(Input.Value, OnInput, -1);
        }

        // 06003603 (1.10.1 RVA 0x689D60)
        private void OnInput(float input)
        {
            m_buttonToClick.onClick.Invoke();
        }

        // 06003604 (1.10.1 RVA 0x689D80): input 0 clears the override and restores the serialized input.
        // The subscriptions move even while this component is disabled.
        public void SetInput(GameInput input)
        {
            if (Input.Value == input) return;
            m_inputOverridden = input != 0;
            if (!m_inputOverridden) input = m_inputToListenFor;
            ControlMapping.Unsubscribe(Input.Value, OnInput, -1);
            Input.Value = input;
            ControlMapping.Subscribe(Input.Value, OnInput, m_inputTrigger, -1);
        }

        // 06003605 (1.10.1 RVA 0x689EE0)
        public GameInputButtonClick()
        {
        }
    }
}
