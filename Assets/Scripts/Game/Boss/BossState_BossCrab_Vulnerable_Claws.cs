using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000200; bodies translated from the 1.10.1 x86-64 binary.
    // The claw that made the last attack becomes the homing target while the crab is vulnerable; both claw targets are
    // switched off on leaving. An attack that was not made with a claw leaves the state without any vulnerability effect.
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    [GraphNodeDefaultName("Vulnerable")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossState_BossCrab_Vulnerable_Claws : BossState_Vulnerable
    {
        // 06000bac (1.10.1 RVA 0x704C00)
        protected BossState_BossCrab_Vulnerable_Claws(FiniteStateMachine fsm, FSMIdentifier stateId, VulnerableJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        // 06000bad (1.10.1 RVA 0x704C60)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_BossCrab_Vulnerable_Claws(fsm, transitionId, JsonUtility.FromJson<VulnerableJSONCtorArgs>(jsonCtorArgs));
        }

        // 06000bae (1.10.1 RVA 0x704D00)
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            BossAttackTypes attack = boss.Storage.GetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None, true);
            if (attack == BossAttackTypes.BossCrab_RightClaw)
                boss.SetActiveHomingTarget(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, true);
            else if (attack == BossAttackTypes.BossCrab_LeftClaw)
                boss.SetActiveHomingTarget(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, true);
            else
                return;
            boss.Storage.SetValue(BossFSMKeys.BossEffectType, BossEffectTriggerTypes.Vulnerable);
            boss.TriggerEffects();
        }

        // 06000baf (1.10.1 RVA 0x7051C0)
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Boss boss = user.GetAs<Boss>();
            boss.SetActiveHomingTarget(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, false);
            boss.SetActiveHomingTarget(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, false);
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerableTime, 0f);
            boss.Storage.SetValue(BossFSMKeys.CurrentlyVulnerable, false);
        }
    }
}
