using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.SocialPlatforms;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class TimeTrialLeaderboardApple : ITimeTrialLeaderboard
    {
        private readonly SystemRef<LeaderboardManager> m_leaderBoardManagerRef = ProcessManager.GetSystemRef<LeaderboardManager>();

        public bool IsReady()
        {
            return m_leaderBoardManagerRef.IsValid();
        }

        public void SubmitBestTime(LeaderboardIdentifier leaderboardIdentifier, float bestTimeSeconds)
        {
            if (m_leaderBoardManagerRef.TryGet(out LeaderboardManager leaderboardManager))
            {
                // The original uses float multiplication followed by Convert.ToInt64(float).
                leaderboardManager.ReportScore(Convert.ToInt64(bestTimeSeconds * 100f), leaderboardIdentifier);
            }
        }

        public void ShowLeaderboard(LeaderboardIdentifier leaderboardIdentifier)
        {
            if (m_leaderBoardManagerRef.TryGet(out LeaderboardManager leaderboardManager))
                leaderboardManager.ShowLeaderboardUI(leaderboardIdentifier, TimeScope.AllTime);
        }

        public void IssueChallenge(LeaderboardIdentifier leaderboardIdentifier)
        {
            if (m_leaderBoardManagerRef.TryGet(out LeaderboardManager leaderboardManager))
                leaderboardManager.IssueChallenge(leaderboardIdentifier.GetString(), "TODO: challenge message");
        }

        public void Shutdown()
        {
        }

        public TimeTrialLeaderboardApple()
        {
        }
    }
}
