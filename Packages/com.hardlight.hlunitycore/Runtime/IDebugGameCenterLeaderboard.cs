namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000113; 3 genuine abstract APIs.
    // Preserve the complete shipping contract; these declarations have no method bodies.
    public interface IDebugGameCenterLeaderboard : Hardlight.INativeGameCenterLeaderboard
    {
        // 0x060006e4
        void SetGameCenterLeaderboardReferencesHolder(Hardlight.IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder);
        // 0x060006e5
        void SetGameCenterLocalPlayerReferencesHolder(Hardlight.IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder);
        // 0x060006e6
        void SetGameCenterLeaderboardListenerCallbackHandler(Hardlight.IGameCenterLeaderboardListenerCallbackHandler gameCenterLeaderboardListenerCallbackHandler);
    }
}
