using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyBrain : IGraphUser
    {
        public IGraphStorage Storage { get; private set; }
        public Enemy Enemy { get; private set; }
        public Actor Target { get; private set; }
        private FiniteStateMachine m_fsm;

        public EnemyNavigation Navigation => Enemy.Navigation;

        public void Initialise(Enemy enemy, FiniteStateMachine brainFSM)
        {
            Enemy = enemy;
            Storage = new FSMStorage(0);
            m_fsm = brainFSM;
            m_fsm.InitialiseUser(this, null, null);
        }

        public Vector2 GetMovement() => Storage.GetValue(EnemyBrainFSMKeys.MovementImpulse, default(Vector2), true);
        public bool GetTargetLocked() => Storage.GetValue(EnemyBrainFSMKeys.TargetLocked, false, true);
        public bool GetWindUpAttack() => Storage.GetValue(EnemyBrainFSMKeys.StartAttackWindUp, false, true);
        public bool GetAttackTarget() => Storage.GetValue(EnemyBrainFSMKeys.AttackTarget, false, true);
        public bool GetSightingReactionTrigger() => Storage.GetValue(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, false, true);
        public void SetAnimatorWindUpReady(bool setting) => Storage.SetValue(EnemyBrainFSMKeys.AnimatorReadyWindUp, setting);
        public void ConsumeSightingReactionTrigger() => Storage.SetValue(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, false);
        public float GetAttackWindUpTimer() => Storage.GetValue(EnemyBrainFSMKeys.AttackWindUpTimer, 0f, true);
        public bool GetAttackWindUpAnimationComplete() => Storage.GetValue(EnemyBrainFSMKeys.AttackWindUpComplete, false, true);
        public void SetAttackAnimationComplete(bool setting) => Storage.SetValue(EnemyBrainFSMKeys.AttackAnimationCompleted, setting);
        public void SetMovementImpulse(Vector2 movement) => Storage.SetValue(EnemyBrainFSMKeys.MovementImpulse, movement);

        public void OnFixedUpdate(float deltaTime)
        {
            SenseTarget();
            m_fsm.Update(this, new FSMUpdateContext(deltaTime, FSMUpdateType.Update));
        }

        private void SenseTarget()
        {
            var targets = Enemy.EnemyManager?.GetTargetsForEnemies();
            if (targets == null)
            {
                Target = null;
                return;
            }

            Vector3 enemyPosition = Enemy.WorldPosition;
            Actor closestTarget = null;
            float closestDistance = float.MaxValue;
            foreach (Actor actor in targets)
            {
                Vector3 targetPosition = actor.WorldPosition;
                float distance = (enemyPosition - targetPosition).sqrMagnitude;
                if (distance < closestDistance && CanReachTarget(targetPosition))
                {
                    closestTarget = actor;
                    closestDistance = distance;
                }
            }
            Target = closestTarget;
        }

        public bool CanSeeTarget(Actor actor)
        {
            return (Enemy.WorldPosition - actor.WorldPosition).sqrMagnitude < Enemy.Definition.Traits.SightDistanceSquared;
        }

        public bool CanReachTarget(Vector3 position)
        {
            return !Navigation.Constrain || Navigation.CanReachPosition(position);
        }

        public void Close()
        {
            m_fsm.ClearUser(this, null);
            m_fsm = null;
            Storage.Clear();
            Storage = null;
            Enemy = null;
        }

        public void DestroyUser() => Close();
        public EnemyTraits GetTraits() => Enemy.Definition.Traits;
        public EnemyBrain() { }
    }
}
