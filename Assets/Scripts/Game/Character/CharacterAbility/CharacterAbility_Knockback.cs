using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_Knockback : CharacterAbility<CharacterAbilityDefinition_Knockback>, IAbilityTrigger
    {
        private bool m_isKnockedBack;
        private float m_durationSeconds;
        private float m_currentTimeSeconds;
        private float m_recoveryTimeRemaining;

        // Original06001111; genuine interface implementation, no override.
        public bool IsTriggered => m_isKnockedBack;

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            if (!m_isKnockedBack) return;
            m_currentTimeSeconds += deltaTime;
            if (m_recoveryTimeRemaining > 0f) m_recoveryTimeRemaining -= deltaTime;
            // Native rejects unordered duration comparisons but accepts an
            // unordered recovery timer here; retain the two distinct predicates.
            if (!(m_currentTimeSeconds >= m_durationSeconds) || m_recoveryTimeRemaining > 0f) return;
            m_isKnockedBack = false;
            m_currentTimeSeconds = 0f;
            if (m_character.LocalVelocity.z < 0f)
                m_character.SetLocalVelocity(new Vector3(m_character.LocalVelocity.x,
                    m_character.LocalVelocity.y, 0f));
        }

        // Publish the flag before the genuine virtual definition read; a fault
        // leaves that prefix and does not clear the two timing fields.
        public void Trigger()
        {
            m_isKnockedBack = true;
            m_durationSeconds = Definition.MaxDurationSeconds;
            m_currentTimeSeconds = 0f;
            m_recoveryTimeRemaining = 0f;
        }

        public bool MinDurationExceeded() => m_isKnockedBack && m_currentTimeSeconds >= Definition.MinDurationSeconds;
        public void EnterRecovery() => m_recoveryTimeRemaining = Definition.RecoveryDurationSeconds;
        private bool IsRecovering() => m_isKnockedBack && m_recoveryTimeRemaining > 0f;
        public CharacterAbility_Knockback() { }
    }
}
