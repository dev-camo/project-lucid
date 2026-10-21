using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200024c; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("NotActiveBoss")]
	[GraphNodeMenuFormat("Boss/Fourmen/{0}")]
	public class BossTransition_IsNotActiveBoss : FSMTransition
	{
		// 06000c8a (1.10.1 RVA 0x710C60)
		protected BossTransition_IsNotActiveBoss(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c8b (1.10.1 RVA 0x710C70)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_IsNotActiveBoss(fsm, transitionId);
		}

		// 06000c8c (1.10.1 RVA 0x710CD0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return !user.GetAs<Boss>().IsActivePartOfMultiBodyBoss;
		}
	}
}
