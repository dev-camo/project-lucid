using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 0200015e; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/Theatre/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class ApplicationStateCutsceneTheatrePlay : FSMState
	{
		// 0600081a (1.10.1 RVA 0x6611E0)
		private ApplicationStateCutsceneTheatrePlay(FiniteStateMachine fsm, string stateName) : base(fsm, stateName)
		{
		}

		// 0600081b (1.10.1 RVA 0x661210)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new ApplicationStateCutsceneTheatrePlay(fsm, stateId);
		}

		// 0600081c (1.10.1 RVA 0x661290): the original does not chain to FSMState.DoOnEnter.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			user.Storage.GetValueOnly<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre).Play();
		}
	}
}
