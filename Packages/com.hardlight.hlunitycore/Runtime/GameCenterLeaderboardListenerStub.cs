using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200010e: synchronous shipping stub listener.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterLeaderboardListenerStub : IGameCenterLeaderboardListener, IGameCenterLeaderboardListenerCallbackHandler
    {
        // 0x06000646: the original listener has no GameObject name.
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

        // 0x06000669: the original empty Clear leaves all subscribers intact.
        public void Clear() { }

        // 0x0600066a: a null subscriber suppresses only this event.
        public void TriggerLoadLeaderboardsStartedEvent(int hashCode) => OnLoadLeaderboardsStarted?.Invoke(hashCode);
        // 0x0600066b: a null subscriber suppresses only this event.
        public void TriggerLoadLeaderboardEvent(int hashCode, Leaderboard leaderboard) => OnLoadLeaderboard?.Invoke(hashCode, leaderboard);
        // 0x0600066c: a null subscriber suppresses only this event.
        public void TriggerLoadLeaderboardsCompletedEvent(int hashCode, bool success) => OnLoadLeaderboardsCompleted?.Invoke(hashCode, success);
        // 0x0600066d: a null subscriber suppresses only this event.
        public void TriggerSubmitScoreToLeaderboardsCompletedEvent(int hashCode, bool success) => OnSubmitScoreToLeaderboardsCompleted?.Invoke(hashCode, success);
        // 0x0600066e: a null subscriber suppresses only this event.
        public void TriggerLoadPreviousOccurrenceEvent(int hashCode, Leaderboard leaderboard) => OnLoadPreviousOccurrence?.Invoke(hashCode, leaderboard);
        // 0x0600066f: a null subscriber suppresses only this event.
        public void TriggerLoadPreviousOccurrenceCompletedEvent(int hashCode, bool success) => OnLoadPreviousOccurrenceCompleted?.Invoke(hashCode, success);
        // 0x06000670: a null subscriber suppresses only this event.
        public void TriggerSubmitScoreToLeaderboardCompletedEvent(int hashCode, bool success) => OnSubmitScoreToLeaderboardCompleted?.Invoke(hashCode, success);
        // 0x06000671: a null subscriber suppresses only this event.
        public void TriggerLoadImageEvent(int hashCode, Texture2D texture2D) => OnLoadImage?.Invoke(hashCode, texture2D);
        // 0x06000672: a null subscriber suppresses only this event.
        public void TriggerLoadImageCompletedEvent(int hashCode, bool success) => OnLoadImageCompleted?.Invoke(hashCode, success);
        // 0x06000673: a null subscriber suppresses only this event.
        public void TriggerLoadEntriesForPlayerScopeStartedEvent(int hashCode) => OnLoadEntriesForPlayerScopeStarted?.Invoke(hashCode);
        // 0x06000674: a null subscriber suppresses only this event.
        public void TriggerLoadLocalEntryForPlayerScopeEvent(int hashCode, LeaderboardEntry localLeaderboardEntry) => OnLoadLocalEntryForPlayerScope?.Invoke(hashCode, localLeaderboardEntry);
        // 0x06000675: a null subscriber suppresses only this event.
        public void TriggerLoadEntryForPlayerScopeEvent(int hashCode, LeaderboardEntry leaderboardEntry) => OnLoadEntryForPlayerScope?.Invoke(hashCode, leaderboardEntry);
        // 0x06000676: a null subscriber suppresses only this event.
        public void TriggerLoadEntriesForPlayerScopeCompletedEvent(int hashCode, bool success) => OnLoadEntriesForPlayerScopeCompleted?.Invoke(hashCode, success);
        // 0x06000677: a null subscriber suppresses only this event.
        public void TriggerLoadEntriesForPlayersStartedEvent(int hashCode) => OnLoadEntriesForPlayersStarted?.Invoke(hashCode);
        // 0x06000678: a null subscriber suppresses only this event.
        public void TriggerLoadLocalEntryForPlayersEvent(int hashCode, LeaderboardEntry localLeaderboardEntry) => OnLoadLocalEntryForPlayers?.Invoke(hashCode, localLeaderboardEntry);
        // 0x06000679: a null subscriber suppresses only this event.
        public void TriggerLoadEntryForPlayersEvent(int hashCode, LeaderboardEntry leaderboardEntry) => OnLoadEntryForPlayers?.Invoke(hashCode, leaderboardEntry);
        // 0x0600067a: a null subscriber suppresses only this event.
        public void TriggerLoadEntriesForPlayersCompletedEvent(int hashCode, bool success) => OnLoadEntriesForPlayersCompleted?.Invoke(hashCode, success);

        // 0x0600067b: only the original Object constructor is called.
        public GameCenterLeaderboardListenerStub() { }
    }
}
