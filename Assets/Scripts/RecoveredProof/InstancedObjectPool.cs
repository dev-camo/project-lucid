using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InstancedObjectPool<TValue> where TValue : Object
    {
        private readonly Dictionary<int, ObjectPool<TValue>> m_pools = new Dictionary<int, ObjectPool<TValue>>();

        // Original 0x06003cf9; arm64 shared 0x13c1bd4.
        // Keep the first registered pool, its capacity, and its captured source object.
        public int Register(TValue value, int defaultCapacity)
        {
            int key = value.GetInstanceID();
            if (!m_pools.ContainsKey(key))
                m_pools.Add(key, new ObjectPool<TValue>(() => Object.Instantiate(value), null, null,
                    Object.Destroy, true, defaultCapacity, 10000));
            return key;
        }

        // Original 0x06003cfa; arm64 shared 0x13c1da4.
        private bool TryGetPool(int key, out ObjectPool<TValue> pool) => m_pools.TryGetValue(key, out pool);

        // Original 0x06003cfb; arm64 shared 0x13c1db8. Assign only after Get succeeds.
        public bool Get(int key, out TValue value)
        {
            if (TryGetPool(key, out ObjectPool<TValue> pool))
            {
                value = pool.Get();
                return true;
            }
            value = null;
            return false;
        }

        // Original 0x06003cfc; arm64 shared 0x13c1e5c. Unknown keys are silent.
        public void Release(int key, TValue value)
        {
            if (TryGetPool(key, out ObjectPool<TValue> pool)) pool.Release(value);
        }
        // Original 0x06003cfd is the implicit constructor: allocate the dictionary before Object's base constructor.
        // Original natural displayclass 0x06003cfe/0x06003cff captures value and clones it only when the pool creates an item.
    }
}
