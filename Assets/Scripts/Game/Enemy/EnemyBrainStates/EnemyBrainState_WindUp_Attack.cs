using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000646; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyBrainState_WindUp_Attack : ActorState
    {
        // 060021da (1.10.1 RVA 0x573D50)
        protected EnemyBrainState_WindUp_Attack(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021db (1.10.1 RVA 0x573D60)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_WindUp_Attack(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021dc (1.10.1 RVA 0x573DF0). Without a focus target the wind-up does not start.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Actor target = user.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget);
            if (target == null)
            {
                return;
            }
            user.Storage.SetValue(EnemyBrainFSMKeys.StartAttackWindUp, true);
            user.Storage.SetValue(EnemyBrainFSMKeys.AttackWindUpComplete, false);
        }

        // 060021dd (1.10.1 RVA 0x574200)
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            user.GetAs<EnemyBrain>().SetMovementImpulse(Vector2.zero);
        }

        // 060021de (1.10.1 RVA 0x5742A0)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.Storage.SetValue(EnemyBrainFSMKeys.StartAttackWindUp, false);
        }
    }
}
