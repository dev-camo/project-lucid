using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001fa; bodies translated from the 1.10.1 x86-64 binary.
    // Moves the Fourmen turn on to the next boss: the active timer is dropped, nobody is active, then the next boss in the ring is.
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("ActiveBossChange")]
    public class BossState_ActiveBossChange : ActorState_Effect
    {
        protected BossState_ActiveBossChange(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ActiveBossChange(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            user.Storage.RemoveValue<float>(BossFSMKeys.ActiveBossTimer);
            fourmen.ClearActiveBosses();
            fourmen.IncrementActiveBossIndex();
            fourmen.ConfigureActiveBoss();
        }
    }
}
