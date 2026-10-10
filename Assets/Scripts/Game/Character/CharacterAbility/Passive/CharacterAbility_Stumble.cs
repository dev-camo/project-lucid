using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_Stumble : CharacterAbility<CharacterAbilityDefinition_Stumble>, IAbilityTrigger
    {
        private float m_timeRemainingSeconds;
        private readonly List<FullscreenShaderManager.ParametersHandle> m_effectHandleInstances =
            new List<FullscreenShaderManager.ParametersHandle>();

        public bool IsTriggered => m_timeRemainingSeconds > 0f;

        protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
        {
            base.DoOnUpdate(brain, deltaTime);
            // Both original CPUs use the ordered <= rejection here. An unordered
            // timer still undergoes subtraction, unlike the IsTriggered getter.
            if (m_timeRemainingSeconds <= 0f) return;
            m_timeRemainingSeconds -= deltaTime;
            if (m_timeRemainingSeconds <= 0f)
            {
                m_timeRemainingSeconds = 0f;
                m_character.TriggerAnimationLeave(Definition.ActivationAnimationDefinition, m_effectHandleInstances);
            }
        }

        public void TriggerFromHazard(HazardDefinition hazard)
        {
            if (m_timeRemainingSeconds > 0f) return;
            m_timeRemainingSeconds = Definition.DurationSeconds;
            m_character.TriggerAnimationEnter(Definition.ActivationAnimationDefinition, m_effectHandleInstances);
            // Preserve the timer and animation prefixes if the hazard access faults.
            m_character.SetTimeScaledBodyVelocity(m_character.GetTimeScaledBodyVelocity() *
                hazard.StumbleSpeedReductionFraction);
        }

        public CharacterAbility_Stumble() { }
    }
}
