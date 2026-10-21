using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000633; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyAbility_Dead : EnemyAbility
    {
        private EnemyAbilityDefinition_Dead m_definition;

        // 06002167 (1.10.1 RVA 0x56E9F0)
        public override AbilityDefinition DefinitionBase => m_definition;

        // 06002168 (1.10.1 RVA 0x56EA00)
        public override void Initialise(Actor actor, AbilityDefinition definition)
        {
            base.Initialise(actor, definition);
            m_definition = definition as EnemyAbilityDefinition_Dead;
        }

        // 06002169 (1.10.1 RVA 0x56EB70): triggered while the enemy is dead. A revive time of zero keeps it dead for
        // good; otherwise the dead timer in the actor's storage counts up and the enemy is revived once it reaches
        // the revive time.
        protected override void DoOnUpdate(float deltaTime)
        {
            IsTriggered = m_enemy.IsDead;
            if (!IsTriggered || m_definition.ReviveTime == 0f)
            {
                return;
            }

            float deadTime = m_enemy.Storage.AdjustValue(ActorFSMKeys.EnemyDeadTimer, deltaTime);
            if (m_definition.ReviveTime > deadTime)
            {
                return;
            }
            IsTriggered = false;
            m_enemy.OnRevive();
        }

        // 0600216a (1.10.1 RVA 0x56EC50)
        public override void UpdateMovement(float deltaTime)
        {
            m_enemy.SetLocalVelocity(Vector3.zero);
        }

        // 0600216b (1.10.1 RVA 0x56ECA0)
        public override void UpdateMovementAnimation(ActorAnimationDefinition animation)
        {
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement, 0f);
            m_enemy.Animator.TrySetFromAnimationType(animation, ActorAnimationType.Enemy_Movement_Lateral, 0f);
        }

        // 0600216c (1.10.1 RVA 0x56ECF0)
        public EnemyAbility_Dead() { }
    }
}
