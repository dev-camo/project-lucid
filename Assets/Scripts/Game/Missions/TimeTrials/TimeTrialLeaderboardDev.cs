using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000724: development stand-in that keeps the
    // best times as labels for the debug menu. The debug menu registration
    // calls are [Conditional] and absent from the retail bodies.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class TimeTrialLeaderboardDev : ITimeTrialLeaderboard
    {
        private const string DebugMenuRoot = "Leaderboards";
        private readonly string m_debugMenuTimeTrials = DebugMenuRoot + "/Time Trials";
        private readonly Dictionary<LeaderboardIdentifier, string> m_leaderboardsByLabel =
            new Dictionary<LeaderboardIdentifier, string>();
        private readonly SystemRef<DebugMenu> m_debugMenuRef = ProcessManager.GetSystemRef<DebugMenu>(null, true);

        // 0x0600282b.
        public TimeTrialLeaderboardDev() { }

        // 0x0600282c: always ready.
        public bool IsReady() => true;

        // 0x0600282d: a previous label for the same leaderboard is replaced; the
        // new one is built by GetBestTimeLabel (inlined in the native body).
        public void SubmitBestTime(LeaderboardIdentifier leaderboardIdentifier, float bestTimeSeconds)
        {
            if (m_leaderboardsByLabel.TryGetValue(leaderboardIdentifier, out _))
                m_leaderboardsByLabel.Remove(leaderboardIdentifier);
            m_leaderboardsByLabel.Add(leaderboardIdentifier, GetBestTimeLabel(leaderboardIdentifier, bestTimeSeconds));
        }

        // 0x0600282e: the retail body only evaluates the DebugMenu reference (a
        // missing system faults); no menu call survives in it.
        public void ShowLeaderboard(LeaderboardIdentifier leaderboardIdentifier)
        {
            m_debugMenuRef.Get();
        }

        // 0x0600282f: the original body is empty.
        public void IssueChallenge(LeaderboardIdentifier leaderboardIdentifier) { }

        // 0x06002830.
        public void Shutdown()
        {
            m_leaderboardsByLabel.Clear();
        }

        // 0x06002831: returns the literal; no retail body references it.
        private static string GetDefaultMessage()
        {
            return "No leaderboard best times have been submitted yet.";
        }

        // 0x06002832 (x86-64 0x5c4a30): the timer text is built before the name.
        private static string GetBestTimeLabel(LeaderboardIdentifier leaderboardIdentifier, float bestTime)
        {
            string timer = MissionStringsUtil.GetMissionTimerFormat(bestTime, Strings.MISSION_TIMER_ELAPSED_FORMAT);
            return leaderboardIdentifier.GetString() + ": " + timer;
        }
    }
}
