using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalLeaderboardCallbackGapTests
    {
        // LeaderboardManager.IssueChallengeCompletedCallback 0x06000624, x86-64
        // 0x53a880. sendChallenge (esi) is never read: the body tests only
        // String.IsNullOrEmpty(error) 0x53a8c2 and then concatenates the two
        // literals around error 0x53a8eb before HLOutput.LogError 0x53a918.
        // Logging when !sendChallenge, or logging a null/empty error, fails here.
        [Test]
        public void ChallengeCallbackReportsOnlyNonEmptyErrorsRegardlessOfSendFlag()
        {
            using (var log = new OwnedCoreErrorLog())
            {
                LeaderboardManager.IssueChallengeCompletedCallback(false, null);
                LeaderboardManager.IssueChallengeCompletedCallback(false, string.Empty);
                LeaderboardManager.IssueChallengeCompletedCallback(true, null);
                LeaderboardManager.IssueChallengeCompletedCallback(true, string.Empty);
                Assert.That(log.Errors, Is.Empty);

                LeaderboardManager.IssueChallengeCompletedCallback(true, "lucid-sent");
                LeaderboardManager.IssueChallengeCompletedCallback(false, "lucid-unsent");
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    "GameCenterLeaderboard IssueChallenge failed - could not issue challenge: lucid-sent.:0",
                    "GameCenterLeaderboard IssueChallenge failed - could not issue challenge: lucid-unsent.:0",
                }));
            }
        }
    }
}
