using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e7; bodies translated from the 1.10.1 x86-64 binary (0x6FB720..0x6FC8E0).
    // Base of the boss movements that glide the boss to a target: the speed follows an acceleration curve over time
    // and the movement counts as in motion for the Fourmen manager until the target is reached.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_MoveTowardsTarget : ActorAbility, IAbilityBossMovement
    {
        private const float minSqrMagnitude = 1E-05f;

        protected readonly Vector3 ArenaCentre = Vector3.up;

        public BossAbilityDefinition_MoveTowardsTarget Definition;

        protected Boss m_boss;
        protected Vector3 m_currentTargetPosition;
        protected Vector3 m_targetPosition;
        private float m_movementTimer;
        private float m_decelerationTimer;
        protected bool m_reachedTarget;
        protected SystemRef<FourmenManager> m_fourmenSystemRef = ProcessManager.GetSystemRef<FourmenManager>(null, true);

        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000ac6
        public float MovementAnimationAngleMultiplier => 0f; // 06000ac7
        public float FaceAngleTolerance => 0f; // 06000ac8
        public virtual float TurnSpeed => 0f; // 06000ac9
        public bool IsMomentum => false; // 06000aca
        public bool TurnToCharacter => false; // 06000acb

        // The original auto-property is never assigned here, so this base reports no definition; the derived
        // abilities override it with their own.
        public override AbilityDefinition DefinitionBase { get; } // 06000acc

        // 06000acd: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MoveTowardsTarget;
        }

        // 06000ace: the boss starts from where it is and counts as in motion while the manager exists.
        protected override void DoOnEnter()
        {
            m_currentTargetPosition = m_boss.WorldPosition;
            if (m_fourmenSystemRef.IsNull())
                return;
            m_fourmenSystemRef.GetSafe().BossesInMotion.AddUnique(m_boss);
        }

        // 06000acf: the movement timer is deliberately not reset.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_boss.Storage.SetValue(BossFSMKeys.DestinationReachedTimer, 0f);
            m_reachedTarget = false;
        }

        // 06000ad0: once the target was reached the ability finishes and the boss stops counting as in motion. Otherwise the
        // target counts as reached once Vector3 equality holds, and the boss moves at the curve's speed.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (m_reachedTarget)
            {
                AbilityFinished = true;
                if (!m_fourmenSystemRef.IsNull())
                    m_fourmenSystemRef.GetSafe().BossesInMotion.Remove(m_boss);
                return;
            }
            float speedFactor = Definition.AccelerationCurve.Evaluate(m_movementTimer / Definition.AccelerationTime);
            if (m_targetPosition == m_boss.WorldPosition)
            {
                m_currentTargetPosition = m_targetPosition;
                m_reachedTarget = true;
                return;
            }
            float speed = speedFactor * Definition.TopSpeed;
            m_movementTimer = m_movementTimer + deltaTime;
            m_currentTargetPosition = Vector3.MoveTowards(m_boss.WorldPosition, m_targetPosition, speed * deltaTime);
        }
    }
}
