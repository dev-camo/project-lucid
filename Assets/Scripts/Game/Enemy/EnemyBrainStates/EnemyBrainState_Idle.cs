using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200063e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    public class EnemyBrainState_Idle : ActorState
    {
        // 060021bc (1.10.1 RVA 0x571A70)
        protected EnemyBrainState_Idle(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021bd (1.10.1 RVA 0x571A80)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_Idle(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021be (1.10.1 RVA 0x571B10)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.Storage.SetValue(EnemyBrainFSMKeys.TargetLocked, false);
            user.Storage.SetValue(EnemyBrainFSMKeys.MovementImpulse, Vector2.zero);
            user.Storage.SetValue(EnemyBrainFSMKeys.IdleElapsedTime, 0f);
        }

        // 060021bf (1.10.1 RVA 0x571F00). The idle timer advances by the frame delta.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            user.Storage.AdjustValue(EnemyBrainFSMKeys.IdleElapsedTime, context.DeltaTime);
        }

        // 060021c0 (1.10.1 RVA 0x572010)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.Storage.RemoveValue<float>(EnemyBrainFSMKeys.IdleElapsedTime);
        }
    }
}
