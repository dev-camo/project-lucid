using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000248; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("IsInRange")]
	[GraphNodeMenuFormat("Boss/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossTransition_InRange : FSMTransition
	{
		// 06000c80 (1.10.1 RVA 0x7107C0)
		protected BossTransition_InRange(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c81 (1.10.1 RVA 0x7107D0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_InRange(fsm, transitionId);
		}

		// 06000c82 (1.10.1 RVA 0x710830)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(BossFSMKeys.MoveToRange, false, true);
		}
	}
}
