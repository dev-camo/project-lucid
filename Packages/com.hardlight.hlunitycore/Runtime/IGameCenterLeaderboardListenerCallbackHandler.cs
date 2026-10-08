namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000110; 17 genuine abstract APIs.
    // Preserve the complete shipping contract; these declarations have no method bodies.
    public interface IGameCenterLeaderboardListenerCallbackHandler
    {
        // 0x060006a0
        void TriggerLoadLeaderboardsStartedEvent(System.Int32 hashCode);
        // 0x060006a1
        void TriggerLoadLeaderboardEvent(System.Int32 hashCode, Hardlight.Leaderboard leaderboard);
        // 0x060006a2
        void TriggerLoadLeaderboardsCompletedEvent(System.Int32 hashCode, System.Boolean success);
        // 0x060006a3
        void TriggerSubmitScoreToLeaderboardsCompletedEvent(System.Int32 hashCode, System.Boolean success);
        // 0x060006a4
        void TriggerLoadPreviousOccurrenceEvent(System.Int32 hashCode, Hardlight.Leaderboard leaderboard);
        // 0x060006a5
        void TriggerLoadPreviousOccurrenceCompletedEvent(System.Int32 hashCode, System.Boolean success);
        // 0x060006a6
        void TriggerSubmitScoreToLeaderboardCompletedEvent(System.Int32 hashCode, System.Boolean success);
        // 0x060006a7
        void TriggerLoadImageEvent(System.Int32 hashCode, UnityEngine.Texture2D texture2D);
        // 0x060006a8
        void TriggerLoadImageCompletedEvent(System.Int32 hashCode, System.Boolean success);
        // 0x060006a9
        void TriggerLoadEntriesForPlayerScopeStartedEvent(System.Int32 hashCode);
        // 0x060006aa
        void TriggerLoadLocalEntryForPlayerScopeEvent(System.Int32 hashCode, Hardlight.LeaderboardEntry localLeaderboardEntry);
        // 0x060006ab
        void TriggerLoadEntryForPlayerScopeEvent(System.Int32 hashCode, Hardlight.LeaderboardEntry leaderboardEntry);
        // 0x060006ac
        void TriggerLoadEntriesForPlayerScopeCompletedEvent(System.Int32 hashCode, System.Boolean success);
        // 0x060006ad
        void TriggerLoadEntriesForPlayersStartedEvent(System.Int32 hashCode);
        // 0x060006ae
        void TriggerLoadLocalEntryForPlayersEvent(System.Int32 hashCode, Hardlight.LeaderboardEntry localLeaderboardEntry);
        // 0x060006af
        void TriggerLoadEntryForPlayersEvent(System.Int32 hashCode, Hardlight.LeaderboardEntry leaderboardEntry);
        // 0x060006b0
        void TriggerLoadEntriesForPlayersCompletedEvent(System.Int32 hashCode, System.Boolean success);
    }
}
