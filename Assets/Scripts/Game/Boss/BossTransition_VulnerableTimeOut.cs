using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000251; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Boss/{0}")]
	[GraphNodeDefaultName("VulnerableTimedOut")]
	public class BossTransition_VulnerableTimeOut : FSMTransition
	{
		// 06000c97 (1.10.1 RVA 0x711390)
		protected BossTransition_VulnerableTimeOut(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c98 (1.10.1 RVA 0x7113A0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_VulnerableTimeOut(fsm, transitionId);
		}

		// 06000c99 (1.10.1 RVA 0x711400): fires once the boss is no longer vulnerable.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return !user.Storage.GetValue(BossFSMKeys.CurrentlyVulnerable, false, true);
		}
	}
}
