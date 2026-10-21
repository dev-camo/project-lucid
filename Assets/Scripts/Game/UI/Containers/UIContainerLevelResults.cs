using Hardlight;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008c7; bodies translated from the 1.10.1 x86-64 binary.
    // The end-of-level results screen: titles, ranks, mission results, the active mission or challenge summary and the leaderboard.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerLevelResults : UIContainer
    {
        [SerializeField] private UIModernEvent m_exitEvent;
        [SerializeField] private UIContainerMissionRanks m_ranksContainer;
        [SerializeField] private UIContainerMissionResults m_resultsContainer;
        [SerializeField] private UIContainerMissionResults m_resultsSecondaryContainer;
        [SerializeField] private UIContainerIdentifier m_defaultActiveMissionIdentifier;
        [SerializeField] private Transform m_activeMissionRoot;
        [SerializeField] private UIContainerIdentifier m_challengeResultsContainerIdentifier;
        [SerializeField] private Transform m_challengeResultsRoot;
        [SerializeField] private UIContainerOrbCollectableCounter m_collectableCounter;
        public readonly Bindable<Strings> LevelTitle = new Bindable<Strings>();
        public readonly Bindable<Strings> LevelSubtitle = new Bindable<Strings>();
        public readonly Bindable<bool> CanReplayMission = new Bindable<bool>();
        public readonly Bindable<bool> ShowFPSWarningMessage = new Bindable<bool>();
        public readonly Bindable<Sprite> ZoneIconAsset = new Bindable<Sprite>();
        public readonly Bindable<bool> IsChallenge = new Bindable<bool>();
        public readonly Bindable<bool> TimeTrialUnlocked = new Bindable<bool>();
        public readonly Bindable<string> TimeTrialMissionName = new Bindable<string>();
        public readonly Bindable<bool> ShowLeaderboards = new Bindable<bool>();
        public readonly Bindable<LeaderboardIdentifier> LeaderboardIdentifier = new Bindable<LeaderboardIdentifier>();
        public readonly Bindable<bool> ShowMoreGamesButton = new Bindable<bool>();
        public readonly Bindable<bool> ExtraSpaceAboveXPReward = new Bindable<bool>();
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private UIContainerChallengeResults m_challengeResultsContainer;
        private ManagedAddressableAsset<Sprite> m_zoneAccent;
        private bool m_isExiting;

        // 0x060032ae (0x636A30): with an active mission, fills the level titles, hands the mission to the ranks and results
        // containers, builds the active-mission or challenge summary and, for a mission with a scene override, records the
        // meta-game state.
        private void OnEnable()
        {
            ProcessManager.GetSystemRef<VisualQualityManager_SDT>(null, true).InvokeOnValid(OnQualityManagerValid);
            MissionManager missionManager = m_missionManagerRef.Get();
            if (!missionManager.TryGetActiveMissionState(out MissionState activeMissionState) || !missionManager.HasActiveMission())
                return;
            MissionDefinition missionDefinition = activeMissionState.Definition;
            GameplayLevelDefinition levelDefinition = ProcessManager.GetSystem<DataManager>(null, true).MissionDefinitionLevelLookup[missionDefinition];
            LevelTitle.Value = levelDefinition.DisplayName;
            LevelSubtitle.Value = levelDefinition.ActName;
            UpdateTimeTrialBindings(missionManager, levelDefinition);
            ChallengeDefinition challengeDefinition = null;
            if (missionManager.ActiveMissionContext is MissionContextChallenge challengeContext)
                challengeDefinition = challengeContext.ChallengeDefinition;
            m_ranksContainer.Setup(new UIContainerRanksParameters(missionDefinition, challengeDefinition));
            m_resultsContainer.Setup(new UIContainerMissionResultsParameters(missionDefinition, levelDefinition));
            if (m_resultsSecondaryContainer != null)
            {
                m_resultsSecondaryContainer.Setup(new UIContainerMissionResultsParameters(missionDefinition, levelDefinition));
                m_resultsSecondaryContainer.gameObject.SetActive(m_resultsSecondaryContainer.CanShow());
            }
            m_resultsContainer.gameObject.SetActive(m_resultsContainer.CanShow());
            bool isChallenge = missionManager.ActiveMissionContext is MissionContextChallenge;
            SetupActiveMissionUI(activeMissionState, isChallenge);
            SetupZone(levelDefinition, missionDefinition);
            SetupChallenge(missionManager, activeMissionState.Tracker);
            SetupLeaderboards(missionDefinition);
            if (QualitySettings.GetQualityLevel() == 0)
                m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_collectableCounter.UpdateValues(levelDefinition);
            if (missionDefinition.SceneOverride == null)
                return;
            ProcessManager.GetSystem<App>(null, true).Storage.SetValue(AppFSMKeys.MetaGameState, new MetaGameState(levelDefinition, missionDefinition));
        }

        // 0x060032af (0x6370A0): for a regular mission with a time trial whose unlock is unseen, a reach-goal mission announces
        // the time trial and the time-trial mission itself only clears the flag. Both record the unlock as seen and request a save.
        private void UpdateTimeTrialBindings(MissionManager missionManager, GameplayLevelDefinition levelDefinition)
        {
            TimeTrialUnlocked.Value = false;
            if (!(missionManager.ActiveMissionContext is MissionContextRegular regularContext))
                return;
            MissionDefinition missionDefinition = regularContext.MissionDefinition;
            if (!missionDefinition.HasTimeTrial())
                return;
            SaveDataLevelMission saveData = missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), levelDefinition.GetGUID());
            if (saveData.TimeTrialUnlockSeen)
                return;
            if (missionDefinition.IsTimeTrial())
            {
                TimeTrialUnlocked.Value = false;
            }
            else
            {
                if (!missionDefinition.IsReachGoalTracker)
                    return;
                TimeTrialUnlocked.Value = true;
                TimeTrialMissionName.Value = regularContext.OverrideDescription;
            }
            saveData.TimeTrialUnlockSeen = true;
            saveData.RequestSave();
        }

        // 0x060032b0 (0x637260): an unfinished mission, a finished time trial and every challenge can be replayed.
        private void SetupActiveMissionUI(MissionState activeMissionState, bool isChallenge)
        {
            CanReplayMission.Value = isChallenge | (!activeMissionState.Complete || activeMissionState.Definition.IsTimeTrial());
            UIContainerIdentifier activeMissionIdentifier = GetActiveMissionIdentifier(activeMissionState);
            if (!(activeMissionIdentifier.GetOrCreateContainer() is UIContainerActiveMission activeMissionContainer))
            {
                activeMissionIdentifier.Close();
                return;
            }
            Object.Instantiate(activeMissionContainer, m_activeMissionRoot).SetRankAudioActive();
        }

        // 0x060032b1 (0x637380)
        private void SetupZone(GameplayLevelDefinition levelDefinition, MissionDefinition missionDefinition)
        {
            if (!m_levelManagerRef.Get().GameLevels.TryGetZoneForLevel(levelDefinition, out GameplayLevelCategory zone))
                return;
            if (m_zoneAccent != null)
                m_zoneAccent.Unload();
            m_zoneAccent = UIContainerUtilities.GetZoneAccent(missionDefinition, zone, levelDefinition);
            ZoneIconAsset.Value = m_zoneAccent.Load();
        }

        // 0x060032b2 (0x6374C0)
        private void SetupChallenge(MissionManager missionManager, MissionTracker missionTracker)
        {
            ExtraSpaceAboveXPReward.Value = false;
            if (!(missionManager.ActiveMissionContext is MissionContextChallenge challengeContext))
            {
                IsChallenge.Value = false;
                return;
            }
            IsChallenge.Value = true;
            ShowMoreGamesButton.Value = true;
            if (!(m_challengeResultsContainerIdentifier.GetOrCreateContainer() is UIContainerChallengeResults challengeResultsPrefab))
            {
                m_challengeResultsContainerIdentifier.Close();
                return;
            }
            m_challengeResultsContainer = Object.Instantiate(challengeResultsPrefab, m_challengeResultsRoot);
            var parameters = new UIContainerChallengeResults.UIContainerChallengeResultsParameters
            {
                ChallengeDefinition = challengeContext.ChallengeDefinition,
                PreviousBestTimeSeconds = challengeContext.BestTimeSeconds,
                IsTimed = missionTracker.IsTimed(),
                TimeSeconds = challengeContext.NewTime,
                IsScored = missionTracker.IsScored(),
                Score = challengeContext.NewScore,
                ChallengeIndex = challengeContext.ChallengeIndex,
            };
            m_challengeResultsContainer.Setup(parameters);
            ExtraSpaceAboveXPReward.Value = m_challengeResultsContainer.IsNewRecord.Value;
        }

        // 0x060032b3 (0x637740)
        private void SetupLeaderboards(MissionDefinition mission)
        {
            bool hasLeaderboard = mission.TryGetLeaderboardIdentifier(out var leaderboardIdentifier);
            ShowLeaderboards.Value = hasLeaderboard;
            if (hasLeaderboard)
                LeaderboardIdentifier.Value = leaderboardIdentifier;
        }

        // 0x060032b4 (0x6377B0)
        private UIContainerIdentifier GetActiveMissionIdentifier(MissionState activeMissionState)
        {
            UIContainerIdentifier identifier = activeMissionState.Tracker.ResultsActiveMissionOverrideIdentifier;
            if (identifier != null)
                return identifier;
            return m_defaultActiveMissionIdentifier;
        }

        // 0x060032b5 (0x6377E0)
        private void OnQualityManagerValid(VisualQualityManager_SDT qualityManager)
        {
            ShowFPSWarningMessage.Value = qualityManager.LevelDippedFrames;
        }

        // 0x060032b6 (0x637820)
        public void Action_Exit()
        {
            Exit();
        }

        // 0x060032b7 (0x637A40)
        public void Action_Restart()
        {
            Restart();
        }

        // 0x060032b8 (0x637830): once only; a live character stops collecting orbs before the exit event is published.
        private void Exit()
        {
            if (m_isExiting)
                return;
            m_isExiting = true;
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
                character.Storage.SetValue(ActorFSMKeys.IsCollectingOrb, false);
            PublishUIMessage(m_exitEvent);
        }

        // 0x060032b9 (0x637AD0)
        private void Restart()
        {
            if (m_isExiting)
                return;
            m_isExiting = true;
            ProcessManager.GetSystem<LevelManager>(null, true).Restart();
        }

        // 0x060032ba (0x637B60)
        private static void PublishUIMessage(UIModernEvent uiEvent)
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(uiEvent);
        }

        // 0x060032bb (0x637BE0)
        private void OnDestroy()
        {
            if (m_zoneAccent != null)
                m_zoneAccent.Unload();
        }

        // 0x060032bc
        public UIContainerLevelResults() { }
    }
}
