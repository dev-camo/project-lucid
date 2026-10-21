using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200022f; bodies translated from the 1.10.1 x86-64 binary.
    // Marks the boss vulnerable for the authored time: the vulnerable clock restarts on entry and the state ends the
    // vulnerability (and consumes ForceNotVulnerable) once the clock passes the time or the boss was forced out of it.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Boss/{0}")]
    [GraphNodeDefaultName("Vulnerable")]
    public class BossState_Vulnerable : ActorState_Effect
    {
        [Serializable]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class VulnerableJSONCtorArgs : BaseJSONCtorArgs
        {
            public float VulnerableTime;

            // 06000c3f (1.10.1 RVA 0x70D4C0)
            public VulnerableJSONCtorArgs()
            {
            }
        }

        private float m_vulnerableTime;

        // 06000c3a (1.10.1 RVA 0x704C30)
        protected BossState_Vulnerable(FiniteStateMachine fsm, FSMIdentifier stateId, VulnerableJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_vulnerableTime = ctorArgs.VulnerableTime;
        }

        // 06000c3b (1.10.1 RVA 0x70D070)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_Vulnerable(fsm, transitionId, JsonUtility.FromJson<VulnerableJSONCtorArgs>(jsonCtorArgs));
        }

        // 06000c3c (1.10.1 RVA 0x704FE0)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 0f);
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, true);
        }

        // 06000c3d (1.10.1 RVA 0x70D110)
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Boss boss = user.GetAs<Boss>();
            boss.Storage.AdjustValue(BossFSMKeys.CurrentlyVulnerableTime, updateContext.DeltaTime, 0f);
            float vulnerableFor = boss.Storage.GetValue(BossFSMKeys.CurrentlyVulnerableTime, 0f, true);
            if (vulnerableFor > m_vulnerableTime || boss.Storage.GetValue(BossFSMKeys.ForceNotVulnerable, false, true))
            {
                boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, false);
                boss.Storage.SetValue(BossFSMKeys.ForceNotVulnerable, false);
            }
        }

        // 06000c3e (1.10.1 RVA 0x7053C0)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Boss boss = user.GetAs<Boss>();
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 0f);
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, false);
            boss.Storage.SetValue(BossFSMKeys.BossHitPlayer, false);
        }
    }
}
