using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class GameCenterLocalPlayerUtility
    {
        // Original 0x06000717; these Stub types are actual shipping owners.
        // The captured macOS build chooses solely on forceStub.
        public static INativeGameCenterLocalPlayer CreateNativeGameCenterLocalPlayer(
            IGameCenterLocalPlayerListenerCallbackHandler gameCenterLocalPlayerListenerCallbackHandler, bool forceStub)
        {
            if (forceStub) return new GameCenterLocalPlayerStub(gameCenterLocalPlayerListenerCallbackHandler);
            return new GameCenterLocalPlayerMacOS();
        }

        // Original 0x06000718; no extra platform switch or fallback is inserted.
        public static IGameCenterLocalPlayerListener CreateGameCenterLocalPlayerListener(bool forceStub)
        {
            if (forceStub) return new GameCenterLocalPlayerListenerStub();
            return new GameCenterLocalPlayerListenerMacOS();
        }
    }
}
