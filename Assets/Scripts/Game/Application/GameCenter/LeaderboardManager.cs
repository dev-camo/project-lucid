using System;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.SocialPlatforms;
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
using UnityEngine.SocialPlatforms.GameCenter;
#endif

namespace HardlightProject
{
    // Original Game.Runtime 0x020000b7, shipping release 1.10.1.
    // Preserve the Apple leaderboard flow for research. Offline personal
    // records belong behind a separate adapter, retaining this implementation.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class LeaderboardManager : ISystem
    {
        // Original 0x040003bb/0x040003bc. Both lookups run before Object's
        // constructor, in this order; keep genuine instance field initializers.
        private readonly SystemRef<GameCenter> m_gameCenterRef =
            ProcessManager.GetSystemRef<GameCenter>(null, true);
        private readonly SystemRef<GameCenterLeaderboard> m_gameCenterLeaderboardRef =
            ProcessManager.GetSystemRef<GameCenterLeaderboard>(null, true);

        // 0x0600061a: publish first, initialize next, subscribe last. The
        // original action is Shutdown (two), distinct from AppShutdown (four).
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public LeaderboardManager()
        {
            ProcessManager.RegisterSystem(this, null, false, false);
            InitGameCenterLeaderboard();
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown,
                new Action<object>(Shutdown));
        }
#else
        public LeaderboardManager()
        {
            ProjectLucid.Offline.LocalLeaderboardRegistration.Initialise(this,
                InitGameCenterLeaderboard, Shutdown);
        }
#endif

        // 0x0600061b: TryGet and startup subscription are separate original
        // operations. InvokeOnValid would change that ordering and race window.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void InitGameCenterLeaderboard()
        {
            SystemRef<GameCenterLocalPlayer> localPlayerRef =
                ProcessManager.GetSystemRef<GameCenterLocalPlayer>(null, true);
            if (localPlayerRef.TryGet(out GameCenterLocalPlayer gameCenterLocalPlayer))
                ProcessManager.RegisterSystem(
                    new GameCenterLeaderboard(gameCenterLocalPlayer, null, false),
                    null, false, false);
            else
                localPlayerRef.OnSystemStartup +=
                    new Action<GameCenterLocalPlayer>(InitGameCenterLeaderboard);
        }
#else
        private void InitGameCenterLeaderboard()
        {
            ProjectLucid.Offline.LocalPersonalRecords.RequireReady();
        }
#endif

        // 0x0600061c: original delayed-start callback selects the real default
        // factory route. It does not silently force the shipping stub.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void InitGameCenterLeaderboard(GameCenterLocalPlayer gameCenterLocalPlayer)
        {
            ProcessManager.RegisterSystem(
                new GameCenterLeaderboard(gameCenterLocalPlayer, null, false),
                null, false, false);
        }
#else
        private void InitGameCenterLeaderboard(GameCenterLocalPlayer gameCenterLocalPlayer)
        {
            ProjectLucid.Offline.LocalPersonalRecords.RequireReady();
        }
#endif

        // 0x0600061d: only remove the pending callback while the local player
        // is invalid. The Core leaderboard itself is not unregistered here.
#if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private void Shutdown(object objectContext)
        {
            SystemRef<GameCenterLocalPlayer> localPlayerRef =
                ProcessManager.GetSystemRef<GameCenterLocalPlayer>(null, true);
            if (!localPlayerRef.IsValid())
                localPlayerRef.OnSystemStartup -=
                    new Action<GameCenterLocalPlayer>(InitGameCenterLeaderboard);
            ProcessManager.UnregisterSystem(this);
        }
#else
        private void Shutdown(object objectContext)
        {
            // Offline policy owns only this object's default-name registration.
            // A separately named registration of the same object stays valid.
            ProcessManager.UnsubscribeFromAllActions(this);
            string name = ProcessManager.GetDefaultName<LeaderboardManager>();
            if (ReferenceEquals(ProcessManager.GetSystemRef(name, false).GetSafe(), this))
                ProcessManager.UnregisterSystem(name);
        }
#endif

