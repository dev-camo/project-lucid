namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200010f; 36 genuine abstract APIs.
    // Preserve the complete shipping contract; these declarations have no method bodies.
    public interface IGameCenterLeaderboardListener
    {
        // 0x0600067c
        System.String Name { get; }
        // 0x0600067d
        event System.Action<System.Int32> OnLoadLeaderboardsStarted;
        // 0x0600067f
        event System.Action<System.Int32, Hardlight.Leaderboard> OnLoadLeaderboard;
        // 0x06000681
        event System.Action<System.Int32, System.Boolean> OnLoadLeaderboardsCompleted;
        // 0x06000683
        event System.Action<System.Int32, System.Boolean> OnSubmitScoreToLeaderboardsCompleted;
        // 0x06000685
        event System.Action<System.Int32, Hardlight.Leaderboard> OnLoadPreviousOccurrence;
        // 0x06000687
        event System.Action<System.Int32, System.Boolean> OnLoadPreviousOccurrenceCompleted;
        // 0x06000689
        event System.Action<System.Int32, System.Boolean> OnSubmitScoreToLeaderboardCompleted;
        // 0x0600068b
        event System.Action<System.Int32, UnityEngine.Texture2D> OnLoadImage;
        // 0x0600068d
        event System.Action<System.Int32, System.Boolean> OnLoadImageCompleted;
        // 0x0600068f
        event System.Action<System.Int32> OnLoadEntriesForPlayerScopeStarted;
        // 0x06000691
        event System.Action<System.Int32, Hardlight.LeaderboardEntry> OnLoadLocalEntryForPlayerScope;
        // 0x06000693
        event System.Action<System.Int32, Hardlight.LeaderboardEntry> OnLoadEntryForPlayerScope;
        // 0x06000695
        event System.Action<System.Int32, System.Boolean> OnLoadEntriesForPlayerScopeCompleted;
        // 0x06000697
        event System.Action<System.Int32> OnLoadEntriesForPlayersStarted;
        // 0x06000699
        event System.Action<System.Int32, Hardlight.LeaderboardEntry> OnLoadLocalEntryForPlayers;
        // 0x0600069b
        event System.Action<System.Int32, Hardlight.LeaderboardEntry> OnLoadEntryForPlayers;
        // 0x0600069d
        event System.Action<System.Int32, System.Boolean> OnLoadEntriesForPlayersCompleted;
        // 0x0600069f
        void Clear();
    }
}
