using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009ae; bodies translated from the 1.10.1 x86-64 binary.
    // Spawns one character head button per requested character id under a parent.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetCharacterHeadsSpawner : MonoBehaviour
    {
        [SerializeField] private Transform m_spawnParent;
        [SerializeField] private UIWidgetButtonImageCharacter m_characterHeadPrefab;

        [SerializeField]
        [Tooltip("If ticked, if character heads are requested to be shown for all existent characters, instead show none.")]
        private bool m_showNoneIfAll;

        [SerializeField] private bool m_showSwapIcon;
        private SystemRef<DataManager> m_dataManagerRef;
        private readonly List<UIWidgetButtonImageCharacter> m_characterHeadsInstances = new List<UIWidgetButtonImageCharacter>();

        // 0x060037eb
        public void Awake()
        {
            m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        }

        // 0x060037ec: nothing changes while the spawned heads already match. Otherwise the heads
        // are rebuilt, in the data manager's character order, unless there is nothing to show
        // (no ids, or - with m_showNoneIfAll - at least as many ids as there are characters).
        public void UpdateCharacterHeads(IReadOnlyList<CharacterId> characterIds)
        {
            if (CheckCharacterIdsMatch(characterIds))
                return;
            ClearAllCharacterHeads();
            Dictionary<CharacterId, CharacterDefinition> characters = m_dataManagerRef.Get().Characters;
            if (m_showNoneIfAll && characters.Count <= characterIds.Count)
                return;
            if (characterIds.Count == 0)
                return;
            foreach (var (characterId, characterDefinition) in characters)
            {
                if (!characterIds.Contains(characterId))
                    continue;
                UIWidgetButtonImageCharacter head = Instantiate(m_characterHeadPrefab, m_spawnParent);
                m_characterHeadsInstances.Add(head);
                head.Setup(new UIWidgetButtonCharacterImageParameters(characterDefinition, m_showSwapIcon, null));
            }
        }

        // 0x060037ed: same count, and every requested id has a spawned head. Note that an empty
        // request never "matches" (the result is only true after at least one id was confirmed).
        private bool CheckCharacterIdsMatch(IReadOnlyCollection<CharacterId> characterIds)
        {
            if (characterIds.Count != m_characterHeadsInstances.Count)
                return false;
            bool found = false;
            foreach (CharacterId characterId in characterIds)
            {
                found = false;
                foreach (UIWidgetButtonImageCharacter head in m_characterHeadsInstances)
                {
                    if (head.CharacterId == characterId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return found;
        }

        // 0x060037ee
        public void ClearAllCharacterHeads()
        {
            foreach (UIWidgetButtonImageCharacter head in m_characterHeadsInstances)
                Destroy(head.gameObject);
            m_characterHeadsInstances.Clear();
        }

        // 0x060037ef: the list is allocated before the MonoBehaviour constructor runs.
        public UIWidgetCharacterHeadsSpawner() { }
    }
}
