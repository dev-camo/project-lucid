using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000247; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("HitPlayer")]
	[GraphNodeMenuFormat("Boss/{0}")]
	public class BossTransition_HitPlayer : FSMTransition
	{
		// 06000c7c (1.10.1 RVA 0x710330)
		protected BossTransition_HitPlayer(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c7d (1.10.1 RVA 0x710340)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_HitPlayer(fsm, transitionId);
		}

		// 06000c7e (1.10.1 RVA 0x7103A0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(BossFSMKeys.BossHitPlayer, false, true);
		}

		// 06000c7f (1.10.1 RVA 0x710520)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			user.Storage.SetValue(BossFSMKeys.BossHitPlayer, false);
			user.Storage.SetValue(BossFSMKeys.ForceNotVulnerable, false);
		}
	}
}
