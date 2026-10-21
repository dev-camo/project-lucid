using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001dd; bodies translated from the 1.10.1 x86-64 binary (0x6F8FD0..0x6F99A8).
    // The Fourmen charge: the boss winds up while it keeps aiming at the character, then charges along the aimed
    // direction under an acceleration curve until it passes the aimed position, and brakes under a deceleration curve.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_BossFourmenChargeAttack : ActorAbility, IAbilityTrigger, IOffensiveAbility, IAbilityBossMovement
    {
        private readonly Vector3 ArenaCentre = Vector3.up;

        public BossAbilityDefinition_ChargeAttack Definition;

        private bool m_abilityActivated;
        private bool m_canTriggerAttackAnim;
        private bool m_startWindup;
        private bool m_isWindingUp;
        private bool m_isWoundUp;
        private Vector3 m_currentTargetPosition;
        private Vector3 m_playerTargetPosition;
        private Vector3 m_normalisedDirection;
        private Boss m_boss;
        private float m_attackAnimationTimer;
        private float m_decelerationTimer;
        private float m_currentFarRangeSquared;
        private float m_currentTelegraphRangeSquared;
        private float m_previousSquareMagnitude = float.PositiveInfinity;
        private float m_debugWindUpTime = 1f;
        private float m_debugWindUpTimer;
        private bool m_reachedTarget;

        public override AbilityDefinition DefinitionBase => Definition; // 06000a60
        public bool IsTriggered => m_canTriggerAttackAnim; // 06000a61
        public bool StartWindUp => m_startWindup; // 06000a62
        public bool WoundUp => m_isWoundUp; // 06000a63
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000a64
        public float MovementAnimationAngleMultiplier => Definition.MovementAnimationAngleMultiplier; // 06000a65
        public float FaceAngleTolerance => Definition.FaceAngleTolerance; // 06000a66
        public float TurnSpeed => Definition.TurnSpeed; // 06000a67
        public bool IsMomentum => true; // 06000a68
        public bool TurnToCharacter => false; // 06000a69

        // 06000a6a: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_ChargeAttack;
        }

        // 06000a6b: the previous distance goes back to infinity so the next charge approaches again.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_reachedTarget = false;
            m_abilityActivated = false;
            m_canTriggerAttackAnim = false;
            m_startWindup = false;
            m_isWindingUp = false;
            m_isWoundUp = false;
            m_previousSquareMagnitude = float.PositiveInfinity;
            m_debugWindUpTimer = 0f;
            m_attackAnimationTimer = 0f;
            m_decelerationTimer = 0f;
        }

        // 06000a6c: once wound up the charge runs. While winding up the boss keeps aiming at the character.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (!m_abilityActivated)
                return;
            if (!WindingUp(deltaTime))
            {
                if (AbilityFinished || !Attacking(deltaTime))
                    AbilityFinished = true;
                return;
            }
            Character character;
            if (!ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out character))
                return;
            m_playerTargetPosition = character.WorldPosition;
            m_currentTargetPosition = (m_playerTargetPosition - m_boss.WorldPosition).normalized;
            m_normalisedDirection = (m_playerTargetPosition - m_boss.WorldPosition).normalized;
        }

        // 06000a6d: finishes (and stops) when the boss is outside the allowed range and heading away from the arena centre.
        // Otherwise it accelerates towards the aimed position until the distance to it grows (it has passed it), then decelerates
        // for the deceleration time and ends the attack. True while the charge goes on.
        private bool Attacking(float deltaTime)
        {
            if (!m_canTriggerAttackAnim)
                m_canTriggerAttackAnim = true;
            if ((m_boss.WorldPosition - ArenaCentre).sqrMagnitude > Definition.DistanceAllowedFromOriginSquared
                && 0f > Vector3.Dot(ArenaCentre - m_boss.WorldPosition, m_boss.ForwardDirection))
            {
                m_currentTargetPosition = Vector3.zero;
                AbilityFinished = true;
                TriggerAttackStrikeEffects();
                m_attackAnimationTimer = 0f;
                return false;
            }
            if (!m_reachedTarget)
            {
                float squaredDistance = (m_playerTargetPosition - m_boss.WorldPosition).sqrMagnitude;
                if (m_previousSquareMagnitude >= squaredDistance)
                {
                    m_attackAnimationTimer = deltaTime + m_attackAnimationTimer;
                    float acceleration = Definition.AccelerationCurve.Evaluate(m_attackAnimationTimer / Definition.AccelerationTime);
                    m_currentTargetPosition = m_normalisedDirection * (acceleration * Definition.TopSpeed);
                    m_previousSquareMagnitude = squaredDistance;
                    return true;
                }
                m_reachedTarget = true;
            }
            if (Definition.DecelerationTime > m_decelerationTimer)
            {
                m_decelerationTimer = m_decelerationTimer + deltaTime;
                float deceleration = Definition.DecelerationCurve.Evaluate(m_decelerationTimer / Definition.DecelerationTime);
                m_currentTargetPosition = m_normalisedDirection * (deceleration * Definition.TopSpeed);
                return true;
            }
            TriggerAttackStrikeEffects();
            m_attackAnimationTimer = 0f;
            return false;
        }

        // 06000a6e
        private void TriggerAttackStrikeEffects()
        {
            m_boss.Storage.SetValue(BossFSMKeys.BossEffectType, BossEffectTriggerTypes.AttackStrike);
            m_boss.TriggerEffects();
        }

        // 06000a6f: the charge can always start.
        public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime)
        {
            return true;
        }

        // 06000a70: also marks the wind-up as running.
        public void SetAttackFlow(bool abilityActive)
        {
            m_abilityActivated = abilityActive;
            m_startWindup = true;
            m_isWindingUp = true;
        }

        public void SetAttackWindingUp() => m_isWindingUp = true; // 06000a71
        public void SetAttackWoundUp() => m_isWoundUp = true; // 06000a72
        public void SetAttackAnimStarted() { } // 06000a73, original empty body

        // 06000a74
        public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition)
        {
            m_isWindingUp = true;
            m_isWoundUp = true;
        }

        public void ResetProjectileGroupCounter() { } // 06000a75, original empty body

        private bool AbilityActivated() => m_abilityActivated; // 06000a76

        // 06000a77: true while winding up. After the debug wind-up time the boss winds up as soon as it has a clear line
        // of sight to the character within the cone of vision. It also drops the start request once winding up or wound up.
        private bool WindingUp(float deltaTime)
        {
            if (m_isWindingUp)
            {
                m_startWindup = false;
                if (m_isWoundUp)
                    return false;
                if (m_debugWindUpTime > m_debugWindUpTimer)
                {
                    m_debugWindUpTimer = m_debugWindUpTimer + deltaTime;
                    return true;
                }
                if (!m_boss.HasLineOfSightOnCharacter(Definition.ConeOfVisionDistance, true, Definition.ConeOfVisionAngle))
                    return true;
                m_isWoundUp = true;
                return false;
            }
            if (!m_isWoundUp)
                return true;
            m_startWindup = false;
            return false;
        }
    }
}
