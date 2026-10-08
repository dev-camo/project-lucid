using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020000f1. These are the shipped service
    // operations; offline behavior belongs at the separate adapter boundary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class GameCenterLeaderboard : ISystem, IGameCenterLeaderboardReferencesHolder
    {
        public const string Separator = "|";
        public static readonly string[] SeparatorArray = { Separator };
        public const string DateFormatString = "o";
        private readonly IGameCenterLeaderboardListener m_gameCenterLeaderboardListener;
        private readonly INativeGameCenterLeaderboard m_nativeGameCenterLeaderboard;
        private readonly bool m_isStub;

        // 0x06000559..0x0600055c: explicit original reference-holder slots.
        bool IGameCenterLeaderboardReferencesHolder.AreGameCenterLeaderboardReferencesValid => IsGameCenterLeaderboardValid();
        INativeGameCenterLeaderboard IGameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard => m_nativeGameCenterLeaderboard;
        IGameCenterLeaderboardListener IGameCenterLeaderboardReferencesHolder.GameCenterLeaderboardListener => m_gameCenterLeaderboardListener;
        bool IGameCenterLeaderboardReferencesHolder.IsStub => m_isStub;

        // 0x0600055d: the identity helper is inlined in both native forms.
        // Listener creation and its callback-handler cast precede native
        // creation. The constructor subscribes shutdown but does not initialise.
        public GameCenterLeaderboard(
            IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder,
            IDebugGameCenterLeaderboard debugGameCenterLeaderboard = null,
            bool forceStub = false)
        {
            m_isStub = GameCenterLeaderboardUtility.IsStubNeeded(forceStub);
            m_gameCenterLeaderboardListener = GameCenterLeaderboardUtility.CreateGameCenterLeaderboardListener(
                this, gameCenterLocalPlayerReferencesHolder, m_isStub);
            m_nativeGameCenterLeaderboard = GameCenterLeaderboardUtility.CreateNativeGameCenterLeaderboard(
                debugGameCenterLeaderboard,
                m_gameCenterLeaderboardListener as IGameCenterLeaderboardListenerCallbackHandler,
                this, gameCenterLocalPlayerReferencesHolder, m_isStub);
            ProcessManager.SubscribeToAction(this, SystemAction.AppShutdown, OnAppShutdown);
        }

        // 0x0600055e: Clear precedes the managed MonoBehaviour type test.
        // Deinitialise and subscription removal are absent from this path.
        private void OnAppShutdown(object context = null)
        {
            if (!IsGameCenterLeaderboardValid()) return;
            m_gameCenterLeaderboardListener.Clear();
            if (m_gameCenterLeaderboardListener is MonoBehaviour monoBehaviour)
                UnityEngine.Object.Destroy(monoBehaviour.gameObject);
        }

        // 0x0600055f: native is checked first, then the listener reference.
        private bool IsGameCenterLeaderboardValid() =>
            m_nativeGameCenterLeaderboard != null && m_gameCenterLeaderboardListener != null;

        // 0x06000560/0x06000561: reference validity is the only guard.
        public void Initialise()
        {
            if (IsGameCenterLeaderboardValid())
                m_nativeGameCenterLeaderboard.Initialise(m_gameCenterLeaderboardListener.Name, Separator);
        }

        public void Deinitialise()
        {
            if (IsGameCenterLeaderboardValid()) m_nativeGameCenterLeaderboard.Deinitialise();
        }

        // 0x06000562/0x06000563.
        public bool IsAvailable() => IsGameCenterLeaderboardValid() && m_nativeGameCenterLeaderboard.IsAvailable();
        public bool IsLegacyAPI() => IsGameCenterLeaderboardValid() && m_nativeGameCenterLeaderboard.IsLegacyAPI();

        // 0x06000564 and its original iterator/closure 0x06000568..0x06000573.
        // A matching Started event clears the caller's list. A synchronous
        // completion avoids yielding; asynchronous completion yields null.
        // Removals occur only after normal completion, not during disposal or
        // a fault in the native invocation, event handlers, or list operations.
        public IEnumerator LoadLeaderboards(
            IReadOnlyList<string> leaderboardIDs,
            List<Leaderboard> loadedLeaderboards,
            Action<bool> onLoadLeaderboardsCompleted)
        {
            if (!IsGameCenterLeaderboardValid())
            {
                onLoadLeaderboardsCompleted?.Invoke(false);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            m_gameCenterLeaderboardListener.OnLoadLeaderboardsStarted += LoadLeaderboardsStarted;
            m_gameCenterLeaderboardListener.OnLoadLeaderboard += LoadLeaderboard;
            bool loadSuccess = false;
            bool loadLeaderboardsCompleted = false;
            m_gameCenterLeaderboardListener.OnLoadLeaderboardsCompleted += LoadLeaderboardsCompleted;
            m_nativeGameCenterLeaderboard.LoadLeaderboards(hashCodeToCheck, leaderboardIDs);
            while (!loadLeaderboardsCompleted) yield return null;
            m_gameCenterLeaderboardListener.OnLoadLeaderboardsStarted -= LoadLeaderboardsStarted;
            m_gameCenterLeaderboardListener.OnLoadLeaderboard -= LoadLeaderboard;
            m_gameCenterLeaderboardListener.OnLoadLeaderboardsCompleted -= LoadLeaderboardsCompleted;
            onLoadLeaderboardsCompleted?.Invoke(loadSuccess);

            void LoadLeaderboardsStarted(int hashCode)
            {
                if (hashCode == hashCodeToCheck) loadedLeaderboards.Clear();
            }

            void LoadLeaderboard(int hashCode, Leaderboard leaderboard)
            {
                if (hashCode == hashCodeToCheck) loadedLeaderboards.Add(leaderboard);
            }

            void LoadLeaderboardsCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                loadSuccess = success;
                loadLeaderboardsCompleted = true;
            }
        }

        // 0x06000565 and its original iterator/closure 0x0600056c..0x06000579.
        // The original leaves this event subscription in place even after
        // successful completion. Preserve that behavior for source auditing.
        public IEnumerator SubmitScore(
            long score, ulong context, string identifier,
            IReadOnlyList<string> leaderboardIDs,
            Action<bool> onSubmitScoreCompleted)
        {
            if (!IsGameCenterLeaderboardValid())
            {
                onSubmitScoreCompleted?.Invoke(false);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            bool submitSuccess = false;
            bool submitScoreCompleted = false;
            m_gameCenterLeaderboardListener.OnSubmitScoreToLeaderboardsCompleted += SubmitScoreCompleted;
            m_nativeGameCenterLeaderboard.SubmitScore(hashCodeToCheck, score, context, identifier, leaderboardIDs);
            while (!submitScoreCompleted) yield return null;
            onSubmitScoreCompleted?.Invoke(submitSuccess);

            void SubmitScoreCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                submitSuccess = success;
                submitScoreCompleted = true;
            }
        }

        // 0x06000566: direct native slot invocation, with no validity guard.
        public void IssueChallenge(string leaderboardId, string message, UnityHLLeaderboardChallengeIssuedCallback callback) =>
            m_nativeGameCenterLeaderboard.IssueLeaderboardChallenge(leaderboardId, message, callback);
    }
}
