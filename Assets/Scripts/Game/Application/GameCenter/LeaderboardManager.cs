using System;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.SocialPlatforms.GameCenter;

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
        public LeaderboardManager()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-045: the original registered itself and started the GameCenter leaderboard; we register through the local leaderboard registration
            {
                ProjectLucid.Offline.LocalLeaderboardRegistration.Initialise(this,
                    InitGameCenterLeaderboard, Shutdown);
                return;
            }
            ProcessManager.RegisterSystem(this, null, false, false);
            InitGameCenterLeaderboard();
            ProcessManager.SubscribeToAction(this, SystemAction.Shutdown,
                new Action<object>(Shutdown));
        }

        // 0x0600061b: TryGet and startup subscription are separate original
        // operations. InvokeOnValid would change that ordering and race window.
        private void InitGameCenterLeaderboard()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-046: the original created the GameCenter leaderboard once the local player was ready; we require the local personal records to be ready
            {
                ProjectLucid.Offline.LocalPersonalRecords.RequireReady();
                return;
            }
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

        // 0x0600061c: original delayed-start callback selects the real default
        // factory route. It does not silently force the shipping stub.
        private void InitGameCenterLeaderboard(GameCenterLocalPlayer gameCenterLocalPlayer)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-047: the original created the GameCenter leaderboard for the started local player; we require the local personal records to be ready
            {
                ProjectLucid.Offline.LocalPersonalRecords.RequireReady();
                return;
            }
            ProcessManager.RegisterSystem(
                new GameCenterLeaderboard(gameCenterLocalPlayer, null, false),
                null, false, false);
        }

        // 0x0600061d: only remove the pending callback while the local player
        // is invalid. The Core leaderboard itself is not unregistered here.
        private void Shutdown(object objectContext)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-048: the original removed the pending startup callback and unregistered; we unsubscribe and unregister only this object's default-name registration
            {
                // Offline policy owns only this object's default-name registration.
                // A separately named registration of the same object stays valid.
                ProcessManager.UnsubscribeFromAllActions(this);
                string name = ProcessManager.GetDefaultName<LeaderboardManager>();
                if (ReferenceEquals(ProcessManager.GetSystemRef(name, false).GetSafe(), this))
                    ProcessManager.UnregisterSystem(name);
                return;
            }
            SystemRef<GameCenterLocalPlayer> localPlayerRef =
                ProcessManager.GetSystemRef<GameCenterLocalPlayer>(null, true);
            if (!localPlayerRef.IsValid())
                localPlayerRef.OnSystemStartup -=
                    new Action<GameCenterLocalPlayer>(InitGameCenterLeaderboard);
            ProcessManager.UnregisterSystem(this);
        }

        // 0x0600061e; genuine closure 0x020000b8/0x06000625..0x06000626.
        // Preserve the captured account and identifier, failure-only getters,
        // six-part concatenation, and the original error text and query order.
        public void ReportScore(long score, LeaderboardIdentifier leaderboardIdentifier)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-049: the original reported the score to Social; we record it as a local personal record
            {
                ProjectLucid.Offline.LocalPersonalRecords.Report(score, leaderboardIdentifier);
                return;
            }
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

        // 0x0600061f: the original platform UI has no login preflight here.
        public void ShowLeaderboardUI(LeaderboardIdentifier leaderboardIdentifier, TimeScope timeScope)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-050: the original opened the GameCenter leaderboard UI; we raise the local records presentation
            {
                ProjectLucid.Offline.LocalProfilePresentation.ShowRecords(leaderboardIdentifier);
                return;
            }
            GameCenterPlatform.ShowLeaderboardUI(leaderboardIdentifier.GetString(), timeScope);
        }

        // 0x06000620: availability of the real Core service gates this call;
        // authenticated-account state is not independently checked.
        public void IssueChallenge(string leaderboardIdentifier, string message)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-051: the original asked the GameCenter leaderboard to issue a challenge; we report it as unavailable
            {
                IssueChallengeCompletedCallback(false, "Platform leaderboard challenges are unavailable offline.");
                return;
            }
            GameCenterLeaderboard leaderboard = m_gameCenterLeaderboardRef.GetSafe();
            if (leaderboard != null)
                leaderboard.IssueChallenge(leaderboardIdentifier, message,
                    new UnityHLLeaderboardChallengeIssuedCallback(IssueChallengeCompletedCallback));
        }

        // 0x06000621: assign the out value before the distinct validity check.
        private bool TryGetLoggedInGameCenter(out GameCenter gameCenter)
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-052: the original required a logged-in GameCenter account; the local profile never is
            {
                gameCenter = m_gameCenterRef.GetSafe();
                return false;
            }
            gameCenter = m_gameCenterRef.GetSafe();
            return m_gameCenterRef.IsValid() && gameCenter.IsLoggedIn();
        }

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
