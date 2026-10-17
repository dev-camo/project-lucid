using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000139. The shipped simulated provider: every
    // request completes synchronously with success and no data events are raised.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class StubNativeGameCenterSaveGame : INativeGameCenterSaveGame
    {
        private readonly IGameCenterSaveGameListenerCallbacksHandler m_gameCenterSaveGameListenerCallbacksHandler;

        // 0x06000899: no argument guard.
        public StubNativeGameCenterSaveGame(IGameCenterSaveGameListenerCallbacksHandler gameCenterSaveGameListenerCallbacksHandler)
        {
            m_gameCenterSaveGameListenerCallbacksHandler = gameCenterSaveGameListenerCallbacksHandler;
        }

        // 0x0600089a/0x0600089b: complete shipping bodies are empty.
        public void Initialise(string gameObjectName, string separator) { }
        public void Deinitialise() { }

        // 0x0600089c
        public bool IsAvailable() => true;

        // 0x0600089d: Started then Completed; no saved game is reported.
        public void FetchSavedGames(int hashCode)
        {
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerFetchSavedGamesStartedEvent(hashCode);
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerFetchSavedGamesCompletedEvent(hashCode, true);
        }

        // 0x0600089e: name and data are ignored; no save event precedes completion.
        public void SaveGameData(int hashCode, string saveGameName, string saveGameData)
        {
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerSaveGameDataCompletedEvent(hashCode, true);
        }

        // 0x0600089f: completes without a load-data event.
        public void LoadGameData(int hashCode, string propertiesJoinToIdentifySavedGameToLoad)
        {
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerLoadGameDataCompletedEvent(hashCode, true);
        }

        // 0x060008a0
        public void DeleteSavedGamesWithName(int hashCode, string saveGameName)
        {
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerDeleteSavedGamesWithNameCompletedEvent(hashCode, true);
        }

        // 0x060008a1: Started then Completed; no resolved saved game is reported.
        public void ResolveConflictingSavedGames(int hashCode, string conflictingSavedGames, string saveGameData)
        {
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerResolveConflictingSavedGamesStartedEvent(hashCode);
            m_gameCenterSaveGameListenerCallbacksHandler.TriggerResolveConflictingSavedGamesCompletedEvent(hashCode, true);
        }
    }
}
