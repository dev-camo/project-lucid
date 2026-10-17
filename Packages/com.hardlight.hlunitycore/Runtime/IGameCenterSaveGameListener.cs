using System;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000135; 31 abstract APIs (0x06000834..0x06000852)
    // in their original slot order. Declarations only; the listeners implement them.
    public interface IGameCenterSaveGameListener
    {
        string Name { get; }

        event Action<int> OnFetchSavedGamesStarted;
        event Action<int, SavedGame> OnFetchSavedGame;
        event Action<int, bool> OnFetchSavedGamesCompleted;
        event Action<int, SavedGame> OnSaveGameData;
        event Action<int, bool> OnSaveGameDataCompleted;
        event Action<int, string> OnLoadGameData;
        event Action<int, bool> OnLoadGameDataCompleted;
        event Action<int, bool> OnDeleteSavedGamesWithNameCompleted;
        event Action<int> OnResolveConflictingSavedGamesStarted;
        event Action<int, SavedGame> OnResolveConflictingSavedGame;
        event Action<int, bool> OnResolveConflictingSavedGamesCompleted;
        event Action<SavedGame> OnDidModifySavedGame;
        event Action OnHasConflictingSavedGamesStarted;
        event Action<SavedGame> OnHasConflictingSavedGame;
        event Action OnHasConflictingSavedGamesCompleted;
    }
}
