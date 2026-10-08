using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200010c, reconstructed from both native architectures.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterLeaderboardListenerMacOS : IGameCenterLeaderboardListener
    {
        public delegate void UnityCallback(IntPtr methodNamePtr, IntPtr methodParamPtr);

        // 0x06000607: this original listener has no GameObject name.
        public string Name => null;

        public event Action<int> OnLoadLeaderboardsStarted;
        public event Action<int,Leaderboard> OnLoadLeaderboard;
        public event Action<int,bool> OnLoadLeaderboardsCompleted;
        public event Action<int,bool> OnSubmitScoreToLeaderboardsCompleted;
        public event Action<int,Leaderboard> OnLoadPreviousOccurrence;
        public event Action<int,bool> OnLoadPreviousOccurrenceCompleted;
        public event Action<int,bool> OnSubmitScoreToLeaderboardCompleted;
        public event Action<int,Texture2D> OnLoadImage;
        public event Action<int,bool> OnLoadImageCompleted;
        public event Action<int> OnLoadEntriesForPlayerScopeStarted;
        public event Action<int,LeaderboardEntry> OnLoadLocalEntryForPlayerScope;
        public event Action<int,LeaderboardEntry> OnLoadEntryForPlayerScope;
        public event Action<int,bool> OnLoadEntriesForPlayerScopeCompleted;
        public event Action<int> OnLoadEntriesForPlayersStarted;
        public event Action<int,LeaderboardEntry> OnLoadLocalEntryForPlayers;
        public event Action<int,LeaderboardEntry> OnLoadEntryForPlayers;
        public event Action<int,bool> OnLoadEntriesForPlayersCompleted;

        private IGameCenterLeaderboardReferencesHolder m_gameCenterLeaderboardReferencesHolder;
        private IGameCenterLocalPlayerReferencesHolder m_gameCenterLocalPlayerReferencesHolder;
        private int m_hashCode;
        private static IReadOnlyDictionary<string, Action<string>> s_stringCallbackMap;
        private static IReadOnlyDictionary<string, Action> s_callbackMap;

        // 0x0600062a: each map is published after its ordered additions complete.
        // Constructing another listener replaces the maps shared by native callbacks.
        public GameCenterLeaderboardListenerMacOS()
        {
            s_stringCallbackMap = new Dictionary<string, Action<string>>
            {
                { "SetHashCode", SetHashCode },
                { "LoadLeaderboard", LoadLeaderboard },
                { "LoadLeaderboardsCompleted", LoadLeaderboardsCompleted },
                { "SubmitScoreToLeaderboardsCompleted", SubmitScoreToLeaderboardsCompleted },
                { "LoadPreviousOccurrence", LoadPreviousOccurrence },
                { "LoadPreviousOccurrenceCompleted", LoadPreviousOccurrenceCompleted },
                { "SubmitScoreToLeaderboardCompleted", SubmitScoreToLeaderboardCompleted },
                { "LoadImage", LoadImage },
                { "LoadImageCompleted", LoadImageCompleted },
                { "LoadLocalEntryForPlayerScope", LoadLocalEntryForPlayerScope },
                { "LoadEntryForPlayerScope", LoadEntryForPlayerScope },
                { "LoadEntriesForPlayerScopeCompleted", LoadEntriesForPlayerScopeCompleted },
                { "LoadLocalEntryForPlayers", LoadLocalEntryForPlayers },
                { "LoadEntryForPlayers", LoadEntryForPlayers },
                { "LoadEntriesForPlayersCompleted", LoadEntriesForPlayersCompleted }
            };
            s_callbackMap = new Dictionary<string, Action>
            {
                { "LoadLeaderboardsStarted", LoadLeaderboardsStarted },
                { "LoadEntriesForPlayerScopeStarted", LoadEntriesForPlayerScopeStarted },
                { "LoadEntriesForPlayersStarted", LoadEntriesForPlayersStarted }
            };
        }

        // 0x0600062b: convert both pointers before selecting the map by parameter emptiness.
        // The original diagnostic names LocalPlayer even in this leaderboard listener.
        [MonoPInvokeCallback(typeof(UnityCallback))]
        public static void Callback(IntPtr methodNamePtr, IntPtr methodParamPtr)
        {
            string methodName = Marshal.PtrToStringAuto(methodNamePtr);
            string methodParam = Marshal.PtrToStringAuto(methodParamPtr);
            if (string.IsNullOrEmpty(methodParam))
            {
                if (s_callbackMap.TryGetValue(methodName, out Action method))
                    method();
                else
                    HLOutput.LogError("[GameCenterLocalPlayer] GameCenterLocalPlayerCallback - No method with name '" + methodName + "'.");
            }
            else
            {
                if (s_stringCallbackMap.TryGetValue(methodName, out Action<string> method))
                    method(methodParam);
                else
                    HLOutput.LogError("[GameCenterLocalPlayer] GameCenterLocalPlayerCallback - No method with string param and name '" + methodName + "'.");
            }
        }

        // 0x0600062c/0x0600062d: the first nonnull holder wins until Clear.
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

        // 0x0600062e: retain subscribers, hash and shared maps, clearing holders in this order.
        public void Clear()
        {
            m_gameCenterLeaderboardReferencesHolder = null;
            m_gameCenterLocalPlayerReferencesHolder = null;
        }

        // 0x0600062f: allocation precedes the string check; literal texture format 53,
        // raw UTF8 bytes and Apply are present in both original native implementations.
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

        // 0x06000630: failed parsing resets the field through the out argument.
        private void SetHashCode(string hashCodeString) => int.TryParse(hashCodeString, out m_hashCode);

        // 0x06000631.
        private void LoadLeaderboardsStarted() => OnLoadLeaderboardsStarted?.Invoke(m_hashCode);

        // 0x06000632: parse even when the corresponding event has no subscribers.
        private void LoadLeaderboard(string propertiesJoinToIdentifyLeaderboard)
        {
            if (Leaderboard.TryParseProperties(propertiesJoinToIdentifyLeaderboard, m_gameCenterLeaderboardReferencesHolder, out Leaderboard leaderboard))
                OnLoadLeaderboard?.Invoke(m_hashCode, leaderboard);
        }

        // 0x06000633/0x06000634: malformed booleans suppress the completion event.
        private void LoadLeaderboardsCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnLoadLeaderboardsCompleted?.Invoke(m_hashCode, success);
        }
        private void SubmitScoreToLeaderboardsCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnSubmitScoreToLeaderboardsCompleted?.Invoke(m_hashCode, success);
        }

        // 0x06000635.
        private void LoadPreviousOccurrence(string propertiesJoinToIdentifyLeaderboard)
        {
            if (Leaderboard.TryParseProperties(propertiesJoinToIdentifyLeaderboard, m_gameCenterLeaderboardReferencesHolder, out Leaderboard leaderboard))
                OnLoadPreviousOccurrence?.Invoke(m_hashCode, leaderboard);
        }

        // 0x06000636/0x06000637.
        private void LoadPreviousOccurrenceCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnLoadPreviousOccurrenceCompleted?.Invoke(m_hashCode, success);
        }
        private void SubmitScoreToLeaderboardCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnSubmitScoreToLeaderboardCompleted?.Invoke(m_hashCode, success);
        }

        // 0x06000638: query height before width through one native reference. Image
        // allocation/loading precedes the event check and does not complete the request.
        private void LoadImage(string imageDataString)
        {
            INativeGameCenterLeaderboard nativeGameCenterLeaderboard = m_gameCenterLeaderboardReferencesHolder.NativeGameCenterLeaderboard;
            int height = nativeGameCenterLeaderboard.GetLastImageLoadedHeight();
            int width = nativeGameCenterLeaderboard.GetLastImageLoadedWidth();
            if (TryGetTexture2D(imageDataString, height, width, out Texture2D texture2D))
                OnLoadImage?.Invoke(m_hashCode, texture2D);
        }

        // 0x06000639/0x0600063a.
        private void LoadImageCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnLoadImageCompleted?.Invoke(m_hashCode, success);
        }
        private void LoadEntriesForPlayerScopeStarted() => OnLoadEntriesForPlayerScopeStarted?.Invoke(m_hashCode);

        // 0x0600063b/0x0600063c.
        private void LoadLocalEntryForPlayerScope(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadLocalEntryForPlayerScope?.Invoke(m_hashCode, leaderboardEntry);
        }
        private void LoadEntryForPlayerScope(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadEntryForPlayerScope?.Invoke(m_hashCode, leaderboardEntry);
        }

        // 0x0600063d/0x0600063e.
        private void LoadEntriesForPlayerScopeCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnLoadEntriesForPlayerScopeCompleted?.Invoke(m_hashCode, success);
        }
        private void LoadEntriesForPlayersStarted() => OnLoadEntriesForPlayersStarted?.Invoke(m_hashCode);

        // 0x0600063f/0x06000640.
        private void LoadLocalEntryForPlayers(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadLocalEntryForPlayers?.Invoke(m_hashCode, leaderboardEntry);
        }
        private void LoadEntryForPlayers(string propertiesJoinToIdentifyLeaderboardEntry)
        {
            if (LeaderboardEntry.TryParseProperties(propertiesJoinToIdentifyLeaderboardEntry, m_gameCenterLocalPlayerReferencesHolder, out LeaderboardEntry leaderboardEntry))
                OnLoadEntryForPlayers?.Invoke(m_hashCode, leaderboardEntry);
        }

        // 0x06000641.
        private void LoadEntriesForPlayersCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success)) OnLoadEntriesForPlayersCompleted?.Invoke(m_hashCode, success);
        }
    }
}
