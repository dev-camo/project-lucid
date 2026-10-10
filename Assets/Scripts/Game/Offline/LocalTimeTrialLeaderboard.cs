using System;
using Hardlight;
using HardlightProject;
using UnityEngine.SocialPlatforms;

namespace ProjectLucid.Offline
{
    internal sealed class LocalTimeTrialLeaderboard : ITimeTrialLeaderboard
    {
        private readonly SystemRef<LeaderboardManager> manager = ProcessManager.GetSystemRef<LeaderboardManager>();
        public bool IsReady() => manager.IsValid() && LocalPersonalRecords.IsReady;
        public void SubmitBestTime(LeaderboardIdentifier identifier, float bestTimeSeconds)
        {
            if (manager.TryGet(out LeaderboardManager value))
                value.ReportScore(Convert.ToInt64(bestTimeSeconds * 100f), identifier);
        }
        public void ShowLeaderboard(LeaderboardIdentifier identifier)
        {
            if (manager.TryGet(out LeaderboardManager value)) value.ShowLeaderboardUI(identifier, TimeScope.AllTime);
        }
        public void IssueChallenge(LeaderboardIdentifier identifier)
        {
            if (manager.TryGet(out LeaderboardManager value)) value.IssueChallenge(identifier.GetString(), "Platform challenges are unavailable offline.");
        }
        public void Shutdown() { }
    }
}
