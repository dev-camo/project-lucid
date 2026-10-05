using System.Collections.Generic;

namespace Hardlight.Pooling
{
    // Original HLUnityCore.Runtime.dll type, recovered from shared generic arm64
    // implementations at 0x18bef40..0x18c0250. Kept here as an FSM dependency.
    public sealed class ObjectPool<TType> where TType : class, new()
    {
        // 0x06001025; arm64 0x18c0130 calls UnityEngine.Object's constructor.
        private static UnityEngine.Object s_lockObject = new UnityEngine.Object();
        private static ObjectPool<TType> s_objectPool;
        private Stack<TType> m_freeObjects;
        private List<TType> m_usedObjects;

        // 0x0600101a; arm64 0x18bef40: no lock, zero if not initialized.
        public static int UsedObjectCount => s_objectPool == null ? 0 : s_objectPool.m_usedObjects.Count;

        // 0x0600101b; arm64 0x18bf0f4: double-checked initialization.
        public static void InitialisePool(int poolSize)
        {
            if (s_objectPool != null) return;
            lock (s_lockObject)
            {
                if (s_objectPool == null) s_objectPool = new ObjectPool<TType>(poolSize);
            }
        }

        // 0x0600101c; arm64 0x18bf4b0. Initialization is the caller's job.
        public static TType Spawn() => s_objectPool.SpawnInternal();

        // 0x0600101d; arm64 0x18bf5c8.
        public static void DespawnAndNullify(ref TType objectToDespawn)
        {
            Despawn(objectToDespawn);
            objectToDespawn = null;
        }

        // 0x0600101e; arm64 0x18bf684: null is ignored.
        public static void Despawn(TType objectToDespawn)
        {
            if (objectToDespawn != null) s_objectPool.DespawnInternal(objectToDespawn);
        }

        // 0x0600101f; arm64 0x18bf7a0.
        public static bool IsInitialised() => s_objectPool != null;

        // 0x06001020; arm64 0x18bf874.
        public static void DespawnEntirePool()
        {
            if (s_objectPool != null) s_objectPool.DespawnEntirePoolInternal();
        }

        // 0x06001021; arm64 0x18bfa54. Both collections use count as capacity;
        // construction pushes count new objects in ascending construction order.
        private ObjectPool(int count)
        {
            m_freeObjects = new Stack<TType>(count);
            m_usedObjects = new List<TType>(count);
            for (int i = 0; i < count; ++i) m_freeObjects.Push(new TType());
        }

        // 0x06001022; arm64 0x18bfb60.
        private TType SpawnInternal()
        {
            lock (s_lockObject)
            {
                TType value = m_freeObjects.Count != 0 ? m_freeObjects.Pop() : new TType();
                m_usedObjects.Add(value);
                return value;
            }
        }

        // 0x06001023; arm64 0x18bfdbc. IndexOf uses the original type's equality;
        // duplicate despawns and objects absent from the used list are ignored.
        private void DespawnInternal(TType objectToDespawn)
        {
            lock (s_lockObject)
            {
                int index = m_usedObjects.IndexOf(objectToDespawn);
                if (index == -1) return;
                m_usedObjects.RemoveAt(index);
                m_freeObjects.Push(objectToDespawn);
            }
        }

        // 0x06001024; arm64 0x18bff54. Push used objects in list order before
        // clearing, preserving the original pool's subsequent reuse order.
        public void DespawnEntirePoolInternal()
        {
            lock (s_lockObject)
            {
                for (int i = 0; i < m_usedObjects.Count; ++i) m_freeObjects.Push(m_usedObjects[i]);
                m_usedObjects.Clear();
            }
        }
    }
}
