using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 0200011c; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateResetAudioSources : FSMState
	{
		// 06000747 (1.10.1 RVA 0x653750)
		public ApplicationStateResetAudioSources(FiniteStateMachine fsm, string stateName) : base(fsm, stateName)
		{
		}

		// 06000748 (1.10.1 RVA 0x653780)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new ApplicationStateResetAudioSources(fsm, stateId);
		}

		// 06000749 (1.10.1 RVA 0x653800)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			AudioManager audioManager = ProcessManager.GetSystem<AudioManager>();
			SaveDataSettings settings = ProcessManager.GetSystem<SaveManager>().GetSaveDataSettings();
			audioManager.GetMixer(HLAudioMixerIdentifier.Main).ResetVolume(settings);
		}
	}
}
