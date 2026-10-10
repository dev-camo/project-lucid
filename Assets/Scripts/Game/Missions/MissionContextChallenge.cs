using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    // Original Game.Runtime020006fc,complete33 methods/nineteen fields. Genuine
    // IMissionContext default methods are inherited; no copied or invented facade.
    public class MissionContextChallenge : IMissionContext
    {
        // Original readonly compiler fields04001957..0400195e,then mutable real-time.
        public MissionDefinition MissionDefinition { get; }
        public GameplayLevelDefinition LevelDefinition { get; }
        public IReadOnlyList<int> OverrideActiveObjectives { get; }
        public IReadOnlyList<CharacterId> OverrideAllowedCharacters { get; }
        public float BestTimeSeconds { get; }
        public float OverrideTimeLimitSeconds { get; }
        public LevelStartPositionDefinition OverrideStartPosition { get; }
        // Original06002652 returns true; no saved-progress fallback.
        public bool IgnoreSavedProgress => true;
        public bool IsReplay { get; }
        public float RealTimeTaken { get; set; }
        // Original06002656 returns false.
        public bool SaveLastLevelVisited => false;
        public string Description { get; }
        public int ObjectiveTarget { get; }
        public int ChallengeIndex { get; }
        public int ChallengeSetIndex { get; }
        // Original0600265b reads the current referenced state's genuine field0x28.
        public ChallengeDefinition ChallengeDefinition => m_challengeState.ChallengeDefinition;
        public float NewTime { get; private set; }
        public int NewScore { get; private set; }
        public int RingXPEarned { get; private set; }

        // Original04001967/0x68,readonly authored reference;04001968 is genuine constant.
        private readonly ChallengeState m_challengeState;
        private const float TestModeTimeLimitSeconds = 1800f;
        // Original04001969 readonly reference to mutable shared List;0600266b adds
        // these six exact original enum values in this order, excluding Shadow.
        private static readonly List<CharacterId> s_testModeCharacterList = new List<CharacterId>
        {
            CharacterId.Sonic, CharacterId.Amy, CharacterId.Tails,
            CharacterId.Cream, CharacterId.Knuckles, CharacterId.Rouge
        };

        // Original06002662: base first,then state/definition/seeded references.
        // Count queries happen before testing testMode; repeated getter calls and
        // shared fallback-list aliasing are preserved without null/empty repairs.
        public MissionContextChallenge(ChallengeState challengeState, bool testMode)
        {
            m_challengeState = challengeState;
            ChallengeDefinition definition = challengeState.ChallengeDefinition;
            MissionDefinition = definition.MissionDefinition;
            LevelDefinition = definition.GameplayLevelDefinition;
            ChallengeDefinition.ChallengeSeededData seededData = challengeState.ChallengeSeededData;
            if (seededData.ObjectiveIndices.Count > 0 && !testMode)
                OverrideActiveObjectives = seededData.ObjectiveIndices;
            if (seededData.CharacterList.Count > 0 && !testMode)
                OverrideAllowedCharacters = seededData.CharacterList;
            else
                OverrideAllowedCharacters = s_testModeCharacterList;
            BestTimeSeconds = challengeState.BestTimeSeconds;
            OverrideTimeLimitSeconds = testMode ? TestModeTimeLimitSeconds : challengeState.ChallengeDefinition.TimeLimitSeconds;
            OverrideStartPosition = seededData.StartPosition;
            Description = definition.GetDescription();
            ChallengeIndex = challengeState.ChallengeIndex;
            ChallengeSetIndex = challengeState.ChallengeSetIndex;
            RealTimeTaken = 0f;
            IsReplay = challengeState.Played;
            ObjectiveTarget = testMode ? MissionDefinition.TotalObjectiveCount : MissionDefinition.TargetObjectivesToComplete;
        }

        // Original06002663 ignores missionManager. All four service lookups precede
        // the ring-state query; safe Challenge lookup has no newly added null guard.
        // Score/update precedes original telemetry,bonus reset then progress caching.
        public void OnActiveMissionComplete(MissionManager missionManager, float timeElapsedSeconds, int score)
        {
            ChallengeManager challengeManager = ProcessManager.GetSystemSafe<ChallengeManager>(null, true);
            CollectableManager collectableManager = ProcessManager.GetSystem<CollectableManager>(null, true);
            DreamPowerStoreManager dreamPowerStoreManager = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true);
            ProgressionManager progressionManager = ProcessManager.GetSystem<ProgressionManager>(null, true);
            if (collectableManager.TryGetCollectableState(CollectableType.Ring, out CollectableState ringState))
                RingXPEarned = challengeManager.ChallengeRingRewardDefinition.GetRingRewardXP(ringState.Collected);
            NewTime = timeElapsedSeconds;
            NewScore = score;
            int totalChallengeScore = unchecked(m_challengeState.ChallengeDefinition.CalculateScore(timeElapsedSeconds) + score);
            m_challengeState.UpdateScoreInfo(totalChallengeScore, timeElapsedSeconds, RingXPEarned, false);
            int totalScore = challengeManager.GetTotalScore();
            int totalXP = challengeManager.GetCurrentXP();
            IReadOnlyDictionary<int, DreamPowerDefinition> loadout = dreamPowerStoreManager.GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
            AnalyticsChallengePayload payload = new AnalyticsChallengePayload(this, m_challengeState, totalChallengeScore, totalScore, totalXP, timeElapsedSeconds, loadout);
            AnalyticsEventCollector.ChallengeCompleteEvent(payload);
            m_challengeState.BonusXPAwarded = 0;
            progressionManager.CacheChallengeProgress();
        }

        // Original06002664 ignores both arguments;literal3851/14159 and separate
        // culture-sensitive Int32 conversions are preserved in the original order.
        public string CalculateMissionIndex(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            return string.Concat("Challenge_", ChallengeSetIndex.ToString(), "_", ChallengeIndex.ToString());
        }

        // Original06002665 returns authentic HLAutoGenerated Challenge0x6c346841.
        public AnalyticsMissionType CalculateAnalyticsMissionType(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            return AnalyticsMissionType.Challenge;
        }

        // Original06002666: no event if the genuine current-character query fails.
        // Reuse the captured loadout for MissionStart before ChallengeStart payload.
        public void SendMissionStartAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> loadout = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionStartEvent(new AnalyticsMissionPayload(this, missionState, character, loadout));
                AnalyticsEventCollector.ChallengeStartEvent(new AnalyticsChallengePayload(this, m_challengeState, loadout));
            }
        }

        // Original06002667 preserves MissionQuit before ChallengeQuit,one loadout.
        public void SendMissionQuitAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> loadout = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionQuitEvent(new AnalyticsMissionPayload(this, missionState, character, loadout));
                AnalyticsEventCollector.ChallengeQuitEvent(new AnalyticsChallengePayload(this, m_challengeState, loadout));
            }
        }

        // Original06002668 similarly emits MissionFailed before ChallengeFailed.
        public void SendMissionFailedAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> loadout = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionFailedEvent(new AnalyticsMissionPayload(this, missionState, character, loadout));
                AnalyticsEventCollector.ChallengeFailedEvent(new AnalyticsChallengePayload(this, m_challengeState, loadout));
            }
        }

        // Original06002669 emits only MissionComplete here;ChallengeComplete belongs
        // to OnActiveMissionComplete and must not be duplicated or reordered.
        public void SendMissionCompleteAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> loadout = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionCompleteEvent(new AnalyticsMissionPayload(this, missionState, character, loadout));
            }
        }

        // Original0600266a: increment challenge first,then rereadAttempts before
        // genuine MissionState.SetAttempts. Save only when first-play flag changes.
        public void MarkNewAttempt(MissionState state)
        {
            m_challengeState.MarkNewAttempt();
            int attempts = m_challengeState.Attempts;
            state.SetAttempts(attempts);
            SaveManager saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            SaveDataChallenges saveDataChallenges = saveManager.CurrentSave.SaveDataChallenges;
            if (!saveDataChallenges.HasPlayedChallenge)
            {
                saveDataChallenges.HasPlayedChallenge = true;
                saveManager.CurrentSave.RequestSave();
            }
        }
    }
}
