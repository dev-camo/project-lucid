using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020000f6. The shipping macOS factory
    // remains available for research; the offline adapter selects its own
    // service boundary without rewriting the original native selection.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class GameCenterLeaderboardUtility
    {
        // 0x0600057a: an explicit debug provider takes precedence over forceStub.
        // Preserve the three debug assignments and the two stub assignments in
        // their original order. Assignment faults prevent later assignments.
        public static INativeGameCenterLeaderboard CreateNativeGameCenterLeaderboard(
            IDebugGameCenterLeaderboard debugGameCenterLeaderboard,
            IGameCenterLeaderboardListenerCallbackHandler gameCenterLeaderboardListenerCallbackHandler,
            IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder,
            IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder,
            bool forceStub)
        {
            if (debugGameCenterLeaderboard != null)
            {
                debugGameCenterLeaderboard.SetGameCenterLeaderboardReferencesHolder(gameCenterLeaderboardReferencesHolder);
                debugGameCenterLeaderboard.SetGameCenterLocalPlayerReferencesHolder(gameCenterLocalPlayerReferencesHolder);
                debugGameCenterLeaderboard.SetGameCenterLeaderboardListenerCallbackHandler(gameCenterLeaderboardListenerCallbackHandler);
                return debugGameCenterLeaderboard;
            }
            if (forceStub)
            {
                GameCenterLeaderboardStub stub = new GameCenterLeaderboardStub(gameCenterLeaderboardListenerCallbackHandler);
                stub.SetGameCenterLeaderboardReferencesHolder(gameCenterLeaderboardReferencesHolder);
                stub.SetGameCenterLocalPlayerReferencesHolder(gameCenterLocalPlayerReferencesHolder);
                return stub;
            }
            return new GameCenterLeaderboardMacOS();
        }

        // 0x0600057b: the ARM64 identity return is a genuine RET and x86 copies
        // its bool argument into the return register. No platform query occurs.
        public static bool IsStubNeeded(bool forceStub) => forceStub;

        // 0x0600057c: the shipping stub listener requires no reference setters.
        // The Mac listener receives leaderboard references before player refs.
        public static IGameCenterLeaderboardListener CreateGameCenterLeaderboardListener(
            IGameCenterLeaderboardReferencesHolder gameCenterLeaderboardReferencesHolder,
            IGameCenterLocalPlayerReferencesHolder gameCenterLocalPlayerReferencesHolder,
            bool forceStub)
        {
            if (forceStub) return new GameCenterLeaderboardListenerStub();
            GameCenterLeaderboardListenerMacOS listener = new GameCenterLeaderboardListenerMacOS();
            listener.SetGameCenterLeaderboardReferencesHolder(gameCenterLeaderboardReferencesHolder);
            listener.SetGameCenterLocalPlayerReferencesHolder(gameCenterLocalPlayerReferencesHolder);
            return listener;
        }
    }
}
