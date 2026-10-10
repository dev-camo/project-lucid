using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    // Original Game.Runtime 02000a4d; per-ID collection state for unique pickups.
    public class UniqueCollectableStateData
    {
        // Original accessors 06003b35/06003b36 and backing field 04002b5b.
        public int Collected { get; private set; }
        private readonly Dictionary<string, bool> m_collectedById = new Dictionary<string, bool>();

        // Original 06003b37: registration reports existing state and never increments the count.
        // The out value is written after the dictionary lookup or insertion succeeds.
        public void TryAddId(string id, out bool isCollected)
        {
            if (m_collectedById.ContainsKey(id)) isCollected = m_collectedById[id];
            else
            {
                m_collectedById.Add(id, false);
                isCollected = false;
            }
        }

        // Original 06003b38: an unknown ID is not registered here. A known ID
        // increments only on its first collection; the Int32 increment wraps.
        public void CollectId(string id, out bool wasCollected)
        {
            wasCollected = false;
            if (m_collectedById.TryGetValue(id, out bool collected) && !collected)
            {
                m_collectedById[id] = true;
                wasCollected = true;
                Collected = unchecked(Collected + 1);
            }
        }

        // Original 06003b39: probing an unknown ID leaves the registry untouched.
        public bool IsCollected(string id)
        {
            return m_collectedById.TryGetValue(id, out bool collected) && collected;
        }

        // Original 06003b3a: retain registered IDs, snapshot keys before updating
        // dictionary values, and clear the count before allocating that snapshot.
        public void Reset()
        {
            Collected = 0;
            foreach (string id in new List<string>(m_collectedById.Keys)) m_collectedById[id] = false;
        }

        // Original 06003b3b: the dictionary initializer runs before the base constructor.
        public UniqueCollectableStateData() { }
    }
}
