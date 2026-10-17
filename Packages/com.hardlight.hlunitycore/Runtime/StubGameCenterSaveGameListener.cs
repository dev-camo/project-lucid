using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000137: synchronous shipping stub
    // listener. The stub native save game raises its events directly.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class StubGameCenterSaveGameListener : IGameCenterSaveGameListener, IGameCenterSaveGameListenerCallbacksHandler
    {
        // 0x06000862: the stub has no GameObject name.
        public string Name => null;

        public event Action<int> OnFetchSavedGamesStarted;
        public event Action<int, SavedGame> OnFetchSavedGame;
        public event Action<int, bool> OnFetchSavedGamesCompleted;
        public event Action<int, SavedGame> OnSaveGameData;
        public event Action<int, bool> OnSaveGameDataCompleted;
        public event Action<int, string> OnLoadGameData;
        public event Action<int, bool> OnLoadGameDataCompleted;
        public event Action<int, bool> OnDeleteSavedGamesWithNameCompleted;
        public event Action<int> OnResolveConflictingSavedGamesStarted;
        public event Action<int, SavedGame> OnResolveConflictingSavedGame;
        public event Action<int, bool> OnResolveConflictingSavedGamesCompleted;
        public event Action<SavedGame> OnDidModifySavedGame;
        public event Action OnHasConflictingSavedGamesStarted;
        public event Action<SavedGame> OnHasConflictingSavedGame;
        public event Action OnHasConflictingSavedGamesCompleted;

        // 0x06000881..0x0600088f: each trigger raises only its own event,
        // and a null subscriber list makes the trigger a no-op.
        public void TriggerFetchSavedGamesStartedEvent(int hashCode) => OnFetchSavedGamesStarted?.Invoke(hashCode);
        public void TriggerFetchSavedGameEvent(int hashCode, SavedGame savedGame) => OnFetchSavedGame?.Invoke(hashCode, savedGame);
        public void TriggerFetchSavedGamesCompletedEvent(int hashCode, bool success) => OnFetchSavedGamesCompleted?.Invoke(hashCode, success);
        public void TriggerSaveGameDataEvent(int hashCode, SavedGame savedGame) => OnSaveGameData?.Invoke(hashCode, savedGame);
        public void TriggerSaveGameDataCompletedEvent(int hashCode, bool success) => OnSaveGameDataCompleted?.Invoke(hashCode, success);
        public void TriggerLoadGameDataEvent(int hashCode, string saveGameData) => OnLoadGameData?.Invoke(hashCode, saveGameData);
        public void TriggerLoadGameDataCompletedEvent(int hashCode, bool success) => OnLoadGameDataCompleted?.Invoke(hashCode, success);
        public void TriggerDeleteSavedGamesWithNameCompletedEvent(int hashCode, bool success) => OnDeleteSavedGamesWithNameCompleted?.Invoke(hashCode, success);
        public void TriggerResolveConflictingSavedGamesStartedEvent(int hashCode) => OnResolveConflictingSavedGamesStarted?.Invoke(hashCode);
        public void TriggerResolveConflictingSavedGameEvent(int hashCode, SavedGame savedGame) => OnResolveConflictingSavedGame?.Invoke(hashCode, savedGame);
        public void TriggerResolveConflictingSavedGamesCompletedEvent(int hashCode, bool success) => OnResolveConflictingSavedGamesCompleted?.Invoke(hashCode, success);
        public void TriggerDidModifySavedGameEvent(SavedGame savedGame) => OnDidModifySavedGame?.Invoke(savedGame);
        public void TriggerHasConflictingSavedGamesStartedEvent() => OnHasConflictingSavedGamesStarted?.Invoke();
        public void TriggerHasConflictingSavedGameEvent(SavedGame savedGame) => OnHasConflictingSavedGame?.Invoke(savedGame);
        public void TriggerHasConflictingSavedGamesCompletedEvent() => OnHasConflictingSavedGamesCompleted?.Invoke();

        // 0x06000890: only the Object constructor is called.
        public StubGameCenterSaveGameListener() { }
    }
}
