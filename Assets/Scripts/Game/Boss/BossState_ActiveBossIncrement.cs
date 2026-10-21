using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001fb; bodies translated from the 1.10.1 x86-64 binary.
    // Advances the Fourmen manager's active boss index by one, without configuring the bosses.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [GraphNodeDefaultName("ActiveBossIncrement")]
    public class BossState_ActiveBossIncrement : ActorState_Effect
    {
        protected BossState_ActiveBossIncrement(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ActiveBossIncrement(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ProcessManager.GetSystem<FourmenManager>().IncrementActiveBossIndex();
        }
    }
}
