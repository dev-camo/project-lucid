using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterMovementSubscription : InputMovementSubscription
    {
        private readonly CharacterBrain m_brain;
        private readonly Vector2 m_direction;

        public CharacterMovementSubscription(CharacterBrain brain, GameControlsBinding binding, Vector2 direction, ValidatorFunc validator = null) : base(binding, validator) // Original 06001322.
        {
            m_brain = brain;
            m_direction = direction;
        }

        protected override void OnMovement(float value) => m_brain.AddMovement(m_direction, value); // Original 06001323.
    }
}
