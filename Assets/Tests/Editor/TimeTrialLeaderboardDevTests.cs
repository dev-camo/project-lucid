using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // TimeTrialLeaderboardDev (x86-64 0x5C4810..0x5C4B50): the development leaderboard keeps one label per
    // leaderboard in a dictionary the debug menu would list. The [Conditional] debug menu calls are absent from
    // the retail bodies, so the dictionary is the observable state. The label text uses the timer format of the
    // string table, which is empty here, so only the name and separator are asserted literally.
    public sealed class TimeTrialLeaderboardDevTests
    {
        private static Dictionary<LeaderboardIdentifier, string> Labels(TimeTrialLeaderboardDev leaderboard) =>
            Get<Dictionary<LeaderboardIdentifier, string>>(leaderboard, "m_leaderboardsByLabel");

        // IsReady 0x5C4900 is the constant true; IssueChallenge 0x5C4B00 is a lone ret.
        [Test]
        public void TheDevLeaderboardIsAlwaysReadyAndIgnoresChallenges()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<DebugMenu>();
                var leaderboard = new TimeTrialLeaderboardDev();
                Assert.That(leaderboard.IsReady(), Is.True);
                Assert.DoesNotThrow(() => leaderboard.IssueChallenge(LeaderboardIdentifier.TimeTrials_Z1A1M5));
                Assert.That(Labels(leaderboard), Is.Empty);
            });
        }

        // SubmitBestTime 0x5C4910 removes an existing label (TryGetValue 0x5C4977, Remove 0x5C4990) before the
        // Add (0x5C4A1A), so a second submission replaces rather than throwing on a duplicate key; the label is
        // the leaderboard's name, ": " (0x32833f0) and the timer text. Shutdown 0x5C4B10 clears the dictionary.
        [Test]
        public void ASubmittedTimeReplacesTheLabelOfItsLeaderboardAndShutdownClearsThem()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<DebugMenu>();
                var leaderboard = new TimeTrialLeaderboardDev();
                leaderboard.SubmitBestTime(LeaderboardIdentifier.TimeTrials_Z1A1M5, 61.5f);
                Assert.That(Labels(leaderboard).Keys, Is.EqualTo(new[] { LeaderboardIdentifier.TimeTrials_Z1A1M5 }));
                Assert.That(Labels(leaderboard)[LeaderboardIdentifier.TimeTrials_Z1A1M5],
                    Does.StartWith(LeaderboardIdentifier.TimeTrials_Z1A1M5.GetString() + ": "));

                Assert.DoesNotThrow(() => leaderboard.SubmitBestTime(LeaderboardIdentifier.TimeTrials_Z1A1M5, 59.25f));
                leaderboard.SubmitBestTime(LeaderboardIdentifier.TimeTrials_Z1A2M5, 70f);
                Assert.That(Labels(leaderboard).Count, Is.EqualTo(2), "one label per leaderboard");

                leaderboard.Shutdown();
                Assert.That(Labels(leaderboard), Is.Empty);
            });
        }

        // ShowLeaderboard 0x5C4AC0 tail-calls SystemRef.Get on the DebugMenu reference and discards the result;
        // no menu is opened by the retail body.
        [Test]
        public void ShowingALeaderboardOnlyReadsTheDebugMenuReference()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<DebugMenu>();
                var menu = owned.Register(owned.Host("Lucid debug menu", false).AddComponent<DebugMenu>());
                var leaderboard = new TimeTrialLeaderboardDev();
                string pathBefore = DebugMenu.CurrentMenuPath;
                Assert.DoesNotThrow(() => leaderboard.ShowLeaderboard(LeaderboardIdentifier.TimeTrials_Z1A1M5));
                Assert.That(menu.IsShown, Is.False);
                Assert.That(DebugMenu.CurrentMenuPath, Is.EqualTo(pathBefore));
            });
        }
    }
}
