using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e2; bodies translated from the 1.10.1 x86-64 binary (0x6FA890..0x6FB5A0).
    // The boss circles a randomly chosen centre near itself, accelerating to a top speed and braking once close to the
    // final target on that circle; it picks a new circle when it gets near the final target, and finishes once it is
    // beyond the allowed range from the origin and heading further out. The debug helpers are conditional on
    // BUILD_DEVELOPMENT and have no calls in the retail body.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_MoveRandom : ActorAbility, IAbilityBossMovement
    {
        private readonly Vector3 ArenaCentre = Vector3.up;

        public BossAbilityDefinition_MoveRandom Definition;

        private Boss m_boss;
        private Vector3 m_bossEntryPoint;
        private Vector3 m_currentTargetPosition;
        private Vector3 m_finalTargetPosition;
        private Vector3 m_centerOfCircle = Vector3.zero;
        private float m_radiusOfCircle;
        private float m_speedNormalised;
        private float m_originalArcRadians = float.PositiveInfinity;
        private float m_finalArcRadians;
        private float m_progressionArcRadians;
        private float m_speedAdjusted;
        private float m_distanceFullAccelerationReachedSquared;
        private bool m_reachedTopSpeed;
        private bool m_pumpTheBrakes;
        private int m_recursionLimit = 100;
        private int m_recursionCount;
        private bool m_invertDirection;
        private List<Vector3> m_debugInnerCoords = new List<Vector3>();

        public override AbilityDefinition DefinitionBase => Definition; // 06000aa2
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000aa3
        public float MovementAnimationAngleMultiplier => Definition.MovementAnimationAngleMultiplier; // 06000aa4
        public float FaceAngleTolerance => Definition.FaceAngleTolerance; // 06000aa5
        public float TurnSpeed => Definition.TurnSpeed; // 06000aa6
        public bool IsMomentum => false; // 06000aa7
        public bool TurnToCharacter => true; // 06000aa8

        // 06000aa9: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_MoveRandom;
        }

        // 06000aaa: starts at the boss's current speed, sets up the first circle and runs one update at once with the
        // enemy-movement delta time.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            AbilityFinished = false;
            m_speedAdjusted = m_boss.WorldVelocity.magnitude;
            SetupAbility();
            DoOnUpdate(ProcessManager.GetSystem<TimeManager>().GetDeltaTime(TimeCategory.EnemyMovement));
        }

        // 06000aab: a fresh circle and final target, flipping the direction each time.
        private void SetupAbility()
        {
            m_recursionCount = 0;
            m_bossEntryPoint = m_boss.WorldPosition;
            m_centerOfCircle = CalculateNewCenter();
            m_finalTargetPosition = CalculateTargetPositionFromNewCenter();
            m_progressionArcRadians = m_originalArcRadians;
            m_currentTargetPosition = m_centerOfCircle.PointOnCircleWithFixedY(m_progressionArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);
            m_speedNormalised = m_radiusOfCircle / m_speedAdjusted;
            m_invertDirection = !m_invertDirection;
            m_reachedTopSpeed = false;
            m_pumpTheBrakes = false;
        }

        // 06000aac: the next entry picks its first circle afresh.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            m_originalArcRadians = float.PositiveInfinity;
        }

        // 06000aad: braking wins over cruising, which wins over accelerating. The brakes go on once the boss is closer to the
        // final target than the distance it needed to reach full speed. Near the final target, or after too many retries,
        // a new circle is set up; otherwise the ability finishes when the boss is beyond the allowed range from the origin and
        // the target is farther out still.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            float speed;
            if (m_pumpTheBrakes)
            {
                speed = m_speedAdjusted - Definition.AccelerationPerSecond * deltaTime;
                m_speedAdjusted = speed;
                m_pumpTheBrakes = speed > Definition.MinimumSpeed;
            }
            else if (m_reachedTopSpeed)
            {
                speed = m_speedAdjusted;
            }
            else
            {
                speed = m_speedAdjusted + Definition.AccelerationPerSecond * deltaTime;
                m_speedAdjusted = speed;
                m_reachedTopSpeed = speed > Definition.MaximumSpeed;
                m_distanceFullAccelerationReachedSquared = (m_boss.WorldPosition - m_bossEntryPoint).sqrMagnitude;
                speed = m_speedAdjusted;
            }

            m_speedNormalised = m_radiusOfCircle / speed;
            float arcStep = deltaTime / m_speedNormalised;
            if (m_invertDirection)
                arcStep = -arcStep;
            m_progressionArcRadians = arcStep + m_progressionArcRadians;
            float squaredDistanceToFinalTarget = (m_boss.WorldPosition - m_finalTargetPosition).sqrMagnitude;
            if (m_distanceFullAccelerationReachedSquared > squaredDistanceToFinalTarget && speed > Definition.MinimumSpeed)
                m_pumpTheBrakes = true;
            m_currentTargetPosition = m_centerOfCircle.PointOnCircleWithFixedY(m_progressionArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);

            if (Definition.EndDistanceTolerance > squaredDistanceToFinalTarget || m_recursionCount > m_recursionLimit)
            {
                SetupAbility();
                return;
            }
            float squaredBossDistance = m_boss.WorldPosition.sqrMagnitude;
            if (squaredBossDistance > Definition.DistanceAllowedFromOriginSquared && m_currentTargetPosition.sqrMagnitude > squaredBossDistance)
                AbilityFinished = true;
        }

        // 06000aae
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetDebugActive(bool isActive)
        {
            if (m_boss == null)
                return;
            SetDebugObjectActive(m_boss.m_movementTargetingDebugBox, isActive);
            SetDebugObjectActive(m_boss.m_movementTargetingDebugBoxCenter, isActive);
        }

        // 06000aaf
        private static void SetDebugObjectActive(Transform debugObject, bool isActive)
        {
            if (debugObject == null)
                return;
            debugObject.gameObject.SetActive(isActive);
        }

        // 06000ab0
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetDebugPositions()
        {
            m_boss.m_movementTargetingDebugBox.position = m_finalTargetPosition;
            m_boss.m_movementTargetingDebugBoxCenter.position = m_centerOfCircle;
        }

        // 06000ab1: the first circle's angle comes from the boss's position (pointing at the origin); later ones continue from
        // the last final arc. The circle's centre is a random distance from the boss, within the allowed range of the arena
        // centre, and the stored angle becomes the direction from that centre to the boss.
        private Vector3 CalculateNewCenter()
        {
            float arc;
            if (m_originalArcRadians >= float.PositiveInfinity)
            {
                arc = Mathf.Atan2(-m_boss.WorldPosition.z, -m_boss.WorldPosition.x);
                m_radiusOfCircle = Definition.MinDistanceTargetX;
            }
            else
            {
                arc = m_finalArcRadians;
            }
            m_originalArcRadians = arc;
            float distance = Vector3.Distance(m_boss.WorldPosition, ArenaCentre);
            m_radiusOfCircle = Random.Range(Definition.MinDistanceTargetX, Mathf.Abs(Definition.DistanceAllowedFromOriginAdjustedForRadius - distance));
            Vector3 bossPosition = m_boss.WorldPosition;
            Vector3 centre = bossPosition.PointOnCircleWithFixedY(m_originalArcRadians, m_radiusOfCircle, bossPosition.y);
            m_originalArcRadians = Mathf.Atan2(m_boss.WorldPosition.z - centre.z, m_boss.WorldPosition.x - centre.x);
            return centre;
        }

        // 06000ab2: a random point on the circle, up to one full turn ahead of the original angle. Too many retries set up
        // a new circle; a point too far from the arena centre is retried while the ability is still running.
        private Vector3 CalculateTargetPositionFromNewCenter()
        {
            m_recursionCount++;
            if (m_recursionCount > m_recursionLimit)
                SetupAbility();
            m_finalArcRadians = Random.Range(m_originalArcRadians, m_originalArcRadians + Mathf.PI * 2f);
            Vector3 position = m_centerOfCircle.PointOnCircleWithFixedY(m_finalArcRadians, m_radiusOfCircle, m_boss.WorldPosition.y);
            if ((position - ArenaCentre).sqrMagnitude > Definition.DistanceAllowedFromOriginAdjustedForDiameterSquared && !AbilityFinished)
                return CalculateTargetPositionFromNewCenter();
            return position;
        }
    }
}
