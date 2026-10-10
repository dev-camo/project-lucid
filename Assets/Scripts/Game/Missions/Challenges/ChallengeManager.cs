using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Hardlight.Analytics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020006ed: complete 77-method owner and four enums.
    // Natural lambdas retain their original bodies; generated ordinal identity
    // requires complete original compiler context and is not asserted here.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ChallengeManager : TimeScaledComponent_SDT, ISystem, ISaveGameListener
    {
        [SerializeField] private ChallengeCycleDefinition m_challengeCycleDefinition;
        [SerializeField, Range(1f, 10f)] private float m_checkRefreshPeriodSeconds = 5f;
        [SerializeField] private ChallengeMaintenance m_challengeMaintenance;
        [SerializeField] private RequirementGameplayLevelBase[] m_unlockRequirements;
        [SerializeField] private int m_xpLimit = 10000000;
        [SerializeField] private MissionRingRewardDefinition m_challengeRingRewardDefinition;
        [Header("Shadow FTUE references"), SerializeField] private CutsceneDefinition m_shadowFtueCutscene;
        [SerializeField] private ChallengeDefinition m_shadowFtueChallenge;
        [SerializeField] private CutsceneDefinition m_preShadowFtueCutscene;
        [Header("Reward Tracks Feature Unlock"), SerializeField]
        private RequirementGameplayLevelBase[] m_rewardTracksUnlockedRequirements;
        [SerializeField] private GameObject m_rewardTracksFeatureUnlockWidget;
        private ChallengeLeaderboardIntegration m_leaderboardIntegration;
        private readonly List<ChallengeState> m_currentChallenges = new List<ChallengeState>();
        private int m_lastCycleIndex = int.MinValue;
        private float m_timeSinceCheckRefresh;
        private bool m_ready;
        private SaveDataChallenges m_saveDataChallenges;
        private const string DebugMenuPath = "Challenges";
        private const int XPEconomyMultiplier = 100;

        //06002583..06002589. Keep backing fields in the original declaration order.
        public int BonusXPUnacknowledged => m_saveDataChallenges != null
            ? m_saveDataChallenges.BonusXPUnacknowledged : 0;
        public int PeriodBonusXP { get; private set; }
        public Action<IReadOnlyList<ChallengeState>> OnChallengesUpdated;
        public Action OnChallengeXPAwarded = () => { };
        public ChallengeFeatureState FeatureState { get; private set; }
        public bool TestModeActive { get; private set; }
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly SystemRef<VisualQualityManager_SDT> m_visualQualityManagerRef = ProcessManager.GetSystemRef<VisualQualityManager_SDT>(null, true);
        private readonly SystemRef<CutsceneManager> m_cutsceneManagerRef = ProcessManager.GetSystemRef<CutsceneManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<OrnamentManager> m_ornamentManagerRef = ProcessManager.GetSystemRef<OrnamentManager>(null, true);
        private readonly Dictionary<RewardTrackType, ChallengeRewardTrackState> m_rewardTrackStates =
            new Dictionary<RewardTrackType, ChallengeRewardTrackState>();
        private readonly Dictionary<ChallengeReward.ChallengeRewardType, List<ChallengeReward>> m_challengeRewardsByType =
            new Dictionary<ChallengeReward.ChallengeRewardType, List<ChallengeReward>>();
        private IntegrationType m_currentIntegrationType;
        private int m_displayedXPTotal;

        //0600258a..06002591: live collections and unguarded save access.
        public bool Ready => m_ready && m_currentChallenges.Count > 0;
        public IReadOnlyDictionary<RewardTrackType, ChallengeRewardTrackState> RewardTrackStates => m_rewardTrackStates;
        public IReadOnlyDictionary<ChallengeReward.ChallengeRewardType, List<ChallengeReward>> ChallengeRewardsByType => m_challengeRewardsByType;
        public ChallengeDefinition ShadowFTUEChallengeDefinition => m_shadowFtueChallenge;
        public CutsceneDefinition ShadowFTUECutsceneDefinition => m_shadowFtueCutscene;
        public MissionRingRewardDefinition ChallengeRingRewardDefinition => m_challengeRingRewardDefinition;
        public SaveDataChallengeRewardTrack.FeatureUnlockState RewardTracksFeatureUnlocked
        {
            get => m_saveDataChallenges.RewardTracksFeatureUnlocked;
            private set => m_saveDataChallenges.RewardTracksFeatureUnlocked = value;
        }

        //06002592..06002596: original service selection and remove-before-add order.
        protected override void Awake()
        {
            base.Awake();
            ProcessManager.RegisterSystem(this, null, false, false);
            SetupLeaderboardIntegration(SystemConfiguration.GetConfig<CoreGameConfiguration>().ChallengeManagerIntegrationType);
        }

        private void SetupLeaderboardIntegration(IntegrationType integrationType = IntegrationType.Default)
        {
            m_ready = false;
            m_leaderboardIntegration?.Deinitialise();
            m_currentIntegrationType = integrationType == IntegrationType.Default ? IntegrationType.GameCenter : integrationType;
            switch (m_currentIntegrationType)
            {
                case IntegrationType.Dev:
                    m_leaderboardIntegration = new ChallengeLeaderboardIntegration_Dev();
                    break;
                case IntegrationType.GameCenter:
                    m_leaderboardIntegration = new ChallengeLeaderboardIntegration_GameCenter();
                    break;
                default:
                    return;
            }
            m_leaderboardIntegration.Initialise(m_challengeCycleDefinition, OnIntegrationInitialised);
        }

        private void OnIntegrationInitialised()
        {
            m_ready = true;
            m_lastCycleIndex = m_leaderboardIntegration.GetCycleIndex();
            m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
            m_missionManagerRef.InvokeOnValid(OnMissionManagerValid);
        }

        private void OnSaveManagerValid(SaveManager saveManager)
        {
            saveManager.RemoveListener(this);
            saveManager.AddListener(this, true);
        }

        private void OnMissionManagerValid(MissionManager missionManager)
        {
            missionManager.OnMissionCompleted -= OnMissionCompleted;
            missionManager.OnMissionCompleted += OnMissionCompleted;
        }

        //06002597..0600259c.
        public void OnSaveGameOpen(SaveDataGame saveDataGame)
        {
            m_saveDataChallenges = saveDataGame.SaveDataChallenges;
            SetFeatureState(m_saveDataChallenges.FeatureState, true);
            if (FeatureState == ChallengeFeatureState.Locked && Requirements.AreMet(m_unlockRequirements, null))
                SetFeatureState(ChallengeFeatureState.UnlockedShowInterstitial, true);
            m_dataManagerRef.InvokeOnValid(OnDataManagerValid);
        }

        public void OnSaveGameClose(SaveDataGame saveDataGame) => m_saveDataChallenges = null;
        private void OnMissionCompleted(MissionState missionState) => RefreshRewardTracksFeatureUnlocked(false);

        private void OnDataManagerValid(DataManager dataManager)
        {
            m_rewardTrackStates.Clear();
            SaveManager saveManager = m_saveManagerRef.Get();
            m_challengeRewardsByType.Clear();
            foreach (ChallengeReward.ChallengeRewardType type in EnumUtilities.GetValues<ChallengeReward.ChallengeRewardType>())
                m_challengeRewardsByType[type] = new List<ChallengeReward>();
            foreach (var (type, definition) in dataManager.RewardTrackDefinitions)
            {
                ChallengeRewardTrackState state = new ChallengeRewardTrackState(definition);
                state.OnSaveGameOpen(saveManager.CurrentSave.GetOrCreateRewardTrackData(type));
                m_rewardTrackStates.Add(type, state);
                foreach (ChallengeReward reward in definition.Rewards)
                    m_challengeRewardsByType[reward.RewardType].AddUnique(reward);
            }
            m_ornamentManagerRef.InvokeOnValid(OnOrnamentManagerValid);
        }

        private void OnOrnamentManagerValid(OrnamentManager ornamentManager)
        {
            if (ornamentManager.Initialised)
            {
                RefreshXPRewards();
                RefreshChallengeData();
                SetupTrackRewardsSaveData();
            }
            else
            {
                ornamentManager.OnInitialised -= OnOrnamentInitialised;
                ornamentManager.OnInitialised += OnOrnamentInitialised;
            }
        }

        private void OnOrnamentInitialised()
        {
            RefreshXPRewards();
            RefreshChallengeData();
            SetupTrackRewardsSaveData();
        }

        //0600259d: original virtual slot9 at native pair0x1c8 runs first,
        //before the fresh CurrentSave and inherited definition GUID reads.
        private void SetupTrackRewardsSaveData()
        {
            SaveManager saveManager = m_saveManagerRef.Get();
            foreach (var (type, state) in m_rewardTrackStates)
                foreach (ChallengeReward reward in state.Definition.Rewards)
                {
                    string rewardGuid = reward.GetRewardGUID();
                    saveManager.CurrentSave.CreateRewardTrackData(type, reward.GetGUID(), rewardGuid);
                }
            if (m_rewardTrackStates.TryGetValue(RewardTrackType.Adventure, out _))
            {
                m_challengeMaintenance.RunMaintenanceSteps(saveManager.CurrentSave);
                RefreshRewardTracksFeatureUnlocked(true);
                saveManager.RequestSave();
            }
        }

        //0600259e: clearing occurs only after the managed save-null guard.
        private void RefreshChallengeData()
        {
            if (m_saveDataChallenges == null) return;
            m_currentChallenges.Clear();
            if (m_saveDataChallenges.LastSeenPeriodIndex != m_lastCycleIndex)
            {
                m_saveDataChallenges.LastSeenPeriodIndex = m_lastCycleIndex;
                m_saveDataChallenges.LastSubmittedScore = 0;
                m_saveDataChallenges.ClearChallenges();
            }
            PerformanceProfile profile = m_visualQualityManagerRef.Get().GetDefaultPerformanceProfile();
            ChallengeCyclePeriodDefinition cycle = m_challengeCycleDefinition.GetCyclePeriodDefinition(m_lastCycleIndex);
            IReadOnlyList<ChallengeDefinition> definitions = cycle.ChallengeDefinitions;
            for (int index = 0; index < definitions.Count; ++index)
            {
                ChallengeDefinition definition = definitions[index];
                if (definition == null)
                {
                    HLOutput.LogError(string.Format("Challenge definition {0} in cycle {1} is null, this must be fixed.", index, cycle.name), null);
                    continue;
                }
                if (!IsSupported(definition, profile)) continue;
                int seed = unchecked(m_challengeCycleDefinition.ExpectedChallengesPerCyclePeriod * m_lastCycleIndex + index);
                ChallengeState state = new ChallengeState(index, definition, seed, m_saveDataChallenges, m_lastCycleIndex);
                state.OnUpdateScore += OnChallengeScoreUpdate;
                state.OnAwardXP += OnChallengeAwardXP;
                m_currentChallenges.Add(state);
            }
            RefreshXPRewards();
            m_saveDataChallenges.RequestSave();
            m_dataManagerRef.Get().CacheLevelDefinitionsForChallenges(m_currentChallenges);
            OnChallengesUpdated?.Invoke(m_currentChallenges);
        }

        //0600259f: managed attribute-null then Unity feature-null, in that order.
        private static bool IsSupported(ChallengeDefinition challengeDefinition, PerformanceProfile performanceProfile)
        {
            PerformanceAttribute attribute = challengeDefinition.PerformanceAttribute;
            return attribute == null || attribute.Feature == null || performanceProfile.IsSupported(attribute);
        }

        //060025a0..060025a2: required XP callback follows saving; ring-only awards
        //also request saving. Bonus assignment is guarded by all current states.
        private void OnChallengeScoreUpdate(ChallengeState challengeState)
        {
            if (!m_currentChallenges.Contains(challengeState)) return;
            int totalScore = GetTotalScore();
            if (totalScore > m_saveDataChallenges.LastSubmittedScore && m_leaderboardIntegration.SubmitScore(totalScore, m_lastCycleIndex))
            {
                m_saveDataChallenges.LastSubmittedScore = totalScore;
                m_saveDataChallenges.RequestSave();
            }
        }

        private void OnChallengeAwardXP(ChallengeState challengeState, bool firstTimePlaying, int ringXpEarned)
        {
            string itemId = challengeState.ChallengeDefinition.name;
            PeriodBonusXP = 0;
            bool saveNeeded = ringXpEarned > 0;
            if (ringXpEarned > 0) AddXP(ringXpEarned, XPSource.ChallengeRings, true, itemId);
            if (firstTimePlaying)
            {
                AddXP(challengeState.ChallengeDefinition.XPReward, XPSource.ChallengeCompletion, true, itemId);
                int bonus = CalculateBonusChallengeXP(challengeState);
                if (bonus > 0) AddXP(bonus, XPSource.ChallengeBonus, true, itemId);
                if (challengeState.ChallengeDefinition != m_shadowFtueChallenge)
                    m_saveDataChallenges.ChallengesCompleted = unchecked(m_saveDataChallenges.ChallengesCompleted + 1);
                saveNeeded = true;
            }
            if (challengeState.SRankXPAwarded > 0)
            {
                AddXP(challengeState.SRankXPAwarded, XPSource.ChallengeSRank, true, itemId);
                challengeState.SRankXPAwarded = 0;
                saveNeeded = true;
            }
            if (saveNeeded)
            {
                m_saveDataChallenges.RequestSave();
                OnChallengeXPAwarded();
                RefreshXPRewards();
            }
        }

        private int CalculateBonusChallengeXP(ChallengeState challengeState)
        {
            if (!m_currentChallenges.Contains(challengeState)) return 0;
            foreach (ChallengeState state in m_currentChallenges)
                if (!state.Played) return 0;
            int bonus = m_challengeCycleDefinition.PeriodBonusXP;
            challengeState.BonusXPAwarded = bonus;
            PeriodBonusXP = bonus;
            m_saveDataChallenges.BonusXPUnacknowledged = unchecked(m_saveDataChallenges.BonusXPUnacknowledged + bonus);
            return bonus;
        }

        //060025a3..060025a5: RefreshRewards gets the original two delegates.
        public void RefreshXPRewards()
        {
            if (!m_ornamentManagerRef.IsValid() || !m_ornamentManagerRef.Get().Initialised || m_saveDataChallenges == null) return;
            int displayedXP = unchecked(GetCurrentXP() - BonusXPUnacknowledged);
            foreach (var (_, state) in m_rewardTrackStates)
                state.RefreshRewards(CheckIfRewardOwned, reward => reward.GiveReward());
            m_displayedXPTotal = displayedXP;
            m_saveDataChallenges.RequestSave();
        }

        private bool CheckIfRewardOwned(ChallengeReward reward, int spentXP)
        {
            bool owned = reward.XPThreshold <= spentXP;
            if (reward is ChallengeRewardOrnament ornament)
                owned &= m_ornamentManagerRef.Get().IsOrnamentUnlocked(ornament.Ornament);
            return owned;
        }

        public void AcknowledgeBonusXP()
        {
            PeriodBonusXP = 0;
            m_saveDataChallenges.BonusXPUnacknowledged = 0;
            RefreshXPRewards();
            m_saveDataChallenges.RequestSave();
        }

        //060025a6: unordered/NaN timer comparison does not return early.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_ready) return;
            m_timeSinceCheckRefresh += deltaTime;
            if (m_timeSinceCheckRefresh < m_checkRefreshPeriodSeconds) return;
            m_timeSinceCheckRefresh = 0f;
            int cycleIndex = m_leaderboardIntegration.GetCycleIndex();
            if (cycleIndex != m_lastCycleIndex)
            {
                m_lastCycleIndex = cycleIndex;
                RefreshChallengeData();
            }
        }

        //060025a7..060025a9: preserve the shipping conditional bodies. The
        //validation Contains result is discarded; no additional assertion exists.
        public void SetRewardTrackState(RewardTrackType rewardTrackType, SaveDataChallengeRewardTrack.FeatureUnlockState state)
        {
            m_saveManagerRef.Get().CurrentSave.GetOrCreateRewardTrackData(rewardTrackType).UnlockState = state;
            RefreshXPRewards();
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void DoValidate()
        {
            if (Application.isPlaying) ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(_ => { });
        }

        [Conditional("BUILD_DEVELOPMENT")]
        public void ValidateRewardsRuntimeOnly()
        {
            DataManager dataManager = ProcessManager.GetSystemSafe<DataManager>(null, true);
            if (dataManager == null) return;
            var ornaments = dataManager.OrnamentDefinitions;
            HashSet<OrnamentIdentifier> rewardIdentifiers = new HashSet<OrnamentIdentifier>();
            foreach (var (_, definition) in dataManager.RewardTrackDefinitions)
                foreach (ChallengeReward reward in definition.Rewards)
                    if (reward is ChallengeRewardOrnament ornament)
                        rewardIdentifiers.Add(ornament.Ornament.OrnamentIdentifier);
            foreach (var (identifier, _) in ornaments) rewardIdentifiers.Contains(identifier);
        }

        //060025aa: unregister, deinitialise, unsubscribe, then original base.
        public override void OnDestroy()
        {
            ProcessManager.UnregisterSystem(this);
            m_ready = false;
            m_leaderboardIntegration?.Deinitialise();
            m_leaderboardIntegration = null;
            if (m_saveManagerRef.TryGet(out SaveManager saveManager)) saveManager.RemoveListener(this);
            if (m_ornamentManagerRef.TryGet(out OrnamentManager ornamentManager)) ornamentManager.OnInitialised -= OnOrnamentInitialised;
            if (m_missionManagerRef.TryGet(out MissionManager missionManager)) missionManager.OnMissionCompleted -= OnMissionCompleted;
            base.OnDestroy();
        }

        //060025ab..060025b3.
        public IReadOnlyList<ChallengeState> GetChallengeStates() => m_currentChallenges;

        public int GetTotalScore()
        {
            int score = 0;
            foreach (ChallengeState state in m_currentChallenges) score = unchecked(score + state.BestScore);
            return score;
        }

        public int GetCurrentXP()
        {
            int spent = 0;
            foreach (var (_, state) in m_rewardTrackStates) spent = unchecked(spent + state.SaveData.SpentXP);
            return unchecked(m_saveDataChallenges.TotalXPEarned - spent);
        }

        public TimeSpan GetChallengePeriodTimeRemaining() => m_leaderboardIntegration.GetCurrentPeriodEnd() - m_leaderboardIntegration.GetTime();
        public void ShowLeaderboard() => m_leaderboardIntegration.ShowLeaderboard();
        public bool CanShowLeaderboard() => m_leaderboardIntegration.CanShowLeaderboard();
        public int GetBonusXP() => m_challengeCycleDefinition.PeriodBonusXP;

        public void SetFeatureUnlockedInterstitialSeen()
        {
            if (FeatureState == ChallengeFeatureState.UnlockedSeenInterstitial || FeatureState == ChallengeFeatureState.UnlockedSeenFeature) return;
            SetFeatureState(ChallengeFeatureState.UnlockedSeenInterstitial, false);
        }

        public void SetFeatureUnlockedSeen() => SetFeatureState(ChallengeFeatureState.UnlockedSeenFeature, false);

        //060025b4: storage access and list mutation precede auto-completion and
        //refresh. skipRefresh does not suppress the final save request.
        private void SetFeatureState(ChallengeFeatureState featureState, bool skipRefresh = false)
        {
            if (featureState == FeatureState) return;
            ChallengeFeatureState oldState = FeatureState;
            FeatureState = featureState;
            m_saveDataChallenges.FeatureState = featureState;
            List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>(null, true).Storage
                .GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
            if (featureState == ChallengeFeatureState.UnlockedShowInterstitial)
                unlocks.Add(new MetaGameUnlockChallengesFeature(m_challengeCycleDefinition));
            else
                for (int i = 0; i < unlocks.Count; ++i)
                    if (unlocks[i] is MetaGameUnlockChallengesFeature) { unlocks.RemoveAt(i); --i; }
            if (featureState != ChallengeFeatureState.Locked) TryAutoCompleteShadowFTUE();
            if (oldState == ChallengeFeatureState.Locked && !skipRefresh) RefreshChallengeData();
            if (!skipRefresh) RefreshRewardTracksFeatureUnlocked(true);
            m_saveDataChallenges.RequestSave();
        }

        //060025b5..060025b7: existing Unlocked state only triggers the fanfare.
        public void RefreshRewardTracksFeatureUnlocked(bool skipSave = false)
        {
            if (RewardTracksFeatureUnlocked == SaveDataChallengeRewardTrack.FeatureUnlockState.Seen) return;
            if (RewardTracksFeatureUnlocked == SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked)
            {
                TriggerRewardTracksFeatureFanfare();
                return;
            }
            if (m_rewardTrackStates.TryGetValue(RewardTrackType.Adventure, out ChallengeRewardTrackState adventure) && adventure.SaveData.SpentXP > 0)
            {
                RewardTracksFeatureUnlocked = SaveDataChallengeRewardTrack.FeatureUnlockState.Seen;
                if (adventure.UnlockState != SaveDataChallengeRewardTrack.FeatureUnlockState.Seen)
                    SetRewardTrackState(RewardTrackType.Adventure, SaveDataChallengeRewardTrack.FeatureUnlockState.Seen);
            }
            else if (Requirements.AreMet(m_rewardTracksUnlockedRequirements, null))
            {
                RewardTracksFeatureUnlocked = SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
                SetRewardTrackState(RewardTrackType.Adventure, SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked);
                TriggerRewardTracksFeatureFanfare();
            }
            else return;
            if (!skipSave) m_saveDataChallenges.RequestSave();
        }

        public void SetRewardTracksFeatureUnlockedInterstitialSeen()
        {
            RewardTracksFeatureUnlocked = SaveDataChallengeRewardTrack.FeatureUnlockState.Seen;
            m_saveDataChallenges.RequestSave();
        }

        private void TriggerRewardTracksFeatureFanfare()
        {
            List<IMetaGameUnlock> unlocks = ProcessManager.GetSystem<App>(null, true).Storage
                .GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
            foreach (IMetaGameUnlock unlock in unlocks) if (unlock is MetaGameUnlockRewardTracksFeature) return;
            unlocks.Add(new MetaGameUnlockRewardTracksFeature(m_rewardTracksFeatureUnlockWidget.GetComponent<UIWidgetProgression>()));
        }

        //060025b8: keep unchecked arithmetic, original cap correction target,
        //and analytics of the requested amount rather than the capped amount.
        public void AddXP(int xpToAdd, XPSource source, bool skipSave = false, string analyticsItemId = null)
        {
            if (source == XPSource.MissionCompletion)
                m_saveDataChallenges.TotalStoryMissionXPEarned = unchecked(m_saveDataChallenges.TotalStoryMissionXPEarned + xpToAdd);
            else m_saveDataChallenges.TotalXPEarned = unchecked(m_saveDataChallenges.TotalXPEarned + xpToAdd);
            int deficit = unchecked(m_xpLimit - GetCurrentXP());
            if (deficit < 0) m_saveDataChallenges.TotalXPEarned = unchecked(m_saveDataChallenges.TotalXPEarned + deficit);
            AnalyticsEventCollector.XPGainedEvent(xpToAdd, source, analyticsItemId);
            if (!skipSave) m_saveDataChallenges.RequestSave();
        }

        //060025b9..060025bd: native stores displayed index1/set0. The original
        //ChallengeState constructor increments its zero-based input index.
        private bool HasSeenShadowFTUECutscene() => m_cutsceneManagerRef.TryGet(out CutsceneManager manager) && manager.HasCutsceneBeenSeen(m_shadowFtueCutscene);
        private bool HasCompletedShadowFTUEChallenge() => m_missionManagerRef.Get()
            .GetSaveDataForMission(m_shadowFtueChallenge.MissionDefinition.GetGUID(), m_shadowFtueChallenge.GameplayLevelDefinition.GetGUID(), null).Complete;
        public bool ShouldShowShadowFTUE() => !HasSeenShadowFTUECutscene() && !HasCompletedShadowFTUEChallenge();
        private bool ShouldSkipFTUE() => m_saveManagerRef.Get().GetSaveDataSettings().SkipFTUE;
        public ChallengeState GetShadowFTUEChallengeState() => new ChallengeState(0, m_shadowFtueChallenge, 0, null, 0);

        //060025be..060025c0: original save/progression calls remain intact.
        private void TryAutoCompleteShadowFTUE()
        {
            if (!ShouldSkipFTUE() || HasCompletedShadowFTUEChallenge()) return;
            SaveManager saveManager = m_saveManagerRef.Get();
            SaveDataLevelMission mission = m_missionManagerRef.Get()
                .GetSaveDataForMission(m_shadowFtueChallenge.MissionDefinition.GetGUID(), m_shadowFtueChallenge.GameplayLevelDefinition.GetGUID(), null);
            mission.MarkComplete(m_shadowFtueChallenge.MissionDefinition.TargetObjectivesToComplete);
            saveManager.RequestSave();
            ProcessManager.GetSystem<ProgressionManager>(null, true).OnSaveGameOpen(saveManager.CurrentSave);
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugUncompleteShadowFTUE()
        {
            SaveDataLevelMission mission = m_missionManagerRef.Get()
                .GetSaveDataForMission(m_shadowFtueChallenge.MissionDefinition.GetGUID(), m_shadowFtueChallenge.GameplayLevelDefinition.GetGUID(), null);
            mission.Complete = false;
            mission.CompletedAt = default(DateTime);
            mission.Progress = 0;
            SaveManager saveManager = m_saveManagerRef.Get();
            saveManager.CurrentSave.RemoveSeenCutsceneGuid(m_shadowFtueCutscene.GetGUID());
            saveManager.CurrentSave.RemoveSeenCutsceneGuid(m_preShadowFtueCutscene.GetGUID());
            saveManager.RequestSave();
            ProcessManager.GetSystem<ProgressionManager>(null, true).OnSaveGameOpen(saveManager.CurrentSave);
            TryAutoCompleteShadowFTUE();
        }

        public bool TryGetRewardTrackForReward(string rewardGuid, out RewardTrackType trackType)
        {
            foreach (var (type, definition) in m_dataManagerRef.Get().RewardTrackDefinitions)
                if (definition.ContainsRewardGUID(rewardGuid)) { trackType = type; return true; }
            trackType = default(RewardTrackType);
            return false;
        }

        //060025c1: shipping bodies keep string construction, deconstruction and
        //enum ToString but contain no native debug-button registration calls.
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetupDebugButtons()
        {
            int count = m_challengeCycleDefinition.ExpectedChallengesPerCyclePeriod;
            for (int i = 0; i < count; ++i)
                string.Concat("Challenges/", string.Format("Challenge {0}", unchecked(i + 1)));
            foreach (var (_, _) in m_rewardTrackStates) { }
            foreach (ChallengeFeatureState state in EnumUtilities.GetValues<ChallengeFeatureState>()) state.ToString();
        }

        //060025c2: preserve the two save-record GUID roles as observed, and
        //break after the first matching track even if no reward matched.
        [Conditional("DEBUG_MENU"), Conditional("BUILD_DEVELOPMENT")]
        private void DebugResetSpentXP()
        {
            foreach (var (_, state) in m_rewardTrackStates) state.SaveData.SpentXP = 0;
            DataManager dataManager = m_dataManagerRef.Get();
            OrnamentManager ornamentManager = m_ornamentManagerRef.Get();
            SaveManager saveManager = m_saveManagerRef.Get();
            foreach (SaveDataChallengeReward savedReward in saveManager.CurrentSave.DebugGetAdventureRewards())
            {
                bool collected = false;
                foreach (var (_, definition) in dataManager.RewardTrackDefinitions)
                {
                    if (!definition.ContainsRewardGUID(savedReward.RewardGUID)) continue;
                    foreach (ChallengeReward reward in definition.Rewards)
                    {
                        if (reward.GetGUID() != savedReward.TrackGUID) continue;
                        if (reward is ChallengeRewardOrnament ornament)
                            collected = ornamentManager.IsOrnamentCollected(ornament.Ornament);
                        break;
                    }
                    break;
                }
                savedReward.Collected = collected;
            }
            m_saveDataChallenges.RequestSave();
            RefreshXPRewards();
        }

        //060025c3..060025c7.
        [Conditional("BUILD_DEVELOPMENT"), Conditional("DEBUG_MENU")]
        public void DebugResetXP()
        {
            m_saveDataChallenges.TotalStoryMissionXPEarned = 0;
            m_saveDataChallenges.TotalXPEarned = 0;
            m_saveDataChallenges.BonusXPUnacknowledged = 0;
            AddXP(unchecked(-GetCurrentXP()), XPSource.Debug, false, null);
            RefreshXPRewards();
        }

        [Conditional("DEBUG_MENU"), Conditional("BUILD_DEVELOPMENT")]
        public void DebugAddXP(int xpToAdd) { AddXP(xpToAdd, XPSource.Debug, false, null); RefreshXPRewards(); }

        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugResetChallenge(int challengeIndex)
        {
            m_currentChallenges[challengeIndex].UpdateScoreInfo(0, 0f, 0, true);
            OnChallengesUpdated?.Invoke(m_currentChallenges);
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugCompleteChallenge(int challengeIndex)
        {
            ChallengeState state = m_currentChallenges[challengeIndex];
            if (state.Played) return;
            float time = state.ChallengeDefinition.TimeLimitSeconds / 2;
            int score = state.ChallengeDefinition.CalculateScore(time);
            state.UpdateScoreInfo(score, time, 0, true);
            OnChallengesUpdated?.Invoke(m_currentChallenges);
        }

        [Conditional("BUILD_DEVELOPMENT")]
        private void RemoveDebugButtons() { } // Original ARM RET/x86 frame+RET.

        //060025c8..060025cc: switching test mode off does not restore integration.
        public void DebugSetFeatureState(ChallengeFeatureState featureState)
        {
            if (featureState == ChallengeFeatureState.Locked || featureState == ChallengeFeatureState.UnlockedShowInterstitial)
            {
                m_saveDataChallenges.IsNewState = ChallengesIsNewState.IsNew;
                m_saveDataChallenges.UnlockSeen = false;
            }
            SetFeatureState(featureState, false);
        }

        public void DebugSetTestMode(bool value)
        {
            TestModeActive = value;
            if (value && m_currentIntegrationType != IntegrationType.Dev) SetupLeaderboardIntegration(IntegrationType.Dev);
            SetFeatureState(ChallengeFeatureState.UnlockedShowInterstitial, false);
            RefreshChallengeData();
        }

        public string GetCycleDebugLabel() => m_leaderboardIntegration.GetCycleDebugLabel();
        public void DebugAdvanceCycle() => m_leaderboardIntegration.AdvanceCycle();
        public void DebugRegressCycle() => m_leaderboardIntegration.RegressCycle();

        //060025cd..060025ce: major is written before minor, including aliasing.
        public static void SetSplitXPValues(int combinedValue, out int major, out int minor)
        {
            if (combinedValue < 0) { major = 0; minor = 0; return; }
            major = combinedValue / XPEconomyMultiplier;
            minor = unchecked(combinedValue - major * XPEconomyMultiplier);
        }

        public static int GetXPFromSplitValues(int major, int minor) => unchecked(major * XPEconomyMultiplier + minor);
        public ChallengeManager() { } //060025cf: initializers then original base.

        public enum ChallengeFeatureState { Locked = 0, UnlockedShowInterstitial = 1, UnlockedSeenInterstitial = 2, UnlockedSeenFeature = 3 }
        public enum XPSource { Debug = 0, SaveMaintenance = 1, ChallengeCompletion = 100, ChallengeRings = 101, ChallengeSRank = 102, ChallengeBonus = 103, MissionCompletion = 200, MissionRings = 201 }
        public enum XPSink { Debug = 0, RewardsAdventure = 100, RewardsOutfits = 101, RewardsShadow = 102 }
        public enum IntegrationType { Default = 0, Dev = 1, GameCenter = 2 }
    }
}
