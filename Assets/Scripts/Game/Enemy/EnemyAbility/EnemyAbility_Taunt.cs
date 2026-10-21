using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200063a; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyAbility_Taunt : EnemyAbility
    {
        private EnemyAbilityDefinition_Taunt m_definition;
        private float m_tauntedForSeconds;

        // 06002196 (1.10.1 RVA 0x570A90)
        public override AbilityDefinition DefinitionBase => m_definition;

        // 06002197 (1.10.1 RVA 0x570AA0)
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_definition = definition as EnemyAbilityDefinition_Taunt;
        }

        // 06002198 (1.10.1 RVA 0x570C10): the taunt follows the enemy having hit the player.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_tauntedForSeconds = 0f;
            IsTriggered = m_enemy.HitPlayer;
        }

        // 06002199 (1.10.1 RVA 0x570C40): a taunt time of zero lasts until the player hit is cleared elsewhere;
        // otherwise the hit is cleared once the taunt has run for the taunt time.
        protected override void DoOnUpdate(float deltaTime)
        {
            IsTriggered = m_enemy.HitPlayer;
            if (!IsTriggered || m_definition.TauntTime == 0f)
            {
                return;
            }

            m_tauntedForSeconds += deltaTime;
            if (m_definition.TauntTime > m_tauntedForSeconds)
            {
                return;
            }
            m_enemy.ClearHitPlayer();
            IsTriggered = false;
        }

        // 0600219a (1.10.1 RVA 0x570C90)
        public override void UpdateMovement(float deltaTime)
        {
            m_enemy.SetLocalVelocity(Vector3.zero);
        }

        // 0600219b (1.10.1 RVA 0x570CE0)
        public override void UpdateMovementAnimation(ActorAnimationDefinition animation)
        {
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement, 0f);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Lateral, 0f);
        }

        // 0600219c (1.10.1 RVA 0x570D30)
        public EnemyAbility_Taunt() { }
    }
}
