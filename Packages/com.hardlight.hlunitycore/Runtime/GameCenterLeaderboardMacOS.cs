using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Whole original HLUnityCore.Runtime 0x02000111. Exact managed ImplMap
    // metadata is unavailable; HLMacCore and all entry points are proven by
    // both shipping native import descriptors. CLR/native execution held.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameCenterLeaderboardMacOS : INativeGameCenterLeaderboard
    {
        // 0x060006b1: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_InitialiseGameCenterLeaderboards(GameCenterLeaderboardListenerMacOS.UnityCallback callback, string separator);

        // 0x060006b2: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_DeinitialiseGameCenterLeaderboards();

        // 0x060006b3: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern bool Unity_IsGameCenterLeaderboardsAvailable();

        // 0x060006b4: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern bool Unity_IsGameCenterAPIOlderThanMacOS11();

        // 0x060006b5: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_LoadLeaderboards(int hashCode, string leaderboardIDsJoin);

        // 0x060006b6: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_SubmitScoreToLeaderboards(int hashCode, long score, ulong context, string identifier, string leaderboardIDsJoin);

        // 0x060006b7: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_LoadPreviousOccurrence(int hashCode, string leaderboardID);

        // 0x060006b8: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_SubmitScoreToLeaderboard(int hashCode, long score, ulong context, string identifier, string propertiesJoinToIdentifyLeaderboard);

        // 0x060006b9: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_LoadImage(int hashCode, string propertiesJoinToIdentifyLeaderboard);

        // 0x060006ba: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern int Unity_GetLastImageLoadedPixelHeight();

        // 0x060006bb: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern int Unity_GetLastImageLoadedPixelWidth();

        // 0x060006bc: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_LoadEntriesForPlayerScope(int hashCode, long playerScope, long timeScope, ulong rangeStartIndex, ulong rangeLength, string propertiesJoinToIdentifyLeaderboard);

        // 0x060006bd: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern long Unity_GetLastLoadEntriesForPlayerScopeTotalPlayerCount();

        // 0x060006be: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_LoadEntriesForPlayers(int hashCode, string identifiersJoin, long timeScope, string propertiesJoinToIdentifyLeaderboard);

        // 0x060006bf: shipping strings/delegate marshal and native result normalization.
        [DllImport("HLMacCore")]
        private static extern void Unity_IssueLeaderboardChallenge(string leaderboardId, string message, UnityHLLeaderboardChallengeIssuedCallback callback);

        // 0x060006c0
        public bool IsLegacyAPI()
        {
            return Unity_IsGameCenterAPIOlderThanMacOS11();
        }

        // 0x060006c1
        public bool IsAvailable()
        {
            return Unity_IsGameCenterLeaderboardsAvailable();
        }

        // 0x060006c2
        public bool IsOldAPI()
        {
            return Unity_IsGameCenterAPIOlderThanMacOS11();
        }

        // 0x060006c3
        public void Initialise(string gameObjectName, string separator)
        {
            Unity_InitialiseGameCenterLeaderboards(new GameCenterLeaderboardListenerMacOS.UnityCallback(GameCenterLeaderboardListenerMacOS.Callback), separator);
        }

        // 0x060006c4
        public void Deinitialise()
        {
            Unity_DeinitialiseGameCenterLeaderboards();
        }

        // 0x060006c5
        public void LoadLeaderboards(int hashCode, IReadOnlyList<string> leaderboardIDs)
        {
            Unity_LoadLeaderboards(hashCode, string.Join("|", leaderboardIDs));
        }

        // 0x060006c6
        public void SubmitScore(int hashCode, long score, ulong context, string identifier, IReadOnlyList<string> leaderboardIDs)
        {
            Unity_SubmitScoreToLeaderboards(hashCode, score, context, identifier, string.Join("|", leaderboardIDs));
        }

        // 0x060006c7
        public void LoadPreviousOccurrence(int hashCode, string propertiesJoinToIdentifyLeaderboard)
        {
            Unity_LoadPreviousOccurrence(hashCode, propertiesJoinToIdentifyLeaderboard);
        }

        // 0x060006c8
        public void SubmitScore(int hashCode, long score, ulong context, string identifier, string propertiesJoinToIdentifyLeaderboard)
        {
            Unity_SubmitScoreToLeaderboard(hashCode, score, context, identifier, propertiesJoinToIdentifyLeaderboard);
        }

        // 0x060006c9
        public void LoadImage(int hashCode, string propertiesJoinToIdentifyLeaderboard)
        {
            Unity_LoadImage(hashCode, propertiesJoinToIdentifyLeaderboard);
        }

        // 0x060006ca
        public int GetLastImageLoadedHeight()
        {
            return Unity_GetLastImageLoadedPixelHeight();
        }

        // 0x060006cb
        public int GetLastImageLoadedWidth()
        {
            return Unity_GetLastImageLoadedPixelWidth();
        }

        // 0x060006cc
        public void LoadEntriesForPlayerScope(int hashCode, LeaderboardPlayerScope playerScope, LeaderboardTimeScope timeScope, ulong rangeStartIndex, ulong rangeLength, string propertiesJoinToIdentifyLeaderboard)
        {
            Unity_LoadEntriesForPlayerScope(hashCode, (long)playerScope, (long)timeScope, rangeStartIndex, rangeLength, propertiesJoinToIdentifyLeaderboard);
        }

        // 0x060006cd
        public long GetLastLoadEntriesForPlayerScopeTotalPlayerCount()
        {
            return Unity_GetLastLoadEntriesForPlayerScopeTotalPlayerCount();
        }

        // 0x060006ce
        public void LoadEntriesForPlayers(int hashCode, IReadOnlyList<string> identifiers, LeaderboardTimeScope timeScope, string propertiesJoinToIdentifyLeaderboard)
        {
            Unity_LoadEntriesForPlayers(hashCode, string.Join("|", identifiers), (long)timeScope, propertiesJoinToIdentifyLeaderboard);
        }

        // 0x060006cf
        public void IssueLeaderboardChallenge(string leaderboardId, string message, UnityHLLeaderboardChallengeIssuedCallback callback)
        {
            Unity_IssueLeaderboardChallenge(leaderboardId, message, callback);
        }

        // 0x060006d0
        public GameCenterLeaderboardMacOS()
        {

        }

    }
}
