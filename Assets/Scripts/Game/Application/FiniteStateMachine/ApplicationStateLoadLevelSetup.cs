using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 0200013f; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/Levels/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateLoadLevelSetup : FSMState
	{
		// 060007b0 (1.10.1 RVA 0x659540)
		private ApplicationStateLoadLevelSetup(FiniteStateMachine fsm, FSMIdentifier stateId) : base(fsm, stateId)
		{
		}

		// 060007b1 (1.10.1 RVA 0x659550)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			return new ApplicationStateLoadLevelSetup(fsm, stateId);
		}

		// 060007b2 (1.10.1 RVA 0x6595B0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			LevelManagerLoadLevelParameters parameters = user.Storage.GetValue<LevelManagerLoadLevelParameters>(AppFSMKeys.LevelLoadParameters, null, true);
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			DataManager dataManager = ProcessManager.GetSystem<DataManager>();
			if (parameters == null)
			{
				List<GameplayLevelDefinition> unlockedLevels = levelManager.GetUnlockedLevels();
				GameplayLevelDefinition levelToLoad = unlockedLevels[unlockedLevels.Count - 1];
				if (TryGetLevelFromActiveMission(out GameplayLevelDefinition missionLevel))
				{
					levelToLoad = missionLevel;
				}
				levelManager.SetupFsmVarsToLoadLevel(levelToLoad);
				parameters = new LevelManagerLoadLevelParameters(levelToLoad);
			}
			GameplayLevelDefinition levelDefinition = parameters.LevelDefinition;
			LevelSetupDefinition setup = dataManager.LevelSetupDefinitions[levelDefinition.LevelSetupType];
			levelManager.ManagedSystems.ReplacePrefab(setup.CinemachineCameraManager);
			levelManager.ManagedSystems.ReplacePrefab(setup.InstancedObjectPoolManager);
			if (!user.Storage.GetValue<bool>(AppFSMKeys.RestartInProgress, false, true))
			{
				levelManager.ManagedSystems.ReplacePrefab(setup.PrefabPoolManager);
			}
			else
			{
				levelManager.ReplaceManagedSystemIfRequired(levelDefinition, setup.PrefabPoolManager);
			}
			LoadAudio(setup);
		}

		// 060007b3 (1.10.1 RVA 0x659A60)
		private bool TryGetLevelFromActiveMission(out GameplayLevelDefinition levelDefinition)
		{
			levelDefinition = null;
			SystemRef<MissionManager> missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
			if (!missionManagerRef.IsValid() || !missionManagerRef.Get().HasActiveMission())
			{
				return false;
			}
			levelDefinition = missionManagerRef.Get().ActiveMissionContext.MissionDefinition.SceneOverride;
			return levelDefinition != null;
		}

		// 060007b4 (1.10.1 RVA 0x659BE0)
		private static void LoadAudio(LevelSetupDefinition levelSetupDefinition)
		{
			SaveDataSettings settings = ProcessManager.GetSystem<SaveManager>().GetSaveDataSettings();
			AudioManager audioManager = ProcessManager.GetSystem<AudioManager>();
			HLAudioMixerDefinition mixer = audioManager.GetMixer(HLAudioMixerIdentifier.Main);
			mixer.SetMasterVolume(settings.MasterVolume);
			mixer.SetMusicVolume(settings.MusicVolume);
			mixer.SetSfxVolume(settings.SfxVolume);
			mixer.SetVoiceVolume(settings.VoiceVolume);
			foreach (HLAudioSourceRegisterComponent sourcePrefab in levelSetupDefinition.AudioSourcePrefabs)
			{
				if (sourcePrefab != null)
				{
					audioManager.InstantiateSource(sourcePrefab);
				}
			}
		}
	}
}
