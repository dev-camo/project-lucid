using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PrefabPool<T> : MonoBehaviour, IPrefabPool where T : Component
    {
        [SerializeField] protected PrefabPoolType m_objectPoolType;
        [SerializeField] protected T m_prefab;
        [FormerlySerializedAs("m_maxObjects")]
        [SerializeField]
        [Tooltip("The amount of instances this pool initially contains. If m_resizeWhenFull is unchecked, the pool size will always match this.")]
        protected int m_initialObjectCount = 10;
        [Tooltip("If this pool runs out of free objects, expand its size to fit. If disabled, the pool returns a null object when full.")]
        [SerializeField] protected bool m_resizeWhenFull;
        [Tooltip("The soft limit for a resizable pool. If the pool size exceeds this, a warning will be logged. Use 0 to disable.")]
        [SerializeField] protected int m_softSizeLimit;

        public PrefabPoolType ObjectPoolType => m_objectPoolType;
        public int MaxObjects => m_objects.Count;
        public int FreeObjects => m_freeInactiveObjects.Count + m_freeActiveObjects.Count;
        public int UsedObjects => m_objects.Count - (m_freeInactiveObjects.Count + m_freeActiveObjects.Count);
        public bool HasOverflowed { get; private set; }
        public int MaxUsedObjects { get; private set; }
        protected bool SpawnDespawnCallbackEnabled { get; set; }
        protected bool Initialised { get; private set; }
        protected GameObject InactiveObjects { get; private set; }
        protected GameObject ActiveObjects { get; private set; }
        protected const int BucketSize = 50;
        protected Stack<PooledPrefab<T>> m_freeInactiveObjects;
        private Stack<PooledPrefab<T>> m_freeActiveObjects;
        protected List<T> m_objects;
        protected List<GameObject> m_inactiveBuckets;
        private List<GameObject> m_activeBuckets;
        private bool m_poolDestroyed;
        private PrefabPoolManager m_prefabPoolManager;
        protected int PoolSize => m_objects.Count;

        protected virtual void Awake()
        {
            Initialise();
            for (int i = 0; i < m_initialObjectCount; i++) InstantiatePrefab();
        }

        protected void Initialise()
        {
            m_poolDestroyed = false;
            m_freeInactiveObjects = new Stack<PooledPrefab<T>>(m_initialObjectCount);
            m_freeActiveObjects = new Stack<PooledPrefab<T>>(m_initialObjectCount);
            m_objects = new List<T>(m_initialObjectCount);
            int bucketCount = Mathf.CeilToInt(m_initialObjectCount / 50f);
            InactiveObjects = new GameObject(string.Format("{0}_InactiveObjects", m_objectPoolType));
            InactiveObjects.transform.SetParent(transform);
            InactiveObjects.SetActive(false);
            ActiveObjects = new GameObject(string.Format("{0}_ActiveObjects", m_objectPoolType));
            ActiveObjects.transform.SetParent(transform);
            ActiveObjects.SetActive(true);
            m_inactiveBuckets = new List<GameObject>(bucketCount);
            m_activeBuckets = new List<GameObject>(bucketCount);
            for (int i = 0; i < bucketCount; i++) CreateNewBucketPair();
            ProcessManager.GetSystemRef<PrefabPoolManager>(null, true).InvokeOnValid(OnPrefabPoolManagerStartup);
            Initialised = true;
        }

        private void OnPrefabPoolManagerStartup(PrefabPoolManager manager)
        {
            m_prefabPoolManager = manager;
            m_prefabPoolManager.RegisterPool(this);
        }

        // Returned active instances remain reusable until this phase moves them
        // to inactive buckets. Keep the stack push before transform access.
        private void LateUpdate()
        {
            if (!Initialised) return;
            while (m_freeActiveObjects.Count > 0)
            {
                PooledPrefab<T> pooledPrefab = m_freeActiveObjects.Pop();
                m_freeInactiveObjects.Push(pooledPrefab);
                int bucket = pooledPrefab.Index / BucketSize;
                Transform instanceTransform = pooledPrefab.Instance.transform;
                instanceTransform.SetParent(m_inactiveBuckets[bucket].transform);
            }
        }

        protected virtual void OnDestroy()
        {
            m_poolDestroyed = true;
            foreach (T instance in m_objects) Destroy(instance.gameObject);
            m_prefabPoolManager.UnregisterPool(this);
        }

        public bool HasFreeInstance() => m_freeActiveObjects.Count > 0 || m_freeInactiveObjects.Count > 0;

        public void MarkOverflow()
        {
            if (!HasOverflowed) HasOverflowed = true;
        }

        public void MakeActive(in PooledPrefab<T> pooledPrefab)
        {
            if (pooledPrefab.Index != -1)
                pooledPrefab.Instance.transform.SetParent(m_activeBuckets[pooledPrefab.Index / BucketSize].transform);
        }

        public void MakeInactive(in PooledPrefab<T> pooledPrefab)
        {
            if (pooledPrefab.Index != -1)
                pooledPrefab.Instance.transform.SetParent(m_inactiveBuckets[pooledPrefab.Index / BucketSize].transform);
        }

        public virtual PooledPrefab SpawnInstance(Transform root) =>
            SpawnInstance(root.position, root.rotation, root.localScale, 1f, null);

        public PooledPrefab<T> SpawnInstance(Vector3 position, Quaternion rotation, Vector3 scale,
            float scaleMod = 1f, UpdateSpawnedObjectCallback callback = null)
        {
            // Exhaustion returns the original zero-index/null value; -1 is the
            // separate sentinel accepted by the reparent/despawn operations.
            PooledPrefab<T> instance = default;
            Transform instanceTransform;
            if (m_freeActiveObjects.Count > 0)
            {
                instance = m_freeActiveObjects.Pop();
                instanceTransform = instance.Instance.transform;
            }
            else
            {
                if (m_freeInactiveObjects.Count < 1 && m_resizeWhenFull) ExpandPool();
                if (m_freeActiveObjects.Count > 0)
                {
                    instance = m_freeActiveObjects.Pop();
                    instanceTransform = instance.Instance.transform;
                }
                else
                {
                    if (m_freeInactiveObjects.Count < 1) return instance;
                    instance = m_freeInactiveObjects.Pop();
                    int bucket = instance.Index / BucketSize;
                    instanceTransform = instance.Instance.transform;
                    instanceTransform.SetParent(m_activeBuckets[bucket].transform);
                }
            }
            instanceTransform.position = position;
            instanceTransform.rotation = rotation;
            instanceTransform.localScale = scale * scaleMod;
            int usedObjects = UsedObjects;
            if (usedObjects > MaxUsedObjects) MaxUsedObjects = usedObjects;
            if (SpawnDespawnCallbackEnabled) Spawned(in instance, callback);
            return instance;
        }

        public void DespawnInstance(in PooledPrefab prefabInstance)
        {
            if (prefabInstance is PooledPrefab<T> instance) DespawnInstance(in instance);
        }

        public void DespawnInstance(in PooledPrefab<T> objectToDespawn)
        {
            if (m_poolDestroyed || objectToDespawn.Index == -1) return;
            // The original accepts duplicate returns and delays reparenting.
            m_freeActiveObjects.Push(objectToDespawn);
            if (SpawnDespawnCallbackEnabled) Despawned(in objectToDespawn);
        }

        public void OnDestroyDespawnInstance(in PooledPrefab<T> objectToDespawn)
        {
            if (m_poolDestroyed || objectToDespawn.Index == -1) return;
            if (objectToDespawn.Instance != null && objectToDespawn.Instance.transform != null)
            {
                m_freeInactiveObjects.Push(objectToDespawn);
                GameObject bucket = m_inactiveBuckets[objectToDespawn.Index / BucketSize];
                if (bucket != null && bucket.transform != null)
                {
                    objectToDespawn.Instance.transform.SetParent(bucket.transform);
                    if (SpawnDespawnCallbackEnabled) Despawned(in objectToDespawn);
                }
            }
        }

        protected void ExpandPool()
        {
            int bucketCount = Mathf.CeilToInt(unchecked(PoolSize + 1) / 50f);
            if (m_inactiveBuckets.Count < bucketCount) CreateNewBucketPair();
            InstantiatePrefab();
        }

        private void InstantiatePrefab()
        {
            T instance = Instantiate(m_prefab, Vector3.zero, Quaternion.identity);
            int index = m_objects.Count;
            instance.transform.SetParent(m_inactiveBuckets[index / BucketSize].transform);
            m_freeInactiveObjects.Push(new PooledPrefab<T>(instance, index));
            m_objects.Add(instance);
        }

        private void CreateNewBucketPair()
        {
            GameObject inactive = new GameObject(string.Format("{0}_InactiveObjects_{1}", m_objectPoolType, m_inactiveBuckets.Count));
            inactive.transform.SetParent(InactiveObjects.transform);
            inactive.SetActive(false);
            m_inactiveBuckets.Add(inactive);
            GameObject active = new GameObject(string.Format("{0}_ActiveObjects_{1}", m_objectPoolType, m_activeBuckets.Count));
            active.transform.SetParent(ActiveObjects.transform);
            active.SetActive(true);
            m_activeBuckets.Add(active);
        }

        // These original base hooks are empty. Descendants decide whether to
        // dispatch the callback; the generic pool itself does not invoke it.
        protected virtual void Spawned(in PooledPrefab<T> spawnedObject, UpdateSpawnedObjectCallback callback) { }
        protected virtual void Despawned(in PooledPrefab<T> despawnedObject) { }

        public PrefabPool() { }

        string IPrefabPool.name => name;
        void IPrefabPool.DespawnInstance(in PooledPrefab prefabInstance) => DespawnInstance(in prefabInstance);
    }
}
