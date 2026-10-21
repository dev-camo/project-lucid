using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000252; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("WasHit")]
	[GraphNodeMenuFormat("Boss/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossTransition_WasHit : FSMTransition
	{
		// 06000c9a (1.10.1 RVA 0x711570)
		protected BossTransition_WasHit(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 06000c9b (1.10.1 RVA 0x711580)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new BossTransition_WasHit(fsm, transitionId);
		}

		// 06000c9c (1.10.1 RVA 0x7115E0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue(BossFSMKeys.WasHit, false, true);
		}

		// 06000c9d (1.10.1 RVA 0x711750)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			user.Storage.SetValue(BossFSMKeys.WasHit, false);
		}
	}
}
