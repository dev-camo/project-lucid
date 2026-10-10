using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class InputMovementSubscription : InputAxisSubscription
    {
        protected InputMovementSubscription(GameControlsBinding binding, ValidatorFunc validator) : base(binding, validator) { } // Original 060023d3.

        public override void Subscribe() // Original 060023d4.
        {
            base.Subscribe();
            ControlMapping.Subscribe(GameInput, OnCallbackMovement, InputTrigger.Held, -1);
        }

        public override void Unsubscribe() // Original 060023d5; base unsubscribe is empty.
        {
            ControlMapping.Unsubscribe(GameInput, OnCallbackMovement, -1);
        }

        // Original 060023d6 forwards directly, without applying modifiers.
        private void OnCallbackMovement(float value) => OnMovement(value);
        protected abstract void OnMovement(float value); // Original 060023d7.
    }
}
