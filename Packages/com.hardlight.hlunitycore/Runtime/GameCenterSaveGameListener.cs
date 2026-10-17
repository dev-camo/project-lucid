using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000134; MethodDefs 0x06000804..0x06000833
    // in declaration order. Request callbacks report the hash set by the
    // preceding SetHashCode; the modification and conflict notifications do
    // not read it. The string-taking public methods and the gameObject.name
    // Name suggest a UnitySendMessage receiver (inferred; no body shows the
    // sender). GameCenterSaveGameUtility.CreateGameCenterSaveGameListener
    // (0x06000803 @0x1ae6540) creates only StubGameCenterSaveGameListener,
    // so the shipping factory never reaches this component.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameCenterSaveGameListener : MonoBehaviour, IGameCenterSaveGameListener
    {
        // 0x06000804: Component.gameObject precedes Object.name.
        public string Name => gameObject.name;

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

        private int m_hashCode;

        // 0x06000823: a failed parse writes zero through the out argument.
        public void SetHashCode(string hashCodeString) => int.TryParse(hashCodeString, out m_hashCode);

        public void FetchSavedGamesStarted() => OnFetchSavedGamesStarted?.Invoke(m_hashCode);

        // 0x06000825: parse before the event null test, so parsing always runs.
        public void FetchSavedGame(string propertiesJoinToIdentifySavedGame)
        {
            if (SavedGame.TryParseProperties(propertiesJoinToIdentifySavedGame, out SavedGame savedGame))
                OnFetchSavedGame?.Invoke(m_hashCode, savedGame);
        }

        // 0x06000826: malformed booleans suppress the completion event.
        public void FetchSavedGamesCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnFetchSavedGamesCompleted?.Invoke(m_hashCode, success);
        }

        public void SaveGameData(string propertiesJoinToIdentifySavedGame)
        {
            if (SavedGame.TryParseProperties(propertiesJoinToIdentifySavedGame, out SavedGame savedGame))
                OnSaveGameData?.Invoke(m_hashCode, savedGame);
        }

        public void SaveGameDataCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnSaveGameDataCompleted?.Invoke(m_hashCode, success);
        }

        // 0x06000829: the game data string is forwarded unparsed, empty included.
        public void LoadGameData(string gameData) => OnLoadGameData?.Invoke(m_hashCode, gameData);

        public void LoadGameDataCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnLoadGameDataCompleted?.Invoke(m_hashCode, success);
        }

        public void DeleteSavedGamesWithNameCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnDeleteSavedGamesWithNameCompleted?.Invoke(m_hashCode, success);
        }

        public void ResolveConflictingSavedGamesStarted() => OnResolveConflictingSavedGamesStarted?.Invoke(m_hashCode);

        public void ResolveConflictingSavedGame(string propertiesJoinToIdentifySavedGame)
        {
            if (SavedGame.TryParseProperties(propertiesJoinToIdentifySavedGame, out SavedGame savedGame))
                OnResolveConflictingSavedGame?.Invoke(m_hashCode, savedGame);
        }

        public void ResolveConflictingSavedGamesCompleted(string successString)
        {
            if (bool.TryParse(successString, out bool success))
                OnResolveConflictingSavedGamesCompleted?.Invoke(m_hashCode, success);
        }

        // 0x0600082f..0x06000832 do not read m_hashCode.
        public void DidModifySavedGame(string propertiesJoinToIdentifySavedGame)
        {
            if (SavedGame.TryParseProperties(propertiesJoinToIdentifySavedGame, out SavedGame savedGame))
                OnDidModifySavedGame?.Invoke(savedGame);
        }

        public void HasConflictingSavedGamesStarted() => OnHasConflictingSavedGamesStarted?.Invoke();

        public void HasConflictingSavedGame(string propertiesJoinToIdentifySavedGame)
        {
            if (SavedGame.TryParseProperties(propertiesJoinToIdentifySavedGame, out SavedGame savedGame))
                OnHasConflictingSavedGame?.Invoke(savedGame);
        }

        public void HasConflictingSavedGamesCompleted() => OnHasConflictingSavedGamesCompleted?.Invoke();

        // 0x06000833: MonoBehaviour constructor only.
        public GameCenterSaveGameListener() { }
    }
}
