using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 0200015a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/Theatre/{0}")]
	public sealed class ApplicationStateCutsceneTheatreCleanup : FSMState
	{
		// 0600080b (1.10.1 RVA 0x6604B0)
		private ApplicationStateCutsceneTheatreCleanup(FiniteStateMachine fsm, string stateName) : base(fsm, stateName)
		{
		}

		// 0600080c (1.10.1 RVA 0x6604E0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new ApplicationStateCutsceneTheatreCleanup(fsm, stateId);
		}

		// 0600080d (1.10.1 RVA 0x660560): hands the UI background source its previous music back,
		// re-enables the menu cameras and drops the theatre session from the user's storage.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CutsceneTheatre theatre = user.Storage.GetValueOnly<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre);
			ProcessManager.GetSystem<AudioManager>().PlayClipAtSource(theatre.PreviousMusic, HLAudioSourceIdentifier.UiBackground, false, null);
			GUICameraManager.Instance.ToggleCameras(true);
			theatre.Cleanup();
			user.Storage.RemoveValue<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre);
		}
	}
}
