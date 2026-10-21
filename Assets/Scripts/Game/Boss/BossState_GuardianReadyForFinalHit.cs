using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200020a; bodies translated from the 1.10.1 x86-64 binary.
    // Tells the guardian manager that the boss is ready for its final hit.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/Guardian/{0}")]
    [GraphNodeDefaultName("GuardianReadyForFinalHit")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_GuardianReadyForFinalHit : ActorState
    {
        protected BossState_GuardianReadyForFinalHit(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_GuardianReadyForFinalHit(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            ProcessManager.GetSystem<GuardianManager>().BossReadyForFinalHit();
        }
    }
}
