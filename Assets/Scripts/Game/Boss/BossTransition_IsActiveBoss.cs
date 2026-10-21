using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000249; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	[GraphNodeDefaultName("ActiveBoss")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BossTransition_IsActiveBoss : FSMTransition
	{
		// 06000c83 (1.10.1 RVA 0x7109A0)
		protected BossTransition_IsActiveBoss(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c84 (1.10.1 RVA 0x7109B0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_IsActiveBoss(fsm, transitionId);
		}

		// 06000c85 (1.10.1 RVA 0x710A10)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.GetAs<Boss>().IsActivePartOfMultiBodyBoss;
		}
	}
}
