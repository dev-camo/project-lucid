using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200034a. Starts the chaos burst attack's stamina use on demand.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterChaosStaminaTrigger : CharacterStaminaTrigger<IAbilityBurstAttackChaos>
    {
        [SerializeField]
        [Tooltip("Triggers ability immediately.")]
        protected bool m_triggerAbility;

        // 060014ae
        protected override CharacterStamina Stamina => Character.ChaosStamina;

        // 060014af; activates the chaos stamina with the burst attack's own cost definition,
        // overriding both the impulse and the activation checks.
        public void Trigger()
        {
            if (!TryGetCharacter(out Character character) || Ability == null)
                return;
            CharacterStamina.Definition definition = Ability.BurstAttackDefinition.ChaosStaminaCostDefinition;
            character.RetainCurrentImpulses();
            Stamina.Activate(m_abilityType, definition, true, true);
            Ability.OnStaminaRecharge(m_triggerAbility);
        }

        // 060014b0
        public CharacterChaosStaminaTrigger() { }
    }
}
