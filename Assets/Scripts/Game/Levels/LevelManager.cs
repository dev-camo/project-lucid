using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006ba: the complete 65-declaration manager.
    // The two original iterators, two instance callbacks, and three constructor callbacks are generated
    // naturally from their source. Their emitted metadata shape is unverified
    // until the genuine provider graph compiles; no replacement types are used.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class LevelManager : MonoBehaviour, ISystem
    {
        [SerializeField] private UIModernEvent m_restartUIEvent;
        [SerializeField] private UIModernEvent m_openMetaGameEvent;
        [SerializeField] private ApplicationStateEvent m_loadGameStartEvent;
        [SerializeField] private GameplayLevelCategory m_bonusZone1Category;
        [SerializeField] private GameplayLevelDefinition m_ftueLevelDefinition;
        [SerializeField] private GameplayLevelDefinition m_finalBossLevelDefinition;
        [SerializeField] private MissionRingRewardDefinition m_missionRingRewardDefinition;
        [SerializeField] private GameplayLevelDefinition[] m_tutorialLevels;
        [Tooltip("Levels to ignore as part of progression.")]
        [SerializeField] private GameplayLevelDefinition[] m_supplementalLevels;

        public MissionRingRewardDefinition MissionRingRewardDefinition => m_missionRingRewardDefinition;
        public GameplayLevelDefinition FinalBossLevelDefinition => m_finalBossLevelDefinition;
        public GameplayLevelDefinition[] TutorialLevels => m_tutorialLevels;
        public bool CurrentLevelValid { get; private set; }
        public LevelManagerSystems ManagedSystems { get; private set; }
        public Transform ManagedGameObjectParent { get; private set; }
        public GameplayLevelCategory BonusZone1Category => m_bonusZone1Category;
        private Action<LevelManagerLevel> m_actionOnLevelActivated;
        private Action<LevelManagerLevel> m_actionOnLevelLoaded;
        public Action OnLevelRestart = () => { };
        public Action OnIntroSequenceBegin { get; set; }
        public Action OnIntroSequenceComplete { get; set; }
        public Action OnLevelSelectRefreshed = () => { };
        public Action OnLevelExit = () => { };
        public GameLevels GameLevels { get; private set; }
        private const float MaximumDeltaTime = 5f;
        private LevelManagerLevel m_currentLevel;
        private CharacterManager m_characterManager;
        private App m_app;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private float m_sessionTimer;
        private bool m_levelStarted;
        private bool m_introSequencePlaying;

        // Original direct accessors 0x06002440..0x0600244f, including the
        // auto accessors above. No lookup, copy, or lazy initialization occurs.

        // 0x06002450; ARM 0x569e48. Registry publication precedes subscriptions.
        private void Awake()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            this.SubscribeToAction(SystemAction.Initialise, Initialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
            GameLevels = SystemConfiguration.GetConfig<GameLevels>();
        }

        // 0x06002451 and its two original instance callbacks 0x0600247f/80.
        private void Initialise(object context = null)
        {
            var managedObjects = new GameObject("LevelManager_ManagedObjects");
            DontDestroyOnLoad(managedObjects);
            ManagedGameObjectParent = managedObjects.transform;
            ManagedSystems = new LevelManagerSystems(ManagedGameObjectParent);
            ProcessManager.GetSystemRef<CharacterManager>().InvokeOnValid(manager => m_characterManager = manager);
            ProcessManager.GetSystemRef<App>().InvokeOnValid(manager => m_app = manager);
        }

        // 0x06002452. Child count is reread for each deferred Destroy call;
        // fields and registry membership are not reset by the original body.
        private void Shutdown(object context = null)
        {
            ManagedSystems?.Shutdown();
            if (ManagedGameObjectParent == null) return;
            for (int index = 0; index < ManagedGameObjectParent.childCount; ++index)
                Destroy(ManagedGameObjectParent.GetChild(index).gameObject);
            Destroy(ManagedGameObjectParent.gameObject);
        }

        // 0x06002453..55: a persistent multicast delegate, never cleared here.
        public void InvokeOnLevelLoaded(Action<LevelManagerLevel> action) => m_actionOnLevelLoaded += action;
        public void RemoveLevelLoadedAction(Action<LevelManagerLevel> action) => m_actionOnLevelLoaded -= action;
        public void OnLevelLoaded(LevelManagerLevel level) => m_actionOnLevelLoaded?.Invoke(level);

        // 0x06002456. Registration happens before the immediate direct call.
        public void InvokeOnLevelActivated(Action<LevelManagerLevel> action, bool triggerImmediately = true)
        {
            m_actionOnLevelActivated += action;
            if (triggerImmediately && TryGetCurrentLevel(out LevelManagerLevel level)) action(level);
        }

        // 0x06002457..59: validity is independent of the current reference.
        public void RemoveLevelActivatedAction(Action<LevelManagerLevel> action) => m_actionOnLevelActivated -= action;
        public LevelManagerLevel GetCurrentLevelUnsafe() => m_currentLevel;
        public bool TryGetCurrentLevel(out LevelManagerLevel level)
        {
            bool currentLevelValid = CurrentLevelValid;
            level = currentLevelValid ? m_currentLevel : null;
            return currentLevelValid;
        }

        // 0x0600245a; ARM 0x56a908. Valid and done are the only handle tests;
        // Result is directly unboxed to SceneInstance without a success test.
        public bool TryGetAddressableLevel(string sceneName, out SceneInstance scene)
        {
            scene = default;
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (!m_app.Storage.TryGetValue(AppFSMKeys.LoadedLevels,
                out Dictionary<string, AsyncOperationHandle> loadedLevels)) return false;
            if (!loadedLevels.TryGetValue(sceneName, out AsyncOperationHandle handle)) return false;
            if (!handle.IsValid()) return false;
            if (!handle.IsDone) return false;
            scene = (SceneInstance)handle.Result;
            return true;
        }

        // 0x0600245b: ordinary string identity after the validity flag.
        public bool IsLoadedSceneGUID(string sceneGUID) =>
            CurrentLevelValid && m_currentLevel.SceneDefinition.GetGUID() == sceneGUID;

        // 0x0600245c: the redundant SetParent retains worldPositionStays=true.
        public T InstantiateParented<T>(T prefab, Vector3 start, Quaternion rotation) where T : Component
        {
            T instance = UnityEngine.Object.Instantiate(prefab, start, rotation, ManagedGameObjectParent);
            instance.transform.SetParent(ManagedGameObjectParent, true);
            return instance;
        }

        // 0x0600245d: a missing storage value stores the original null default.
        public void LoadGame() => m_app.Storage.GetValue<List<ApplicationStateEvent>>(
            AppFSMKeys.StateEvents, null, true).AddUnique(m_loadGameStartEvent);

        // 0x0600245e/5f: no current-level test and no timer clamp.
        public void StartLevel()
        {
            m_levelStarted = true;
            m_sessionTimer = 0f;
            m_actionOnLevelActivated?.Invoke(m_currentLevel);
        }
        private void Update()
        {
            if (m_levelStarted) m_sessionTimer += Time.unscaledDeltaTime;
        }

        // 0x06002460; ARM 0x56ac5c. The public OnLevelRestart action is not
        // called here. Save settings are obtained before the mixer lookup.
        public void Restart()
        {
            if (m_currentLevel == null) return;
            if (m_app.Storage.GetValue(AppFSMKeys.RestartInProgress, false, true)) return;
            if (m_app.IsMetaGameActive()) return;
            if (m_audioManagerRef.TryGet(out AudioManager audioManager)
                && m_saveManagerRef.TryGet(out SaveManager saveManager))
            {
                SaveDataSettings settings = saveManager.GetSaveDataSettings();
                audioManager.GetMixer(HLAudioMixerIdentifier.Main).ResetVolume(settings);
            }
            m_app.AddUniqueUIModernEvent(m_restartUIEvent);
            m_app.Storage.SetValue(AppFSMKeys.RestartInProgress, true);
            m_app.Storage.SetValue(AppFSMKeys.IsInPostGame, false);
        }

        // 0x06002461; ARM 0x56af60. Save mutation precedes character/storage
        // callbacks. OnLevelExit is invoked directly, preserving a null fault.
        public void ExitLevel(bool skipOutro, bool showFailureScreen = false)
        {
            m_levelStarted = false;
            SaveManager saveManager = ProcessManager.GetSystem<SaveManager>();
            // Preserve the elapsed-time float before resolving the stat record.
            float sessionTimeMS = m_sessionTimer * 1000f;
            saveManager.CurrentSave.GetOrCreatePlayerStatData(SaveDataPlayerStat.Type.TotalPlayTimeMS)
                .IncrementCounter((long)sessionTimeMS);
            saveManager.CurrentSave.RequestSave();
            if (m_characterManager.TryGetCurrentCharacter(out Character character)) character.OnExitLevel();
            m_app.Storage.SetValue(AppFSMKeys.SkipOutroSequence, skipOutro);
            m_app.Storage.SetValue(AppFSMKeys.ShowFailureScreen, showFailureScreen);
            m_app.Storage.RemoveValue<bool>(AppFSMKeys.RestartInProgress);
            m_app.AddUniqueUIModernEvent(m_openMetaGameEvent);
            OnLevelExit();
        }

        // 0x06002462. Finding a production definition only gates assignment:
        // the original does not create a level or assign the found definition.
        public void RegisterLevelDataForMonoBehaviour(LevelData levelData)
        {
            GameplayLevelDefinition definition = GetProductionLevelBySceneName(levelData.gameObject.scene.name);
            if (definition == null) return;
            m_currentLevel.Data = levelData;
            CurrentLevelValid = true;
        }

        // 0x06002463; ARM 0x56b5fc. Engine equality selects the respawn override.
        public void TeleportInstant(LevelStartPositionDefinition startPositionDefinition)
        {
            if (!CurrentLevelValid) return;
            Transform start = m_currentLevel.Data.GetStartingPosition(startPositionDefinition);
            Character character = m_characterManager.GetCurrentCharacterUnsafe();
            character.Teleport(start.position, start.rotation, false, true);
            Transform respawn = m_currentLevel.Data.GetDefaultRespawnPoint();
            if (respawn != null) start = respawn.transform;
            character.Storage.SetValue(ActorFSMKeys.LastRespawnPoint, start);
            ProcessManager.GetSystem<CinemachineCameraManager>().ForceAllVirtualCamerasToPosition(
                character.WorldPosition, character.WorldRotation, true, true);
        }

        // 0x06002464 and the original six-method iterator. Both comparison
        // results are evaluated before deciding whether a loaded scene is reused.
        // There is no finally: disposal/failure does not restore the Time values.
        public IEnumerator LoadLevelSceneForFSM(GameplayLevelDefinition gameplayLevelDefinition,
            GameplayLevelDefinition sceneDefinition)
        {
            if (m_currentLevel != null)
            {
                AsyncOperationHandle<SceneInstance> currentHandle = m_currentLevel.SceneInstance;
                bool notLoaded = !currentHandle.IsValid() || !currentHandle.Result.Scene.isLoaded;
                bool shouldUnload = ShouldUnloadBeforeReload(m_currentLevel.SceneDefinition);
                bool differentScene = m_currentLevel.SceneDefinition != sceneDefinition;
                if (!notLoaded && !(shouldUnload | differentScene)) yield break;
                if (!notLoaded) yield return UnloadLevel();
            }
            float cachedMaxDelta = Time.maximumDeltaTime;
            float cachedParticleDelta = Time.maximumParticleDeltaTime;
            Time.maximumDeltaTime = MaximumDeltaTime;
            Time.maximumParticleDeltaTime = MaximumDeltaTime;
            yield return Resources.UnloadUnusedAssets();
            AsyncOperationHandle<SceneInstance> loadHandle = Addressables.LoadSceneAsync(
                sceneDefinition.SceneName, LoadSceneMode.Additive, true, 100);
            m_currentLevel = new LevelManagerLevel(gameplayLevelDefinition, sceneDefinition)
                { SceneInstance = loadHandle };
            yield return loadHandle;
            m_app.SetLastLevelLoadedGUID(sceneDefinition);
            Time.maximumDeltaTime = cachedMaxDelta;
            Time.maximumParticleDeltaTime = cachedParticleDelta;
        }

        // 0x06002465 and the original six-method iterator. Read Result before
        // clearing current state; skip all post-unload cleanup for an unloaded or
        // invalid scene. The original Dispose is empty and supplies no finally.
        public IEnumerator UnloadLevel()
        {
            AsyncOperationHandle<SceneInstance> handle = m_currentLevel.SceneInstance;
            Scene scene = handle.Result.Scene;
            m_currentLevel = null;
            CurrentLevelValid = false;
            if (!scene.isLoaded) yield break;
            if (!scene.IsValid()) yield break;
            yield return Addressables.UnloadSceneAsync(handle, true);
            yield return Resources.UnloadUnusedAssets();
            ProcessManager.GetSystem<GameplayIslandManager>().ClearPendingResetCapableGameplayElements();
            ProcessManager.GetSystem<EntityActivationManager>().Clear();
            m_app.SetLastLevelLoadedGUID(null);
        }

        // 0x06002466: state clearing only.
        public void UnloadLevelForFSM()
        {
            m_currentLevel = null;
            CurrentLevelValid = false;
        }

        // 0x06002467: does not publish a current level or set its validity.
        public void SetLevelActiveForFSM(LevelManagerLevel level)
        {
            SceneManager.SetActiveScene(level.SceneInstance.Result.Scene);
            VisualQualityManager_SDT visualQuality = ProcessManager.GetSystem<VisualQualityManager_SDT>();
            if (level.LevelDefinition.ShouldOverrideShadowDistance)
                visualQuality.OverrideShadowDistance(level.LevelDefinition.ShadowDistance);
            else visualQuality.RestoreDefaultShadowDistance();
        }

        // 0x06002468: Unity equality precedes the managed ownership lookup.
        public void ReplaceManagedSystemIfRequired<T>(GameplayLevelDefinition levelDefinition, T replaceWithSystem)
            where T : Component, ISystem
        {
            if (replaceWithSystem == null) return;
            if (ManagedSystems.HasSystemOfType<T>())
            {
                GameplayLevelDefinition previous = GetProductionLevelByGUID(m_app.GetLastLevelUIVisitedGUID(false));
                if (previous != null && previous.LevelSetupType == levelDefinition.LevelSetupType) return;
            }
            ManagedSystems.ReplacePrefab(replaceWithSystem);
        }

        // 0x06002469: capture Storage once; construction and writes retain order.
        public void SetupFsmVarsToLoadLevel(GameplayLevelDefinition levelToLoad,
            GameplayLevelDefinition overrideLoadingImage = null)
        {
            IGraphStorage storage = m_app.Storage;
            storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(levelToLoad));
            storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(levelToLoad, null));
            GameplayLevelDefinition loadingImage = overrideLoadingImage == null ? levelToLoad : overrideLoadingImage;
            storage.SetValue(AppFSMKeys.UIContainerParameters, new UIContainerLoadingParameters(loadingImage, null));
        }

        // 0x0600246a: the FTUE reference itself.
        public GameplayLevelDefinition GetDefaultProductionLevel() => m_ftueLevelDefinition;

        // 0x0600246b: save data is created before requirements are evaluated.
        public GameplayLevelDefinition GetNextLevelNotSeen()
        {
            IReadOnlyList<GameplayLevelDefinition> levels = GameLevels.GetLevels();
            SaveDataGame currentSave = m_saveManagerRef.Get().CurrentSave;
            foreach (GameplayLevelDefinition level in levels)
            {
                if (m_tutorialLevels.Contains(level)) continue;
                if (!level.ShowInLevelSelect) continue;
                if (level.ExcludeFromLevelOrdering) continue;
                SaveDataLevel saveLevel = currentSave.GetOrCreateLevelData(level.GetGUID());
                if (!level.MeetsAllRequirements()) continue;
                if (saveLevel.UnlockSeen) continue;
                return level;
            }
            return null;
        }

        // 0x0600246c: first ordinary string match in the cached level order.
        public GameplayLevelDefinition GetProductionLevelByGUID(string levelGUID)
        {
            foreach (GameplayLevelDefinition level in GameLevels.GetLevels())
                if (level.GetGUID() == levelGUID) return level;
            return null;
        }

        // 0x0600246d/6e: supplemental membership is checked here, unlike the
        // unlocked-list method. No ShowInLevelSelect test belongs to these bodies.
        public GameplayLevelDefinition GetFirstProductionLevelLocked()
        {
            foreach (GameplayLevelDefinition level in GameLevels.GetLevels())
            {
                if (m_tutorialLevels.Contains(level)) continue;
                if (m_supplementalLevels.Contains(level)) continue;
                if (level.ExcludeFromLevelOrdering) continue;
                if (!level.MeetsAllRequirements()) return level;
            }
            return null;
        }
        public GameplayLevelDefinition GetFirstProductionLevelLocked(GameplayLevelCategory category)
        {
            foreach (GameplayLevelDefinition level in GameLevels.GetLevels(category))
            {
                if (m_tutorialLevels.Contains(level)) continue;
                if (m_supplementalLevels.Contains(level)) continue;
                if (level.ExcludeFromLevelOrdering) continue;
                if (!level.MeetsAllRequirements()) return level;
            }
            return null;
        }

        // 0x0600246f: compares SceneName, rather than the virtual GetName.
        public GameplayLevelDefinition GetProductionLevelBySceneName(string sceneName)
        {
            foreach (GameplayLevelDefinition level in GameLevels.GetLevels())
                if (level.SceneName == sceneName) return level;
            return null;
        }

        // 0x06002470: allocate a fresh list after obtaining the cached levels.
        public List<GameplayLevelDefinition> GetUnlockedLevels()
        {
            IReadOnlyList<GameplayLevelDefinition> levels = GameLevels.GetLevels();
            var unlocked = new List<GameplayLevelDefinition>();
            foreach (GameplayLevelDefinition level in levels)
            {
                if (m_tutorialLevels.Contains(level)) continue;
                if (level.MeetsAllRequirements()) unlocked.Add(level);
            }
            return unlocked;
        }

        // 0x06002471: append one unlock, without marking it seen or deduplicating.
        public void UpdateUnlocks()
        {
            GameplayLevelDefinition level = GetNextLevelNotSeen();
            if (level == null) return;
            List<IMetaGameUnlock> unlocks = m_app.Storage.GetValue<List<IMetaGameUnlock>>(
                AppFSMKeys.MetaGameUnlocks, null, true);
            IMetaGameUnlock unlock;
            if (m_bonusZone1Category.Contains(level)
                && !m_saveManagerRef.Get().CurrentSave.ZoneUnlockSeenGuids.Contains(m_bonusZone1Category.GetGUID()))
                unlock = new MetaGameUnlockBonusZone1(m_bonusZone1Category);
            else unlock = new MetaGameUnlockLevel(level);
            unlocks.Add(unlock);
        }

        // 0x06002472/73: retain original GUID equality and first mission access.
        public bool ShouldUnloadBeforeReload(GameplayLevelDefinition levelDefinition) =>
            levelDefinition.IsBossLevel || m_ftueLevelDefinition == levelDefinition;
        public bool HasFTUEBeenCompleted()
        {
            GameplayLevelDefinition level = m_ftueLevelDefinition;
            MissionDefinition mission = level.MissionList.GetMissions(false)[0];
            return ProcessManager.GetSystem<MissionManager>().GetSaveDataForMission(
                mission.GetGUID(), level.GetGUID(), null).Complete;
        }

        // 0x06002474: no zone index validation; engine equality checks MissionList.
        public int MissionsOfTypeInZone(int zoneIndex, MissionType missionType, bool playerCompleted)
        {
            IReadOnlyList<GameplayLevelCategory> zones = GameLevels.GetZones();
            IReadOnlyList<GameplayLevelDefinition> levels = GameLevels.GetLevels(zones[zoneIndex]);
            int count = 0;
            foreach (GameplayLevelDefinition level in levels)
            {
                if ((UnityEngine.Object)level.MissionList == null) continue;
                foreach (MissionDefinition mission in level.MissionList.GetMissionsOfType(missionType))
                {
                    if (playerCompleted && !m_missionManagerRef.Get().GetSaveDataForMission(
                        mission.GetGUID(), level.GetGUID(), null).Complete) continue;
                    ++count;
                }
            }
            return count;
        }

        // 0x06002475: only an empty zone list returns false. Invalid supplied
        // indices retain indexer faults; a null progress still produces true.
        private bool TryGetRewardProgressForLevel(int zoneIndex, int levelIndex,
            out ProgressionCollectableLevel levelRewardProgress)
        {
            IReadOnlyList<GameplayLevelCategory> zones = GameLevels.GetZones();
            if (zones.Count == 0)
            {
                levelRewardProgress = null;
                return false;
            }
            GameplayLevelDefinition level = GameLevels.GetLevels(zones[zoneIndex])[levelIndex];
            levelRewardProgress = ProcessManager.GetSystem<ProgressionManager>().GetLevelProgress(level);
            return true;
        }

        // 0x06002476: the mission-list count is independent of progression state.
        public int GetRewardTotalForLevel(int zoneIndex, int levelIndex, CollectableType rewardType = CollectableType.Orb)
        {
            IReadOnlyList<GameplayLevelCategory> zones = GameLevels.GetZones();
            GameplayLevelDefinition level = GameLevels.GetLevels(zones[zoneIndex])[levelIndex];
            if ((UnityEngine.Object)level.MissionList == null) return 0;
            return level.MissionList.MissionCountWithReward(rewardType);
        }

        // 0x06002477: absent dictionary keys return the value-type zero default.
        public int GetRewardCollectedForLevel(int zoneIndex, int levelIndex, CollectableType rewardType = CollectableType.Orb)
        {
            if (!TryGetRewardProgressForLevel(zoneIndex, levelIndex, out ProgressionCollectableLevel progress)) return 0;
            return progress.Progress.GetValueOrDefault(rewardType).Collected;
        }

        // 0x06002478: the upper zone bound is checked, but a negative zone is not.
        // Selected acts are zero-based array membership, without deduplication.
        public int GetRankedMissionCountForZone(int zoneIndex, int[] specificActs = null)
        {
            IReadOnlyList<GameplayLevelCategory> zones = GameLevels.GetZones();
            if (zones.Count <= zoneIndex) return 0;
            IReadOnlyList<GameplayLevelDefinition> levels = GameLevels.GetLevels(zones[zoneIndex]);
            int count = 0;
            for (int index = 0; index < levels.Count; ++index)
            {
                if (specificActs != null && !specificActs.Contains(index)) continue;
                GameplayLevelDefinition level = levels[index];
                if ((UnityEngine.Object)level.MissionList == null) continue;
                foreach (MissionDefinition mission in level.MissionList.GetMissions(false))
                    if (mission.Ranks != null) ++count;
            }
            return count;
        }

        // 0x06002479: the save system is fetched once after IsNull, then its
        // open-save predicate gates reading CurrentSave and enumerating acts.
        public int RankCountAchievedInZone(int zoneIndex, RankType rankType, int[] specificActsToCheck = null)
        {
            IReadOnlyList<GameplayLevelCategory> zones = GameLevels.GetZones();
            if (zones.Count <= zoneIndex) return 0;
            if (m_saveManagerRef.IsNull()) return 0;
            SaveManager saveManager = m_saveManagerRef.Get();
            if (!saveManager.IsAnySaveOpen) return 0;
            SaveDataGame currentSave = saveManager.CurrentSave;
            IReadOnlyList<GameplayLevelDefinition> levels = GameLevels.GetLevels(zones[zoneIndex]);
            int count = 0;
            for (int index = 0; index < levels.Count; ++index)
            {
                if (specificActsToCheck != null && !specificActsToCheck.Contains(index)) continue;
                count += RankCountAchievedInLevel(rankType, levels[index], currentSave);
            }
            return count;
        }

        // 0x0600247a: read missions before creating save-level data; only ranked
        // completed missions create mission save data and test BestTimeSeconds.
        private static int RankCountAchievedInLevel(RankType rankType, GameplayLevelDefinition level,
            SaveDataGame currentSave)
        {
            if ((UnityEngine.Object)level.MissionList == null) return 0;
            IReadOnlyList<MissionDefinition> missions = level.MissionList.GetMissions(false);
            SaveDataLevel saveLevel = currentSave.GetOrCreateLevelData(level.GetGUID());
            int count = 0;
            foreach (MissionDefinition mission in missions)
            {
                if (mission.Ranks == null) continue;
                SaveDataLevelMission saveMission = saveLevel.GetOrCreateMissionData(mission.GetGUID());
                if (!saveMission.Complete) continue;
                if (mission.TryGetRankForTimeSeconds(saveMission.BestTimeSeconds, out MissionRank rank)
                    && rank.RankType == rankType) ++count;
            }
            return count;
        }

        // 0x0600247b: publish the changed flag before invoking the chosen action.
        public void SetIntroSequencePlaying(bool playing)
        {
            if (m_introSequencePlaying == playing) return;
            m_introSequencePlaying = playing;
            if (playing) OnIntroSequenceBegin?.Invoke();
            else OnIntroSequenceComplete?.Invoke();
        }

        // 0x0600247c: saved selection is requested explicitly; FTUE is fallback.
        public GameplayLevelDefinition FindMetaGameLevel()
        {
            string guid = m_app.GetLastLevelUISelectedGUID(true);
            if (string.IsNullOrWhiteSpace(guid)) return m_ftueLevelDefinition;
            GameplayLevelDefinition level = GetProductionLevelByGUID(guid);
            if (level == null || !level.MeetsAllRequirements()) return m_ftueLevelDefinition;
            return level;
        }

        // 0x0600247d: read the non-gameplay scene before fetching configuration.
        public GameplayLevelDefinition GetLevelForCutscene(CutsceneDefinition cutscene)
        {
            string sceneName = cutscene.NonGameplaySceneName;
            if (SystemConfiguration.GetConfig<CoreGameConfiguration>().UseExportedScenes) sceneName += "_exported";
            return GameLevels.GetLevel(sceneName);
        }

        // 0x0600247e: action and SystemRef field initializers run in their
        // original order before the MonoBehaviour base constructor. No other
        // state initialization or registration belongs to this constructor.
        public LevelManager() { }
    }
}
