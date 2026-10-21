using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000216 and its arguments class 02000217; bodies translated from the 1.10.1 x86-64 binary.
    // Stores the crab's attack type override in the FSM storage on entry.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("SetAttackOverride")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    public class BossState_SetAttackOverride : ActorState_Effect
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class AttackTypeJSONArgs : BaseJSONCtorArgs
        {
            public BossAttackTypes AttackTypeToSet;

            public AttackTypeJSONArgs()
            {
            }
        }

        private BossAttackTypes m_attackTypeToSet;

        protected BossState_SetAttackOverride(FiniteStateMachine fsm, FSMIdentifier stateId, AttackTypeJSONArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_attackTypeToSet = ctorArgs.AttackTypeToSet;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_SetAttackOverride(fsm, transitionId, JsonUtility.FromJson<AttackTypeJSONArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.Storage.SetValue(BossFSMKeys.BossAttackTypeOverride, m_attackTypeToSet);
        }
    }
}
