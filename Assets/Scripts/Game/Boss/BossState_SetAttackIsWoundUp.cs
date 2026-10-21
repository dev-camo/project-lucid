using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000215; bodies translated from the 1.10.1 x86-64 binary.
    // Marks the offensive ability's attack as wound up on entry.
    [GraphNodeDefaultName("SetAttackIsWoundUp")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/{0}")]
    public class BossState_SetAttackIsWoundUp : ActorState_Ability<ActorAbility>
    {
        protected BossState_SetAttackIsWoundUp(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetAttackIsWoundUp(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ActorAbility ability = GetAbility(user.GetAs<Boss>());
            if (ability == null)
                return;
            (ability as IOffensiveAbility).SetAttackWoundUp();
        }
    }
}
