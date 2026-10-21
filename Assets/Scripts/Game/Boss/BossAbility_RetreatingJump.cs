using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001ee; bodies translated from the 1.10.1 x86-64 binary (0x6FE9B0..0x6FEFE0).
    // A jump away from the boss's current position to the desired movement position stored by the FSM, started once that
    // position is farther than the minimum jump range and the jump animation has been triggered. The X and Z progress follow
    // the speed curve, the Y progress follows the height curve towards the target height plus the maximum jump height.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_RetreatingJump : ActorAbility, IAbilityTrigger, IAbilityRange, IAbilityAnimatorTriggerable
    {
        public BossAbilityDefinition_MovementRetreatingJump Definition;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b20
        public float MaxRangeSquared => Definition.MiniumJumpRangeSquared; // 06000b21
        public float MinRangeSquared => Definition.MiniumJumpRangeSquared; // 06000b22

        // 06000b23 and 06000b24: the original auto-properties are never assigned, so both stay zero.
        public float DestinationReachedToleranceSquared { get; }
        public float MaxFollowUpRangeSquared { get; }

        public bool IsTriggered => m_triggerJump; // 06000b25
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000b26
        public float JumpProgress => m_jumpProgress; // 06000b27

        private bool m_triggerJump;
        private bool m_animationTriggered;
        private Boss m_boss;
        private Vector3 m_preJumpPosition;
        private Vector3 m_rawTargetPosition;
        private Vector3 m_currentTargetPosition;
        private float m_jumpProgress;

        // 06000b28: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MovementRetreatingJump;
        }

        // 06000b29
        protected override void DoOnEnter()
        {
            m_preJumpPosition = m_boss.WorldPosition;
            base.DoOnEnter();
        }

        // 06000b2a
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_triggerJump = false;
            m_jumpProgress = 0f;
        }

        // 06000b2b: a new jump is triggered once the desired position (defaulting to the boss's own, not stored) is beyond
        // the jump range. Once the animation has triggered, the jump progresses by delta time each update until it reaches one,
        // when it ends.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            Vector3 bossPosition = m_boss.WorldPosition;
            Vector3 desiredPosition = m_boss.Storage.GetValue(BossFSMKeys.DesiredMovementPosition, bossPosition, false);
            if (!m_triggerJump)
            {
                m_triggerJump = (bossPosition - desiredPosition).sqrMagnitude > Definition.MiniumJumpRangeSquared;
                if (!m_triggerJump)
                    return;
                AbilityFinished = false;
                m_preJumpPosition = m_boss.WorldPosition;
                m_currentTargetPosition = m_boss.WorldPosition;
            }
            if (!m_animationTriggered)
                return;
            if (m_jumpProgress >= 1f)
            {
                m_jumpProgress = 0f;
                m_triggerJump = false;
                m_animationTriggered = false;
                return;
            }

            m_jumpProgress = m_jumpProgress + deltaTime;
            m_rawTargetPosition = m_boss.Storage.GetValue(BossFSMKeys.DesiredMovementPosition, Vector3.zero, false);
            float speedProgress = Mathf.Clamp01(Definition.JumpSpeedCurve.Evaluate(m_jumpProgress));
            float heightProgress = Mathf.Clamp01(Definition.JumpHeightCurve.Evaluate(m_jumpProgress));
            float peakHeight = m_rawTargetPosition.y + Definition.JumpMaxHeight;
            float x = (m_rawTargetPosition.x - m_preJumpPosition.x) * speedProgress + m_preJumpPosition.x;
            float y = (peakHeight - m_preJumpPosition.y) * heightProgress + m_preJumpPosition.y;
            float z = speedProgress * (m_rawTargetPosition.z - m_preJumpPosition.z) + m_preJumpPosition.z;
            m_currentTargetPosition = new Vector3(x, y, z);
        }

        public void SetAnimationTriggered() => m_animationTriggered = true; // 06000b2c
        public void SetAnimationComplete() => AbilityFinished = true; // 06000b2d
    }
}
