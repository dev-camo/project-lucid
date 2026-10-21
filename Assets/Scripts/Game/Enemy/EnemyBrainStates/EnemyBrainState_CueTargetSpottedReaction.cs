using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200063d; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    public class EnemyBrainState_CueTargetSpottedReaction : ActorState
    {
        // 060021b9 (1.10.1 RVA 0x571860)
        protected EnemyBrainState_CueTargetSpottedReaction(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021ba (1.10.1 RVA 0x571870)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_CueTargetSpottedReaction(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021bb (1.10.1 RVA 0x571900). The native body does not call the base hook.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            user.Storage.SetValue(EnemyBrainFSMKeys.TargetSpottedAnimationTrigger, true);
        }
    }
}
