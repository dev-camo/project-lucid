using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000635; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbility_GroundAttack : EnemyAbility
    {
        private EnemyAbilityDefinition_GroundAttack m_definition;

        // 0600217b (1.10.1 RVA 0x56FFE0)
        public override AbilityDefinition DefinitionBase => m_definition;

        // 0600217c (1.10.1 RVA 0x56FFF0)
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_definition = definition as EnemyAbilityDefinition_GroundAttack;
        }

        // 0600217d (1.10.1 RVA 0x570160): the attack is triggered while the brain stands still, locked on, and winding
        // up. The wind-up timer lives in the actor's own storage (not the brain's), advances while the animator is
        // ready for the wind-up, and is read with the frame time as its stored default. Beyond the wind-up time it is
        // reset and the brain is told the wind-up is complete.
        protected override void DoOnUpdate(float deltaTime)
        {
            IsTriggered = m_enemy.BrainMovement == Vector2.zero && m_enemy.BrainTargetLocked && m_enemy.BrainWindUpAttack;
            if (m_enemy.AnimatorReadyWindUpAttack)
            {
                m_enemy.Storage.AdjustValue(EnemyBrainFSMKeys.AttackWindUpTimer, deltaTime);
            }

            float windUpTimer = m_enemy.Storage.GetValue(EnemyBrainFSMKeys.AttackWindUpTimer, deltaTime, true);
            if (windUpTimer > m_definition.WindUpAttackTime)
            {
                m_enemy.SetAttackWindUpComplete(true);
                m_enemy.Storage.SetValue(EnemyBrainFSMKeys.AttackWindUpTimer, 0f);
            }
            AbilityFinished = m_enemy.AnimatorAttackComplete;
        }

        // 0600217e (1.10.1 RVA 0x570440): stands still, keeping only a downward local y velocity.
        public override void UpdateMovement(float deltaTime)
        {
            float verticalVelocity = m_enemy.LocalVelocity.y;
            m_enemy.SetLocalVelocity(new Vector3(0f, verticalVelocity < 0f ? verticalVelocity : 0f, 0f));
        }

        // 0600217f (1.10.1 RVA 0x5704B0)
        public override void UpdateMovementAnimation(ActorAnimationDefinition animation)
        {
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement, 0f);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Lateral, 0f);
        }

        // 06002180 (1.10.1 RVA 0x570500): the detached proxy follows the enemy's rotation, whatever the navigation.
        public override void UpdateVisualProxyMovement(float deltaTime)
        {
            if (m_enemy.Definition.Traits.ShouldDetachVisualProxy)
            {
                m_enemy.VisualProxy.UpdateRotation(m_enemy.WorldRotation, deltaTime);
            }
        }

        // 06002181 (1.10.1 RVA 0x570540)
        public EnemyAbility_GroundAttack() { }
    }
}
