using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e1; bodies translated from the 1.10.1 x86-64 binary (0x6FA400..0x6FA890).
    // The boss spirals in: it circles the arena centre from where it stands, and the radius decays from the starting
    // to the ending radius over time while the speed follows a curve.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_MoveBigCircleWithDecayingRadius : ActorAbility, IAbilityBossMovement
    {
        private readonly Vector3 ArenaCentre = Vector3.zero;

        public BossAbilityDefinition_MoveBigCircleWithDecayingRadius Definition;

        private Boss m_boss;
        private Vector3 m_bossEntryPoint;
        private Vector3 m_currentTargetPosition;
        private Vector3 m_centerOfCircle;
        private Vector3 m_bossChargingPoint;
        private float m_radiusOfCircle;
        private float m_counterSpinSpeedNormalised;
        private float m_finalArcRadians;
        private float m_progressionArcRadians;
        private float m_speed;
        private float m_speedCounterSpin;
        private float m_speedNormalised;
        private float m_totalMovementTime;
        private float m_distanceFullAccelerationReachedSquared;
        private bool m_invertDirection;

        public override AbilityDefinition DefinitionBase => Definition; // 06000a96
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000a97
        public float MovementAnimationAngleMultiplier => 0f; // 06000a98
        public float FaceAngleTolerance => 0f; // 06000a99
        public float TurnSpeed => 0f; // 06000a9a
        public bool IsMomentum => false; // 06000a9b
        public bool TurnToCharacter => false; // 06000a9c

        // 06000a9d: both casts are `as`; a null boss or definition stores null. The charging point is the arena centre.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MoveBigCircleWithDecayingRadius;
            m_bossChargingPoint = ArenaCentre;
        }

        // 06000a9e: the spiral starts at the boss's bearing (z over x) on the starting radius, at the curve's initial speed.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            AbilityFinished = false;
            m_centerOfCircle = ArenaCentre;
            m_radiusOfCircle = Definition.StartingRadius;
            m_progressionArcRadians = Mathf.Atan2(m_boss.WorldPosition.z, m_boss.WorldPosition.x);
            m_currentTargetPosition = m_centerOfCircle.PointOnCircleWithFixedY(m_progressionArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);
            m_speed = Definition.MovementSpeedCurve.Evaluate(0f);
            m_speedNormalised = m_radiusOfCircle / m_speed;
        }

        // 06000a9f
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_progressionArcRadians = 0f;
            m_totalMovementTime = 0f;
        }

        // 06000aa0: finished once the boss is within the tolerance (compared against the squared distance) of the
        // charging point and the radius has decayed to exactly the ending radius.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (AbilityFinished)
                return;
            m_totalMovementTime = m_totalMovementTime + deltaTime;
            m_speed = Definition.MovementSpeedCurve.Evaluate(m_totalMovementTime) * Definition.MaxMovementSpeed;
            m_speedNormalised = m_radiusOfCircle / m_speed;
            m_radiusOfCircle = Mathf.Lerp(Definition.StartingRadius, Definition.EndingRadius,
                Definition.RadiusDecayCurve.Evaluate(m_totalMovementTime / Definition.TimeToDecayFully));
            m_progressionArcRadians -= deltaTime / m_speedNormalised;
            m_currentTargetPosition = m_centerOfCircle.PointOnCircleWithFixedY(m_progressionArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);
            if (Definition.EndDistanceTolerance > (m_boss.WorldPosition - m_bossChargingPoint).sqrMagnitude
                && m_radiusOfCircle == Definition.EndingRadius)
                AbilityFinished = true;
        }
    }
}
