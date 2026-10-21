using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000642; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    public class EnemyBrainState_ResetHome : ActorState
    {
        // 060021cd (1.10.1 RVA 0x5732C0)
        protected EnemyBrainState_ResetHome(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021ce (1.10.1 RVA 0x5732D0)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_ResetHome(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021cf (1.10.1 RVA 0x573360)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.GetAs<EnemyBrain>().Navigation.InterruptNavigation();
        }
    }
}
