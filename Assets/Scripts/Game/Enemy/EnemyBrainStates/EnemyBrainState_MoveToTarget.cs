using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000641; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    public class EnemyBrainState_MoveToTarget : EnemyBrainState_Move
    {
        // 060021c8 (1.10.1 RVA 0x572950)
        protected EnemyBrainState_MoveToTarget(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021c9 (1.10.1 RVA 0x572960)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_MoveToTarget(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021ca (1.10.1 RVA 0x5729F0). The target lock is set whether or not a focus
        // target exists; the native body evaluates the null comparison and discards it
        // (the branch body is not in retail and cannot be recovered; possibly a compiled-out [Conditional] call).
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            EnemyBrain enemyBrain = user.GetAs<EnemyBrain>();
            Actor target = enemyBrain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget);
            if (target == null)
            {
            }
            enemyBrain.Storage.SetValue(EnemyBrainFSMKeys.TargetLocked, true);
        }

        // 060021cb (1.10.1 RVA 0x572C10). A cached target position, when stored,
        // replaces the focus target's live position as the destination.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            EnemyBrain enemyBrain = user.GetAs<EnemyBrain>();
            Vector3 destination = enemyBrain.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget).WorldPosition;
            if (user.Storage.HasValue(EnemyBrainFSMKeys.TargetCachedPosition))
            {
                destination = user.Storage.GetValue<Vector3>(EnemyBrainFSMKeys.TargetCachedPosition);
            }
            bool reached = SetMovementImpulseForDestination(enemyBrain, destination);
            enemyBrain.Storage.SetValue(EnemyBrainFSMKeys.TargetReached, reached);
        }

        // 060021cc (1.10.1 RVA 0x573020)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.Storage.RemoveValue<bool>(EnemyBrainFSMKeys.TargetReached);
            user.Storage.RemoveValue<Vector3>(EnemyBrainFSMKeys.TargetCachedPosition);
        }
    }
}
