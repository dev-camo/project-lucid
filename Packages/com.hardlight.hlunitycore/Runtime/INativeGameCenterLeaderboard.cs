namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000115; 15 genuine abstract APIs.
    // Preserve the complete shipping contract; these declarations have no method bodies.
    public interface INativeGameCenterLeaderboard
    {
        // 0x060006eb
        System.Boolean IsLegacyAPI();
        // 0x060006ec
        System.Boolean IsAvailable();
        // 0x060006ed
        void Initialise(System.String gameObjectName, System.String separator);
        // 0x060006ee
        void Deinitialise();
        // 0x060006ef
        void LoadLeaderboards(System.Int32 hashCode, System.Collections.Generic.IReadOnlyList<System.String> leaderboardIDs);
        // 0x060006f0
        void SubmitScore(System.Int32 hashCode, System.Int64 score, System.UInt64 context, System.String identifier, System.Collections.Generic.IReadOnlyList<System.String> leaderboardIDs);
        // 0x060006f1
        void LoadPreviousOccurrence(System.Int32 hashCode, System.String propertiesJoinToIdentifyLeaderboard);
        // 0x060006f2
        void SubmitScore(System.Int32 hashCode, System.Int64 score, System.UInt64 context, System.String identifier, System.String propertiesJoinToIdentifyLeaderboard);
        // 0x060006f3
        void LoadImage(System.Int32 hashCode, System.String propertiesJoinToIdentifyLeaderboard);
        // 0x060006f4
        System.Int32 GetLastImageLoadedHeight();
        // 0x060006f5
        System.Int32 GetLastImageLoadedWidth();
        // 0x060006f6
        void LoadEntriesForPlayerScope(System.Int32 hashCode, Hardlight.LeaderboardPlayerScope playerScope, Hardlight.LeaderboardTimeScope timeScope, System.UInt64 rangeStartIndex, System.UInt64 rangeLength, System.String propertiesJoinToIdentifyLeaderboard);
        // 0x060006f7
        System.Int64 GetLastLoadEntriesForPlayerScopeTotalPlayerCount();
        // 0x060006f8
        void LoadEntriesForPlayers(System.Int32 hashCode, System.Collections.Generic.IReadOnlyList<System.String> identifiers, Hardlight.LeaderboardTimeScope timeScope, System.String propertiesJoinToIdentifyLeaderboard);
        // 0x060006f9
        void IssueLeaderboardChallenge(System.String leaderboardId, System.String message, Hardlight.UnityHLLeaderboardChallengeIssuedCallback callback);
    }
}
