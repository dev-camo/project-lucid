using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterActionSubscription : InputActionSubscription
    {
        private readonly GameAction m_action;
        private readonly CharacterBrain m_brain;

        // Original 0600131b; validator has the genuine optional null default.
        public CharacterActionSubscription(GameAction action, CharacterBrain brain, GameControlsBinding binding, ValidatorFunc validator = null) : base(binding, validator)
        {
            m_action = action;
            m_brain = brain;
        }

        protected override void OnDown(float value) => m_brain.SetState(m_action, true); // Original 0600131c.
        protected override void OnHeld(float value) { } // Original 0600131d is empty.
        protected override void OnUp(float value) => m_brain.SetState(m_action, false); // Original 0600131e.
        protected override void OnDisabled() => m_brain.SetState(m_action, false); // Original 0600131f.
    }
}
