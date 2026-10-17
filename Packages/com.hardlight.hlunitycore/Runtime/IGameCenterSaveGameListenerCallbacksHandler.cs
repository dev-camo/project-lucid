namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000136; 15 abstract APIs (0x06000853..0x06000861)
    // in their original slot order. Declarations only; the listeners implement them.
    public interface IGameCenterSaveGameListenerCallbacksHandler
    {
        void TriggerFetchSavedGamesStartedEvent(int hashCode);
        void TriggerFetchSavedGameEvent(int hashCode, SavedGame savedGame);
        void TriggerFetchSavedGamesCompletedEvent(int hashCode, bool success);
        void TriggerSaveGameDataEvent(int hashCode, SavedGame savedGame);
        void TriggerSaveGameDataCompletedEvent(int hashCode, bool success);
        void TriggerLoadGameDataEvent(int hashCode, string saveGameData);
        void TriggerLoadGameDataCompletedEvent(int hashCode, bool success);
        void TriggerDeleteSavedGamesWithNameCompletedEvent(int hashCode, bool success);
        void TriggerResolveConflictingSavedGamesStartedEvent(int hashCode);
        void TriggerResolveConflictingSavedGameEvent(int hashCode, SavedGame savedGame);
        void TriggerResolveConflictingSavedGamesCompletedEvent(int hashCode, bool success);
        void TriggerDidModifySavedGameEvent(SavedGame savedGame);
        void TriggerHasConflictingSavedGamesStartedEvent();
        void TriggerHasConflictingSavedGameEvent(SavedGame savedGame);
        void TriggerHasConflictingSavedGamesCompletedEvent();
    }
}
