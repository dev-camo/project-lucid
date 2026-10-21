using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008e8; bodies translated from the 1.10.1 x86-64 binary.
    // The mission list of the selected level: one toggle per mission, with the selected mission's results,
    // leaderboard and zone theme. The closure of InstantiateMissionToggle is <>c__DisplayClass40_0 (30 fields and
    // 10 methods precede it) and captures this, mission, complete and missionWidget.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerMissionSelect : UIContainer
    {
        [SerializeField] private UIToggleGroup m_missionsRoot;
        [SerializeField] private UIWidgetToggleMission m_missionPrefab;
        [SerializeField] private GameObject m_ghostedMissionPrefab;
        [SerializeField] private UIContainerMissionResults m_resultsContainer;
        [SerializeField] private UIContainerMissionResults m_resultsSecondaryContainer;
        [SerializeField] private UIContainerMissionResults m_resultsAltContainer;
        [SerializeField] private UIContainerActiveMission m_activeMissionContainer;
        [SerializeField] private UIContainerLevelDefinition m_levelDefinition;
        [SerializeField] private UIContainerOrbCollectableCounter m_collectableCounter;
        [SerializeField] private MissionDefinition[] m_expertMissionDefinitions;
        public readonly Bindable<Strings> LevelTitle = new Bindable<Strings>();
        public readonly Bindable<Strings> LevelSubtitle = new Bindable<Strings>();
        public readonly Bindable<bool> HasMissions = new Bindable<bool>();
        public readonly Bindable<bool> MissionSelected = new Bindable<bool>();
        public readonly Bindable<bool> SelectedMissionUnlocked = new Bindable<bool>();
        public readonly Bindable<Sprite> ZoneIconAsset = new Bindable<Sprite>();
        public readonly Bindable<Color> ZoneBackgroundStartColour = new Bindable<Color>();
        public readonly Bindable<Color> ZoneBackgroundEndColour = new Bindable<Color>();
        public readonly Bindable<bool> ShowLeaderboards = new Bindable<bool>();
        public readonly Bindable<LeaderboardIdentifier> LeaderboardIdentifier = new Bindable<LeaderboardIdentifier>();
        public readonly Bindable<bool> UseAltLayout = new Bindable<bool>();
        public readonly Bindable<bool> IsExpertLevel = new Bindable<bool>();
        private readonly SystemRef<UINavigationManager> m_navigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>(null, true);
        private readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>(null, true);
        private App m_app;
        private SaveManager m_saveManager;
        private MetaGameState m_metaGameState;
        private MissionManager m_missionManager;
        private UIWidgetToggleMission m_activeWidget;
        private ManagedAddressableAsset<Sprite> m_zoneAccent;

        // 06003367 (0x641660)
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>(null, true);
            m_metaGameState = m_app.Storage.GetValue<MetaGameState>(AppFSMKeys.MetaGameState, null, true);
            m_levelDefinition.Setup(new UIContainerLevelViewParameters(m_metaGameState.LevelDefinition));
            RefreshZoneTheme();
            ProcessManager.GetSystemRef<SaveManager>(null, true).InvokeOnValid(OnSaveManagerValid);
        }

        // 06003368 (0x641A40)
        private void OnDestroy()
        {
            if (m_zoneAccent != null)
                m_zoneAccent.Unload();
            if (m_uiManagerRef.TryGet(out UIManager uiManager))
                uiManager.OnContainerClosed -= OnContainerClosed;
        }

        // 06003369 (0x641B20)
        private void OnSaveManagerValid(SaveManager saveManager)
        {
            m_saveManager = saveManager;
            ProcessManager.GetSystemRef<MissionManager>(null, true).InvokeOnValid(OnMissionManagerValid);
        }

        // 0600336a (0x641C10)
        private void OnMissionManagerValid(MissionManager missionManager)
        {
            m_missionManager = missionManager;
            m_uiManagerRef.InvokeOnValid(OnUIManagerValid);
        }

        // 0600336b (0x641CC0): no mission stays active while selecting, then the level's missions are listed.
        private void OnUIManagerValid(UIManager uiManager)
        {
            uiManager.OnContainerClosed += OnContainerClosed;
            m_missionManager.SetActiveMissionContext(null);
            m_missionManager.RefreshMissionSaveStates();
            IReadOnlyList<MissionGroup> missionGroups = FetchMissions();
            HasMissions.Value = missionGroups.Count > 0;
            PopulateUI(missionGroups);
        }

        // 0600336c (0x642B30): the selection returns to the active mission's toggle when a container closes.
        private void OnContainerClosed(UIContainerIdentifier _)
        {
            if (m_activeWidget == null)
                return;
            TryNavigateToWidget(m_activeWidget);
        }

        // 0600336d (0x6418D0)
        private void RefreshZoneTheme(MissionDefinition missionDefinition = null)
        {
            UIContainerUtilities.SetBackgroundGradientBinders(ZoneBackgroundStartColour, ZoneBackgroundEndColour, missionDefinition);
            LevelTitle.Value = m_metaGameState.LevelDefinition.DisplayName;
            LevelSubtitle.Value = m_metaGameState.LevelDefinition.ActName;
            if (m_zoneAccent != null)
                m_zoneAccent.Unload();
            m_zoneAccent = UIContainerUtilities.GetZoneAccent(missionDefinition, null, null);
            ZoneIconAsset.Value = m_zoneAccent.Load();
        }

        // 0600336e (0x641E00): every mission of the level is loaded before its groups are listed.
        private IReadOnlyList<MissionGroup> FetchMissions()
        {
            GameplayLevelDefinition levelDefinition = m_metaGameState.LevelDefinition;
            if (levelDefinition.MissionList == null)
                return new List<MissionGroup>();
            foreach (MissionDefinition mission in levelDefinition.MissionList.GetMissions(false))
                m_missionManager.LoadMission(mission, m_metaGameState.LevelDefinition);
            return levelDefinition.MissionList.Groups;
        }

        // 0600336f (0x642180): a toggle is made for every mission of a group whose requirements are met (or a ghost
        // for a group that shows as ghosted). The first incomplete, unlocked mission becomes the selection unless
        // the meta game state names a mission, whose toggle is then selected.
        private void PopulateUI(IReadOnlyList<MissionGroup> missionGroups)
        {
            GameplayLevelDefinition levelDefinition = m_metaGameState.LevelDefinition;
            SaveDataLevel saveDataLevel = m_saveManager.CurrentSave.GetOrCreateLevelData(levelDefinition.GetGUID());
            Dictionary<string, UIWidgetToggleMission> missionWidgets = new Dictionary<string, UIWidgetToggleMission>();
            string selectedMissionGuid = string.Empty;
            int index = 0;
            foreach (MissionGroup missionGroup in missionGroups)
            {
                if (missionGroup.HasAlwaysHiddenRequirement())
                    continue;
                bool requirementsMet = missionGroup.MeetsAllRequirements(levelDefinition);
                foreach (MissionDefinition groupMission in missionGroup.Definitions)
                {
                    string missionGuid = groupMission.GetGUID();
                    if (!m_missionManager.MissionStates.TryGetValue(missionGuid, out MissionState missionState))
                        continue;
                    MissionDefinition mission = missionState.Definition;
                    if (mission.IsPersistentTracker)
                        continue;
                    if (!requirementsMet)
                    {
                        if (missionGroup.ShowAsGhostedWhenRequirementsNotMet)
                            Instantiate(m_ghostedMissionPrefab, m_missionsRoot.transform);
                        continue;
                    }
                    index++;
                    SaveDataLevelMission missionSaveData = saveDataLevel.GetOrCreateMissionData(mission.GetGUID());
                    bool unlockSeen = missionSaveData.LevelSelectUnlockSeen;
                    MissionIsNewState isNewState = GetIsNewMission(missionGroups, missionSaveData);
                    if (missionSaveData.Complete)
                    {
                        missionWidgets[missionGuid] = InstantiateMissionToggle(index, mission, true, false, unlockSeen, isNewState, missionSaveData.BestTimeSeconds);
                        continue;
                    }
                    bool archetypeRequirementMet = mission.ArchetypeRequirementMet(m_saveManager.CurrentSave);
                    if (archetypeRequirementMet && selectedMissionGuid == string.Empty)
                    {
                        SetMissionActive(mission, false);
                        selectedMissionGuid = missionGuid;
                    }
                    // The widget still receives the unlock state from before it was marked as seen.
                    if (!unlockSeen)
                    {
                        missionSaveData.LevelSelectUnlockSeen = true;
                        m_saveManager.RequestSave();
                    }
                    missionWidgets[missionGuid] = InstantiateMissionToggle(index, mission, false, !archetypeRequirementMet, unlockSeen, isNewState, missionSaveData.BestTimeSeconds);
                }
            }
            m_collectableCounter.UpdateValues(levelDefinition);
            if (selectedMissionGuid == string.Empty)
            {
                selectedMissionGuid = m_metaGameState.MissionDefinition != null
                    ? m_metaGameState.MissionDefinition.GetGUID()
                    : string.Empty;
            }
            if (selectedMissionGuid != string.Empty && missionWidgets.TryGetValue(selectedMissionGuid, out UIWidgetToggleMission selectedWidget))
                selectedWidget.SetSelected(true);
        }

        // 06003370 (0x642C80): the first group holding the mission decides; a mission in no group is not new.
        private static MissionIsNewState GetIsNewMission(IEnumerable<MissionGroup> missionGroups, SaveDataLevelMission missionSaveData)
        {
            foreach (MissionGroup missionGroup in missionGroups)
            {
                if (missionGroup.HasMission(missionSaveData.GUID))
                    return missionSaveData.IsNewState;
            }
            return MissionIsNewState.None;
        }

        // 06003371 (0x642F50): the sub-reward states come from the sub-missions by type. Red star rings and blue
        // coins follow the last sub-mission of their type, and the jukebox stays locked once one is locked.
        private UIWidgetToggleMission InstantiateMissionToggle(int index, MissionDefinition mission, bool complete, bool locked, bool unlockSeen, MissionIsNewState isNewState, float bestTimeSeconds)
        {
            UIWidgetToggleMission missionWidget = Instantiate(m_missionPrefab, m_missionsRoot.transform);
            string label = index.ToString();
            bool isTimeTrial = mission.IsTimeTrial();
            CollectableType rewardType = mission.RewardType;
            bool subRewardsAnchorRight = mission.UseAltUILayout;
            UIShowType jukeboxState = UIShowType.Hidden;
            UIShowType blueCoinsState = UIShowType.Hidden;
            UIShowType redStarRingsState = UIShowType.Hidden;
            foreach (MissionDefinition subMission in mission.SubMissions)
            {
                switch (subMission.Type)
                {
                    case MissionType.RedStarRings:
                        redStarRingsState = GetUIShowTypeForMission(subMission);
                        break;
                    case MissionType.BlueCoins:
                        blueCoinsState = GetUIShowTypeForMission(subMission);
                        break;
                    case MissionType.Jukebox:
                        UIShowType subMissionState = GetUIShowTypeForMission(subMission);
                        if (jukeboxState != UIShowType.Locked)
                            jukeboxState = subMissionState;
                        break;
                }
            }
            missionWidget.Setup(new UIWidgetToggleMissionParameters(
                complete, locked, unlockSeen, subRewardsAnchorRight, label, rewardType,
                toggleSelected => ToggleMission(mission, toggleSelected, complete, missionWidget),
                isNewState, redStarRingsState, blueCoinsState, jukeboxState));
            missionWidget.SetToggleGroup(m_missionsRoot);
            if (bestTimeSeconds > 0f || isTimeTrial)
                UpdateRankVisibility(mission, missionWidget, bestTimeSeconds);
            return missionWidget;
        }

        // 06003372 (0x643690)
        private static void UpdateRankVisibility(MissionDefinition missionDefinition, UIWidgetToggleMission missionWidget, float bestTimeSeconds)
        {
            ManagedAddressableAsset<Sprite> foregroundIcon;
            if (missionDefinition.TryGetRankForTimeSeconds(bestTimeSeconds, out MissionRank missionRank))
            {
                missionWidget.SetRankIconVisibility(true);
                missionWidget.SetRankIconBase(missionRank.GetBaseAddressable());
                missionWidget.SetRankIconBackground(missionRank.GetBackgroundAddressable());
                foregroundIcon = missionRank.GetForegroundAddressable();
            }
            else
            {
                foregroundIcon = null;
                missionWidget.SetRankIconVisibility(false);
                missionWidget.SetRankIconBase(null);
                missionWidget.SetRankIconBackground(null);
            }
            missionWidget.SetRankIconForeground(foregroundIcon);
        }

        // 06003373 (0x643750): selecting a mission makes it the active one and shows its results (the alternate
        // layout uses the alternate container when there is one). Deselecting clears it.
        private void ToggleMission(MissionDefinition mission, bool selected, bool complete, UIWidgetToggleMission widgetToggleMission)
        {
            MissionState activeMissionState = null;
            IsExpertLevel.Value = m_expertMissionDefinitions.Contains(mission);
            UseAltLayout.Value = mission.UseAltUILayout;
            m_activeWidget = widgetToggleMission;
            if (selected)
            {
                SetMissionActive(mission, complete);
                widgetToggleMission.SetIsNew(false);
                TryNavigateToWidget(widgetToggleMission);
            }
            else
            {
                m_missionManager.ClearMissionIfActive(mission);
            }
            UIContainerMissionResults resultsContainer = m_resultsContainer;
            if (mission.UseAltUILayout && m_resultsAltContainer != null)
                resultsContainer = m_resultsAltContainer;
            resultsContainer.Setup(new UIContainerMissionResultsParameters(mission, m_metaGameState.LevelDefinition));
            resultsContainer.gameObject.SetActive(resultsContainer.CanShow());
            if (m_resultsSecondaryContainer != null)
            {
                UIContainerMissionResults secondaryResultsContainer = m_resultsSecondaryContainer;
                secondaryResultsContainer.Setup(new UIContainerMissionResultsParameters(mission, m_metaGameState.LevelDefinition));
                m_resultsSecondaryContainer.gameObject.SetActive(m_resultsSecondaryContainer.CanShow());
            }
            m_missionManager.TryGetActiveMissionState(out activeMissionState);
            m_activeMissionContainer.Refresh(activeMissionState);
            bool hasActiveMission = m_missionManager.HasActiveMission();
            MissionSelected.Value = hasActiveMission;
            if (hasActiveMission)
                SelectedMissionUnlocked.Value = m_missionManager.ActiveMissionContext.MissionDefinition.ArchetypeRequirementMet(m_saveManager.CurrentSave);
            else
                SelectedMissionUnlocked.Value = false;
            SetupLeaderboards(mission);
            RefreshZoneTheme(mission);
        }

        // 06003374 (0x6433E0): the mission becomes part of the meta game state and the active context, and is marked seen.
        private void SetMissionActive(MissionDefinition missionDefinition, bool complete)
        {
            m_metaGameState = new MetaGameState(m_metaGameState.LevelDefinition, missionDefinition);
            m_app.Storage.SetValue(AppFSMKeys.MetaGameState, m_metaGameState);
            m_missionManager.SetActiveMissionContext(new MissionContextRegular(missionDefinition, m_metaGameState.LevelDefinition, complete));
            m_levelDefinition.SetMissionDefinition(missionDefinition);
            m_missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), m_metaGameState.LevelDefinition.GetGUID(), null).IsNewState = MissionIsNewState.Seen;
            m_saveManager.RequestSave();
        }

        // 06003375 (0x642C00)
        private void TryNavigateToWidget(UIWidgetToggleMission widgetToggleMission)
        {
            if (m_navigationManagerRef.IsNull())
                return;
            m_navigationManagerRef.Get().Select(widgetToggleMission.WidgetNavigation);
        }

        // 06003376 (0x643650)
        private UIShowType GetUIShowTypeForMission(MissionDefinition missionDefinition)
        {
            return m_missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), m_metaGameState.LevelDefinition.GetGUID(), null).Complete
                ? UIShowType.Unlocked
                : UIShowType.Locked;
        }

        // 06003377 (0x643BF0)
        private void SetupLeaderboards(MissionDefinition mission)
        {
            LeaderboardIdentifier leaderboardIdentifier = default(LeaderboardIdentifier);
            if (mission.TryGetLeaderboardIdentifier(out leaderboardIdentifier))
            {
                ShowLeaderboards.Value = true;
                LeaderboardIdentifier.Value = leaderboardIdentifier;
            }
            else
            {
                ShowLeaderboards.Value = false;
            }
        }

        // 06003378
        public UIContainerMissionSelect() { }
    }
}
