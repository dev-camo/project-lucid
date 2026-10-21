using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 0200015f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/Theatre/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public sealed class ApplicationStateCutsceneTheatreSetup : FSMState
	{
		// 0600081d (1.10.1 RVA 0x6613A0)
		private ApplicationStateCutsceneTheatreSetup(FiniteStateMachine fsm, string stateName) : base(fsm, stateName)
		{
		}

		// 0600081e (1.10.1 RVA 0x6613D0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new ApplicationStateCutsceneTheatreSetup(fsm, stateId);
		}

		// 0600081f (1.10.1 RVA 0x661450)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			user.Storage.GetValueOnly<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre).DequeueNext();
		}
	}
}
