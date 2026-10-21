using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001ef; bodies translated from the 1.10.1 x86-64 binary (0x6FEFE0..0x6FF5E0).
    // A short scuttle towards the desired movement position stored by the FSM. It can start between the minimum movement
    // distance and the scuttle range, and finishes once the boss has stayed within the end tolerance for the stated seconds
    // (counted in the FSM's destination-reached timer) or the target is out of range.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_Scuttle : ActorAbility, IAbilityTrigger, IAbilityRange
    {
        public BossAbilityDefinition_MovementScuttle Definition;

        private bool m_canScuttle;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b2f
        public bool IsTriggered => m_canScuttle; // 06000b30
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000b31
        public float MaxRangeSquared => Definition.ScuttleRangeSquared; // 06000b32
        public float MinRangeSquared => Definition.ScuttleMinimumMovementDistanceSquared; // 06000b33
        public float DestinationReachedToleranceSquared => Definition.ScuttleEndDistanceToleranceSquared; // 06000b34
        public float MaxFollowUpRangeSquared { get; set; } // 06000b35 / 06000b36

        private Boss m_boss;
        private Vector3 m_currentTargetPosition;

        // 06000b37: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MovementScuttle;
        }

        // 06000b38
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_canScuttle = false;
            m_boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 0f);
        }

        // 06000b39: the target is the desired movement position (infinitely far when none is stored, and not stored). Inside
        // the end tolerance the destination-reached timer counts up, otherwise it restarts at zero.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            m_currentTargetPosition = m_boss.Storage.GetValue(BossFSMKeys.DesiredMovementPosition, Vector3.positiveInfinity, false);
            float squaredDistance = (m_boss.WorldPosition - m_currentTargetPosition).sqrMagnitude;
            m_canScuttle = Definition.ScuttleRangeSquared > squaredDistance
                && squaredDistance > Definition.ScuttleMinimumMovementDistanceSquared;
            float destinationReachedTime;
            if (Definition.ScuttleEndDistanceToleranceSquared >= squaredDistance)
            {
                destinationReachedTime = m_boss.Storage.AdjustValue(BossFSMKeys.DestinationReachedTimer, deltaTime);
            }
            else
            {
                m_boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 0f);
                destinationReachedTime = 0f;
            }
            AbilityFinished = squaredDistance > Definition.ScuttleRangeSquared
                || destinationReachedTime > Definition.ScuttleDestinationReachedSeconds;
        }
    }
}
