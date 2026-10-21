using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200024d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("NotInRange")]
	[GraphNodeMenuFormat("Boss/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BossTransition_NotInRange : FSMTransition
	{
		// 06000c8d (1.10.1 RVA 0x710D20)
		protected BossTransition_NotInRange(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c8e (1.10.1 RVA 0x710D30)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_NotInRange(fsm, transitionId);
		}

		// 06000c8f (1.10.1 RVA 0x710D90)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return !user.Storage.GetValue(BossFSMKeys.MoveToRange, false, true);
		}
	}
}
