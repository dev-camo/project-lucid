using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000201; bodies translated from the 1.10.1 x86-64 binary.
    // Removes the crab's attack type override from its FSM storage.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("ClearAttackOverride")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    public class BossState_ClearAttackOverride : ActorState_Effect
    {
        protected BossState_ClearAttackOverride(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ClearAttackOverride(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.Storage.RemoveValue<BossAttackTypes>(BossFSMKeys.BossAttackTypeOverride);
        }
    }
}
