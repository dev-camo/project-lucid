using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterLocalPlayer : ISystem, IGameCenterLocalPlayerReferencesHolder
    {
        public const string Separator = "|";
        public static readonly string[] SeparatorArray = { Separator };
        // Original 0x060006fa; both native forms inline GetAuthenticated.
        public bool Authenticated => GetAuthenticated();

        public event Action<bool> OnAuthenticateHandler;
        public event Action OnLocalPlayerDidChange;
        public event Action<string> OnPlayerDidChange;
        private readonly IGameCenterLocalPlayerListener m_gameCenterLocalPlayerListener;
        private readonly INativeGameCenterLocalPlayer m_nativeGameCenterLocalPlayer;
        private Player m_localPlayer;
        private bool m_isLocalPlayerCached;

        INativeGameCenterLocalPlayer IGameCenterLocalPlayerReferencesHolder.NativeGameCenterLocalPlayer => m_nativeGameCenterLocalPlayer;
        IGameCenterLocalPlayerListener IGameCenterLocalPlayerReferencesHolder.GameCenterLocalPlayerListener => m_gameCenterLocalPlayerListener;
        bool IGameCenterLocalPlayerReferencesHolder.AreGameCenterLocalPlayerReferencesValid => IsGameCenterLocalPlayerValid();

        // Original 0x06000704; listener construction precedes callback-handler
        // cast, native construction, then the two authored app subscriptions.
        public GameCenterLocalPlayer(bool forceStub = false)
        {
            m_gameCenterLocalPlayerListener = GameCenterLocalPlayerUtility.CreateGameCenterLocalPlayerListener(forceStub);
            m_nativeGameCenterLocalPlayer = GameCenterLocalPlayerUtility.CreateNativeGameCenterLocalPlayer(
                m_gameCenterLocalPlayerListener as IGameCenterLocalPlayerListenerCallbackHandler, forceStub);
            ProcessManager.SubscribeToAction(this, SystemAction.AppInitialise, OnAppInitialise);
            ProcessManager.SubscribeToAction(this, SystemAction.AppShutdown, OnAppShutdown);
        }

        // Original 0x06000705; no native Initialise call occurs here.
        private void OnAppInitialise(object context = null)
        {
            if (!IsGameCenterLocalPlayerValid()) return;
            m_gameCenterLocalPlayerListener.OnAuthenticateHandler += CheckAuthentication;
            m_gameCenterLocalPlayerListener.OnPlayerDidChange += TriggerPlayerDidChangeEvents;
        }

        // Original 0x06000706; clear follows both removals. The MonoBehaviour
        // test is a managed type test, retaining a destroyed wrapper's path.
        private void OnAppShutdown(object context = null)
        {
            if (!IsGameCenterLocalPlayerValid()) return;
            m_gameCenterLocalPlayerListener.OnPlayerDidChange -= TriggerPlayerDidChangeEvents;
            m_gameCenterLocalPlayerListener.OnAuthenticateHandler -= CheckAuthentication;
            m_gameCenterLocalPlayerListener.Clear();
            if (m_gameCenterLocalPlayerListener is MonoBehaviour monoBehaviour)
                UnityEngine.Object.Destroy(monoBehaviour.gameObject);
        }

        // Original 0x06000707; refresh and its callback precede authentication.
        private void CheckAuthentication(bool authenticated)
        {
            if (authenticated) RefreshLocalPlayerCache();
            OnAuthenticateHandler?.Invoke(authenticated);
        }

        // Original 0x06000708; String.Equals is invoked on the supplied
        // identifier only after TryGetLocalPlayer succeeds (null can fault).
        private void TriggerPlayerDidChangeEvents(string identifier)
        {
            if (TryGetLocalPlayer(out Player localPlayer) && identifier.Equals(localPlayer.GamePlayerID))
                RefreshLocalPlayerCache();
            OnPlayerDidChange?.Invoke(identifier);
        }

        // Original 0x06000709; plain managed reference checks, native first.
        private bool IsGameCenterLocalPlayerValid() =>
            m_nativeGameCenterLocalPlayer != null && m_gameCenterLocalPlayerListener != null;

        // Original 0x0600070a; Name is evaluated before the native invocation.
        public void Initialise()
        {
            if (IsGameCenterLocalPlayerValid())
                m_nativeGameCenterLocalPlayer.Initialise(m_gameCenterLocalPlayerListener.Name, Separator);
        }

        // Original 0x0600070b.
        public void Deinitialise()
        {
            if (IsGameCenterLocalPlayerValid()) m_nativeGameCenterLocalPlayer.Deinitialise();
        }

        // Original 0x0600070c; native call then parsing, without authentication.
        public bool TryGetPlayer(string identifier, out Player player)
        {
            player = default;
            if (!IsGameCenterLocalPlayerValid()) return false;
            return Player.TryParseProperties(m_nativeGameCenterLocalPlayer.GetPlayerPropertiesJoin(identifier), identifier, this, out player);
        }

        // Original 0x0600070d/0x0600070e.
        public void RegisterAuthenticateHandler()
        {
            if (IsGameCenterLocalPlayerValid()) m_nativeGameCenterLocalPlayer.RegisterAuthenticateHandler();
        }
        public void UnregisterAuthenticateHandler()
        {
            if (IsGameCenterLocalPlayerValid()) m_nativeGameCenterLocalPlayer.UnregisterAuthenticateHandler();
        }

        // Original 0x0600070f.
        private bool GetAuthenticated() => IsGameCenterLocalPlayerValid() && m_nativeGameCenterLocalPlayer.Authenticated;

        // Original 0x06000710/0x06000711/0x06000712; each output initially true.
        public bool TryGetUnderage(out bool underage)
        {
            underage = true;
            if (!IsGameCenterLocalPlayerValid() || !GetAuthenticated()) return false;
            underage = m_nativeGameCenterLocalPlayer.Underage;
            return true;
        }
        public bool TryGetMultiplayerGamingRestricted(out bool multiplayerGamingRestricted)
        {
            multiplayerGamingRestricted = true;
            if (!IsGameCenterLocalPlayerValid() || !GetAuthenticated()) return false;
            multiplayerGamingRestricted = m_nativeGameCenterLocalPlayer.MultiplayerGamingRestricted;
            return true;
        }
        public bool TryGetPersonalisedCommunicationRestricted(out bool personalisedCommunicationRestricted)
        {
            personalisedCommunicationRestricted = true;
            if (!IsGameCenterLocalPlayerValid() || !GetAuthenticated()) return false;
            personalisedCommunicationRestricted = m_nativeGameCenterLocalPlayer.PersonalisedCommunicationRestricted;
            return true;
        }

        // Original 0x06000713; no validity guard, two native reads, then parse.
        // The local-player event fires after an unsuccessful parse and its log.
        private void RefreshLocalPlayerCache()
        {
            string propertiesJoin = m_nativeGameCenterLocalPlayer.GetLocalPlayerPropertiesJoin();
            m_isLocalPlayerCached = Player.TryParseProperties(propertiesJoin, m_nativeGameCenterLocalPlayer.GamePlayerID, this, out m_localPlayer);
            if (!m_isLocalPlayerCached)
                HLOutput.LogError("[GameCenterLocalPlayer] RefreshLocalPlayerCache - Local player failed to refresh with properties join '" + propertiesJoin + "'.");
            OnLocalPlayerDidChange?.Invoke();
        }

        // Original 0x06000714; the output is reset before both guards.
        public bool TryGetLocalPlayer(out Player localPlayer)
        {
            localPlayer = default;
            if (!GetAuthenticated() || !m_isLocalPlayerCached) return false;
            localPlayer = m_localPlayer;
            return true;
        }

        // Original 0x06000715; directly invokes native slot14, with no guard.
        public void IssueAchievementChallenge(string achievementId, string message, UnityHLAchievementChallengeIssuedCallback callback) =>
            m_nativeGameCenterLocalPlayer.IssueAchievementChallenge(achievementId, message, callback);
    }
}
