using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001e8 and its closure class 020001e9; bodies translated from the 1.10.1 x86-64 binary
    // (shared generic code 0xE3DE10..0xE3E85C, closure 0xB065B0). Base of the guardian's projectile attacks: after the
    // wind-up it spawns the group's projectiles one by one over the spawn time, hands them to the derived attack to launch,
    // and finishes when the derived attack is done. The derived classes assign Definition in their own Initialise.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BossAbility_ProjectileSpawnAndShootBase<T> : ActorAbility, IAbilityTrigger, IOffensiveAbility
        where T : BossAbilityDefinition_ProjectileSpawnAndShoot
    {
        protected T Definition;

        // The original auto-property is never assigned here; the derived abilities override it.
        public override AbilityDefinition DefinitionBase { get; } // 06000ad2

        public bool StartWindUp => m_startWindup; // 06000ad3
        public bool WoundUp => m_isWoundUp; // 06000ad4
        public bool IsTriggered => m_canTriggerAttackAnim; // 06000ad5
        public Vector3 CurrentTargetPosition => m_currentTargetPosition; // 06000ad6

        private bool m_startWindup;
        private bool m_isWindingUp;
        private bool m_isWoundUp;
        private bool m_canTriggerAttackAnim;
        protected bool m_attackAnimStarted;
        private bool m_projectilesSpawned;
        protected Boss m_boss;
        protected Vector3 m_currentTargetPosition;
        private bool m_abilityActivated;
        protected int m_projectileCount;
        protected int m_currentlyFiringProjectileIndex;
        private int m_currentlySpawningProjectileIndex;
        protected int m_projectileGroupCount;
        protected float m_attackAnimationTimer;
        protected float m_projectileFireTimer;
        private float m_projectileSpawnTimer;
        protected float m_spawnDelayTimer;
        protected float m_projectileFireDivisible;
        protected float m_projectSpawnDivisible;
        protected bool m_hasReturningProjectile;
        private bool m_spawnedCurrentProjectile;
        protected List<Projectile> m_projectiles = new List<Projectile>();
        protected List<Transform> m_availableLanes = new List<Transform>();
        protected List<int> m_projectileSpawnOrder = new List<int>();
        protected Transform m_spawnTransform;
        private SystemRef<BossManager> m_bossManagerRef;
        private BossManager m_bossManager;

        // 06000ad7: only the boss is taken here; the boss manager arrives when its system becomes valid.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            m_bossManagerRef = ProcessManager.GetSystemRef<BossManager>(null, true);
            m_bossManagerRef.InvokeOnValid(manager => m_bossManager = manager);
        }

        // 06000ad8
        protected override void DoOnEnter()
        {
            m_spawnTransform = m_boss.GetSpawnPoint<Projectile>(Definition.SpawnPointAttach);
            base.DoOnEnter();
        }

        // 06000ad9: leaving clears the group and counts it.
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_abilityActivated = false;
            m_hasReturningProjectile = false;
            m_spawnedCurrentProjectile = false;
            m_currentlyFiringProjectileIndex = 0;
            m_currentlySpawningProjectileIndex = 0;
            m_startWindup = false;
            m_isWindingUp = false;
            m_isWoundUp = false;
            m_canTriggerAttackAnim = false;
            m_attackAnimStarted = false;
            m_projectilesSpawned = false;
            m_attackAnimationTimer = 0f;
            m_projectileFireTimer = 0f;
            m_projectileSpawnTimer = 0f;
            m_spawnDelayTimer = 0f;
            m_projectiles.Clear();
            m_projectileGroupCount++;
        }

        // 06000ada: nothing happens until the wind-up has been wound up, then spawning and attacking run in turn.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (!m_abilityActivated || WindingUp() || Spawning(deltaTime) || Attacking(deltaTime))
                return;
            AbilityFinished = true;
        }

        // 06000adb
        protected void TriggerAttackStrikeEffects()
        {
            m_boss.Storage.SetValue(BossFSMKeys.BossEffectType, BossEffectTriggerTypes.AttackStrike);
            m_boss.TriggerEffects();
        }

        // 06000adc, 06000add
        protected abstract void DecideProjectileType(int projectileIndex, out ProjectileType projectileType);
        protected abstract Vector3 GetSpawnOrigin(int projectileIndex);

        // 06000ade: the projectile is spawned from the boss, aimed along its lane, placed and parented into the lane. At the
        // end of the frame it takes the definition's returnable spawn angle as its local rotation.
        private void SpawnAProjectile(int projectileIndex)
        {
            ProjectileType projectileType = default(ProjectileType);
            Transform transform;
            DecideProjectileType(projectileIndex, out projectileType);
            Projectile spawned = m_bossManager.SpawnProjectileFromBoss(projectileType, m_boss, GetSpawnOrigin(projectileIndex));
            transform = spawned.transform;
            GuardianProjectile guardianProjectile = spawned as GuardianProjectile;
            guardianProjectile.SetBoss(m_boss);
            if (Definition.InitialMovementState == GuardianProjectile.ProjectileMovementState.FiredOverhead)
                guardianProjectile.SetSpline(GetSpline(projectileIndex));
            guardianProjectile.SetProgressionRates(Definition.ProjectileRadiansPerSecond, Definition.ReturnRadiansPerSecond);
            guardianProjectile.SetCullingValues(Definition.CullTimeOnceBehindPlayerSeconds);
            Transform lane = GetLaneChoice(projectileIndex);
            OffsetSpawnLocation(projectileIndex, transform);
            BlockSpawnLane(lane);
            transform.SetParent(lane);
            CoroutineUtils.OnFrameEnd(() =>
            {
                Quaternion discarded = transform.localRotation;
                transform.localRotation = Quaternion.Euler(Definition.ReturnableProjectileSpawnAngle);
            });
            guardianProjectile.SetTargetDirection(lane.forward);
            m_projectiles.Add(guardianProjectile);
        }

        // 06000adf to 06000ae1
        protected abstract Spline GetSpline(int projectileIndex);
        protected abstract void OffsetSpawnLocation(int projectileIndex, Transform transform);
        protected abstract Transform GetLaneChoice(int projectileIndex);

        // 06000ae2: true while the wind-up is not wound up. It also drops the start request once winding up or wound up.
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

        // 06000ae3: waits for the attack animation to start and run its length, then spawns the group's projectiles in the
        // chosen order, each one at its share of the group spawn time. True while the group is still being spawned.
        protected virtual bool Spawning(float deltaTime)
        {
            if (!m_canTriggerAttackAnim)
                m_canTriggerAttackAnim = true;
            if (!m_attackAnimStarted)
                return true;
            if (Definition.AttackAnimationLengthSeconds > m_attackAnimationTimer)
            {
                m_attackAnimationTimer = m_attackAnimationTimer + deltaTime;
                return true;
            }
            if (!(Definition.ProjectileGroupSpawnTimeSeconds > m_projectileSpawnTimer) || m_projectilesSpawned)
            {
                m_projectilesSpawned = true;
                return false;
            }
            m_projectileSpawnTimer = m_projectileSpawnTimer + deltaTime;
            if (m_projectileSpawnTimer > m_currentlySpawningProjectileIndex * m_projectSpawnDivisible && !m_spawnedCurrentProjectile)
            {
                SpawnAProjectile(m_projectileSpawnOrder[m_currentlySpawningProjectileIndex]);
                m_spawnedCurrentProjectile = true;
            }
            if (m_projectileSpawnTimer > (m_currentlySpawningProjectileIndex + 1) * m_projectSpawnDivisible)
            {
                m_currentlySpawningProjectileIndex = m_currentlySpawningProjectileIndex + 1;
                m_spawnedCurrentProjectile = false;
            }
            return true;
        }

        // 06000ae4
        protected abstract bool Attacking(float deltaTime);

        public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime) => true; // 06000ae5

        public void SetAttackFlow(bool abilityActive) // 06000ae6
        {
            m_abilityActivated = abilityActive;
            m_startWindup = true;
        }

        public void SetAttackWindingUp() => m_isWindingUp = true; // 06000ae7
        public void SetAttackWoundUp() => m_isWoundUp = true; // 06000ae8
        public void SetAttackAnimStarted() => m_attackAnimStarted = true; // 06000ae9

        public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition) // 06000aea
        {
            m_isWindingUp = true;
            m_isWoundUp = true;
        }

        // 06000aeb
        protected abstract void BlockSpawnLane(Transform transform);

        // 06000aec: the group counter restarts at one.
        public virtual void ResetProjectileGroupCounter()
        {
            m_projectileGroupCount = 1;
        }
    }
}
