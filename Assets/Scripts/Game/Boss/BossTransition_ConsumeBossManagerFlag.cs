using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200023f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("FoundFlagOnBossManager")]
	[GraphNodeMenuFormat("Boss/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossTransition_ConsumeBossManagerFlag : FSMTransition
	{
		// 06000c64 (1.10.1 RVA 0x70F420)
		protected BossTransition_ConsumeBossManagerFlag(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c65 (1.10.1 RVA 0x70F430)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_ConsumeBossManagerFlag(fsm, transitionId);
		}

		// 06000c66 (1.10.1 RVA 0x70F490): reading the manager's phase continuation flag clears it.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.GetAs<Boss>().ManagerHasFlagForPhaseContinuation();
		}
	}
}
