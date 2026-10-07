using UnityEngine;

namespace Hardlight
{
    public interface IPrefabPool
    {
        PrefabPoolType ObjectPoolType { get; }
        string name { get; }
        bool HasOverflowed { get; }
        int UsedObjects { get; }
        int MaxObjects { get; }
        int MaxUsedObjects { get; }
        PooledPrefab SpawnInstance(Transform parent);
        void DespawnInstance(in PooledPrefab prefabInstance);
    }
}
