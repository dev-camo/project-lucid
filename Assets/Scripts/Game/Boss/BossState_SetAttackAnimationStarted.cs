using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000212; bodies translated from the 1.10.1 x86-64 binary.
    // Marks the offensive ability's attack animation as started on entry.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/{0}")]
    [GraphNodeDefaultName("SetAttackAnimationStarted")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_SetAttackAnimationStarted : ActorState_Ability<ActorAbility>
    {
        protected BossState_SetAttackAnimationStarted(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetAttackAnimationStarted(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ActorAbility ability = GetAbility(user.GetAs<Boss>());
            if (ability == null)
                return;
            (ability as IOffensiveAbility).SetAttackAnimStarted();
        }
    }
}
