using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001fc; bodies translated from the 1.10.1 x86-64 binary.
    // Restarts the Fourmen rotation: the first boss in the list becomes the active one and no boss has had its loop turn.
    [GraphNodeDefaultName("ActiveReset")]
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_ActiveBossReset : ActorState_Effect
    {
        protected BossState_ActiveBossReset(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ActiveBossReset(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            fourmen.ResetActiveBossIndex();
            fourmen.ClearLoopStatus();
            fourmen.ClearActiveBosses();
            fourmen.ConfigureActiveBoss();
        }
    }
}
