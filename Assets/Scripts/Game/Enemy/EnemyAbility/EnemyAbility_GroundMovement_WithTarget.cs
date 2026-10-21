using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000637; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyAbility_GroundMovement_WithTarget : EnemyAbility_GroundMovement
    {
        // 06002183 (1.10.1 RVA 0x570590): triggered when the brain is steering and has locked on to the target.
        protected override void DoOnUpdate(float deltaTime)
        {
            IsTriggered = m_enemy.BrainTargetLocked && m_enemy.BrainMovement != Vector2.zero;
        }

        // 06002184 (1.10.1 RVA 0x570620): the movement animation takes the definition's forward speed and whether the
        // enemy is turning, with no lateral or movement flag.
        public override void UpdateMovementAnimation(ActorAnimationDefinition animation)
        {
            if (m_enemy.BrainTargetSpotted)
            {
                m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.BoolSet, true);
                m_enemy.ConsumeSightingReactionTrigger();
            }
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement, Definition.ForwardSpeed);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Turning, m_thisFrameMovement.x != 0f);
        }

        // 06002185 (1.10.1 RVA 0x5706F0): the detached proxy follows the enemy's rotation, whatever the navigation.
        public override void UpdateVisualProxyMovement(float deltaTime)
        {
            if (m_enemy.Definition.Traits.ShouldDetachVisualProxy)
            {
                m_enemy.VisualProxy.UpdateRotation(m_enemy.WorldRotation, deltaTime);
            }
        }

        // 06002186 (1.10.1 RVA 0x570730)
        public EnemyAbility_GroundMovement_WithTarget() { }
    }
}
