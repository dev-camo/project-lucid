using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000227 and its arguments class 02000228; bodies translated from the 1.10.1 x86-64 binary.
    // Stores a boss effect trigger type in the boss's storage on entry and triggers the boss's effects.
    [GraphNodeDefaultName("TriggerBossEffect")]
    [GraphNodeMenuFormat("Boss/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_TriggerEffectOnEntry : ActorState_Effect
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class TriggerEffectJSONCtorArgs : BaseJSONCtorArgs
        {
            public BossEffectTriggerTypes TriggerEffect;

            public TriggerEffectJSONCtorArgs()
            {
            }
        }

        private BossEffectTriggerTypes m_triggerEffect;

        protected BossState_TriggerEffectOnEntry(FiniteStateMachine fsm, FSMIdentifier stateId, TriggerEffectJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_triggerEffect = ctorArgs.TriggerEffect;
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_TriggerEffectOnEntry(fsm, transitionId, JsonUtility.FromJson<TriggerEffectJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            boss.Storage.SetValue(BossFSMKeys.BossEffectType, m_triggerEffect);
            boss.TriggerEffects();
        }
    }
}
