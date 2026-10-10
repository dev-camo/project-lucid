namespace HardlightProject
{
    public interface ITimeTrialLeaderboard
    {
        bool IsReady();
        void SubmitBestTime(LeaderboardIdentifier leaderboardIdentifier, float bestTimeSeconds);
        void ShowLeaderboard(LeaderboardIdentifier leaderboardIdentifier);
        void IssueChallenge(LeaderboardIdentifier leaderboardIdentifier);
        void Shutdown();
    }
}
