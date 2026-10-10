using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.Serialization;

namespace HardlightProject
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "MissionDefinition", menuName = "HardlightProject/DefinitionData/Definitions/MissionDefinition")]
	public class MissionDefinition : ScriptableObjectWithGuid, ISaveGameListener
	{
		[SerializeField]
		[HashEnum(typeof(Strings))]
		private Strings m_missionName;

		[SerializeField]
		private MissionType m_type = MissionType.Default;

		[SerializeField]
		private AssetReferenceT<GameObject> m_missionTrackerAddressable;

		[SerializeField]
		private GameplayLevelDefinition m_missionLevelDefinition;

		[SerializeField]
		private GameplayLevelDefinition m_subLevelDefinition;

		[SerializeField]
		private Sprite m_missionProgressIcon;

		[SerializeField]
		private Sprite m_missionObjectiveIcon;

		[SerializeField]
		private bool m_showInMissionList = true;

		[Tooltip("Can this mission be replayed? Will only apply to sub-missions.")]
		[SerializeField]
		private bool m_replayable = true;

		[Tooltip("Mission asset will be reloaded on character respawn.")]
		[SerializeField]
		private bool m_reloadOnCharacterRespawn;

		[SerializeField]
		private LevelStartPositionDefinition m_customStartPosition;

		[Tooltip("Secondary missions that will be loaded and active along this mission.")]
		[SerializeField]
		private List<MissionDefinition> m_subMissions;

		[Tooltip("Does this mission only apply in specific islands?")]
		[SerializeField]
		private List<GameplayIslandDefinition> m_islands = new List<GameplayIslandDefinition>();

		[SerializeField]
		[Tooltip("Should the mission tracker be left in memory on reload / retry")]
		private bool m_doNotUnloadOnRetry;

		[Tooltip("Player can choose dream powers in this mission.")]
		[SerializeField]
		private bool m_enableDreamPowers = true;

		[InspectorReadOnly]
		[SerializeField]
		private int m_totalObjectivesCount = 1;

		[HideIf("m_totalObjectivesCount", 1)]
		[SerializeField]
		private int m_targetObjectivesCount = -1;

		[InspectorReadOnly]
		[Tooltip("This comes from the mission tracker prefab.")]
		[SerializeField]
		private bool m_isTimed = true;

		[InspectorReadOnly]
		[Tooltip("This comes from the mission tracker prefab.")]
		[SerializeField]
		private bool m_isScored;

		[InspectorReadOnly]
		[SerializeField]
		[Tooltip("This comes from the mission tracker prefab.")]
		private float m_timeLimitSeconds;

		[Tooltip("This comes from the mission tracker prefab.")]
		[SerializeField]
		[InspectorReadOnly]
		private bool m_useBestTime = true;

		[InspectorReadOnly]
		[Tooltip("This comes from the mission tracker prefab.")]
		[SerializeField]
		private bool m_failOnTimeLimitReached;

		[InspectorReadOnly]
		[Tooltip("This comes from the objectives in the mission tracker prefab.")]
		[SerializeField]
		private float m_objectivesAdditionalTotalTimeSeconds;

		[Header("Requirements")]
		[SerializeField]
		private bool m_showProgressInUI = true;

		[SerializeField]
		private CharacterId m_characterId;

		[Tooltip("Should this mission be exclusive to the specified character? If not, the UI will show the suggested character, but will be available to all.")]
		[SerializeField]
		private bool m_exclusiveToCharacter;

		[SerializeField]
		private CharacterArchetype m_characterArchetype;

		[SerializeField]
		[Tooltip("Which, if any, character archetype do you want this mission to unlock?")]
		private CharacterArchetype m_unlocksCharacterArchetype;

		[FormerlySerializedAs("m_orbValue")]
		[Header("Completion")]
		[Tooltip("How many collectables this mission awards on completion. Include the object that will be present in the level for collection, if relevant.")]
		[SerializeField]
		private int m_rewardValue = 1;

		[Tooltip("What type of currency is rewarded.")]
		[SerializeField]
		private CollectableType m_rewardType = CollectableType.Orb;

		[Tooltip("Which ornament to award when the mission completes.")]
		[SerializeField]
		private OrnamentDefinition m_ornament;

		[SerializeField]
		[Tooltip("Which music track to award when the mission completes.")]
		private MusicTrackDefinition m_musicTrack;

		[SerializeField]
		private UIContainerIdentifier m_resultsScreenIdentifier;

		[SerializeField]
		private UIContainerIdentifier m_shortResultsScreenIdentifier;

		[SerializeField]
		private int m_xpReward = 2500;

		[Header("Trackers")]
		[SerializeField]
		private bool m_isInstancedTracker;

		[SerializeField]
		private bool m_isInstancedSequencedTracker;

		[SerializeField]
		private bool m_isPersistentTracker;

		[SerializeField]
		private bool m_isReachGoalTracker;

		[Header("Audio")]
		[Tooltip("Will play instead of zone's default music track for duration of mission.")]
		[HashEnum(null)]
		[SerializeField]
		private HLAudioClipIdentifier m_musicOverrideIdentifier;

		[SerializeField]
		private bool m_musicStartAtRandomTime;

		[SerializeField]
		[Tooltip("Seconds of delay between completing the objectives and presenting the results screen or level-end cutscene.")]
		private float m_completionCooldownTime;

		[SerializeField]
		[Tooltip("Enter a non-zero value to give free rings to the player on mission start.")]
		private int m_startingRingCount;

		[Tooltip("Which leaderboards to use when applying scores such as best times. Does not apply to Tail's Challenges as there is one leaderboard for it.")]
		[SerializeField]
		[Header("Leaderboards")]
		private LeaderboardIdentifier m_leaderboardIdentifier;

		[Tooltip("QA version of the leaderboard identifier. Does not apply to Tail's Challenges as there is one leaderboard for it.")]
		[SerializeField]
		private LeaderboardIdentifier m_qaLeaderboardIdentifier;

		[Tooltip("Can be calculated via an editor button, or manually input for higher precision.")]
		public float TheoreticalBestTimePossibleSeconds;

		[Header("Ranks")]
		[SerializeField]
		private MissionRankDefinition m_ranks;

		[Header("Overrides")]
		[Tooltip("Specify another mission definition here to allow overriding of data.\nGenerally, this should be a copy of the original mission definition, with only required values changed")]
		[SerializeField]
		private MissionDefinition m_completedMissionOverrides;

		[SerializeField]
		private GameplayLevelDefinition m_sceneOverride;

		[Tooltip("Specify another tips definition to override the default level tips.")]
		[SerializeField]
		private GameTipsDefinition m_tipsOverride;

		[Tooltip("Mission can be played with specified character, regardless of character unlock status")]
		[SerializeField]
		private bool m_allowLockedCharacters;

		[SerializeField]
		private ZoneThemeOverride m_zoneThemeOverride;

		[SerializeField]
		private bool m_useAltUILayout;

		[SerializeField]
		private AssetReferenceT<Sprite> m_overrideLoadingImage;

		public ManagedAddressableAsset<Sprite> LoadingImageOverride;

		private static readonly CollectableType[] s_rewardCollectableTypes = new[] { CollectableType.Orb, CollectableType.Moon };

		private AsyncOperationHandle<GameObject> m_missionTrackerHandle;

		private MissionDefinition m_definitionDataSource;

		private bool m_hasOverrides;

		private bool m_isBeingOverridden;

		[NonSerialized]
		private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);

		[NonSerialized]
		private readonly SystemRef<TimeTrialLeaderboardManager> m_timeTrialLeaderboardManagerRef = ProcessManager.GetSystemRef<TimeTrialLeaderboardManager>(null, true);

		[NonSerialized]
		private SceneInstance m_missionSceneInstance;

        // Game.Runtime 0x06001dfe, native 0x5251c8: DefinitionData source field.
        public Strings MissionName => DefinitionData.m_missionName;

        // Game.Runtime 0x06001dff, native 0x528a84: DefinitionData source field.
        public MissionType Type => DefinitionData.m_type;

        // Game.Runtime 0x06001e00, native 0x528ad0: DefinitionData source field.
        public CharacterId CharacterId => DefinitionData.m_characterId;

        // Game.Runtime 0x06001e01, native 0x528b1c: DefinitionData source field.
        public bool ExclusiveToCharacter => DefinitionData.m_exclusiveToCharacter;

        // Game.Runtime 0x06001e02, native 0x528b68: owner field.
        public CharacterArchetype CharacterArchetype => m_characterArchetype;

        // Game.Runtime 0x06001e03, native 0x528b70: DefinitionData source field.
        public Sprite MissionProgressIcon => DefinitionData.m_missionProgressIcon;

        // Game.Runtime 0x06001e04, native 0x528bbc: DefinitionData source field.
        public Sprite MissionObjectiveIcon => DefinitionData.m_missionObjectiveIcon;

        // Game.Runtime 0x06001e05, native 0x528c08: DefinitionData source field.
        public bool ShowInMissionList => DefinitionData.m_showInMissionList;

        // Game.Runtime 0x06001e06, native 0x528c54: DefinitionData source field.
        public bool Replayable => DefinitionData.m_replayable;

        // Game.Runtime 0x06001e07, native 0x528ca0: DefinitionData source field.
        public bool ReloadOnCharacterRespawn => DefinitionData.m_reloadOnCharacterRespawn;

        // Game.Runtime 0x06001e08, native 0x525ac8: DefinitionData source field.
        public LevelStartPositionDefinition CustomStartPosition => DefinitionData.m_customStartPosition;

        // Game.Runtime 0x06001e09, native 0x5209e8: DefinitionData source field.
        public List<MissionDefinition> SubMissions => DefinitionData.m_subMissions;

        // Game.Runtime 0x06001e0a, native 0x528cec: DefinitionData source field.
        public List<GameplayIslandDefinition> Islands => DefinitionData.m_islands;

        // Game.Runtime 0x06001e0b, native 0x528d38: DefinitionData source field.
        public bool DoNotUnloadOnRetry => DefinitionData.m_doNotUnloadOnRetry;

        // Game.Runtime 0x06001e0c, native 0x528d84: DefinitionData source field.
        public bool EnableDreamPowers => DefinitionData.m_enableDreamPowers;

        // Game.Runtime 0x06001e0d, native 0x528dd0: DefinitionData source field.
        public bool ShowProgressInUI => DefinitionData.m_showProgressInUI;

        // Game.Runtime 0x06001e0e, native 0x528e1c: DefinitionData source field.
        public CharacterArchetype UnlocksCharacterArchetype => DefinitionData.m_unlocksCharacterArchetype;

        // Game.Runtime 0x06001e0f, native 0x528e68: DefinitionData source field.
        public GameplayLevelDefinition SubLevelDefinition => DefinitionData.m_subLevelDefinition;

        // Game.Runtime 0x06001e10, native 0x528eb4: DefinitionData source field.
        public int RewardValue => DefinitionData.m_rewardValue;

        // Game.Runtime 0x06001e11, native 0x528f00: owner field.
        public CollectableType RewardType => m_rewardType;

        // Game.Runtime 0x06001e12, native 0x528f08: DefinitionData source field.
        public OrnamentDefinition Ornament => DefinitionData.m_ornament;

        // Game.Runtime 0x06001e13, native 0x528f54: DefinitionData source field.
        public MusicTrackDefinition MusicTrack => DefinitionData.m_musicTrack;

        // Game.Runtime 0x06001e14, native 0x528fa0: owner field.
        public int XPReward => m_xpReward;

        // Game.Runtime 0x06001e15, native 0x525118: DefinitionData source field.
        public int TotalObjectiveCount => DefinitionData.m_totalObjectivesCount;

        // Game.Runtime 0x06001e16, native 0x528fa8: owner field.
        public bool IsScored => m_isScored;

        // Game.Runtime 0x06001e17, native 0x528fb0: owner field.
        public bool IsTimed => m_isTimed;

        // Game.Runtime 0x06001e18, native 0x528fb8: DefinitionData source field.
        public float TimeLimitSeconds => DefinitionData.m_timeLimitSeconds;

        // Game.Runtime 0x06001e19, native 0x529004: DefinitionData source field.
        public bool UseBestTime => DefinitionData.m_useBestTime;

        // Game.Runtime 0x06001e1a, native 0x529050: DefinitionData source field.
        public bool FailOnTimeLimitReached => DefinitionData.m_failOnTimeLimitReached;

        // Game.Runtime 0x06001e1b: repeated source reads; a zero/negative target uses the original total.
        public int TargetObjectivesToComplete => DefinitionData.m_targetObjectivesCount > 0
            ? DefinitionData.m_targetObjectivesCount : DefinitionData.m_totalObjectivesCount;

        // Game.Runtime 0x06001e1c, native 0x52914c: DefinitionData source field.
        public bool IsInstancedTracker => DefinitionData.m_isInstancedTracker;

        // Game.Runtime 0x06001e1d, native 0x529198: owner field.
        public bool IsInstancedSequencedTracker => m_isInstancedSequencedTracker;

        // Game.Runtime 0x06001e1e, native 0x5291a0: DefinitionData source field.
        public bool IsPersistentTracker => DefinitionData.m_isPersistentTracker;

        // Game.Runtime 0x06001e1f, native 0x5291ec: owner field.
        public bool IsReachGoalTracker => m_isReachGoalTracker;

        // Game.Runtime 0x06001e20, native 0x5291f4: DefinitionData source field.
        public UIContainerIdentifier ResultsScreenIdentifier => DefinitionData.m_resultsScreenIdentifier;

        // Game.Runtime 0x06001e21, native 0x529240: DefinitionData source field.
        public UIContainerIdentifier ShortResultsScreenIdentifier => DefinitionData.m_shortResultsScreenIdentifier;

        // Game.Runtime 0x06001e22, native 0x52928c: DefinitionData source field.
        public HLAudioClipIdentifier MusicOverrideIdentifier => DefinitionData.m_musicOverrideIdentifier;

        // Game.Runtime 0x06001e23, native 0x5292d8: DefinitionData source field.
        public bool MusicStartAtRandomTime => DefinitionData.m_musicStartAtRandomTime;

        // Game.Runtime 0x06001e24, native 0x529324: DefinitionData source field.
        public float CompletionCooldownTime => DefinitionData.m_completionCooldownTime;

        // Game.Runtime 0x06001e25, native 0x529370: DefinitionData source field.
        public int StartingRingCount => DefinitionData.m_startingRingCount;

        // Game.Runtime 0x06001e26, native 0x525dd4: DefinitionData source field.
        public float ObjectivesAdditionalTotalTimeSeconds => DefinitionData.m_objectivesAdditionalTotalTimeSeconds;

        // Game.Runtime 0x06001e27, native 0x5293bc: owner field.
        public MissionDefinition CompletedMissionOverrides => m_completedMissionOverrides;

        // Game.Runtime 0x06001e28, native 0x5293c4: DefinitionData source field.
        public LeaderboardIdentifier LeaderboardIdentifier => DefinitionData.m_leaderboardIdentifier;

        // Game.Runtime 0x06001e29, native 0x529410: DefinitionData source field.
        public MissionRankDefinition Ranks => DefinitionData.m_ranks;

        // Game.Runtime 0x06001e2a, native 0x52945c: owner field.
        public GameplayLevelDefinition SceneOverride => m_sceneOverride;

        // Game.Runtime 0x06001e2b, native 0x529464: owner field.
        public bool AllowLockedCharacters => m_allowLockedCharacters;

        // Game.Runtime 0x06001e2c, native 0x52946c: owner field.
        public ZoneThemeOverride ZoneThemeOverride => m_zoneThemeOverride;

        // Game.Runtime 0x06001e2d, native 0x529474: owner field.
        public bool UseAltUILayout => m_useAltUILayout;

        // Game.Runtime 0x06001e2e, native 0x528a3c: original GUID equality, lazy self source, without m_hasOverrides gating.
        private MissionDefinition DefinitionData
        {
            get
            {
                if (m_definitionDataSource == null) m_definitionDataSource = this;
                return m_definitionDataSource;
            }
        }

        // Game.Runtime 0x06001e2f, native 0x52947c: raw source dereference, original retained handle and unguarded completed callback.
        public void LoadMissionPrefab(Action<MissionTracker, bool> onComplete)
        {
            if (m_missionLevelDefinition != null)
            {
                LoadMissionScene(onComplete);
                return;
            }
            m_missionTrackerHandle = Addressables.LoadAssetAsync<GameObject>(m_definitionDataSource.m_missionTrackerAddressable);
            m_missionTrackerHandle.Completed += handle => onComplete(handle.Result.GetComponent<MissionTracker>(), false);
        }

        // Game.Runtime 0x06001e30, native 0x529624: additive scene, activation enabled, priority100.
        private void LoadMissionScene(Action<MissionTracker, bool> onComplete)
        {
            var operation = Addressables.LoadSceneAsync(m_missionLevelDefinition.SceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Additive, true, 100);
            operation.Completed += sceneInstanceOpHandle => OnMissionSceneLoaded(sceneInstanceOpHandle, onComplete);
        }

        // Game.Runtime 0x06001e31: first root containing the original tracker wins; missing tracker reports and invokes no callback.
        private void OnMissionSceneLoaded(AsyncOperationHandle<SceneInstance> sceneInstanceOpHandle,
            Action<MissionTracker, bool> onComplete)
        {
            m_missionSceneInstance = sceneInstanceOpHandle.Result;
            foreach (var gameObject in m_missionSceneInstance.Scene.GetRootGameObjects())
            {
                var tracker = gameObject.GetComponent<MissionTracker>();
                if (tracker != null)
                {
                    onComplete(tracker, true);
                    return;
                }
            }
            HLOutput.LogError("Couldn't load mission scene", null);
        }

        // Game.Runtime 0x06001e32: loaded original scene precedes valid prefab release; original handles/fields retained.
        public void UnloadMissionPrefab()
        {
            if (m_missionLevelDefinition != null && m_missionSceneInstance.Scene.isLoaded)
            {
                Addressables.UnloadSceneAsync(m_missionSceneInstance, true);
                return;
            }
            if (m_missionTrackerHandle.IsValid()) Addressables.Release(m_missionTrackerHandle);
        }

        // Game.Runtime 0x06001e33: debug/source/save queries all execute before deciding the requirement.
        public bool ArchetypeRequirementMet(SaveDataGame saveDataGame)
        {
            bool debugUnlocked = DebugUnlockLevels.AreAllLevelsUnlocked();
            CharacterArchetype unlocks = DefinitionData.m_unlocksCharacterArchetype;
            var saveData = saveDataGame.GetOrCreateCharacterArchetypeData(m_characterArchetype);
            return debugUnlocked || unlocks != CharacterArchetype.None || saveData.Unlocked || m_allowLockedCharacters;
        }

        // Game.Runtime 0x06001e34: original description utility receives name, time and effective objective target in that order.
        public string GetFormattedMissionName() => MissionStringsUtil.GetMissionDescription(MissionName, TimeLimitSeconds, TargetObjectivesToComplete);

        // Game.Runtime 0x06001e35: original deferred/ready registration; no additional data-source assignment here.
        public void InitialiseDefinition() => m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
        // Game.Runtime 0x06001e36: preserve the listener's original immediate-open flag.
        private void OnSaveManagerValid(SaveManager saveManager) => saveManager.AddListener(this, true);

        // Game.Runtime 0x06001e37: update first, then subscribe a fresh shutdown callback; no deduplication.
        public void OnSaveGameOpen(SaveDataGame saveDataGame)
        {
            OnSaveGameLoaded(saveDataGame);
            m_saveManagerRef.OnSystemShutdown += saveManager => saveManager.RemoveListener(this);
        }
        // Game.Runtime 0x06001e38, native 0x529e6c: genuine RET, no extra unsubscribe/reset.
        public void OnSaveGameClose(SaveDataGame saveDataGame) { }

        // Game.Runtime 0x06001e39: original DataManager mapping, original saved level/mission lookup; unknown mapping leaves fields untouched.
        private void OnSaveGameLoaded(SaveDataGame saveDataGame)
        {
            if (ProcessManager.GetSystem<DataManager>(null, true).MissionDefinitionLevelLookup.TryGetValue(this, out var level))
                UpdateOverrides(saveDataGame.GetOrCreateLevelData(level.GetGUID()).GetOrCreateMissionData(m_guid));
        }

        // Game.Runtime 0x06001e3a: original GUID string match precedes all writes; completion and actual override determine the source.
        public void UpdateOverrides(SaveDataLevelMission missionSaveData)
        {
            if (missionSaveData.GUID != m_guid) return;
            m_hasOverrides = m_completedMissionOverrides != null;
            m_isBeingOverridden = missionSaveData.Complete && m_hasOverrides;
            m_definitionDataSource = m_isBeingOverridden ? m_completedMissionOverrides : this;
        }

        // Game.Runtime 0x06001e3b: active completion override is required before the original timing test.
        public bool IsTimeTrial() => m_isBeingOverridden && CanBeTimeTrial();
        // Game.Runtime 0x06001e3c: native timing test does not require the active override flag.
        public bool HasTimeTrial() => CanBeTimeTrial();
        // Game.Runtime 0x06001e3d: original GUID override selection, then its effective source time; NaN/zero/negative fail.
        private bool CanBeTimeTrial()
        {
            var definition = m_completedMissionOverrides != null ? m_completedMissionOverrides : this;
            return definition.DefinitionData.m_timeLimitSeconds > 0;
        }

        // Game.Runtime 0x06001e3e: owner tips, Unity destroyed/null rule, output is assigned even when empty/false.
        public bool TryGetTips(out IReadOnlyList<Strings> tips)
        {
            tips = m_tipsOverride != null ? m_tipsOverride.TipIds : null;
            return tips != null && tips.Count > 0;
        }

        // Game.Runtime 0x06001e3f: default output first; a valid manager writes the key before its readiness result.
        public bool TryGetLeaderboardIdentifier(out LeaderboardIdentifier leaderboardIdentifier)
        {
            leaderboardIdentifier = LeaderboardIdentifier.None;
            if (LeaderboardIdentifier == LeaderboardIdentifier.None || !m_timeTrialLeaderboardManagerRef.TryGet(out var manager)) return false;
            leaderboardIdentifier = LeaderboardIdentifier;
            return manager.IsReady();
        }

        // Game.Runtime 0x06001e40: effective rank definition, but original owner's theoretical minimum; Unity null failure clears output.
        public bool TryGetRankForTimeSeconds(float timeSeconds, out MissionRank missionRank)
        {
            if (Ranks == null) { missionRank = null; return false; }
            return Ranks.TryGetRankForTimeSeconds(timeSeconds, TheoreticalBestTimePossibleSeconds, out missionRank);
        }
        // Game.Runtime 0x06001e41: same effective-rank null behavior without original minimum.
        public bool TryGetRankForTimeSecondsIgnoreMinimumTime(float timeSeconds, out MissionRank missionRank)
        {
            if (Ranks == null) { missionRank = null; return false; }
            return Ranks.TryGetRankForTimeSecondsIgnoreMinimumTime(timeSeconds, out missionRank);
        }
        // Game.Runtime 0x06001e42: original rank helper returns first sorted rank or null.
        public MissionRank GetBestRank() => Ranks == null ? null : Ranks.GetBestRank();
        // Game.Runtime 0x06001e43: original generic Contains over the exact Orb/Moon array; no broader reward list.
        public static bool IsReward(CollectableType collectableType) => s_rewardCollectableTypes.Contains(collectableType);

        // Game.Runtime 0x06001e44: output cleared before virtual key test; successful key returns true even if Load returns null.
        public bool TryGetLoadingImageOverride(out Sprite loadingImage)
        {
            loadingImage = null;
            bool valid = m_overrideLoadingImage.RuntimeKeyIsValid();
            if (valid)
            {
                if (LoadingImageOverride == null) LoadingImageOverride = new ManagedAddressableAsset<Sprite>(m_overrideLoadingImage);
                loadingImage = LoadingImageOverride.Load();
            }
            return valid;
        }
        // Game.Runtime 0x06001e45: virtual key check occurs before wrapper-null check; wrapper retained after unload.
        public void UnloadImageOverride()
        {
            if (m_overrideLoadingImage.RuntimeKeyIsValid() && LoadingImageOverride != null) LoadingImageOverride.Unload();
        }
        // Game.Runtime 0x06001e46: every original field initializer and SystemRef request precedes the genuine GUID base constructor.
        public MissionDefinition() { }
    }
}