        // 0x0600061e; genuine closure 0x020000b8/0x06000625..0x06000626.
        // Preserve the captured account and identifier, failure-only getters,
        // six-part concatenation, and the original error text and query order.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void ReportScore(long score, LeaderboardIdentifier leaderboardIdentifier)
        {
            if (TryGetLoggedInGameCenter(out GameCenter gameCenter))
                Social.ReportScore(score, leaderboardIdentifier.GetString(),
                    new Action<bool>(success =>
                    {
                        if (!success)
                            HLOutput.LogError(string.Concat(new string[]
                            {
                                "LeaderboardManager ReportScore failed - could not report score for user ",
                                gameCenter.GetAccountNickname(),
                                " (ID: ",
                                gameCenter.GetAccountId(),
                                ") on leaderboard ",
                                leaderboardIdentifier.GetString()
                            }), null);
                    }));
        }
#else
        public void ReportScore(long score, LeaderboardIdentifier leaderboardIdentifier)
        {
            ProjectLucid.Offline.LocalPersonalRecords.Report(score, leaderboardIdentifier);
        }
#endif

        // 0x0600061f: the original platform UI has no login preflight here.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void ShowLeaderboardUI(LeaderboardIdentifier leaderboardIdentifier, TimeScope timeScope)
        {
            GameCenterPlatform.ShowLeaderboardUI(leaderboardIdentifier.GetString(), timeScope);
        }
#else
        public void ShowLeaderboardUI(LeaderboardIdentifier leaderboardIdentifier, TimeScope timeScope)
        {
            ProjectLucid.Offline.LocalProfilePresentation.ShowRecords(leaderboardIdentifier);
        }
#endif

        // 0x06000620: availability of the real Core service gates this call;
        // authenticated-account state is not independently checked.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        public void IssueChallenge(string leaderboardIdentifier, string message)
        {
            GameCenterLeaderboard leaderboard = m_gameCenterLeaderboardRef.GetSafe();
            if (leaderboard != null)
                leaderboard.IssueChallenge(leaderboardIdentifier, message,
                    new UnityHLLeaderboardChallengeIssuedCallback(IssueChallengeCompletedCallback));
        }
#else
        public void IssueChallenge(string leaderboardIdentifier, string message)
        {
            IssueChallengeCompletedCallback(false, "Platform leaderboard challenges are unavailable offline.");
        }
#endif

        // 0x06000621: assign the out value before the distinct validity check.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        private bool TryGetLoggedInGameCenter(out GameCenter gameCenter)
        {
            gameCenter = m_gameCenterRef.GetSafe();
            return m_gameCenterRef.IsValid() && gameCenter.IsLoggedIn();
        }
#else
        private bool TryGetLoggedInGameCenter(out GameCenter gameCenter)
        {
            gameCenter = m_gameCenterRef.GetSafe();
            return false;
        }
#endif

        // 0x06000622.
        public bool IsLoggedIn() => TryGetLoggedInGameCenter(out GameCenter gameCenter);

        // 0x06000623: the retail compiler removed debug menu calls but retained
        // complete non-generic enumeration (including Current and disposal).
        // Typed enum casts or invented debug callbacks are not evidenced.
        [Conditional("BUILD_DEVELOPMENT")]
        private void AddDebugButtons()
        {
            foreach (object ignored in Enum.GetValues(typeof(LeaderboardIdentifier))) { }
        }

        // 0x06000624: the original AOT attribute names Action<bool,string>,
        // although the native request uses the studio's distinct delegate.
        // sendChallenge is ignored; only a nonempty error is reported.
        [AOT.MonoPInvokeCallback(typeof(Action<bool, string>))]
        public static void IssueChallengeCompletedCallback(bool sendChallenge, string error)
        {
            if (!string.IsNullOrEmpty(error))
                HLOutput.LogError(string.Concat(
                    "GameCenterLeaderboard IssueChallenge failed - could not issue challenge: ",
                    error, "."), null);
        }
    }
}
