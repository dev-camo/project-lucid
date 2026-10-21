using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000638; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbility_Idle : EnemyAbility
    {
        private EnemyAbilityDefinition_Idle m_definition;

        // 06002187 (1.10.1 RVA 0x570770)
        public override AbilityDefinition DefinitionBase => m_definition;

        // 06002188 (1.10.1 RVA 0x570780)
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_definition = definition as EnemyAbilityDefinition_Idle;
        }

        // 06002189 (1.10.1 RVA 0x5708F0): idle while the brain is not steering, not winding up or attacking, and the
        // enemy is on the ground.
        protected override void DoOnUpdate(float deltaTime)
        {
            IsTriggered = m_enemy.BrainMovement == Vector2.zero
                && !m_enemy.BrainWindUpAttack
                && !m_enemy.BrainAttackTarget
                && m_enemy.CollidingWithGround;
        }

        // 0600218a (1.10.1 RVA 0x570990)
        public override void UpdateMovement(float deltaTime)
        {
            m_enemy.SetLocalVelocity(Vector3.zero);
        }

        // 0600218b (1.10.1 RVA 0x5709E0)
        public override void UpdateMovementAnimation(ActorAnimationDefinition animation)
        {
            if (m_enemy.BrainTargetSpotted)
            {
                m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.BoolSet, true);
                m_enemy.ConsumeSightingReactionTrigger();
            }
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement, 0f);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Lateral, 0f);
        }

        // 0600218c (1.10.1 RVA 0x570A80)
        public EnemyAbility_Idle() { }
    }
}
