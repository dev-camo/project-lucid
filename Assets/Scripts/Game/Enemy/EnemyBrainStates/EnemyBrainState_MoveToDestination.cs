using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000640; bodies translated from the 1.10.1 x86-64 binary.
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyBrainState_MoveToDestination : EnemyBrainState_Move
    {
        // 060021c3 (1.10.1 RVA 0x572430)
        protected EnemyBrainState_MoveToDestination(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021c4 (1.10.1 RVA 0x572440)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_MoveToDestination(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021c5 (1.10.1 RVA 0x5724D0)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            EnemyBrain enemyBrain = user.GetAs<EnemyBrain>();
            if (enemyBrain.Navigation.IsInitialised)
            {
                enemyBrain.Navigation.StartNavigation();
            }
        }

        // 060021c6 (1.10.1 RVA 0x572570). A brain whose navigation is not initialised
        // reports the destination as reached.
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            EnemyBrain enemyBrain = user.GetAs<EnemyBrain>();
            if (enemyBrain.Navigation.IsInitialised)
            {
                bool reached = SetMovementImpulseForDestination(enemyBrain, enemyBrain.Navigation.GetDestination());
                enemyBrain.Storage.SetValue(EnemyBrainFSMKeys.MoveToDestinationReached, reached);
            }
            else
            {
                enemyBrain.Storage.SetValue(EnemyBrainFSMKeys.MoveToDestinationReached, true);
            }
        }

        // 060021c7 (1.10.1 RVA 0x5727C0)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.Storage.RemoveValue<bool>(EnemyBrainFSMKeys.MoveToDestinationReached);
        }
    }
}
