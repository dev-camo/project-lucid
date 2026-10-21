using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000223; bodies translated from the 1.10.1 x86-64 binary.
    // While the laser fence ability is enabled, the Fourmen's lasers are switched on at entry, aimed every update (at the
    // ability's fixed target when it has static targets, otherwise between this boss and the next one) and switched off on
    // leaving. With two bosses or fewer, only the boss at index zero enables, aims and fires its lasers.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Boss/Fourmen/{0}")]
    [GraphNodeDefaultName("ShootLasers")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossState_ShootLasers : ActorState_Ability<ActorAbility>
    {
        protected BossState_ShootLasers(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
        {
        }

        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new BossState_ShootLasers(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }

        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnEnter(user, action);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null || !ability.Enabled)
                return;
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            if (fourmen.BossCount() <= 2 && fourmen.GetIndexForBoss(boss) != 0)
                return;
            BossAbility_LaserFence laserFence = ability as BossAbility_LaserFence;
            boss.EnableLasers(true, laserFence.Definition.Attachment);
        }

        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            base.DoUpdate(user, context);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null || !ability.Enabled)
                return;
            FourmenManager fourmen = ProcessManager.GetSystem<FourmenManager>();
            if (fourmen.BossCount() <= 2 && fourmen.GetIndexForBoss(boss) != 0)
                return;
            BossAbility_LaserFence laserFence = ability as BossAbility_LaserFence;
            ActorAttachPointType attachPoint = laserFence.Definition.Attachment;
            Vector3[] positions = new Vector3[2];
            if (laserFence.Definition.HasStaticTargets)
            {
                positions[0] = Vector3.zero;
                positions[1] = laserFence.Definition.LaserTarget;
            }
            else
            {
                positions = fourmen.CalculateLaserPositions(boss, attachPoint);
            }
            boss.SetLaserPositions(positions, laserFence.GetHazardDepth(), laserFence.GetHazardHeight(), laserFence.GetHazardYOffset(),
                attachPoint, laserFence.Definition.HasStaticTargets);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            base.DoOnLeave(user, action);
            Boss boss = user.GetAs<Boss>();
            ActorAbility ability = GetAbility(boss);
            if (ability == null)
                return;
            BossAbility_LaserFence laserFence = ability as BossAbility_LaserFence;
            boss.EnableLasers(false, laserFence.Definition.Attachment);
        }
    }
}
