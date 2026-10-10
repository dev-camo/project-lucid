using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original mission orchestration. Native method tokens identify preservation
    // evidence; compiler-generated iterator and delegate identities remain separate
    // validation frontiers until bound to a real compiled Game.Runtime assembly.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionManager : ISystem
    {
        public MissionList MissionList { get; private set; }
        public MissionGroup CurrentGroup { get; private set; }
        public IMissionContext ActiveMissionContext { get; private set; }
        public IReadOnlyDictionary<string, MissionState> MissionStates => m_missionStates;
        public IReadOnlyDictionary<string, SaveDataLevelMission> PreLevelCachedMissionData => m_preLevelCachedMissionData;
        public event Action<bool> OnMissionsReset = _ => { };
        public Action OnRewardCountChanged = () => { };
        public Action<MissionState> OnMissionCompleted;
        public Action<bool> OnMissionTimersPaused;
        public event Action<MissionState> OnMissionIslandActive = _ => { };
        private GameObject m_activeTracker;
        private readonly Dictionary<string, MissionState> m_missionStates = new Dictionary<string, MissionState>();
        private readonly Dictionary<string, SaveDataLevelMission> m_preLevelCachedMissionData = new Dictionary<string, SaveDataLevelMission>();
        private readonly LevelManager m_levelManager = ProcessManager.GetSystem<LevelManager>();
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
        private readonly SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private WaypointManager m_waypointManager;
        private string m_islandOverrideMissionStateGUID;
        private bool m_timersPaused;

        //060026e5: original field/delegate initializers precede Object's ctor;
        //then subscribe to actual Initialise=1 and Shutdown=2 process actions.
        public MissionManager()
        {
            ProcessManager.SubscribeToAction(this, SystemAction.Initialise, OnInitialise);
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown, OnShutdown);
        }

        //060026e6: clear an earlier context before publishing the supplied one.
        //The captured parameter and the current property are deliberately used at
        //different points, retaining reentrant context getter behavior.
        public void SetActiveMissionContext(IMissionContext missionContext)
        {
            if (ActiveMissionContext != null) ClearActiveMissionContext();
            ActiveMissionContext = missionContext;
            if (missionContext == null || missionContext.MissionDefinition == null) return;
            CalculateMissionGroup(ActiveMissionContext.MissionDefinition);
            string missionGUID = missionContext.MissionDefinition.GetGUID();
            if (m_missionStates.TryGetValue(missionGUID, out MissionState state))
            {
                state.DestroyMissionTracker(m_missionStates, true);
                m_missionStates.Remove(missionGUID);
            }
            LoadMission(missionContext.MissionDefinition, missionContext.LevelDefinition);
        }

        //060026e7: a missing list yields a new empty enumerable. A missing match
        //leaves CurrentGroup intact. Authored groups and definitions are unguarded;
        //both enumerators are disposed when a match returns or an access faults.
        private void CalculateMissionGroup(MissionDefinition missionDefinition)
        {
            IEnumerable<MissionGroup> groups = MissionList != null ? MissionList.Groups : new List<MissionGroup>();
            foreach (MissionGroup group in groups)
                foreach (MissionDefinition definition in group.Definitions)
                    if (definition == missionDefinition)
                    {
                        CurrentGroup = group;
                        return;
                    }
        }

        //060026e8: only visible level-select levels contribute; hidden mission
        //groups are excluded. Read SubMissions.Count even when skipping children;
        //append a second property read only for a nonempty list and false flag.
        public List<MissionDefinition> GetAllMissionDefinitions(bool skipSubMissions = false)
        {
            var definitions = new List<MissionDefinition>();
            foreach (GameplayLevelDefinition level in m_levelManager.GameLevels.GetLevels())
            {
                if (level is null || level.MissionList == null || !level.ShowInLevelSelect || level.MissionList == null)
                    continue;
                foreach (MissionDefinition mission in level.MissionList.GetMissions())
                {
                    definitions.Add(mission);
                    if (mission.SubMissions.Count != 0 && !skipSubMissions)
                        definitions.AddRange(mission.SubMissions);
                }
            }
            return definitions;
        }

        //060026e9/0600271e/f: GUID predicate dereferences every candidate; first
        //match only. The actual base API is GetGUID, with a direct m_guid read.
        public MissionDefinition GetMissionDefinition(string GUID)
        {
            return GetAllMissionDefinitions().Find(mission => mission.GetGUID() == GUID);
        }

        //060026ea: a newly loaded state reports true even if a reentrant context
        //has caused loading to return null. Existing state values are not filtered.
        public bool TryGetActiveMissionState(out MissionState activeMissionState, bool createIfMissing = false)
        {
            activeMissionState = null;
            if (ActiveMissionContext == null || ActiveMissionContext.MissionDefinition == null) return false;
            if (m_missionStates.TryGetValue(ActiveMissionContext.MissionDefinition.GetGUID(), out activeMissionState)) return true;
            if (!createIfMissing) return false;
            activeMissionState = LoadMission(ActiveMissionContext.MissionDefinition, ActiveMissionContext.LevelDefinition);
            return true;
        }

        //060026eb: compare with string.Empty exactly. A null override is not
        //sanitized and therefore reaches the dictionary's original key failure.
        public bool TryGetIslandOverrideMissionState(out MissionState activeMissionState)
        {
            activeMissionState = null;
            if (ActiveMissionContext == null || ActiveMissionContext.MissionDefinition == null ||
                m_islandOverrideMissionStateGUID == string.Empty) return false;
            return m_missionStates.TryGetValue(m_islandOverrideMissionStateGUID, out activeMissionState);
        }

        //060026ec: always create the output list before the context checks.
        //Append the primary state and only immediate stored sub-mission states.
        public bool TryGetAllActiveMissionStates(out List<MissionState> activeMissionStates)
        {
            activeMissionStates = new List<MissionState>();
            if (ActiveMissionContext == null || ActiveMissionContext.MissionDefinition == null ||
                !m_missionStates.TryGetValue(ActiveMissionContext.MissionDefinition.GetGUID(), out MissionState state)) return false;
            activeMissionStates.Add(state);
            foreach (MissionDefinition subMission in state.Definition.SubMissions)
                if (m_missionStates.TryGetValue(subMission.GetGUID(), out MissionState subState)) activeMissionStates.Add(subState);
            return true;
        }

        //060026ed: preserve states in the dictionary while destroying trackers.
        //An absent context returns before even resetting the island override.
        private void ClearActiveMissionContext()
        {
            if (ActiveMissionContext == null) return;
            if (ActiveMissionContext.MissionDefinition != null &&
                m_missionStates.TryGetValue(ActiveMissionContext.MissionDefinition.GetGUID(), out MissionState state))
                state.DestroyMissionTracker(m_missionStates, true);
            ActiveMissionContext = null;
            m_islandOverrideMissionStateGUID = string.Empty;
        }

        //060026ee: GUID-aware equality is intentional, including two null
        //definitions under a nonnull context.
        public void ClearMissionIfActive(MissionDefinition mission)
        {
            if (ActiveMissionContext != null && ActiveMissionContext.MissionDefinition == mission) ClearActiveMissionContext();
        }

        //060026ef: successful lookup, managed-null state, then genuine complete
        //setup test. Callback is unguarded on the active path.
        public void InvokeIfMissionActive(MissionDefinition missionDefinition, Action<MissionState> action)
        {
            if (m_missionStates.TryGetValue(missionDefinition.GetGUID(), out MissionState state) &&
                state != null && state.SetUpComplete) action(state);
        }

        //060026f0: load eligible children before the parent. Save data is fetched
        //before Replayable, and already-complete nonreplayable children are skipped.
        public MissionState LoadMission(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            if (missionDefinition == null) return null;
            foreach (MissionDefinition subMission in missionDefinition.SubMissions)
            {
                string missionGUID = subMission.GetGUID();
                string levelGUID = levelDefinition.GetGUID();
                SaveDataLevelMission saveData = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelGUID).GetOrCreateMissionData(missionGUID);
                if (subMission.Replayable || !saveData.Complete) LoadSingleMission(subMission, levelDefinition);
            }
            return LoadSingleMission(missionDefinition, levelDefinition);
        }

        //060026f1: existing state returned verbatim; no definition/lifetime
        //guard or tracker setup. Real save data supplies the missing state.
        private MissionState LoadSingleMission(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            if (m_missionStates.TryGetValue(missionDefinition.GetGUID(), out MissionState state)) return state;
            return CreateMissionState(missionDefinition, m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID()));
        }

        //060026f2: set up only a state whose data or tracker is incomplete.
        //Capture Definition before the context getter; reread the current context
        //after its comparison. The tracker-parent object is unguarded.
        public void SetUpMissionTracker(MissionState missionState)
        {
            if (missionState.SetUpComplete) return;
            MissionDefinition definition = missionState.Definition;
            IMissionContext context = definition == ActiveMissionContext?.MissionDefinition ? ActiveMissionContext : null;
            missionState.SetUpTracker(m_activeTracker.transform, context);
        }

        //060026f3: challenge contexts use genuine unsaved mission data rather than
        //persisted progression. Retain allocation before definition/context reads
        //and publish the dictionary entry only after full SetUpData completes.
        private MissionState CreateMissionState(MissionDefinition missionDefinition, SaveDataLevel saveDataLevel)
        {
            var state = new MissionState();
            string missionGUID = missionDefinition.GetGUID();
            SaveDataLevelMission saveData;
            if (ActiveMissionContext?.MissionDefinition == missionDefinition && ActiveMissionContext is MissionContextChallenge)
            {
                saveData = new SaveDataLevelMission(missionGUID);
                saveData.DisableSaving();
            }
            else saveData = saveDataLevel.GetOrCreateMissionData(missionGUID);
            state.SetUpData(missionDefinition, saveData, saveDataLevel.GUID);
            m_missionStates[missionGUID] = state;
            return state;
        }

        //060026f4: immediate validity and future startup registration are
        //separate original paths. InvokeOnValid can synchronously install the
        //island callback before the explicit IsValid branch installs it again.
        private void OnInitialise(object context = null)
        {
            m_waypointManager = ProcessManager.GetSystem<WaypointManager>();
            m_gameplayIslandManagerRef.InvokeOnValid(OnGameplayIslandManagerValid);
            if (m_gameplayIslandManagerRef.IsValid())
            {
                GameplayIslandManager manager = m_gameplayIslandManagerRef.Get();
                manager.OnIslandDefinitionChanged += OnGameplayIslandChanged;
                OnGameplayIslandChanged(manager.ActiveIslandDefinitions);
            }
            m_gameplayIslandManagerRef.OnSystemStartup += OnGameplayIslandManagerValid;
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().OnCharacterRespawn += OnCharacterRespawn;
            m_characterManagerRef.OnSystemStartup += OnCharacterManagerValid;
            OnMissionCompleted += UpdateStateAfterMissionComplete;
            OnMissionCompleted += SendMissionCompleteAnalytics;
        }

        //060026f5: subscribe before notifying the manager's current list.
        private void OnGameplayIslandManagerValid(GameplayIslandManager gameplayIslandManager)
        {
            gameplayIslandManager.OnIslandDefinitionChanged += OnGameplayIslandChanged;
            OnGameplayIslandChanged(gameplayIslandManager.ActiveIslandDefinitions);
        }

        //060026f6: no duplicate-subscription guard in the original.
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            characterManager.OnCharacterRespawn += OnCharacterRespawn;
        }

        //060026f7: scheduling starts the actual deferred respawn iterator.
        private void OnCharacterRespawn()
        {
            CoroutineUtils.RunCoroutine(ResetMissionsOnRespawn());
        }

        //060026f8/06002720-21/06002724-2a: restart only authored respawn-reload children.
        //The closure is allocated before the reload getter in the original. Wait
        //for full setup, start as a sub-mission, then restore island selection and
        //notify after the child enumerator has been disposed.
        private IEnumerator ResetMissionsOnRespawn()
        {
            if (!m_levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel) || ActiveMissionContext == null)
                yield break;
            bool subMissionsReloaded = false;
            foreach (MissionDefinition subMission in ActiveMissionContext.MissionDefinition.SubMissions)
            {
                MissionState missionState;
                bool reload = subMission.ReloadOnCharacterRespawn;
                subMissionsReloaded |= reload;
                if (!reload) continue;
                if (m_missionStates.TryGetValue(subMission.GetGUID(), out MissionState previous))
                {
                    previous.DestroyMissionTracker(m_missionStates, false);
                    m_missionStates.Remove(subMission.GetGUID());
                }
                missionState = LoadMission(subMission, currentLevel.LevelDefinition);
                SetUpMissionTracker(missionState);
                yield return new WaitUntil(() => missionState.SetUpComplete);
                missionState.Tracker.StartMission(true);
            }
            if (m_gameplayIslandManagerRef.TryGet(out GameplayIslandManager gameplayIslandManager))
            {
                gameplayIslandManager.SanitisePendingGameplayElements();
                gameplayIslandManager.ForceActiveIsland(m_characterManagerRef.Get().GetCurrentCharacterUnsafe().WorldPosition);
            }
            OnMissionsReset(subMissionsReloaded);
        }

        //060026f9: original global-system lookup, rather than the cached ref.
        public IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> GetMissionSaveDataForLevel(GameplayLevelDefinition level)
        {
            return GetMissionSaveDataForLevel(level, ProcessManager.GetSystem<SaveManager>().CurrentSave);
        }

        //060026fa: evaluate the Unity list-null test before allocating the
        //result. Traverse every authored group and immediate child regardless of
        //visibility/unlock state; an existing key faults after its save-data lookup.
        public IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> GetMissionSaveDataForLevel(GameplayLevelDefinition level, SaveDataGame saveDataGame)
        {
            bool missingList = level.MissionList == null;
            var missions = new Dictionary<MissionDefinition, SaveDataLevelMission>();
            if (missingList) return missions;
            SaveDataLevel data = saveDataGame.GetOrCreateLevelData(level.GetGUID());
            foreach (MissionGroup group in level.MissionList.Groups)
                foreach (MissionDefinition definition in group.Definitions)
                {
                    missions.Add(definition, data.GetOrCreateMissionData(definition.GetGUID()));
                    foreach (MissionDefinition subMission in definition.SubMissions)
                        missions.Add(subMission, data.GetOrCreateMissionData(subMission.GetGUID()));
                }
            return missions;
        }

        //060026fb: destruction precedes delegate removal. Remove one original
        //subscription per path; duplicate initialization subscriptions can remain.
        private void OnShutdown(object context = null)
        {
            UnityEngine.Object.Destroy(m_activeTracker);
            OnMissionCompleted -= UpdateStateAfterMissionComplete;
            OnMissionCompleted -= SendMissionCompleteAnalytics;
            if (m_gameplayIslandManagerRef.IsValid())
                m_gameplayIslandManagerRef.Get().OnIslandDefinitionChanged -= OnGameplayIslandChanged;
            m_gameplayIslandManagerRef.OnSystemStartup -= OnGameplayIslandManagerValid;
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().OnCharacterRespawn -= OnCharacterRespawn;
            m_characterManagerRef.OnSystemStartup -= OnCharacterManagerValid;
        }

        //060026fc: publish the level's authored mission list first, then restore
        //ignored-group collectable totals and saved amounts. Select index zero
        //without an empty-list guard. A retry retains a live tracker parent.
        public void LoadMissionsFromLevel(LevelManagerLevel level, bool isReload)
        {
            GameplayLevelDefinition definition = level.LevelDefinition;
            MissionList = definition.MissionList;
            CollectableManager collectableManager = ProcessManager.GetSystem<CollectableManager>();
            ProgressionCollectableLevel progress = ProcessManager.GetSystem<ProgressionManager>().GetLevelProgress(definition);
            if (progress != null)
                foreach ((CollectableType type, ProgressionCollectable collectable) in progress.ProgressGroupsIgnored)
                {
                    collectableManager.RegisterCollectable(type, collectable.Total);
                    collectableManager.ChangeCollectableAmount(type, collectable.Collected,
                        new CollectableChangeMetadata { Source = CollectableSource.MissionReward });
                }
            CurrentGroup = definition.TryGetMissionGroups(out IReadOnlyList<MissionGroup> groups) ? groups[0] : new MissionGroup();
            if (isReload && m_activeTracker != null) return;
            SetUpMissionTrackerParent(level);
        }

        //060026fd: replace the original named tracker parent and attach it to
        //the level's Data GameObject. The original uses SetParent's default mode.
        private void SetUpMissionTrackerParent(LevelManagerLevel level)
        {
            if (m_activeTracker != null) UnityEngine.Object.Destroy(m_activeTracker.gameObject);
            m_activeTracker = new GameObject("MissionTrackerParent");
            m_activeTracker.transform.SetParent(level.Data.gameObject.transform);
        }

        //060026fe: allow creation, resume timers, then schedule the genuine
        //startup iterator. Even the no-active-state branch performs its original
        //storage read, whose result is discarded but whose fault behavior remains.
        public void StartMission()
        {
            if (TryGetActiveMissionState(out MissionState state, true))
            {
                SetTimersPaused(false);
                CoroutineUtils.RunCoroutine(StartMissionTracker(state, false));
            }
            else ProcessManager.GetSystem<App>().Storage.GetValue(AppFSMKeys.IsFastLoading, false, true);
        }

        //060026ff/06002722-23/0600272b-31: await the Unity tracker lifetime test, rather
        //than SetUpComplete. Start it before the current context's attempt and
        //analytics callbacks. Yield recursive child iterators in authored order.
        private IEnumerator StartMissionTracker(MissionState state, bool isSubMission)
        {
            yield return new WaitUntil(() => state.Tracker != null);
            state.Tracker.StartMission(isSubMission);
            ActiveMissionContext.MarkNewAttempt(state);
            ActiveMissionContext.SendMissionStartAnalytics(state);
            foreach (MissionDefinition subMission in state.Definition.SubMissions)
                if (m_missionStates.TryGetValue(subMission.GetGUID(), out MissionState subState))
                    yield return StartMissionTracker(subState, true);
        }

        //06002700: the caller supplies the list; clear it before any context
        //getter. Override lists and exclusive characters bypass lock filtering.
        public void GetCharactersForActiveMission(ref List<CharacterId> characterIds)
        {
            characterIds.Clear();
            if (ActiveMissionContext == null || ActiveMissionContext.MissionDefinition == null)
            {
                GetCharacterIdsByArchetype(CharacterArchetype.None, ref characterIds, null);
                return;
            }
            if (ActiveMissionContext.OverrideAllowedCharacters != null)
            {
                characterIds.AddRange(ActiveMissionContext.OverrideAllowedCharacters);
                return;
            }
            MissionDefinition definition = ActiveMissionContext.MissionDefinition;
            if (definition.ExclusiveToCharacter)
            {
                characterIds.Add(definition.CharacterId);
                return;
            }
            SaveDataCharacterArchetype archetypeData = m_saveManagerRef.Get().CurrentSave.GetOrCreateCharacterArchetypeData(definition.UnlocksCharacterArchetype);
            if (definition.UnlocksCharacterArchetype != CharacterArchetype.None && !archetypeData.Unlocked)
            {
                GetCharacterIdsByArchetype(definition.UnlocksCharacterArchetype, ref characterIds, null);
                return;
            }
            CharacterArchetype archetype = definition.CharacterArchetype;
            Predicate<CharacterArchetype> filter = m_characterManagerRef.Get().IsCharacterArchetypeLocked;
            GetCharacterIdsByArchetype(archetype, ref characterIds, DebugUnlockLevels.AreAllLevelsUnlocked() ? null : filter);
        }

        //06002701: return an empty array for an absent state or an exclusive
        //character. A specific archetype is returned without its lock check.
        public IReadOnlyList<CharacterArchetype> GetCharacterArchetypesForActiveMission()
        {
            if (!TryGetActiveMissionState(out MissionState state)) return Array.Empty<CharacterArchetype>();
            MissionDefinition definition = state.Definition;
            DataManager dataManager = ProcessManager.GetSystem<DataManager>();
            if (definition.ExclusiveToCharacter) return Array.Empty<CharacterArchetype>();
            if (definition.CharacterArchetype != CharacterArchetype.None) return new[] { definition.CharacterArchetype };
            var archetypes = new List<CharacterArchetype>();
            foreach ((CharacterArchetype archetype, CharacterArchetypeDefinition _) in dataManager.CharacterArchetypes)
                if (archetype != CharacterArchetype.None && !m_characterManagerRef.Get().IsCharacterArchetypeLocked(archetype))
                    archetypes.Add(archetype);
            return archetypes;
        }

        //06002702: the lock predicate executes even for a nonmatching archetype.
        //Append genuine dictionary keys through the original AddUnique utility.
        private static void GetCharacterIdsByArchetype(CharacterArchetype archetype, ref List<CharacterId> characterIds, Predicate<CharacterArchetype> filter)
        {
            foreach ((CharacterId id, CharacterDefinition definition) in ProcessManager.GetSystem<DataManager>().Characters)
            {
                CharacterArchetype characterArchetype = definition.Archetype;
                if (filter != null && filter(characterArchetype)) continue;
                if (archetype != CharacterArchetype.None && characterArchetype != archetype) continue;
                characterIds.AddUnique(id);
            }
        }

        //06002703: context and definition are reread between the GUID-aware
        //validity check, override test and selected return path.
        public LevelStartPositionDefinition GetActiveMissionStartPosition()
        {
            if (ActiveMissionContext == null || ActiveMissionContext.MissionDefinition == null) return null;
            return ActiveMissionContext.OverrideStartPosition != null
                ? ActiveMissionContext.OverrideStartPosition
                : ActiveMissionContext.MissionDefinition.CustomStartPosition;
        }

        //06002704: destroy this state's tracker without recursively destroying
        //sub-missions, then reread the supplied definition's GUID for removal.
        public void PurgeMissionState(MissionDefinition missionDef)
        {
            if (!m_missionStates.TryGetValue(missionDef.GetGUID(), out MissionState state)) return;
            state.DestroyMissionTracker(m_missionStates, false);
            m_missionStates.Remove(missionDef.GetGUID());
        }

        //06002705: destroy while enumerating, collecting keys separately. Retry
        //keeps DoNotUnloadOnRetry states. A failure before the second loop retains
        //the dictionary entries and island override just as in the original.
        public void PurgeMissionStates(bool isReload = false)
        {
            var toRemove = new List<string>();
            foreach ((string guid, MissionState state) in m_missionStates)
            {
                if (isReload && state.Definition.DoNotUnloadOnRetry) continue;
                state.DestroyMissionTracker(m_missionStates, false);
                toRemove.Add(guid);
            }
            foreach (string guid in toRemove) m_missionStates.Remove(guid);
            m_islandOverrideMissionStateGUID = string.Empty;
        }

        //06002706: validity of a context's GUID-bearing definition only.
        public bool HasActiveMission() => ActiveMissionContext != null && ActiveMissionContext.MissionDefinition != null;

        //06002707: IgnoreSavedProgress is read once before dictionary iteration.
        //Only states that have not completed setup and have no persistent definition
        //receive replacement data; save-disabled challenge data stays transient.
        public void RefreshMissionSaveStates()
        {
            bool useSavedProgress = !(ActiveMissionContext?.IgnoreSavedProgress ?? false);
            foreach ((string guid, MissionState state) in m_missionStates)
            {
                if (state.SetUpComplete || state.Definition.IsPersistentTracker) continue;
                SaveDataLevelMission data;
                if (useSavedProgress)
                    data = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(state.LevelGUID).GetOrCreateMissionData(guid);
                else
                {
                    data = new SaveDataLevelMission(guid);
                    data.DisableSaving();
                }
                state.ReplaceSaveData(data);
            }
        }

        //06002708: managed-null optional save data is the only fallback test;
        //never substitute data merely because an existing level has no mission.
        public SaveDataLevelMission GetSaveDataForMission(string missionGUID, string levelGUID, SaveDataLevel saveDataLevel = null)
        {
            if (saveDataLevel == null) saveDataLevel = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelGUID);
            return saveDataLevel.GetOrCreateMissionData(missionGUID);
        }

        //06002709: iterate the live dictionary and use the definition's authored
        //RewardType followed by its effective RewardValue. Addition wraps.
        public int GetPendingRewards(CollectableType collectableType)
        {
            int rewards = 0;
            foreach ((string guid, MissionState state) in m_missionStates)
                if (state.HasPendingReward && state.Definition.RewardType == collectableType)
                    rewards = unchecked(rewards + state.Definition.RewardValue);
            return rewards;
        }

        //0600270a: clear every stored flag, without saving or filtering nulls.
        public void ClearPendingRewards()
        {
            foreach ((string guid, MissionState state) in m_missionStates) state.ClearPendingReward();
        }

        //0600270b: the notification's argument is unused. Resolve the current
        //active state, retain the unguarded state access, then update waypoints.
        private void UpdateStateAfterMissionComplete(MissionState _)
        {
            if (TryGetActiveMissionState(out MissionState state) && state.SetUpComplete)
                m_waypointManager.SetOverrideTargets(state.Tracker.WaypointTargets);
        }

        //0600270c: append newly eligible authored groups to the graph's
        //existing unlock list. Seen flags, widgets and requirements are checked
        //in that order; this method neither marks groups seen nor deduplicates.
        public void UpdateUnlocks()
        {
            List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>().Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
            foreach (GameplayLevelDefinition level in m_levelManager.GameLevels.GetLevels())
            {
                MissionList missionList = level.MissionList;
                if (missionList == null || !level.MeetsAllRequirements()) continue;
                SaveDataLevel data = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(level.GetGUID());
                foreach (MissionGroup group in missionList.Groups)
                {
                    if (data.MissionGroupsUnlockSeen.Contains(group.GUID)) continue;
                    if (group.ProgressionUnlockWidget == null) continue;
                    if (!group.MeetsAllRequirements(level)) continue;
                    unlocks.Add(new MetaGameUnlockMissionGroup(level, group));
                }
            }
        }

        //0600270d: HasActiveMission reads the context before the later callback;
        //the callback intentionally dispatches through the current property.
        private void SendMissionCompleteAnalytics(MissionState _)
        {
            if (HasActiveMission()) ActiveMissionContext.SendMissionCompleteAnalytics(_);
        }

        //0600270e: valid lookup only, no state-null fallback.
        public void IncrementActiveMissionDeathCount()
        {
            if (TryGetActiveMissionState(out MissionState state)) state.IncrementDeathCount();
        }

        //0600270f: remove the previous island notification and clear its GUID
        //before enumerating the new list. Choose the first incomplete ready state,
        //set waypoints before its GUID and event, and leave remaining islands unread.
        private void OnGameplayIslandChanged(IReadOnlyList<GameplayIslandDefinition> islandDefinitions)
        {
            if (TryGetIslandOverrideMissionState(out MissionState previous))
                previous.OnUpdated -= OnIslandMissionUpdated;
            m_islandOverrideMissionStateGUID = string.Empty;
            foreach (GameplayIslandDefinition island in islandDefinitions)
                if (TryGetMissionForIsland(island, out MissionState state) && state.SetUpComplete)
                {
                    m_waypointManager.SetOverrideTargets(state.Tracker.WaypointTargets);
                    m_islandOverrideMissionStateGUID = state.Definition.GetGUID();
                    state.OnUpdated += OnIslandMissionUpdated;
                    OnMissionIslandActive(state);
                    return;
                }
            ClearIslandMissionOverride();
        }

        //06002710: a completed island state removes this handler before
        //restoring the primary mission's waypoints and notification.
        private void OnIslandMissionUpdated()
        {
            if (!TryGetIslandOverrideMissionState(out MissionState state) || !state.IsInstanceCompleted) return;
            state.OnUpdated -= OnIslandMissionUpdated;
            ClearIslandMissionOverride();
        }

        //06002711: do not clear the GUID when the primary state is unready.
        //Restore waypoints before invoking the original nonnull event delegate.
        private void ClearIslandMissionOverride()
        {
            if (!TryGetActiveMissionState(out MissionState state) || !state.SetUpComplete) return;
            m_islandOverrideMissionStateGUID = string.Empty;
            m_waypointManager.SetOverrideTargets(state.Tracker.WaypointTargets);
            OnMissionIslandActive(state);
        }

        //06002712: first dictionary state whose authored island list contains
        //the definition and whose current instance is incomplete. Assign null
        //only on failure, retaining the caller's prior out slot during faults.
        public bool TryGetMissionForIsland(GameplayIslandDefinition islandDefinition, out MissionState missionState)
        {
            if (islandDefinition != null)
                foreach ((string guid, MissionState state) in m_missionStates)
                    if (state.Definition.Islands.Contains(islandDefinition) && !state.IsInstanceCompleted)
                    {
                        missionState = state;
                        return true;
                    }
            missionState = null;
            return false;
        }

        //06002713: test level requirements first, then include every authored
        //group definition. Only parent mission save data is considered here.
        public bool AnyMissionCompleted(GameplayLevelDefinition levelDefinition)
        {
            if (!levelDefinition.MeetsAllRequirements() || !levelDefinition.TryGetMissionGroups(out IReadOnlyList<MissionGroup> groups))
                return false;
            foreach (MissionGroup group in groups)
                foreach (MissionDefinition definition in group.Definitions)
                {
                    string missionGUID = definition.GetGUID();
                    string levelGUID = levelDefinition.GetGUID();
                    if (m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelGUID).GetOrCreateMissionData(missionGUID).Complete)
                        return true;
                }
            return false;
        }

        //06002714: clear the old cache first. Every tracked definition is
        //copied from the supplied level's save data regardless of its LevelGUID.
        public void CachePreLevelMissionSaveData(string levelGUID)
        {
            m_preLevelCachedMissionData.Clear();
            SaveDataLevel levelData = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelGUID);
            foreach ((string guid, MissionState state) in m_missionStates)
            {
                string missionGUID = state.Definition.GetGUID();
                m_preLevelCachedMissionData.Add(missionGUID, levelData.GetOrCreateMissionData(missionGUID).CreateCopy());
            }
        }

        //06002715: notify only changes; store the field before reading callback.
        public void SetTimersPaused(bool paused)
        {
            if (m_timersPaused == paused) return;
            m_timersPaused = paused;
            OnMissionTimersPaused?.Invoke(paused);
        }

        //06002716: the original uses the context's replay flag, without checking
        //save data, trackers or the mission definition.
        public bool ActiveMissionPreviouslyCompleted() => ActiveMissionContext != null && ActiveMissionContext.IsReplay;
        //06002717: materialize the original typed mission iterator, then require
        //every result to be saved complete. An empty collection is hidden.
        public UIShowType GetMissionTypeShowType(GameplayLevelDefinition levelDefinition, MissionType missionType)
        {
            var missions = new List<MissionDefinition>(levelDefinition.MissionList.GetMissionsOfType(missionType));
            if (missions.Count == 0) return UIShowType.Hidden;
            foreach (MissionDefinition definition in missions)
            {
                string missionGUID = definition.GetGUID();
                string levelGUID = levelDefinition.GetGUID();
                if (!m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelGUID).GetOrCreateMissionData(missionGUID).Complete)
                    return UIShowType.Locked;
            }
            return UIShowType.Unlocked;
        }

        //06002718: both the current ranks and completed override ranks must
        //exist. No qualifying missions means hidden. A completed mission whose
        //time has no matching rank passes this original check; only a valid rank
        //different from the definition's best rank makes the result locked.
        public UIShowType GetRankUIShowType(GameplayLevelDefinition levelDefinition)
        {
            if (levelDefinition.MissionList == null) return UIShowType.Hidden;
            bool hasRanks = false;
            foreach (MissionDefinition definition in levelDefinition.MissionList.GetMissions())
            {
                if (definition.Ranks == null || definition.CompletedMissionOverrides == null || definition.CompletedMissionOverrides.Ranks == null)
                    continue;
                string missionGUID = definition.GetGUID();
                string levelGUID = levelDefinition.GetGUID();
                SaveDataLevelMission data = m_saveManagerRef.Get().CurrentSave.GetOrCreateLevelData(levelGUID).GetOrCreateMissionData(missionGUID);
                if (!data.Complete) return UIShowType.Locked;
                bool ranked = definition.TryGetRankForTimeSeconds(data.BestTimeSeconds, out MissionRank rank);
                hasRanks = true;
                if (ranked && definition.GetBestRank().RankType != rank.RankType) return UIShowType.Locked;
            }
            return hasRanks ? UIShowType.Unlocked : UIShowType.Hidden;
        }

    }
}
