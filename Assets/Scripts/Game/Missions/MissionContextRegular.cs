using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006fd. Regular missions retain their authored
    // definition and level, while best times and rewards accumulate per context.
    // Original analytics and leaderboard boundaries are preserved for research;
    // offline routing belongs at the service boundary, outside this implementation.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionContextRegular : IMissionContext
    {
        // 0x0600266c..0x06002671. Declaration order preserves the original
        // backing fields and their readonly/private-set distinctions.
        public MissionDefinition MissionDefinition { get; }
        public GameplayLevelDefinition LevelDefinition { get; }
        public IReadOnlyList<int> OverrideActiveObjectives => null;
        public IReadOnlyList<CharacterId> OverrideAllowedCharacters => null;
        public float BestTimeSeconds { get; private set; }

        // 0x06002672: zero means the definition supplies its normal time limit.
        public float OverrideTimeLimitSeconds => MissionDefinition.UseBestTime ? BestTimeSeconds : 0f;
        public LevelStartPositionDefinition OverrideStartPosition => null;
        public bool IgnoreSavedProgress { get; }
        public bool IsReplay { get; }
        public float RealTimeTaken { get; set; }
        public bool SaveLastLevelVisited => true;
        public string Description => GetDescription();
        public string OverrideDescription => GetOverrideDescription();
        public int ObjectiveTarget { get; }
        public float NewTime { get; private set; }
        public int RingXPEarned { get; private set; }
        public int MissionClearXPEarned { get; private set; }

        // 0x04001974..0x04001976 / 0x06002682: acquire all three references in
        // this order before the base constructor and before storing definitions.
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<TimeTrialLeaderboardManager> m_timeTrialLeaderboardManager = ProcessManager.GetSystemRef<TimeTrialLeaderboardManager>(null, true);
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>(null, true);

        public MissionContextRegular(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition, bool isReplay)
        {
            MissionDefinition = missionDefinition;
            LevelDefinition = levelDefinition;
            IgnoreSavedProgress = isReplay && !missionDefinition.HasTimeTrial();
            IsReplay = isReplay;
            RealTimeTaken = 0f;
            ObjectiveTarget = missionDefinition.TargetObjectivesToComplete;
            BestTimeSeconds = GetBestTimeSeconds();
        }

        // 0x06002683. Ring XP is stored on the mission save before challenge XP.
        // Completion XP is passed to ChallengeManager, without adding it here to
        // that save's XPEarned. Neither cached reward is reset on repeated calls.
        public void OnActiveMissionComplete(MissionManager missionManager, float timeElapsedSeconds, int score)
        {
            SaveDataLevelMission missionSaveData = missionManager.GetSaveDataForMission(MissionDefinition.GetGUID(), LevelDefinition.GetGUID());
            if (ProcessManager.GetSystem<CollectableManager>(null, true).TryGetCollectableState(CollectableType.Ring, out CollectableState ringState))
            {
                int collected = ringState.Collected;
                RingXPEarned = ProcessManager.GetSystem<LevelManager>(null, true).MissionRingRewardDefinition.GetRingRewardXP(collected);
                missionSaveData.XPEarned = unchecked(missionSaveData.XPEarned + RingXPEarned);
            }

            if (!IsReplay)
                MissionClearXPEarned = MissionDefinition.XPReward;

            if (unchecked(RingXPEarned + MissionClearXPEarned) > 0)
            {
                ChallengeManager challengeManager = m_challengeManagerRef.Get();
                string missionName = MissionDefinition.name;
                challengeManager.AddXP(RingXPEarned, ChallengeManager.XPSource.MissionRings, true, missionName);
                challengeManager.AddXP(MissionClearXPEarned, ChallengeManager.XPSource.MissionCompletion, false, missionName);
            }

            NewTime = timeElapsedSeconds;
            // Both shipping architectures snapshot the previous best for the two comparisons.
            float previousBestTime = BestTimeSeconds;
            if (previousBestTime <= 0f || timeElapsedSeconds < previousBestTime)
            {
                BestTimeSeconds = timeElapsedSeconds;
                missionSaveData.BestTimeSeconds = timeElapsedSeconds;
                missionSaveData.RequestSave();
                if (m_timeTrialLeaderboardManager.TryGet(out TimeTrialLeaderboardManager leaderboardManager))
                    leaderboardManager.SubmitBestTime(MissionDefinition, missionSaveData);
            }

            ProcessManager.GetSystem<ProgressionManager>(null, true).CacheLevelProgress(LevelDefinition);
        }

        // 0x06002684: use the shared regular index calculation with supplied args.
        public string CalculateMissionIndex(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            return GetMissionIndex(missionDefinition, levelDefinition);
        }

        // 0x06002685; original generated predicates 0x06002690/0x06002691.
        // The first visible main mission wins. Only if that search fails are
        // sub-missions searched, using the live list/count and original equality.
        // Format keeps the caller's culture and one-based authored positions.
        public static string GetMissionIndex(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            IReadOnlyList<MissionDefinition> missions = levelDefinition.MissionList.GetMissions(false);
            int index = missions.FindIndex(definition => missionDefinition == definition);
            if (index >= 0)
                return string.Format("Regular_{0}", unchecked(index + 1));

            for (int i = 0; i < missions.Count; ++i)
            {
                int subIndex = missions[i].SubMissions.FindIndex(definition => missionDefinition == definition);
                if (subIndex >= 0)
                    return string.Format("Regular_{0}_{1}", unchecked(i + 1), unchecked(subIndex + 1));
            }

            return "Regular_Unknown";
        }

        // 0x06002686. These two collectible types and the authored key tutorial
        // use persistent analytics even when selected through a regular context.
        public AnalyticsMissionType CalculateAnalyticsMissionType(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition)
        {
            MissionType missionType = missionDefinition.Type;
            if (missionType == MissionType.BlueCoins || missionType == MissionType.RedStarRings)
                return AnalyticsMissionType.Persistent;
            return missionDefinition.MissionName == Strings.FTUE_COLLECT_KEYS ? AnalyticsMissionType.Persistent : AnalyticsMissionType.Regular;
        }

        // 0x06002687..0x0600268a. Each event independently resolves the current
        // character, then the equipped powers for the definition's archetype.
        // Failure to find a character returns before the powers lookup/payload.
        public void SendMissionStartAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowers = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionStartEvent(new AnalyticsMissionPayload(this, missionState, character, dreamPowers));
            }
        }

        public void SendMissionQuitAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowers = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionQuitEvent(new AnalyticsMissionPayload(this, missionState, character, dreamPowers));
            }
        }

        public void SendMissionFailedAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowers = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionFailedEvent(new AnalyticsMissionPayload(this, missionState, character, dreamPowers));
            }
        }

        public void SendMissionCompleteAnalytics(MissionState missionState)
        {
            if (ProcessManager.GetSystem<CharacterManager>(null, true).TryGetCurrentCharacter(out Character character))
            {
                IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowers = ProcessManager.GetSystem<DreamPowerStoreManager>(null, true).GetPowersLoadoutAsDefinitions(MissionDefinition.CharacterArchetype);
                AnalyticsEventCollector.MissionCompleteEvent(new AnalyticsMissionPayload(this, missionState, character, dreamPowers));
            }
        }

        // 0x0600268b: the original MissionState boundary is inlined by IL2CPP.
        public void MarkNewAttempt(MissionState state)
        {
            state.MarkNewAttempt();
        }

        // 0x0600268c: query only when a real save is open; querying creates absent
        // level/mission records in the same order as the supplied game's code.
        private float GetBestTimeSeconds()
        {
            if (m_saveManagerRef.TryGet(out SaveManager saveManager) && saveManager.IsAnySaveOpen)
                return saveManager.CurrentSave.GetOrCreateLevelData(LevelDefinition.GetGUID()).GetOrCreateMissionData(MissionDefinition.GetGUID()).BestTimeSeconds;
            return 0f;
        }

        // 0x0600268d: keep the original definition's overloaded null comparison.
        // A completed override is captured once, unlike the fresh reads below.
        private string GetOverrideDescription()
        {
            MissionDefinition completedOverride = MissionDefinition.CompletedMissionOverrides;
            if (completedOverride == null)
                return string.Empty;
            if (completedOverride.FailOnTimeLimitReached)
                return completedOverride.GetFormattedMissionName();
            float timeLimit = completedOverride.UseBestTime ? BestTimeSeconds : completedOverride.TimeLimitSeconds;
            return MissionStringsUtil.GetMissionDescription(completedOverride.MissionName, timeLimit, completedOverride.TargetObjectivesToComplete);
        }

        // 0x0600268e: keep fresh definition reads around its computed getters.
        private string GetDescription()
        {
            if (MissionDefinition.FailOnTimeLimitReached)
                return MissionDefinition.GetFormattedMissionName();
            float timeLimit = MissionDefinition.UseBestTime ? BestTimeSeconds : MissionDefinition.TimeLimitSeconds;
            return MissionStringsUtil.GetMissionDescription(MissionDefinition.MissionName, timeLimit, MissionDefinition.TargetObjectivesToComplete);
        }
    }
}
