using System;
using Hardlight;
using UnityEngine.SocialPlatforms;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    // Original Game.Runtime020006ea: whole original Game Center implementation15.
    // Platform/service calls are preserved; no offline route is substituted here.
    public sealed class ChallengeLeaderboardIntegration_GameCenter : ChallengeLeaderboardIntegration
    {
        // Original040018d7 at0x20,readonly ProcessManager reference initialized before base.
        private readonly SystemRef<LeaderboardManager> m_leaderboardSystemRef = ProcessManager.GetSystemRef<LeaderboardManager>(null, true);
        // Original040018d8,private string constant retained with the original RET debug body.
        private const string m_debugMenuPath = "Challenges/Leaderboard";

        // Original0600256a permits a null callback, unlike the development implementation.
        protected override void DoInitialise(Action callback)
        {
            callback?.Invoke();
        }

        // Original0600256b and0600256c are immediate RET in the shipping binaries.
        private void SetupDebugButtons()
        {
        }

        protected override void DoDeinitialise()
        {
        }

        // Original0600256d queries index BEFORE reading epoch, unlike Dev06002562.
        public override DateTime GetCurrentPeriodStart()
        {
            int index = GetOfflineCycleIndex();
            DateTime beginning = m_challengeCycleDefinition.CycleBeginsFrom;
            TimeSpan duration = GetOfflineLeaderboardDuration();
            return beginning + duration * (double)index;
        }

        // Original0600256e reads duration after the virtual start query.
        public override DateTime GetCurrentPeriodEnd()
        {
            DateTime start = GetCurrentPeriodStart();
            return start + GetOfflineLeaderboardDuration();
        }

        // Original0600256f/06002570,literals6021/6020,original HLOutput.LogError.
        public override void RegressCycle()
        {
            HLOutput.LogError("GameCenter leaderboards cannot be regressed.", null);
        }

        public override void AdvanceCycle()
        {
            HLOutput.LogError("GameCenter leaderboards cannot be advanced.", null);
        }

        // Original06002571 forwards the original offline duration property.
        private TimeSpan GetOfflineLeaderboardDuration()
        {
            return m_challengeCycleDefinition.OfflineCycleDuration;
        }

        // Original06002572 queries UtcNow with no debug or server offset.
        public override DateTime GetTime()
        {
            return DateTime.UtcNow;
        }

        // Original06002573 retains direct signed ratio conversion and exceptional holds.
        private int GetOfflineCycleIndex()
        {
            DateTime time = GetTime();
            TimeSpan elapsed = time - m_challengeCycleDefinition.CycleBeginsFrom;
            TimeSpan duration = GetOfflineLeaderboardDuration();
            return unchecked((int)(elapsed / duration));
        }

        // Original06002574 sign extends score Int32 to original ReportScore Int64.
        protected override void DoSubmitScore(int score)
        {
            m_leaderboardSystemRef.Get().ReportScore((long)score, GetLeaderboardIdentifier());
        }

        // Original06002575 retains the real service and passes TimeScope.Today (0).
        public override void ShowLeaderboard()
        {
            m_leaderboardSystemRef.Get().ShowLeaderboardUI(GetLeaderboardIdentifier(), TimeScope.Today);
        }

        // Original06002576 returns original TailsChallenges_Daily0x0feae372.
        private static LeaderboardIdentifier GetLeaderboardIdentifier()
        {
            return LeaderboardIdentifier.TailsChallenges_Daily;
        }

        // Original06002577 delegates to the actual original service login query.
        public override bool CanShowLeaderboard()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-054: the original asked the leaderboard system whether the account is logged in; we report whether the local personal records are ready
            {
                return ProjectLucid.Offline.LocalPersonalRecords.IsReady;
            }
            return m_leaderboardSystemRef.Get().IsLoggedIn();
        }

        // Original06002578 preserves reference(null,true) initialization before base.
        public ChallengeLeaderboardIntegration_GameCenter()
        {
        }
    }
}
