using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001fd; bodies translated from the 1.10.1 x86-64 binary.
    // Picks the first active Fourmen boss, unless one is already active.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("ActiveBossSetup")]
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_ActiveBossSetup : ActorState_Effect
    {
        protected BossState_ActiveBossSetup(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ActiveBossSetup(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            if (fourmen.HasActiveBossUnit())
                return;
            fourmen.ClearLoopStatus();
            fourmen.ConfigureActiveBoss();
        }
    }
}
