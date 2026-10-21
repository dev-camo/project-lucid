using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000229 and its arguments class 0200022a; bodies translated from the 1.10.1 x86-64 binary.
    // Stores a boss effect trigger type in the boss's storage on leaving and triggers the boss's effects.
    [GraphNodeMenuFormat("Boss/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("TriggerBossEffect")]
    public class BossState_TriggerEffectOnLeave : ActorState_Effect
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class TriggerEffectJSONCtorArgs : BaseJSONCtorArgs
        {
            public BossEffectTriggerTypes TriggerEffect;

            public TriggerEffectJSONCtorArgs()
            {
            }
        }

        private BossEffectTriggerTypes m_triggerEffect;

        protected BossState_TriggerEffectOnLeave(FiniteStateMachine fsm, FSMIdentifier stateId, TriggerEffectJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_triggerEffect = ctorArgs.TriggerEffect;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_TriggerEffectOnLeave(fsm, transitionId, JsonUtility.FromJson<TriggerEffectJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Boss boss = user.GetAs<Boss>();
            boss.Storage.SetValue(BossFSMKeys.BossEffectType, m_triggerEffect);
            boss.TriggerEffects();
        }
    }
}
