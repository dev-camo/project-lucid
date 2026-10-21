using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000241; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Boss/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("ExitedAttackAndVulnerableAnimation")]
	public class BossTransition_ExitedAttackAndVulnerableAnimation : FSMTransition
	{
		// 06000c6a (1.10.1 RVA 0x70F7F0)
		protected BossTransition_ExitedAttackAndVulnerableAnimation(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c6b (1.10.1 RVA 0x70F800)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_ExitedAttackAndVulnerableAnimation(fsm, transitionId);
		}

		// 06000c6c (1.10.1 RVA 0x70F860)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(BossFSMKeys.AttackAndVulnerableAnimationExited, false, true);
		}

		// 06000c6d (1.10.1 RVA 0x70F9E0)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			user.Storage.SetValue(BossFSMKeys.AttackAndVulnerableAnimationExited, false);
		}
	}
}
