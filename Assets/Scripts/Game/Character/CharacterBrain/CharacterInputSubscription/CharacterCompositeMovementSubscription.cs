using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterCompositeMovementSubscription : InputCompositeMovementSubscription
    {
        private readonly CharacterBrain m_brain;

        public CharacterCompositeMovementSubscription(CharacterBrain brain, GameControlsBinding binding, ValidatorFunc validator = null) : base(binding, validator) // Original 06001320.
        {
            m_brain = brain;
        }

        // Original 06001321: two separate calls in X then Y order.
        protected override void OnMovement(Vector2 amount)
        {
            m_brain.AddMovement(Vector2.right, amount.x);
            m_brain.AddMovement(Vector2.up, amount.y);
        }
    }
}
