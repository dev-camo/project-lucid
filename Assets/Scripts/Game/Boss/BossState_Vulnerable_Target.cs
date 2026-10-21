using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000231; bodies translated from the 1.10.1 x86-64 binary.
    // A vulnerable state whose homing target is only switched on once the target activation delay has passed within
    // the vulnerable time; it is switched off again on leaving.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("Vulnerable")]
    [GraphNodeMenuFormat("Boss/{0}")]
    public class BossState_Vulnerable_Target : BossState_Vulnerable
    {
        [Serializable]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        protected class AttachPointJSONCtorArgs : VulnerableJSONCtorArgs
        {
            public ActorAttachPointType VulnerableTarget;
            public float TargetActivateDelaySeconds;

            // 06000c45 (1.10.1 RVA 0x70DCE0)
            public AttachPointJSONCtorArgs()
            {
            }
        }

        private ActorAttachPointType m_vulnerableTarget;
        private float m_targetActivateDelaySeconds;

        // 06000c40 (1.10.1 RVA 0x70D4D0)
        protected BossState_Vulnerable_Target(FiniteStateMachine fsm, FSMIdentifier stateId, AttachPointJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
            m_vulnerableTarget = ctorArgs.VulnerableTarget;
            m_targetActivateDelaySeconds = ctorArgs.TargetActivateDelaySeconds;
        }

        // 06000c41 (1.10.1 RVA 0x70D510)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_Vulnerable_Target(fsm, transitionId, JsonUtility.FromJson<AttachPointJSONCtorArgs>(jsonCtorArgs));
        }

        // 06000c42 (1.10.1 RVA 0x70D5D0)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            user.Storage.SetValue(BossFSMKeys.VulnerableTargetActive, false);
        }

        // 06000c43 (1.10.1 RVA 0x70D750)
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            base.DoUpdate(user, updateContext);
            Boss boss = user.GetAs<Boss>();
            bool targetActive = boss.Storage.GetValue(BossFSMKeys.VulnerableTargetActive, false, true);
            float vulnerableFor = boss.Storage.GetValue(BossFSMKeys.CurrentlyVulnerableTime, 0f, true);
            if (targetActive || m_targetActivateDelaySeconds > vulnerableFor)
                return;
            boss.Storage.SetValue(BossFSMKeys.VulnerableTargetActive, true);
            boss.SetActiveHomingTarget(m_vulnerableTarget, true);
        }

        // 06000c44 (1.10.1 RVA 0x70DA40)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Boss boss = user.GetAs<Boss>();
            boss.SetActiveHomingTarget(m_vulnerableTarget, false);
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 0f);
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, false);
            boss.Storage.RemoveValue<bool>(BossFSMKeys.VulnerableTargetActive);
        }
    }
}
