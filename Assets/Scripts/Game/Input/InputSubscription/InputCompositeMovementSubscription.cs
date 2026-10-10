using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class InputCompositeMovementSubscription : InputAxisSubscription
    {
        protected InputCompositeMovementSubscription(GameControlsBinding binding, ValidatorFunc validator) : base(binding, validator) { } // Original 060023ce.

        public override void Subscribe() // Original 060023cf.
        {
            base.Subscribe();
            ControlMapping.Subscribe(GameInput, OnCallbackMovement, -1);
        }

        public override void Unsubscribe() // Original 060023d0.
        {
            ControlMapping.Unsubscribe(GameInput, OnCallbackMovement, -1);
        }

        // Original 060023d1: ordered magnitude threshold; modifier bool is ignored.
        private void OnCallbackMovement(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude < 0.0001f)
                return;
            float modifiedMagnitude = magnitude;
            ApplyModifiers(ref modifiedMagnitude);
            OnMovement(value * (modifiedMagnitude / magnitude));
        }

        protected abstract void OnMovement(Vector2 value); // Original 060023d2.
    }
}
