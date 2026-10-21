using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000656; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Enemy/Movement/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyMovementState : ActorState_Ability<EnemyAbility>
    {
        // 06002220 (1.10.1 RVA 0x577B20)
        protected EnemyMovementState(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 06002221 (1.10.1 RVA 0x577B90)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyMovementState(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        // 06002222 (1.10.1 RVA 0x577C50): the enemy's ability for this state moves the enemy, then its visual proxy,
        // then sets the movement animation on the state's own animation definition (not the ability's).
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            EnemyAbility ability = GetAbility(user.GetAs<Enemy>());
            ability.UpdateMovement(context.DeltaTime);
            ability.UpdateVisualProxyMovement(context.DeltaTime);
            ability.UpdateMovementAnimation(m_stateAnimationDefinition);
        }
    }
}
