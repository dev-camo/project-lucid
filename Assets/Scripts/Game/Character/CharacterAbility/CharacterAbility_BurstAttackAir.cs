using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbility_BurstAttackAir : CharacterAbility_BurstAttack<CharacterAbilityDefinition_BurstAttackAir>
    {
        // Complete original three-method owner; both native architectures call
        // the genuine generic base before touching the character queue.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_character.QueuedActions.ResetBuffers();
        }

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (!IsTriggered) return;
            // Bitwise OR retains the original eager brain getter invocation,
            // including its fault prefix when the queued flag is already true.
            m_character.QueuedActions.Jump |= brain.Jump;
            m_character.QueuedActions.Boost |= brain.Boost;
        }

        public CharacterAbility_BurstAttackAir() { }
    }
}
