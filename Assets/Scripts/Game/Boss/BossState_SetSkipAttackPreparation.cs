using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200021e; bodies translated from the 1.10.1 x86-64 binary.
    // Switches the offensive ability's attack flow on and skips its attack preparation, with this state's animation.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("SetSkipAttackPreparation")]
    [GraphNodeMenuFormat("Boss/{0}")]
    public class BossState_SetSkipAttackPreparation : ActorState_Ability<ActorAbility>
    {
        protected BossState_SetSkipAttackPreparation(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetSkipAttackPreparation(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ActorAbility ability = GetAbility(user.GetAs<Boss>());
            if (ability == null)
                return;
            IOffensiveAbility offensive = ability as IOffensiveAbility;
            offensive.SetAttackFlow(true);
            offensive.SkipAttackPreparation(m_stateAnimationDefinition);
        }
    }
}
