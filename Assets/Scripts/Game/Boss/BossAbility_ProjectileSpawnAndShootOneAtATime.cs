using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001eb; bodies translated from the 1.10.1 x86-64 binary (0x6FD4A0..0x6FE0F0).
    // The guardian's one-at-a-time attack: the projectiles spawn on a circle around the spawn point, move one by one into
    // randomly chosen lanes (a lane is not reused until LaneIgnoreCount others were), and are released in turn.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_ProjectileSpawnAndShootOneAtATime : BossAbility_ProjectileSpawnAndShootBase<BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime>
    {
        private Queue<Transform> m_blockedLanes = new Queue<Transform>();

        public override AbilityDefinition DefinitionBase => Definition; // 06000afd

        // 06000afe: the blocked-lane queue is rebuilt with room for the lanes to ignore.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            Definition = definition as BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime;
            m_blockedLanes = new Queue<Transform>(Definition.LaneIgnoreCount);
        }

        // 06000aff: the native body runs the base entry both first and last. The spawn order is a shuffled list of the
        // projectile numbers and every child of the spawn transform is an available lane.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_currentTargetPosition = m_boss.WorldPosition;
            m_boss.Storage.SetValue(BossFSMKeys.GuardianProjectileMovement, Definition.InitialMovementState);
            m_availableLanes.Clear();
            m_blockedLanes.Clear();
            if (m_projectileSpawnOrder.Count != m_projectileCount)
            {
                m_projectileSpawnOrder.Clear();
                for (int i = 0; i < m_projectileCount; i++)
                    m_projectileSpawnOrder.Add(i);
            }
            m_projectileSpawnOrder.Shuffle();
            for (int i = 0; i < m_spawnTransform.childCount; i++)
                m_availableLanes.Add(m_spawnTransform.GetChild(i));
            base.DoOnEnter();
        }

        // 06000b00: in the groups that carry a returning projectile, each projectile may be it, with a chance rising to one
        // for the last of the group; once chosen the rest are killer projectiles. A zero group size divides by zero.
        protected override void DecideProjectileType(int projectileIndex, out ProjectileType projectileType)
        {
            projectileType = Definition.KillerProjectile;
            if (m_projectileGroupCount % Definition.ProjectileGroupToContainReturnable == 0 && !m_hasReturningProjectile)
            {
                float chance = Random.Range(0f, 1f);
                m_hasReturningProjectile = (projectileIndex + 1f) / m_projectileCount > chance;
                projectileType = m_hasReturningProjectile ? Definition.ReturningProjectile : Definition.KillerProjectile;
            }
        }

        // 06000b01
        protected override Spline GetSpline(int _)
        {
            HLOutput.LogError("Spline movement is not supported by BossAbility_ProjectileSpawnAndShootOneAtATime");
            return null;
        }

        // 06000b02: the projectiles spawn evenly spread (in whole degrees) on a circle around the spawn point. The point's
        // components are handed to the transform rotated, as (y, z, x), exactly as the native body does.
        protected override void OffsetSpawnLocation(int projectileIndex, Transform transform)
        {
            float degrees = 360 / m_projectileCount * projectileIndex;
            Vector3 point = Vector3.zero.PointOnCircleWithFixedYLHS(degrees * Mathf.Deg2Rad, Definition.ProjectileSpawnCircleRadius, 0f);
            transform.position = m_spawnTransform.TransformPoint(new Vector3(point.y, point.z, point.x));
        }

        protected override Transform GetLaneChoice(int _) => m_availableLanes[Random.Range(0, m_availableLanes.Count)]; // 06000b03
        protected override Vector3 GetSpawnOrigin(int _) => m_spawnTransform.position; // 06000b04

        // 06000b05: after the base spawning, waits out the delay between spawning and moving into the lanes.
        protected override bool Spawning(float deltaTime)
        {
            if (base.Spawning(deltaTime))
                return true;
            if (Definition.TimeBetweenSpawnAndLaneMoveSeconds > m_spawnDelayTimer)
            {
                m_spawnDelayTimer = m_spawnDelayTimer + deltaTime;
                return true;
            }
            return false;
        }

        // 06000b06: over the launch time the current projectile glides to its lane position, and once its share of the time has
        // passed it is detached and fired. When the launch time has run out the strike effects play. The result is whether the
        // launch time was still running at the start of the call.
        protected override bool Attacking(float deltaTime)
        {
            float launchTime = Definition.ProjectileGroupLaunchTimeSeconds;
            float fireTimer = m_projectileFireTimer;
            if (launchTime > fireTimer)
            {
                m_projectileFireTimer = fireTimer + deltaTime;
                int index = m_currentlyFiringProjectileIndex;
                if (m_projectileFireTimer > index * m_projectileFireDivisible)
                {
                    Transform gliding = m_projectiles[index].transform;
                    gliding.localPosition = Vector3.MoveTowards(gliding.localPosition, Vector3.zero,
                        deltaTime * Definition.ProjectileMoveToLaneVelocity);
                }
                if (m_projectileFireTimer > (m_currentlyFiringProjectileIndex + 1) * m_projectileFireDivisible)
                {
                    Projectile projectile = m_projectiles[m_currentlyFiringProjectileIndex];
                    projectile.transform.localPosition = Vector3.zero;
                    projectile.transform.parent = null;
                    projectile.Fire();
                    m_currentlyFiringProjectileIndex++;
                }
            }
            else
            {
                TriggerAttackStrikeEffects();
                m_attackAnimStarted = false;
                m_attackAnimationTimer = 0f;
            }
            return launchTime > fireTimer;
        }

        // 06000b07
        public void SetProjectileCount(int count)
        {
            m_projectileCount = count;
            m_projectileFireDivisible = Definition.ProjectileGroupLaunchTimeSeconds / count;
            m_projectSpawnDivisible = Definition.ProjectileGroupSpawnTimeSeconds / count;
        }

        // 06000b08: a used lane is blocked, and the lane blocked longest comes free once enough others were used after it.
        protected override void BlockSpawnLane(Transform transform)
        {
            if (m_blockedLanes.Count >= Definition.LaneIgnoreCount)
                m_availableLanes.Add(m_blockedLanes.Dequeue());
            m_blockedLanes.Enqueue(transform);
            m_availableLanes.Remove(transform);
        }
    }
}
