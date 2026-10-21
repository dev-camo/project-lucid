using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e0; bodies translated from the 1.10.1 x86-64 binary (0x6F9EF0..0x6FA3F8).
    // Each boss orbits a point that itself travels round the big circle (counter-spinning, so the two arcs run in
    // opposite directions). The debug helpers are conditional on BUILD_DEVELOPMENT and have no calls in the retail body.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_MoveBigCircleWithCounterSpin : ActorAbility, IAbilityTrigger, IAbilityBossMovement
    {
        private readonly Vector3 ArenaCentre = Vector3.up;

        public BossAbilityDefinition_MoveBigCircleWithCounterSpin Definition;

        private Boss m_boss;
        private Vector3 m_bossEntryPoint;
        private Vector3 m_currentTargetPosition;
        private Vector3 m_bigCircleTargetPosition;
        private Vector3 m_centerOfBigCircleCircle;
        private float m_radiusOfBigCircleCircle;
        private float m_radiusOfCounterSpinCircle;
        private float m_counterSpinSpeedNormalised;
        private float m_finalArcRadians;
        private float m_progressionBigCircleArcRadians;
        private float m_progressionCounterSpinArcRadians;
        private float m_speedBigCircle;
        private float m_speedCounterSpin;
        private float m_speedBigCircleNormalised;
        private float m_speedCounterSpinNormalised;
        private float m_distanceFullAccelerationReachedSquared;
        private bool m_invertDirection;

        public override AbilityDefinition DefinitionBase => Definition; // 06000a86
        public bool IsTriggered => true; // 06000a87
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000a88
        public float MovementAnimationAngleMultiplier => Definition.MovementAnimationAngleMultiplier; // 06000a89
        public float FaceAngleTolerance => Definition.FaceAngleTolerance; // 06000a8a
        public float TurnSpeed => Definition.TurnSpeed; // 06000a8b
        public bool IsMomentum => false; // 06000a8c
        public bool TurnToCharacter => true; // 06000a8d

        // 06000a8e: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MoveBigCircleWithCounterSpin;
        }

        // 06000a8f: the counter-spin circle starts at an angle of its boss's index times pi, so the bosses sit opposite
        // each other. The speeds are stored as seconds per radian (radius over speed).
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            AbilityFinished = false;
            m_bossEntryPoint = m_boss.WorldPosition;
            m_centerOfBigCircleCircle = ArenaCentre;
            m_radiusOfBigCircleCircle = Definition.BigCircleRadius;
            m_radiusOfCounterSpinCircle = Definition.CounterSpinRadius;
            m_bigCircleTargetPosition = CalculateTargetPositionFromNewCenter();
            m_progressionBigCircleArcRadians = 0f;
            m_progressionCounterSpinArcRadians = ProcessManager.GetSystem<FourmenManager>().GetIndexForBoss(m_boss) * Mathf.PI;
            m_bigCircleTargetPosition = m_centerOfBigCircleCircle.PointOnCircleWithFixedY(
                m_progressionBigCircleArcRadians, m_radiusOfBigCircleCircle, m_boss.WorldPosition.y);
            m_currentTargetPosition = m_bigCircleTargetPosition.PointOnCircleWithFixedY(
                m_progressionCounterSpinArcRadians, m_radiusOfCounterSpinCircle, m_boss.WorldPosition.y);
            m_speedBigCircle = Definition.BigCircleSpeed;
            m_speedCounterSpin = Definition.CounterRotateSpeed;
            m_speedBigCircleNormalised = m_radiusOfBigCircleCircle / m_speedBigCircle;
            m_speedCounterSpinNormalised = m_radiusOfCounterSpinCircle / m_speedCounterSpin;
        }

        // 06000a90: every leave flips the direction flag, which nothing in the retail code reads.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_progressionBigCircleArcRadians = 0f;
            m_invertDirection = !m_invertDirection;
        }

        // 06000a91: the big arc runs backwards and the counter-spin arc forwards.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            m_progressionBigCircleArcRadians -= deltaTime / m_speedBigCircleNormalised;
            m_progressionCounterSpinArcRadians += deltaTime / m_speedCounterSpinNormalised;
            m_bigCircleTargetPosition = m_centerOfBigCircleCircle.PointOnCircleWithFixedY(
                m_progressionBigCircleArcRadians, m_radiusOfBigCircleCircle, m_boss.WorldPosition.y);
            m_currentTargetPosition = m_bigCircleTargetPosition.PointOnCircleWithFixedY(
                m_progressionCounterSpinArcRadians, m_radiusOfCounterSpinCircle, m_boss.WorldPosition.y);
        }

        // 06000a92
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetDebugActive(bool isActive)
        {
            m_boss.m_movementTargetingDebugBoxCenter.gameObject.SetActive(isActive);
        }

        // 06000a93
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetDebugPositions()
        {
            m_boss.m_movementTargetingDebugBoxCenter.position = m_bigCircleTargetPosition;
        }

        // 06000a94
        private Vector3 CalculateTargetPositionFromNewCenter()
        {
            m_finalArcRadians = 0f;
            return m_centerOfBigCircleCircle.PointOnCircleWithFixedY(0f, m_radiusOfBigCircleCircle, m_boss.WorldPosition.y);
        }
    }
}
