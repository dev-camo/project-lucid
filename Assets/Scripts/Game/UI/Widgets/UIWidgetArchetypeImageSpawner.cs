using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200099b; bodies translated from the 1.10.1 x86-64 binary.
    // Spawns (replacing any previous instance) one archetype image widget under a parent.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetArchetypeImageSpawner : MonoBehaviour
    {
        [SerializeField] private Transform m_spawnParent;
        [SerializeField] private UIWidgetImageArchetype m_prefab;
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private UIWidgetImageArchetype m_spawnedInstance;

        // 0x0600379b: the parent is hidden and any spawned widget destroyed first. With neither an
        // archetype nor a character, or an archetype missing from the data manager, nothing is
        // spawned and the parent stays hidden. An unknown character id throws.
        public void Spawn(CharacterArchetype characterArchetype, CharacterId characterId = CharacterId.None)
        {
            bool hasSpawnedInstance = m_spawnedInstance != null;
            m_spawnParent.gameObject.SetActive(false);
            if (hasSpawnedInstance)
            {
                Destroy(m_spawnedInstance.gameObject);
                m_spawnedInstance = null;
            }
            if (characterArchetype == CharacterArchetype.None && characterId == CharacterId.None)
                return;
            DataManager dataManager = m_dataManagerRef.Get();
            CharacterArchetypeDefinition archetypeDefinition = null;
            if (characterArchetype != CharacterArchetype.None
                && !dataManager.CharacterArchetypes.TryGetValue(characterArchetype, out archetypeDefinition))
                return;
            CharacterDefinition characterDefinition = characterId != CharacterId.None ? dataManager.Characters[characterId] : null;
            m_spawnParent.gameObject.SetActive(true);
            m_spawnedInstance = Instantiate(m_prefab, m_spawnParent);
            m_spawnedInstance.Setup(new UIWidgetImageArchetypeParameters(archetypeDefinition, characterDefinition));
        }

        // 0x0600379c
        public UIWidgetArchetypeImageSpawner() { }
    }
}
