using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000214; bodies translated from the 1.10.1 x86-64 binary.
    // Marks the offensive ability's attack as winding up on entry.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("SetAttackIsWindingUp")]
    [GraphNodeMenuFormat("Boss/{0}")]
    public class BossState_SetAttackIsWindingUp : ActorState_Ability<ActorAbility>
    {
        protected BossState_SetAttackIsWindingUp(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetAttackIsWindingUp(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ActorAbility ability = GetAbility(user.GetAs<Boss>());
            if (ability == null)
                return;
            (ability as IOffensiveAbility).SetAttackWindingUp();
        }
    }
}
