using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameplayIslandManager : ISystem
    {
        public event Action<IReadOnlyList<GameplayIslandDefinition>> OnIslandDefinitionChanged;
        private readonly List<GameplaySection> m_activeGameplaySections = new List<GameplaySection>();
        private MissionList m_loadedMissionsFromList;
        private readonly Dictionary<GameplayIslandDefinition, List<GameplaySection>> m_gameplayIslands =
            new Dictionary<GameplayIslandDefinition, List<GameplaySection>>();
        private readonly List<IResetCapableGameplayElement> m_pendingResetCapableGameplayElements =
            new List<IResetCapableGameplayElement>();
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private List<GameplayIslandDefinition> m_activeIslandDefinitions = new List<GameplayIslandDefinition>();
        private readonly List<GameplaySection> m_temporaryActiveGameplaySections = new List<GameplaySection>();

        public IReadOnlyList<GameplayIslandDefinition> ActiveIslandDefinitions => m_activeIslandDefinitions;

        public GameplayIslandManager()
        {
            this.SubscribeToAction(SystemAction.Initialise, OnInitialise);
            this.SubscribeToAction(SystemAction.Shutdown, OnShutdown);
        }

        private void OnInitialise(object context = null)
        {
            m_levelManagerRef.Get().InvokeOnLevelLoaded(OnLevelLoaded);
            m_characterManagerRef.InvokeOnValid(OnCharacterManagerValid);
        }

        private void OnLevelLoaded(LevelManagerLevel level)
        {
            if (m_levelManagerRef.Get().TutorialLevels.Contains(level.LevelDefinition))
                return;
            if (level.LevelDefinition.MissionList == m_loadedMissionsFromList)
                return;
            m_loadedMissionsFromList = level.LevelDefinition.MissionList;
            m_activeGameplaySections.Clear();
            m_activeIslandDefinitions.Clear();
            InvokeGameplayIslandChanged();
        }

        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            characterManager.OnCharacterRespawn += OnCharacterRespawn;
        }

        private void OnCharacterRespawn()
        {
            ForceActiveIsland(m_characterManagerRef.Get().GetCurrentCharacterUnsafe().WorldPosition);
        }

        public void SanitisePendingGameplayElements()
        {
            var remainingElements = new List<IResetCapableGameplayElement>();
            foreach (IResetCapableGameplayElement element in m_pendingResetCapableGameplayElements)
                if (!element.IsDestroyed())
                    remainingElements.AddUnique(element);
            m_pendingResetCapableGameplayElements.Clear();
            m_pendingResetCapableGameplayElements.AddRange(remainingElements);
        }

        public void ClearPendingResetCapableGameplayElements()
        {
            m_pendingResetCapableGameplayElements.Clear();
        }

        // The original shutdown removes only the level callback through a freshly
        // acquired reference; it does not unsubscribe the character callback.
        private void OnShutdown(object context = null)
        {
            SystemRef<LevelManager> levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
            if (levelManagerRef.IsNull())
                return;
            levelManagerRef.Get().RemoveLevelLoadedAction(OnLevelLoaded);
        }

        private bool IsActiveIslandDefinition(GameplaySection gameplaySection)
        {
            return m_activeIslandDefinitions.Contains(gameplaySection.Definition);
        }

        private void SetActiveGameplaySection(GameplaySection gameplaySection)
        {
            m_activeGameplaySections.Remove(gameplaySection);
            m_activeGameplaySections.Insert(0, gameplaySection);
            m_activeIslandDefinitions.AddUnique(gameplaySection.Definition);
            InvokeGameplayIslandChanged();
        }

        public void EnterGameplaySection(GameplaySection gameplaySection)
        {
            bool alreadyActive = IsActiveIslandDefinition(gameplaySection);
            SetActiveGameplaySection(gameplaySection);
            if (!alreadyActive)
                SetGameplaySectionVisibility();
        }

        // Original visibility first deactivates all hidden sections, then activates
        // the accumulated visible sections. A fault retains the temporary list;
        // there is no entry clear or exception-time repair in the native bodies.
        private void SetGameplaySectionVisibility()
        {
            if (m_activeIslandDefinitions.Count == 0 || IsCharacterDeactivated())
                return;
            foreach (KeyValuePair<GameplayIslandDefinition, List<GameplaySection>> island in m_gameplayIslands)
            {
                bool visible = false;
                foreach (GameplayIslandDefinition activeIsland in m_activeIslandDefinitions)
                {
                    if (activeIsland.VisibleIslands.Contains(island.Key) || island.Key == activeIsland)
                    {
                        visible = true;
                        break;
                    }
                }
                if (visible)
                    m_temporaryActiveGameplaySections.AddRange(island.Value);
                else
                    foreach (GameplaySection gameplaySection in island.Value)
                        gameplaySection.SetActive(false);
            }
            foreach (GameplaySection gameplaySection in m_temporaryActiveGameplaySections)
                gameplaySection.SetActive(true);
            m_temporaryActiveGameplaySections.Clear();
        }

        public void ReenterActiveIsland()
        {
            InvokeGameplayIslandChanged();
        }

        public void ExitGameplaySection(GameplaySection gameplaySection)
        {
            m_activeGameplaySections.Remove(gameplaySection);
            RegenerateActiveIslandDefinitions();
            if (m_activeIslandDefinitions.Contains(gameplaySection.Definition))
                return;
            SetGameplaySectionVisibility();
            InvokeGameplayIslandChanged();
        }

        private void RegenerateActiveIslandDefinitions()
        {
            m_activeIslandDefinitions.Clear();
            foreach (GameplaySection gameplaySection in m_activeGameplaySections)
                m_activeIslandDefinitions.AddUnique(gameplaySection.Definition);
        }

        // The out argument replaces the live field even on an unsuccessful lookup.
        // A missing dictionary key returns with the original partially filled list.
        public void ForceActiveIsland(Vector3 worldPosition)
        {
            if (!TryGetAllIslandDefinition(worldPosition, out m_activeIslandDefinitions))
                return;
            m_activeGameplaySections.Clear();
            foreach (GameplayIslandDefinition islandDefinition in m_activeIslandDefinitions)
            {
                if (!m_gameplayIslands.TryGetValue(islandDefinition, out List<GameplaySection> gameplaySections))
                    return;
                m_activeGameplaySections.AddRange(gameplaySections);
            }
            SetGameplaySectionVisibility();
            InvokeGameplayIslandChanged();
        }

        public void DeactivateAllIslands()
        {
            foreach (KeyValuePair<GameplayIslandDefinition, List<GameplaySection>> island in m_gameplayIslands)
                foreach (GameplaySection gameplaySection in island.Value)
                    gameplaySection.SetActive(false);
        }

        public IEnumerator IslandCullingTracksCamera()
        {
            Transform cameraTransform = ProcessManager.GetSystem<CinemachineCameraManager>(null, true).MainCamera.transform;
            GameplayIslandDefinition currentIsland = null;
            while (true)
            {
                if (TryGetIslandDefinition(cameraTransform.position, out GameplayIslandDefinition islandDefinition,
                    out GameplaySection gameplaySection) && islandDefinition != currentIsland)
                {
                    currentIsland = islandDefinition;
                    EnterGameplaySection(gameplaySection);
                }
                yield return null;
            }
        }

        public void RegisterGameplaySection(GameplaySection gameplaySection)
        {
            GameplayIslandDefinition islandDefinition = gameplaySection.Definition;
            if (!m_gameplayIslands.TryGetValue(islandDefinition, out List<GameplaySection> gameplaySections))
            {
                gameplaySections = new List<GameplaySection>();
                m_gameplayIslands[islandDefinition] = gameplaySections;
            }
            gameplaySections.Add(gameplaySection);
            var registeredIndices = new List<int>();
            for (int index = 0; index < m_pendingResetCapableGameplayElements.Count; index++)
                if (RegisterResetCapableBehaviour(m_pendingResetCapableGameplayElements[index]))
                    registeredIndices.Add(index);
            for (int index = registeredIndices.Count - 1; index >= 0; index--)
                m_pendingResetCapableGameplayElements.RemoveAt(registeredIndices[index]);
        }

        public void UnregisterGameplaySection(GameplaySection gameplaySection)
        {
            GameplayIslandDefinition islandDefinition = gameplaySection.Definition;
            if (!m_gameplayIslands.TryGetValue(islandDefinition, out List<GameplaySection> gameplaySections))
                return;
            gameplaySections.Remove(gameplaySection);
            if (gameplaySections.Count == 0)
                m_gameplayIslands.Remove(islandDefinition);
        }

        public bool TryGetIslandDefinition(Vector3 worldPosition, out GameplayIslandDefinition islandDefinition,
            out GameplaySection gameplaySection)
        {
            islandDefinition = null;
            gameplaySection = null;
            foreach (KeyValuePair<GameplayIslandDefinition, List<GameplaySection>> island in m_gameplayIslands)
            {
                island.Deconstruct(out GameplayIslandDefinition definition, out List<GameplaySection> sections);
                foreach (GameplaySection section in sections)
                {
                    if (section.ContainsPosition(worldPosition))
                    {
                        islandDefinition = definition;
                        gameplaySection = section;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryGetAllIslandDefinition(Vector3 worldPosition,
            out List<GameplayIslandDefinition> islandDefinitions)
        {
            islandDefinitions = new List<GameplayIslandDefinition>();
            foreach (KeyValuePair<GameplayIslandDefinition, List<GameplaySection>> island in m_gameplayIslands)
            {
                island.Deconstruct(out GameplayIslandDefinition definition, out List<GameplaySection> sections);
                foreach (GameplaySection section in sections)
                {
                    if (section.ContainsPosition(worldPosition))
                    {
                        islandDefinitions.AddUnique(definition);
                        break;
                    }
                }
            }
            return islandDefinitions.Count > 0;
        }

        public bool TryGetAllGameplaySections(Vector3 worldPosition, out List<GameplaySection> containedGameplaySections)
        {
            containedGameplaySections = new List<GameplaySection>();
            foreach (KeyValuePair<GameplayIslandDefinition, List<GameplaySection>> island in m_gameplayIslands)
            {
                island.Deconstruct(out GameplayIslandDefinition definition, out List<GameplaySection> sections);
                foreach (GameplaySection section in sections)
                    if (section.ContainsPosition(worldPosition))
                        containedGameplaySections.AddUnique(section);
            }
            return containedGameplaySections.Count > 0;
        }

        public bool RegisterResetCapableBehaviour(IResetCapableGameplayElement resetCapableGameplayElement)
        {
            if (TryGetIslandDefinition(resetCapableGameplayElement.GetPosition(), out GameplayIslandDefinition islandDefinition,
                out GameplaySection gameplaySection))
            {
                gameplaySection.RegisterResetCapableGameplayElement(resetCapableGameplayElement);
                return true;
            }
            m_pendingResetCapableGameplayElements.AddUnique(resetCapableGameplayElement);
            return false;
        }

        private void InvokeGameplayIslandChanged()
        {
            if (!IsCharacterDeactivated())
                OnIslandDefinitionChanged?.Invoke(m_activeIslandDefinitions);
        }

        private bool IsCharacterDeactivated()
        {
            return m_characterManagerRef.TryGet(out CharacterManager characterManager)
                && characterManager.TryGetCurrentCharacter(out Character character)
                && (character.SwapInIsInProgress() || character.DyingIsInProgress());
        }
    }
}
