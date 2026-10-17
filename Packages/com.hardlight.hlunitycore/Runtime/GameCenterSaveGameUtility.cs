using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000133. The shipped macOS player compiled
    // only the stub branch and the unsupported-platform error below; any
    // platform-specific native branch the source may have had under a define is
    // absent from this build and is not reconstructed here.
    public static class GameCenterSaveGameUtility
    {
        // 0x06000802: the handler is stored unchecked; otherwise the platform
        // error is reported (HLOutput.LogError) and null is returned.
        public static INativeGameCenterSaveGame CreateNativeGameCenterSaveGame(
            IGameCenterSaveGameListenerCallbacksHandler gameCenterSaveGameListenerCallbacksHandler,
            bool forceStub = false)
        {
            if (forceStub) return new StubNativeGameCenterSaveGame(gameCenterSaveGameListenerCallbacksHandler);
            HLOutput.LogError(string.Format(
                "[GameCenterSaveGame] CreateNativeGameCenterSaveGame - Native gameCenter save game is not supporter on platform '{0}'.",
                Application.platform));
            return null;
        }

        // 0x06000803: same shape as the native factory.
        public static IGameCenterSaveGameListener CreateGameCenterSaveGameListener(bool forceStub = false)
        {
            if (forceStub) return new StubGameCenterSaveGameListener();
            HLOutput.LogError(string.Format(
                "[GameCenterSaveGame] CreateGameCenterSaveGameListener - GameCenter save game listener is not supporter on platform '{0}'.",
                Application.platform));
            return null;
        }
    }
}
