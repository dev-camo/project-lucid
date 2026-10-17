using System;
using System.Text;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200010b; MethodDefs 0x060005cd..0x06000606
    // in declaration order. The string-taking public methods and the
    // gameObject.name Name suggest a UnitySendMessage receiver (inferred; the
    // shipping native path is GameCenterLeaderboardListenerMacOS with a
    // UnityCallback delegate). GameCenterLeaderboardUtility creates only the
    // MacOS or stub listener, never this component.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterLeaderboardListener : MonoBehaviour, IGameCenterLeaderboardListener
    {
        // 0x060005cd: Component.gameObject precedes Object.name.
        public string Name => gameObject.name;

        public event Action<int> OnLoadLeaderboardsStarted;
        public event Action<int, Leaderboard> OnLoadLeaderboard;
        public event Action<int, bool> OnLoadLeaderboardsCompleted;
        public event Action<int, bool> OnSubmitScoreToLeaderboardsCompleted;
        public event Action<int, Leaderboard> OnLoadPreviousOccurrence;
        public event Action<int, bool> OnLoadPreviousOccurrenceCompleted;
        public event Action<int, bool> OnSubmitScoreToLeaderboardCompleted;
        public event Action<int, Texture2D> OnLoadImage;
        public event Action<int, bool> OnLoadImageCompleted;
        public event Action<int> OnLoadEntriesForPlayerScopeStarted;
        public event Action<int, LeaderboardEntry> OnLoadLocalEntryForPlayerScope;
        public event Action<int, LeaderboardEntry> OnLoadEntryForPlayerScope;
        public event Action<int, bool> OnLoadEntriesForPlayerScopeCompleted;
        public event Action<int> OnLoadEntriesForPlayersStarted;
        public event Action<int, LeaderboardEntry> OnLoadLocalEntryForPlayers;
        public event Action<int, LeaderboardEntry> OnLoadEntryForPlayers;
        public event Action<int, bool> OnLoadEntriesForPlayersCompleted;

        private IGameCenterLeaderboardReferencesHolder m_gameCenterLeaderboardReferencesHolder;
        private IGameCenterLocalPlayerReferencesHolder m_gameCenterLocalPlayerReferencesHolder;
        private int m_hashCode;

        // 0x060005f0/0x060005f1: the first nonnull holder wins until Clear.
        public void SetGameCenterLeaderboardReferencesHolder(IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder)
        {
            if (gameCenterLeaderboardReferencesHolder != null && m_gameCenterLeaderboardReferencesHolder == null)
                m_gameCenterLeaderboardReferencesHolder = gameCenterLeaderboardReferencesHolder;
        }

        public void SetGameCenterLocalPlayerReferencesHolder(IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder)
        {
            if (gameCenterLocalPlayerReferencesHolder != null && m_gameCenterLocalPlayerReferencesHolder == null)
                m_gameCenterLocalPlayerReferencesHolder = gameCenterLocalPlayerReferencesHolder;
        }

        // 0x060005f2: clear the leaderboard holder, then the local player
        // holder. Subscribers and the hash are retained.
        public void Clear()
        {
            m_gameCenterLeaderboardReferencesHolder = null;
            m_gameCenterLocalPlayerReferencesHolder = null;
        }

        // 0x060005f3: allocation precedes the string check, so a failed
        // conversion still returns the texture. Literal TextureFormat 53
        // (ASTC_12x12, aliased by ASTC_RGB_12x12) with no mip chain.
        private static bool TryGetTexture2D(string imageDataString, int height, int width, out Texture2D texture2D)
        {
            texture2D = new Texture2D(width, height, (TextureFormat)53, false);
            if (string.IsNullOrEmpty(imageDataString)) return false;
            byte[] imageData = Encoding.UTF8.GetBytes(imageDataString);
            if (imageData.Length == 0) return false;
            texture2D.LoadRawTextureData(imageData);
            texture2D.Apply();
            return true;
        }

        // 0x060005f4: a failed parse writes zero through the out argument.
        public void SetHashCode(string hashCodeString) => int.TryParse(hashCodeString, out m_hashCode);

        public void LoadLeaderboardsStarted() => OnLoadLeaderboardsStarted?.Invoke(m_hashCode);

        // 0x060005f6: parse before the event null test, so parsing always runs.
        public void LoadLeaderboard(string propertiesJoinToIdentifyLeaderboard)
        {
            if (Leaderboard.TryParseProperties(propertiesJoinToIdentifyLeaderboard, m_gameCenterLeaderboardReferencesHolder, out Leaderboard leaderboard))
                OnLoadLeaderboard?.Invoke(m_hashCode, leaderboard);
        }

        // 0x060005f7/0x060005f8: malformed booleans suppress the completion event.
        public void LoadLeaderboardsCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadLeaderboardsCompleted?.Invoke(m_hashCode, success);
        }

        public void SubmitScoreToLeaderboardsCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnSubmitScoreToLeaderboardsCompleted?.Invoke(m_hashCode, success);
        }

        public void LoadPreviousOccurrence(string propertiesJoinToIdentifyLeaderboard)
        {
            if (Leaderboard.TryParseProperties(propertiesJoinToIdentifyLeaderboard, m_gameCenterLeaderboardReferencesHolder, out Leaderboard leaderboard))
                OnLoadPreviousOccurrence?.Invoke(m_hashCode, leaderboard);
        }

        public void LoadPreviousOccurrenceCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadPreviousOccurrenceCompleted?.Invoke(m_hashCode, success);
        }

        public void SubmitScoreToLeaderboardCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnSubmitScoreToLeaderboardCompleted?.Invoke(m_hashCode, success);
        }

        // 0x060005fc: one native reference is read, height before width. The
        // holder is not null-checked, and the texture is built before the
        // event null test.
        public void LoadImage(string imageDataString)
        {
            INativeGameCenterLeaderboard nativeGameCenterLeaderboard = m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard;
            int height = nativeGameCenterLeaderboard.GetLastImageLoadedHeight();
            int width = nativeGameCenterLeaderboard.GetLastImageLoadedWidth();
            if (TryGetTexture2D(imageDataString, height, width, out Texture2D texture2D))
                OnLoadImage?.Invoke(m_hashCode, texture2D);
        }

        public void LoadImageCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadImageCompleted?.Invoke(m_hashCode, success);
        }

        public void LoadEntriesForPlayerScopeStarted() => OnLoadEntriesForPlayerScopeStarted?.Invoke(m_hashCode);

        // 0x060005ff/0x06000600: entries resolve players through the local
        // player holder, not the leaderboard holder.
        public void LoadLocalEntryForPlayerScope(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadLocalEntryForPlayerScope?.Invoke(m_hashCode, leaderboardEntry);
        }

        public void LoadEntryForPlayerScope(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadEntryForPlayerScope?.Invoke(m_hashCode, leaderboardEntry);
        }

        public void LoadEntriesForPlayerScopeCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadEntriesForPlayerScopeCompleted?.Invoke(m_hashCode, success);
        }

        public void LoadEntriesForPlayersStarted() => OnLoadEntriesForPlayersStarted?.Invoke(m_hashCode);

        public void LoadLocalEntryForPlayers(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadLocalEntryForPlayers?.Invoke(m_hashCode, leaderboardEntry);
        }

        public void LoadEntryForPlayers(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadEntryForPlayers?.Invoke(m_hashCode, leaderboardEntry);
        }

        public void LoadEntriesForPlayersCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadEntriesForPlayersCompleted?.Invoke(m_hashCode, success);
        }

        // 0x06000606: MonoBehaviour constructor only.
        public GameCenterLeaderboardListener() { }
    }
}
