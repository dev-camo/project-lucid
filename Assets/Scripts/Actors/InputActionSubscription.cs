using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class InputActionSubscription : InputSubscription
    {
        private bool m_inputDown;
        private bool m_inputHeld;
        private static readonly SystemRef<InputSystem> m_inputSystemRef = ProcessManager.GetSystemRef<InputSystem>(null, true);

        // Original 060023bd.
        protected InputActionSubscription(GameControlsBinding binding, ValidatorFunc validator) : base(binding, validator) { }

        // Original 060023be: two static registrations precede the safe instance lookup.
        public override void Subscribe()
        {
            ControlMapping.Subscribe(GameInput, OnCallbackHeld, InputTrigger.Held, -1);
            ControlMapping.Subscribe(GameInput, OnCallbackUp, InputTrigger.Up, -1);
            InputSystem inputSystem = m_inputSystemRef.GetSafe();
            if ((object)inputSystem != null)
                inputSystem.ControlMapping.SubscribeInputDisabled(GameInput, OnInputDisabled, false);
        }

        // Original 060023bf; flags are retained across unsubscription.
        public override void Unsubscribe()
        {
            ControlMapping.Unsubscribe(GameInput, OnCallbackHeld, -1);
            ControlMapping.Unsubscribe(GameInput, OnCallbackUp, -1);
            InputSystem inputSystem = m_inputSystemRef.GetSafe();
            if ((object)inputSystem != null)
                inputSystem.ControlMapping.UnsubscribeInputDisabled(GameInput, OnInputDisabled);
        }

        // Original 060023c0: publish flag changes before the virtual callbacks.
        private void OnCallbackHeld(float value)
        {
            if (ApplyModifiers(ref value))
            {
                if (m_inputDown)
                {
                    m_inputHeld = true;
                    OnHeld(value);
                }
                else
                {
                    m_inputDown = true;
                    OnDown(value);
                }
            }
        }

        // Original 060023c1 + actual display owner 020006aa/060023c8..c9.
        private void OnCallbackUp(float value)
        {
            m_inputDown = false;
            if (m_inputHeld)
            {
                m_inputHeld = false;
                OnUp(value);
            }
            else
            {
                Hardlight.Utils.CoroutineUtils.OnNextFrame(() => OnUp(value));
            }
        }

        // Original 060023c2 ignores both arguments and retains m_inputHeld.
        private void OnInputDisabled(GameInput gameInput, bool disabled)
        {
            m_inputDown = false;
            OnDisabled();
        }

        protected abstract void OnDown(float value); // Original 060023c3.
        protected abstract void OnHeld(float value); // Original 060023c4.
        protected abstract void OnUp(float value); // Original 060023c5.
        protected abstract void OnDisabled(); // Original 060023c6.
        // Original 060023c7 is emitted from the static field initializer above.
    }
}
