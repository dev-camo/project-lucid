using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001ff; bodies translated from the 1.10.1 x86-64 binary.
    // Counts the attack the crab just made with a claw as made with a deflated claw, and regrows the claw once the
    // regrow ability says so.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("RegrowClaw")]
    [GraphNodeMenuFormat("Boss/Crab/{0}")]
    public class BossState_CalculateClawRegrowth : ActorState_Ability<ActorAbility>
    {
        private readonly AbilityDefinition m_abilityDefinition;

        protected BossState_CalculateClawRegrowth(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_CalculateClawRegrowth(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null)
                return;
            BossAbility_RegrowClaw regrowClaw = ability as BossAbility_RegrowClaw;
            BossAttackTypes attack = boss.Storage.GetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None, true);
            if (attack == BossAttackTypes.BossCrab_RightClaw)
            {
                regrowClaw.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_RightClaw);
                if (regrowClaw.ShouldRegrowClaw(BossAttackTypes.BossCrab_RightClaw))
                    boss.FixDestroyedPart(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint);
            }
            else if (attack == BossAttackTypes.BossCrab_LeftClaw)
            {
                regrowClaw.AttackedWithDeflatedClaw(BossAttackTypes.BossCrab_LeftClaw);
                if (regrowClaw.ShouldRegrowClaw(BossAttackTypes.BossCrab_LeftClaw))
                    boss.FixDestroyedPart(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint);
            }
        }
    }
}
