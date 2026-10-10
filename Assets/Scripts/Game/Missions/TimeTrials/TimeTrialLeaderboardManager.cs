using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class TimeTrialLeaderboardManager : ISystem
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
        private readonly ITimeTrialLeaderboard m_timeTrialLeaderboard;

        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public TimeTrialLeaderboardManager()
        {
            this.SubscribeToAction(SystemAction.Initialise, SystemInitialise);
            this.SubscribeToAction(SystemAction.Shutdown, SystemShutdown);
            m_timeTrialLeaderboard = new TimeTrialLeaderboardApple();
        }
#else
        public TimeTrialLeaderboardManager()
        {
            this.SubscribeToAction(SystemAction.Initialise, SystemInitialise);
            this.SubscribeToAction(SystemAction.Shutdown, SystemShutdown);
            m_timeTrialLeaderboard = new ProjectLucid.Offline.LocalTimeTrialLeaderboard();
        }
#endif

        private void SystemInitialise(object context = null)
        {
            MissionManager missionManager = m_missionManagerRef.Get();
            foreach (GameplayLevelDefinition level in m_levelManagerRef.Get().GetUnlockedLevels())
            {
                if (!level.ShowInLevelSelect)
                    continue;

                foreach (MissionDefinition mission in level.MissionList.GetCompleteMissions(level))
                {
                    if (IsValidLeaderboard(mission.LeaderboardIdentifier))
                    {
                        SaveDataLevelMission missionSaveData = missionManager.GetSaveDataForMission(mission.GetGUID(), level.GetGUID());
                        SubmitBestTime(mission, missionSaveData);
                    }
                }
            }
        }

        private void SystemShutdown(object context = null)
        {
            m_timeTrialLeaderboard.Shutdown();
        }

        public bool IsReady()
        {
            return m_timeTrialLeaderboard.IsReady();
        }

        public void ShowLeaderboard(LeaderboardIdentifier leaderboardIdentifier)
        {
            m_timeTrialLeaderboard.ShowLeaderboard(leaderboardIdentifier);
        }

        public void SubmitBestTime(MissionDefinition missionDefinition, SaveDataLevelMission missionSaveData)
        {
            LeaderboardIdentifier leaderboardIdentifier = missionDefinition.LeaderboardIdentifier;
            if (!IsValidLeaderboard(leaderboardIdentifier))
                return;

            // The identifier gate precedes the save read. The shipped comparison is strict.
            float bestTimeSeconds = missionSaveData.BestTimeSeconds;
            if (IsValidBestTime(missionDefinition.TheoreticalBestTimePossibleSeconds, bestTimeSeconds))
                m_timeTrialLeaderboard.SubmitBestTime(leaderboardIdentifier, bestTimeSeconds);
        }

        public void IssueChallenge(LeaderboardIdentifier leaderboardIdentifier)
        {
            m_timeTrialLeaderboard.IssueChallenge(leaderboardIdentifier);
        }

        private static bool IsValidLeaderboard(LeaderboardIdentifier leaderboardIdentifier)
        {
            return (int)leaderboardIdentifier != 0;
        }

        private static bool IsValidBestTime(float theoreticalBestTimeSeconds, float bestTimeSeconds)
        {
            return bestTimeSeconds > 0f && bestTimeSeconds > theoreticalBestTimeSeconds;
        }
    }
}
