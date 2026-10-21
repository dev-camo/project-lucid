using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000634; bodies translated from the 1.10.1 x86-64 binary. The ability picks the nearest
    // valid target, publishes its position to the brain, warms a projectile up on the enemy, fires it when the wind-up
    // time has passed and finishes after the wind-down time.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbility_FireProjectile : EnemyAbility_Movement<EnemyAbilityDefinition_FireProjectile>
    {
        // 0600216d (1.10.1 RVA 0x56ED00)
        public override AbilityDefinition DefinitionBase => Definition;

        // 0600216e (1.10.1 RVA 0x56ED30), 0600216f (1.10.1 RVA 0x56ED40)
        public Vector3 TargetPosition { get; private set; }

        private Projectile m_projectileInWarmup;
        private float m_windUpElapsedTime;

        // 06002170 (1.10.1 RVA 0x56ED50)
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            AbilityFinished = false;
            SetTriggered(false);
        }

        // 06002171 (1.10.1 RVA 0x56ED80): triggering publishes the target position on the brain and returns. Clearing
        // removes it and the two completion flags, and kills a projectile still warming up. The completion flags are
        // removed through the Vector3 instantiation of RemoveValue (the native method info is the same for all three
        // keys); the type argument is not consulted by the storage.
        private void SetTriggered(bool triggered)
        {
            if (triggered)
            {
                m_windUpElapsedTime = 0f;
                IsTriggered = true;
                m_enemy.Brain.Storage.SetValue(EnemyBrainFSMKeys.FireProjectileTargetPosition, TargetPosition);
                return;
            }

            IsTriggered = false;
            m_enemy.Brain.Storage.RemoveValue<Vector3>(EnemyBrainFSMKeys.FireProjectileTargetPosition);
            m_enemy.Storage.RemoveValue<Vector3>(ActorFSMKeys.EnemyFireProjectileTurnComplete);
            m_enemy.Storage.RemoveValue<Vector3>(ActorFSMKeys.EnemyFireProjectileWindUpComplete);
            if (m_projectileInWarmup != null)
            {
                m_projectileInWarmup.Kill();
                m_projectileInWarmup = null;
            }
        }

        // 06002172 (1.10.1 RVA 0x56F180): while idle, every enemy target whose aim point passes TargetValid is
        // considered and the nearest one (the later of equal distances) is chosen, then the accuracy offset is
        // applied. A target is only taken when there is any enemy target at all.
        protected override void DoOnUpdate(float deltaTime)
        {
            if (IsTriggered)
            {
                UpdateWhileTriggered(deltaTime);
                return;
            }

            IReadOnlyCollection<Actor> targets = m_enemy.EnemyManager.GetTargetsForEnemies();
            if (targets.Count == 0)
            {
                return;
            }

            Vector3 closestPosition = Vector3.zero;
            float closestDistanceSqr = float.MaxValue;
            foreach (Actor target in targets)
            {
                if (!target.ActorAttachPoints.TryGetNodeTransform(Definition.AimForOnTarget, out Transform node))
                {
                    continue;
                }
                Vector3 position = node.position;
                if (!TargetValid(position, out float distanceSqr) || distanceSqr > closestDistanceSqr)
                {
                    continue;
                }
                closestPosition = position;
                closestDistanceSqr = distanceSqr;
            }

            if (closestDistanceSqr == float.MaxValue)
            {
                return;
            }
            TargetPosition = ApplyAccuracy(closestPosition);
            SetTriggered(true);
        }

        // 06002173 (1.10.1 RVA 0x56F770): nothing happens while the brain still holds the published target position.
        // The projectile is warmed up on the first update, fired when the elapsed time crosses the wind-up time, and
        // the ability finishes when the elapsed time reaches wind-up plus wind-down. Once the projectile is gone only
        // the wind-down is timed.
        private void UpdateWhileTriggered(float deltaTime)
        {
            if (m_enemy.Brain.Storage.HasValue<Vector3>(EnemyBrainFSMKeys.FireProjectileTargetPosition))
            {
                return;
            }

            float previousElapsed = m_windUpElapsedTime;
            if (m_projectileInWarmup == null && !(previousElapsed <= 0f))
            {
                m_windUpElapsedTime = deltaTime + previousElapsed;
            }
            else
            {
                if (m_projectileInWarmup == null)
                {
                    WarmupProjectile();
                    m_enemy.Storage.SetValue(ActorFSMKeys.EnemyFireProjectileTurnComplete, true);
                }
                m_windUpElapsedTime = deltaTime + previousElapsed;
                float windUpTime = Definition.WindUpTime;
                if (!(previousElapsed < windUpTime && m_windUpElapsedTime >= windUpTime))
                {
                    return;
                }
                FireProjectile();
                m_enemy.Storage.SetValue(ActorFSMKeys.EnemyFireProjectileWindUpComplete, true);
            }

            float totalTime = Definition.WindUpTime + Definition.WindDownTime;
            if (previousElapsed < totalTime && m_windUpElapsedTime >= totalTime)
            {
                AbilityFinished = true;
                SetTriggered(false);
            }
        }

        // 06002174 (1.10.1 RVA 0x56FAA0): within range, and the sine of the elevation of the direction to the target
        // above the enemy's up direction lies inside the configured band.
        private bool TargetValid(Vector3 position, out float distanceSqr)
        {
            Vector3 toTarget = position - m_enemy.WorldPosition;
            distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr > Definition.RangeSquared)
            {
                return false;
            }
            float elevationSine = Vector3.Dot(m_enemy.UpDirection, toTarget.normalized);
            return elevationSine <= Definition.VerticalAngleMaxSine && elevationSine >= Definition.VerticalAngleMinSine;
        }

        // 06002175 (1.10.1 RVA 0x56FC50)
        private Vector3 ApplyAccuracy(Vector3 target)
        {
            return target + Random.insideUnitSphere * Definition.AccuracyMetres;
        }

        // 06002176 (1.10.1 RVA 0x56FEB0)
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            SetTriggered(false);
        }

        // 06002177 (1.10.1 RVA 0x56FCB0): the comparison of the previous projectile with null is evaluated and its
        // result discarded, so a projectile already warming up is replaced without being killed.
        private void WarmupProjectile()
        {
            _ = m_projectileInWarmup != null;
            Projectile projectile = ProcessManager.GetSystem<ProjectileManager>().AcquireProjectile(Definition.ProjectileType);
            projectile.Setup(TargetPosition, m_enemy.ColliderCollision);
            m_enemy.ActorAttachPoints.Attach(m_enemy, Definition.ShootFromOnEnemy, projectile.transform, false);
            m_projectileInWarmup = projectile;
        }

        // 06002178 (1.10.1 RVA 0x56FDF0)
        private void FireProjectile()
        {
            m_enemy.ActorAttachPoints.Detach(Definition.ShootFromOnEnemy, m_projectileInWarmup.transform, true, true);
            m_projectileInWarmup.UpdateFocus(TargetPosition);
            m_projectileInWarmup.Fire();
            m_projectileInWarmup = null;
        }

        // 06002179 (1.10.1 RVA 0x56FED0): while triggered, a red line from the shoot point to the target position and
        // a unit sphere on the target.
        public override bool OnDrawGizmosSelected()
        {
            if (!base.OnDrawGizmosSelected() || !IsTriggered)
            {
                return false;
            }
            if (!m_enemy.ActorAttachPoints.TryGetNodeTransform(Definition.ShootFromOnEnemy, out Transform shootPoint))
            {
                return false;
            }

            Gizmos.color = new Color(1f, 0f, 0f, 1f);
            Gizmos.DrawLine(shootPoint.position, TargetPosition);
            Gizmos.DrawSphere(TargetPosition, 1f);
            return true;
        }

        // 0600217a (1.10.1 RVA 0x56FFA0)
        public EnemyAbility_FireProjectile() { }
    }
}
