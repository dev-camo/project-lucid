using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [RequireComponent(typeof(HashedComponentGroup))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameplaySection : MonoBehaviour
    {
        [SerializeField] private GameplayIslandDefinition m_definition;
        [SerializeField] private CharacterEventTrigger[] m_islandVolumes;
        [FormerlySerializedAs("m_rendererGroups")]
        [SerializeField] private List<HashedComponentGroup> m_componentGroups = new List<HashedComponentGroup>();
        [SerializeField] private bool m_resetGameplayElementsOnRespawn;
        private readonly SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef =
            ProcessManager.GetSystemRef<GameplayIslandManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private readonly SystemRef<LevelManager> m_levelManagerRef =
            ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<TrackManager> m_trackManagerRef =
            ProcessManager.GetSystemRef<TrackManager>(null, true);
        private readonly List<CharacterEventTrigger> m_activeIslandVolumes = new List<CharacterEventTrigger>();
        private readonly List<IResetCapableGameplayElement> m_resetCapableGameplayElements =
            new List<IResetCapableGameplayElement>();
        private bool m_isActive;

        public GameplayIslandDefinition Definition => m_definition;

        private void Awake()
        {
            foreach (HashedComponentGroup componentGroup in m_componentGroups)
                componentGroup.SetActive(false);
            foreach (CharacterEventTrigger islandVolume in m_islandVolumes)
            {
                islandVolume.SetTriggerWhenInactive(true);
                islandVolume.OnTriggerEnter += EnterVolume;
                islandVolume.OnTriggerExit += ExitVolume;
            }
            m_gameplayIslandManagerRef.InvokeOnValid(islandManager => islandManager.RegisterGameplaySection(this));
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().OnCharacterRespawn += ResetAllGameplayElements;
            m_characterManagerRef.OnSystemStartup += OnCharacterManagerInitialise;
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        private void OnDestroy()
        {
            foreach (CharacterEventTrigger islandVolume in m_islandVolumes)
            {
                islandVolume.OnTriggerEnter -= EnterVolume;
                islandVolume.OnTriggerExit -= ExitVolume;
            }
            ProcessManager.GetSystemSafe<GameplayIslandManager>(null, true)?.UnregisterGameplaySection(this);
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().OnCharacterRespawn -= ResetAllGameplayElements;
            m_characterManagerRef.OnSystemStartup -= OnCharacterManagerInitialise;
            if (m_levelManagerRef.IsValid())
                m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
        }

        private void OnCharacterManagerInitialise(CharacterManager characterManager)
        {
            characterManager.OnCharacterRespawn += ResetAllGameplayElements;
        }

        private void OnLevelManagerValid(LevelManager levelManager)
        {
            levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            ClearActiveIslandVolumes();
        }

        private void ClearActiveIslandVolumes()
        {
            m_activeIslandVolumes.Clear();
        }

        // The original entry callback precedes insertion, allowing callbacks to observe
        // or change the still-empty list. Exiting an absent volume can also notify exit.
        private void EnterVolume(CharacterEventTrigger islandVolume)
        {
            if (m_activeIslandVolumes.Count == 0)
                m_gameplayIslandManagerRef.GetSafe()?.EnterGameplaySection(this);
            m_activeIslandVolumes.AddUnique(islandVolume);
        }

        private void ExitVolume(CharacterEventTrigger islandVolume)
        {
            m_activeIslandVolumes.Remove(islandVolume);
            if (m_activeIslandVolumes.Count == 0)
                m_gameplayIslandManagerRef.GetSafe()?.ExitGameplaySection(this);
        }

        public void SetActive(bool active)
        {
            foreach (HashedComponentGroup componentGroup in m_componentGroups)
                if (componentGroup != null)
                    componentGroup.SetActive(active);
            m_isActive = active;
        }

        public bool ContainsPosition(Vector3 worldPosition)
        {
            foreach (CharacterEventTrigger islandVolume in m_islandVolumes)
                if (islandVolume != null && islandVolume.GetBounds().Contains(worldPosition))
                    return true;
            return false;
        }

        public void RegisterComponentGroup(HashedComponentGroup componentGroup)
        {
            if (m_componentGroups.Contains(componentGroup))
                return;
            m_componentGroups.Add(componentGroup);
            componentGroup.SetActive(m_isActive);
            if (!m_isActive && m_trackManagerRef.IsValid())
                m_trackManagerRef.Get().ShaderPrewarmer.RegisterHashedComponentGroup(componentGroup);
        }

        public void UnRegisterComponentGroup(HashedComponentGroup componentGroup)
        {
            m_componentGroups.Remove(componentGroup);
            if (m_trackManagerRef.IsValid())
                m_trackManagerRef.Get().ShaderPrewarmer.UnRegisterHashedComponentGroup(componentGroup);
        }

        public void RegisterResetCapableGameplayElement(IResetCapableGameplayElement resetCapableGameplayElement)
        {
            m_resetCapableGameplayElements.AddUnique(resetCapableGameplayElement);
        }

        public void ResetAllGameplayElements()
        {
            if (!m_resetGameplayElementsOnRespawn)
                return;
            foreach (IResetCapableGameplayElement resetCapableGameplayElement in m_resetCapableGameplayElements)
                resetCapableGameplayElement.ResetGameplayElement();
        }

        public GameplaySection() { }
    }
}
