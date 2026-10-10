using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityPrefabPool<T> : PrefabPool<T>, IEntityPrefabPool where T : Component, IEntityActivatable
    {
        private readonly Dictionary<T, PooledPrefab<T>> m_spawnedEntities = new Dictionary<T, PooledPrefab<T>>();

        protected override void Awake()
        {
            base.Awake();
            SpawnDespawnCallbackEnabled = true;
        }

        public void SpawnInstance(Transform spawnTransform, float scaleMod = 1f, UpdateSpawnedObjectCallback callback = null)
        {
            base.SpawnInstance(spawnTransform.position, spawnTransform.rotation, spawnTransform.lossyScale, scaleMod, callback);
        }

        protected override void Spawned(in PooledPrefab<T> spawnedObject, UpdateSpawnedObjectCallback callback)
        {
            base.Spawned(in spawnedObject, callback);
            m_spawnedEntities[spawnedObject.Instance] = spawnedObject;
        }

        protected virtual void OnDeactivated(T entity)
        {
            if (m_spawnedEntities.TryGetValue(entity, out PooledPrefab<T> spawnedObject))
                base.DespawnInstance(in spawnedObject);
        }

        protected override void Despawned(in PooledPrefab<T> despawnedObject)
        {
            base.Despawned(in despawnedObject);
            m_spawnedEntities.Remove(despawnedObject.Instance);
        }

        public EntityPrefabPool() { }
    }
}
