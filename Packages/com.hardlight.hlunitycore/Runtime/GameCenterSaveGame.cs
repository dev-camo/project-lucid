using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000127 (MethodDefs 0x060007b7..0x060007cd,
    // iterator and closure bodies 0x060007ce..0x06000801). The shipped Game Center
    // save-game service; offline behaviour belongs at the separate adapter boundary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class GameCenterSaveGame : ISystem
    {
        public const string Separator = "|";
        public event Action<SavedGame> OnDidModifySavedGame;
        public event Action<IReadOnlyList<SavedGame>> OnHasConflictingSavedGames;
        private readonly IGameCenterSaveGameListener m_gameCenterSaveGameListener;
        private readonly INativeGameCenterSaveGame m_nativeGameCenterSaveGame;
        private readonly List<SavedGame> m_fetchedSavedGames = new List<SavedGame>();
        private readonly List<SavedGame> m_conflictingSavedGames = new List<SavedGame>();
        private readonly List<SavedGame> m_resolveConflictingSavedGames = new List<SavedGame>();
        private static readonly Converter<SavedGame, string> s_propertiesJoinToIdentifySavedGamesConverter = SavedGameToPropertiesJoinToIdentifySavedGame;

        // 0x060007bb: the listener is created first and its callback-handler cast
        // feeds native creation. Only the two app actions are subscribed; Initialise
        // is left to the caller.
        public GameCenterSaveGame(bool forceStub = false)
        {
            m_gameCenterSaveGameListener = GameCenterSaveGameUtility.CreateGameCenterSaveGameListener(forceStub);
            m_nativeGameCenterSaveGame = GameCenterSaveGameUtility.CreateNativeGameCenterSaveGame(
                m_gameCenterSaveGameListener as IGameCenterSaveGameListenerCallbacksHandler, forceStub);
            ProcessManager.SubscribeToAction(this, SystemAction.AppInitialise, OnAppInitialise);
            ProcessManager.SubscribeToAction(this, SystemAction.AppShutdown, OnAppShutdown);
        }

        private void OnAppInitialise(object context)
        {
            if (!IsGameCenterSaveGameValid()) return;
            m_gameCenterSaveGameListener.OnDidModifySavedGame += TriggerOnDidModifySavedGameEvent;
            m_gameCenterSaveGameListener.OnHasConflictingSavedGamesStarted += ClearConflictingSavedGames;
            m_gameCenterSaveGameListener.OnHasConflictingSavedGame += AddConflictingSavedGame;
            m_gameCenterSaveGameListener.OnHasConflictingSavedGamesCompleted += TriggerOnHasConflictingSavedGames;
        }

        // 0x060007bd: removals mirror OnAppInitialise; a MonoBehaviour listener's
        // GameObject is then destroyed. Native Deinitialise and the ProcessManager
        // subscriptions are left untouched.
        private void OnAppShutdown(object context)
        {
            if (!IsGameCenterSaveGameValid()) return;
            m_gameCenterSaveGameListener.OnDidModifySavedGame -= TriggerOnDidModifySavedGameEvent;
            m_gameCenterSaveGameListener.OnHasConflictingSavedGamesStarted -= ClearConflictingSavedGames;
            m_gameCenterSaveGameListener.OnHasConflictingSavedGame -= AddConflictingSavedGame;
            m_gameCenterSaveGameListener.OnHasConflictingSavedGamesCompleted -= TriggerOnHasConflictingSavedGames;
            if (m_gameCenterSaveGameListener is MonoBehaviour monoBehaviour)
                UnityEngine.Object.Destroy(monoBehaviour.gameObject);
        }

        // 0x060007be: native is checked first, then the listener.
        private bool IsGameCenterSaveGameValid() =>
            m_nativeGameCenterSaveGame != null && m_gameCenterSaveGameListener != null;

        private void ClearConflictingSavedGames() => m_conflictingSavedGames.Clear();
        private void AddConflictingSavedGame(SavedGame savedGame) => m_conflictingSavedGames.Add(savedGame);

        // 0x060007c2: the live conflict list itself is published.
        private void TriggerOnDidModifySavedGameEvent(SavedGame savedGame) => OnDidModifySavedGame?.Invoke(savedGame);
        private void TriggerOnHasConflictingSavedGames() => OnHasConflictingSavedGames?.Invoke(m_conflictingSavedGames);

        // Reference validity is the only guard here.
        public void Initialise()
        {
            if (IsGameCenterSaveGameValid())
                m_nativeGameCenterSaveGame.Initialise(m_gameCenterSaveGameListener.Name, Separator);
        }

        public void Deinitialise()
        {
            if (IsGameCenterSaveGameValid()) m_nativeGameCenterSaveGame.Deinitialise();
        }

        public bool IsAvailable() => IsGameCenterSaveGameValid() && m_nativeGameCenterSaveGame.IsAvailable();

        // 0x060007c6 (MoveNext 0x060007e6, closure 0x060007cf..0x060007d1): a matching
        // Started event clears the shared fetched list, which is then passed to the
        // caller itself. An invalid service reports (false, null) on the first step.
        public IEnumerator FetchSavedGames(Action<bool, IReadOnlyList<SavedGame>> onFetchSavedGamesCompleted)
        {
            if (!IsGameCenterSaveGameValid())
            {
                onFetchSavedGamesCompleted?.Invoke(false, null);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            m_gameCenterSaveGameListener.OnFetchSavedGamesStarted += FetchSavedGamesStarted;
            m_gameCenterSaveGameListener.OnFetchSavedGame += FetchSavedGame;
            bool fetchSuccess = false;
            bool fetchSavedGameCompleted = false;
            m_gameCenterSaveGameListener.OnFetchSavedGamesCompleted += FetchSavedGamesCompleted;
            m_nativeGameCenterSaveGame.FetchSavedGames(hashCodeToCheck);
            while (!fetchSavedGameCompleted) yield return null;
            m_gameCenterSaveGameListener.OnFetchSavedGamesStarted -= FetchSavedGamesStarted;
            m_gameCenterSaveGameListener.OnFetchSavedGame -= FetchSavedGame;
            m_gameCenterSaveGameListener.OnFetchSavedGamesCompleted -= FetchSavedGamesCompleted;
            onFetchSavedGamesCompleted?.Invoke(fetchSuccess, m_fetchedSavedGames);

            void FetchSavedGamesStarted(int hashCode)
            {
                if (hashCode == hashCodeToCheck) m_fetchedSavedGames.Clear();
            }

            void FetchSavedGame(int hashCode, SavedGame savedGame)
            {
                if (hashCode == hashCodeToCheck) m_fetchedSavedGames.Add(savedGame);
            }

            void FetchSavedGamesCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                fetchSuccess = success;
                fetchSavedGameCompleted = true;
            }
        }

        // 0x060007c7 (MoveNext 0x060007fe, closure 0x060007d3): the last matching save
        // event wins; a default SavedGame is reported otherwise.
        public IEnumerator SaveGameData(string saveGameName, string saveGameData, Action<bool, SavedGame> onSaveGameDataCompleted)
        {
            if (!IsGameCenterSaveGameValid())
            {
                onSaveGameDataCompleted?.Invoke(false, default);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            SavedGame newSavedGame = default;
            m_gameCenterSaveGameListener.OnSaveGameData += SaveGameDataEvent;
            bool saveSuccess = false;
            bool saveGameDataCompleted = false;
            m_gameCenterSaveGameListener.OnSaveGameDataCompleted += SaveGameDataCompleted;
            m_nativeGameCenterSaveGame.SaveGameData(hashCodeToCheck, saveGameName, saveGameData);
            while (!saveGameDataCompleted) yield return null;
            m_gameCenterSaveGameListener.OnSaveGameData -= SaveGameDataEvent;
            m_gameCenterSaveGameListener.OnSaveGameDataCompleted -= SaveGameDataCompleted;
            onSaveGameDataCompleted?.Invoke(saveSuccess, newSavedGame);

            void SaveGameDataEvent(int hashCode, SavedGame savedGame)
            {
                if (hashCode == hashCodeToCheck) newSavedGame = savedGame;
            }

            void SaveGameDataCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                saveSuccess = success;
                saveGameDataCompleted = true;
            }
        }

        // 0x060007c8 (MoveNext 0x060007ec): the boxed copy and its real ISavedGameIdentifier interface call
        // follow both subscriptions and precede the native-field and hash loads
        // (MoveNext 0x1ae8a8f..0x1ae8b14), so an interface-typed local is inferred.
        public IEnumerator LoadGameData(SavedGame savedGameToLoad, Action<bool, string> onLoadGameDataCompleted)
        {
            if (!IsGameCenterSaveGameValid())
            {
                onLoadGameDataCompleted?.Invoke(false, null);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            string loadedGameData = null;
            m_gameCenterSaveGameListener.OnLoadGameData += LoadGameDataEvent;
            bool loadSuccess = false;
            bool loadGameDataCompleted = false;
            m_gameCenterSaveGameListener.OnLoadGameDataCompleted += LoadGameDataCompleted;
            ISavedGameIdentifier savedGameIdentifier = savedGameToLoad;
            m_nativeGameCenterSaveGame.LoadGameData(hashCodeToCheck, savedGameIdentifier.PropertiesJoinToIdentifySavedGame);
            while (!loadGameDataCompleted) yield return null;
            m_gameCenterSaveGameListener.OnLoadGameData -= LoadGameDataEvent;
            m_gameCenterSaveGameListener.OnLoadGameDataCompleted -= LoadGameDataCompleted;
            onLoadGameDataCompleted?.Invoke(loadSuccess, loadedGameData);

            void LoadGameDataEvent(int hashCode, string gameData)
            {
                if (hashCode == hashCodeToCheck) loadedGameData = gameData;
            }

            void LoadGameDataCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                loadSuccess = success;
                loadGameDataCompleted = true;
            }
        }

        public IEnumerator DeleteSavedGame(string savedGameName, Action<bool> onDeleteSavedGameCompleted)
        {
            if (!IsGameCenterSaveGameValid())
            {
                onDeleteSavedGameCompleted?.Invoke(false);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            bool deleteSuccess = false;
            bool deleteSavedGamesWithNameCompleted = false;
            m_gameCenterSaveGameListener.OnDeleteSavedGamesWithNameCompleted += DeleteSavedGamesWithNameCompleted;
            m_nativeGameCenterSaveGame.DeleteSavedGamesWithName(hashCodeToCheck, savedGameName);
            while (!deleteSavedGamesWithNameCompleted) yield return null;
            m_gameCenterSaveGameListener.OnDeleteSavedGamesWithNameCompleted -= DeleteSavedGamesWithNameCompleted;
            onDeleteSavedGameCompleted?.Invoke(deleteSuccess);

            void DeleteSavedGamesWithNameCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                deleteSuccess = success;
                deleteSavedGamesWithNameCompleted = true;
            }
        }

        // 0x060007ca (MoveNext 0x060007f2): yields the list overload's enumerator once, with no validity
        // check of its own.
        public IEnumerator ResolveConflictingSavedGames(string saveGameData, Action<bool, IReadOnlyList<SavedGame>> onResolveConflictingSavedGamesCompleted, params SavedGame[] savedGames)
        {
            yield return ResolveConflictingSavedGames(saveGameData, onResolveConflictingSavedGamesCompleted, (IReadOnlyList<SavedGame>)savedGames);
        }

        // 0x060007cb (MoveNext 0x060007f8, closure 0x060007db..0x060007dd): the "|"
        // join of every identifier is built after all three subscriptions and before
        // the native call. The shared resolve list is passed to the caller itself.
        public IEnumerator ResolveConflictingSavedGames(string saveGameData, Action<bool, IReadOnlyList<SavedGame>> onResolveConflictingSavedGamesCompleted, IReadOnlyList<SavedGame> savedGames)
        {
            if (!IsGameCenterSaveGameValid())
            {
                onResolveConflictingSavedGamesCompleted?.Invoke(false, null);
                yield break;
            }

            int hashCodeToCheck = Guid.NewGuid().GetHashCode();
            m_gameCenterSaveGameListener.OnResolveConflictingSavedGamesStarted += ResolveConflictingSavedGamesStarted;
            m_gameCenterSaveGameListener.OnResolveConflictingSavedGame += ResolveConflictingSavedGame;
            bool resolveSuccess = false;
            bool resolveConflictingSavedGamesCompleted = false;
            m_gameCenterSaveGameListener.OnResolveConflictingSavedGamesCompleted += ResolveConflictingSavedGamesCompleted;
            string conflictingSavedGames = string.Join(Separator, savedGames.ConvertAll(s_propertiesJoinToIdentifySavedGamesConverter));
            m_nativeGameCenterSaveGame.ResolveConflictingSavedGames(hashCodeToCheck, conflictingSavedGames, saveGameData);
            while (!resolveConflictingSavedGamesCompleted) yield return null;
            m_gameCenterSaveGameListener.OnResolveConflictingSavedGamesStarted -= ResolveConflictingSavedGamesStarted;
            m_gameCenterSaveGameListener.OnResolveConflictingSavedGame -= ResolveConflictingSavedGame;
            m_gameCenterSaveGameListener.OnResolveConflictingSavedGamesCompleted -= ResolveConflictingSavedGamesCompleted;
            onResolveConflictingSavedGamesCompleted?.Invoke(resolveSuccess, m_resolveConflictingSavedGames);

            void ResolveConflictingSavedGamesStarted(int hashCode)
            {
                if (hashCode == hashCodeToCheck) m_resolveConflictingSavedGames.Clear();
            }

            void ResolveConflictingSavedGame(int hashCode, SavedGame savedGame)
            {
                if (hashCode == hashCodeToCheck) m_resolveConflictingSavedGames.Add(savedGame);
            }

            void ResolveConflictingSavedGamesCompleted(int hashCode, bool success)
            {
                if (hashCode != hashCodeToCheck) return;
                resolveSuccess = success;
                resolveConflictingSavedGamesCompleted = true;
            }
        }

        // 0x060007cc: the explicit interface getter on a boxed copy.
        private static string SavedGameToPropertiesJoinToIdentifySavedGame(SavedGame savedGame) =>
            ((ISavedGameIdentifier)savedGame).PropertiesJoinToIdentifySavedGame;
    }
}
