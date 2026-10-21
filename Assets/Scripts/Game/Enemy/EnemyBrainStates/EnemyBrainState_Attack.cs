using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000643; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Enemy/Brain/{0}")]
    public class EnemyBrainState_Attack : ActorState
    {
        // 060021d0 (1.10.1 RVA 0x5733E0)
        protected EnemyBrainState_Attack(FiniteStateMachine fsm, FSMIdentifier stateId, BaseJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs) { }

        // 060021d1 (1.10.1 RVA 0x5733F0)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new EnemyBrainState_Attack(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 060021d2 (1.10.1 RVA 0x573480). Without a focus target the attack is not flagged.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Actor target = user.Storage.GetValue<Actor>(EnemyBrainFSMKeys.FocusTarget);
            if (target == null)
            {
                return;
            }
            user.Storage.SetValue(EnemyBrainFSMKeys.AttackTarget, true);
        }

        // 060021d3 (1.10.1 RVA 0x573780)
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            user.GetAs<EnemyBrain>().SetMovementImpulse(Vector2.zero);
        }

        // 060021d4 (1.10.1 RVA 0x573820)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            user.Storage.SetValue(EnemyBrainFSMKeys.AttackTarget, false);
            user.GetAs<EnemyBrain>().Enemy.SetAttackWindUpComplete(false);
        }
    }
}
