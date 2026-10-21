using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001ea; bodies translated from the 1.10.1 x86-64 binary (0x6FC8E0..0x6FD4A0).
    // The guardian's row attack: each entry picks one of the authored row patterns (not one of the most recently used), spawns a
    // projectile in each lane of the row, and releases them all at once when the launch time has run out.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_ProjectileSpawnAndShootInRows : BossAbility_ProjectileSpawnAndShootBase<BossAbilityDefinition_ProjectileSpawnAndShootInRows>
    {
        private BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData m_spawnDataThisRow;
        private Queue<int> m_recentlyUsedPatterns = new Queue<int>();
        private List<Spline> m_splines = new List<Spline>();

        public override AbilityDefinition DefinitionBase => Definition; // 06000af1

        // 06000af2: the base takes the boss; the definition is this class's own cast.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            Definition = definition as BossAbilityDefinition_ProjectileSpawnAndShootInRows;
        }

        // 06000af3: the lane and spline lists are rebuilt from the spawn transform's children when their count changed. A
        // random row pattern sets the group size, the per-projectile launch and spawn intervals and the spawn order, and
        // chooses the lane that carries the returnable projectile.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_currentTargetPosition = m_boss.WorldPosition;
            m_projectileSpawnOrder.Clear();
            m_boss.Storage.SetValue(BossFSMKeys.GuardianProjectileMovement, Definition.InitialMovementState);
            if (m_availableLanes.Count != m_spawnTransform.childCount)
            {
                m_availableLanes.Clear();
                for (int i = 0; i < m_spawnTransform.childCount; i++)
                    m_availableLanes.Add(m_spawnTransform.GetChild(i));
            }
            if (Definition.InitialMovementState == GuardianProjectile.ProjectileMovementState.FiredOverhead
                && m_splines.Count != m_spawnTransform.childCount)
            {
                m_splines.Clear();
                for (int i = 0; i < m_spawnTransform.childCount; i++)
                    m_splines.Add(m_spawnTransform.GetChild(i).GetComponentInChildren<Spline>());
            }
            m_spawnDataThisRow = Definition.SpawnOrders[GetRandomRowPattern()];
            m_projectileCount = m_spawnDataThisRow.LanesInUseInt.Count;
            m_projectileFireDivisible = Definition.ProjectileGroupLaunchTimeSeconds / m_projectileCount;
            m_projectSpawnDivisible = Definition.ProjectileGroupSpawnTimeSeconds / m_projectileCount;
            foreach (int lane in m_spawnDataThisRow.LanesInUseInt)
                m_projectileSpawnOrder.Add(lane);
            m_spawnDataThisRow.ChooseReturnRow();
        }

        // 06000af4: a random pattern that was not used recently; the queue keeps the last RowPatternIgnoreCount of them.
        private int GetRandomRowPattern()
        {
            int pattern;
            do
            {
                pattern = Random.Range(0, Definition.SpawnOrders.Length);
            }
            while (m_recentlyUsedPatterns.Contains(pattern));
            m_recentlyUsedPatterns.Enqueue(pattern);
            if (m_recentlyUsedPatterns.Count > Definition.RowPatternIgnoreCount)
                m_recentlyUsedPatterns.Dequeue();
            return pattern;
        }

        // 06000af5: every group of ProjectileGroupToContainReturnable carries one returning projectile, in the row's returnable
        // lane. A zero group size divides by zero, as in the original.
        protected override void DecideProjectileType(int projectileIndex, out ProjectileType projectileType)
        {
            projectileType = Definition.KillerProjectile;
            if (m_projectileGroupCount % Definition.ProjectileGroupToContainReturnable == 0
                && m_spawnDataThisRow.ReturnableLane == projectileIndex)
            {
                m_hasReturningProjectile = true;
                projectileType = Definition.ReturningProjectile;
            }
        }

        protected override Spline GetSpline(int projectileIndex) => m_splines[projectileIndex]; // 06000af6

        protected override void OffsetSpawnLocation(int projectileIndex, Transform transform) => // 06000af7
            transform.position = GetLaneChoice(projectileIndex).position;

        protected override Transform GetLaneChoice(int projectileIndex) => m_availableLanes[projectileIndex]; // 06000af8
        protected override Vector3 GetSpawnOrigin(int projectileIndex) => GetLaneChoice(projectileIndex).position; // 06000af9

        // 06000afa: counts the launch time up; once it has run out every spawned projectile is detached from its lane and
        // fired. The result is whether the launch time was still running at the start of the call.
        protected override bool Attacking(float deltaTime)
        {
            float launchTime = Definition.ProjectileGroupLaunchTimeSeconds;
            float fireTimer = m_projectileFireTimer;
            if (launchTime > fireTimer)
            {
                m_projectileFireTimer = deltaTime + fireTimer;
            }
            else
            {
                foreach (Projectile projectile in m_projectiles)
                {
                    projectile.transform.localPosition = Vector3.zero;
                    projectile.transform.parent = null;
                    projectile.Fire();
                }
                TriggerAttackStrikeEffects();
                m_attackAnimStarted = false;
                m_attackAnimationTimer = 0f;
            }
            return launchTime > fireTimer;
        }

        protected override void BlockSpawnLane(Transform _) { } // 06000afb, original empty body
    }
}
