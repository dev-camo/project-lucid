using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001db; bodies translated from the 1.10.1 x86-64 binary (0x6F7270..0x6F7F80).
    // The crab body's jump: a triangle of landing points is laid out around the arena centre, one jump per attack index.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_BodySlam : ActorAbility, IAbilityTrigger, IOffensiveAbility
    {
        private const int PointsInATriangle = 3;

        public BossAbilityDefinition_BodySlam Definition;

        private bool m_startWindup;
        private bool m_isWindingUp;
        private bool m_isWoundUp;
        private bool m_canTriggerAttackAnim;
        private bool m_attackAnimStarted;
        private bool m_waveSpawned;
        private Boss m_boss;
        private Vector3 m_preJumpPosition;
        private Vector3 m_currentTargetPosition;
        private bool m_abilityActivated;
        private float m_windUpTimer;

        public override AbilityDefinition DefinitionBase => Definition; // 06000a2f
        public bool StartWindUp => m_startWindup; // 06000a30
        public bool WoundUp => m_isWoundUp; // 06000a31
        public bool IsTriggered => m_canTriggerAttackAnim; // 06000a32
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000a33
        public int AttackIndex { get; private set; } // 06000a34 / 06000a35

        private readonly Vector3[] m_coordinateArray = new Vector3[PointsInATriangle];
        private float m_slamTimer;
        private float m_attackAnimationTimer;
        private float m_currentFarRangeSquared;
        private float m_extraRange;
        private GraphStorageKey m_bodyslamCountFSMVar = new GraphStorageKey("bodySlamCount", 0, 0);

        // 06000a36: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_BodySlam;
        }

        // 06000a37: the jump starts from, and initially aims at, the boss's current position.
        protected override void DoOnEnter()
        {
            m_preJumpPosition = m_boss.WorldPosition;
            m_currentTargetPosition = m_boss.WorldPosition;
            CalculateTriangleCoordinates();
            base.DoOnEnter();
        }

        // 06000a38
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_abilityActivated = false;
            m_attackAnimationTimer = 0f;
            m_extraRange = 0f;
            // Native clears the six flags at 0x20..0x25 together (0x6F7836, 0x6F783D).
            m_startWindup = false;
            m_isWindingUp = false;
            m_isWoundUp = false;
            m_canTriggerAttackAnim = false;
            m_attackAnimStarted = false;
            m_waveSpawned = false;
        }

        // 06000a39: the slam only runs once the wind-up has been wound up; its end finishes the ability.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (m_abilityActivated && !WindingUp())
            {
                if (!Attacking(deltaTime))
                    AbilityFinished = true;
            }
        }

        // 06000a3a
        private void TriggerAttackStrikeEffects()
        {
            m_boss.Storage.SetValue(BossFSMKeys.BossEffectType, BossEffectTriggerTypes.AttackStrike);
            m_boss.TriggerEffects();
        }

        // 06000a3b: the camera target is only spawned for the first jump of a cycle.
        private void FireProjectileFromBoss()
        {
            ProcessManager.GetSystem<BossManager>().SpawnObjectFromBoss<Hazard>(Definition.Projectile, m_boss,
                ActorAttachPointType.Boss_Crab_Body_VulnerablePoint, Definition.ProjectileLifetime);
            if (AttackIndex != 0)
                return;
            ProcessManager.GetSystem<BossManager>().SpawnObjectFromBoss<Transform>(Definition.CameraTarget, m_boss,
                ActorAttachPointType.Boss_Crab_Body_VulnerablePoint, Definition.CameraTargetLifetime);
        }

        // 06000a3c: true while the wind-up has not completed. It also drops the start request once winding up or wound up.
        private bool WindingUp()
        {
            if (m_isWindingUp)
            {
                m_startWindup = false;
                return !m_isWoundUp;
            }
            if (m_isWoundUp)
            {
                m_startWindup = false;
                return false;
            }
            return true;
        }

        // 06000a3d: the first call only arms the animation. Returns true while the slam is still running.
        private bool Attacking(float deltaTime)
        {
            if (!m_canTriggerAttackAnim)
                m_canTriggerAttackAnim = true;
            if (!m_attackAnimStarted)
                return true;

            bool running = Definition.AttackAnimationLength > m_attackAnimationTimer;
            if (!running)
            {
                m_attackAnimStarted = false;
                m_attackAnimationTimer = 0f;
                return running;
            }
            if (m_attackAnimationTimer > Definition.TriggerWaveTime && !m_waveSpawned)
            {
                m_waveSpawned = true;
                TriggerAttackStrikeEffects();
                FireProjectileFromBoss();
            }
            float traversal = Mathf.Clamp01(Definition.TraversalCurve.Evaluate(m_attackAnimationTimer / Definition.TraversalTime));
            Vector3 target = m_coordinateArray[AttackIndex % PointsInATriangle];
            // Unclamped single-precision lerp on X and Z; Y stays at the pre-jump height.
            float x = (target.x - m_preJumpPosition.x) * traversal + m_preJumpPosition.x;
            float z = (target.z - m_preJumpPosition.z) * traversal + m_preJumpPosition.z;
            m_currentTargetPosition = new Vector3(x, m_preJumpPosition.y, z);
            m_attackAnimationTimer += deltaTime;
            return running;
        }

        // 06000a3e: a target is valid between the near range and the far range, which grows while nothing is in range.
        // The native body also evaluates (boss - character).normalized and discards it. Both range tests are negated
        // less-thans (cmpnltps), so a NaN distance is in range.
        public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime)
        {
            float squaredDistance = (boss.WorldPosition - character.WorldPosition).sqrMagnitude;
            bool inRange = !(m_currentFarRangeSquared < squaredDistance) && !(squaredDistance < Definition.TargetValidRangeNearSquared);
            m_boss.Storage.SetValue(BossFSMKeys.BossAttackType, inRange ? BossAttackTypes.BossCrab_Body : BossAttackTypes.None);
            if (!inRange)
                IncreaseValidAttackRange(deltaTime);
            return inRange;
        }

        public void SetAttackFlow(bool abilityActive) // 06000a3f
        {
            m_abilityActivated = abilityActive;
            m_startWindup = true;
        }

        public void SetAttackWindingUp() => m_isWindingUp = true; // 06000a40
        public void SetAttackWoundUp() => m_isWoundUp = true; // 06000a41
        public void SetAttackAnimStarted() => m_attackAnimStarted = true; // 06000a42

        public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition) // 06000a43
        {
            m_isWindingUp = true;
            m_isWoundUp = true;
        }

        public void ResetProjectileGroupCounter() { } // 06000a44, original empty body

        // 06000a45
        private void IncreaseValidAttackRange(float deltaTime)
        {
            m_extraRange += deltaTime * Definition.TargetGrowthPerSecond;
            m_currentFarRangeSquared = (m_extraRange + Definition.TargetValidRangeFar) * (m_extraRange + Definition.TargetValidRangeFar);
        }

        // 06000a46: only the first jump of a cycle (attack index zero) lays out the triangle. Its starting angle is the
        // stored angle change, defaulting to the boss's bearing, and every cycle then turns by a random amount.
        private void CalculateTriangleCoordinates()
        {
            AttackIndex = m_boss.Storage.GetValue(m_bodyslamCountFSMVar, 0, true);
            if (AttackIndex != 0)
                return;
            float bearing = Mathf.Atan2(m_boss.WorldPosition.x, m_boss.WorldPosition.z);
            float angle = m_boss.Storage.GetValue(BossFSMKeys.BossAttackAngleChange, bearing, true);
            m_coordinateArray[0] = Vector3.zero.PointOnCircleWithFixedY(angle, Definition.TriangleJumpDistance, m_boss.WorldPosition.y);
            m_coordinateArray[1] = Vector3.zero.PointOnCircleWithFixedY(angle + Definition.AngleOfJumpsAsRadians, Definition.TriangleJumpDistance, m_boss.WorldPosition.y);
            m_coordinateArray[2] = Vector3.zero.PointOnCircleWithFixedY(angle - Definition.AngleOfJumpsAsRadians, Definition.TriangleJumpDistance, m_boss.WorldPosition.y);
            m_boss.Storage.AdjustValue(BossFSMKeys.BossAttackAngleChange,
                Random.Range(Definition.MinTriangleRotationRadiansPerCycle, Definition.MaxTriangleRotationRadiansPerCycle));
        }
    }
}
