using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CascadeObjectPool : PrefabPool<PooledCascadeObject>, ITimeScaled
    {
        public bool IsPaused { get; set; }
        private readonly List<CascadeGroup> m_cascadeGroups = new List<CascadeGroup>();
        private TimeManager m_timeManager;

        // Original 06000ea5. The shipping Start builds its own inactive objects
        // after Initialise; each object's base velocity is captured before adding it.
        protected void Start()
        {
            Initialise();
            for (int i = 0; i < m_initialObjectCount; i++)
            {
                PooledCascadeObject instance = Instantiate(m_prefab, Vector3.zero,
                    Quaternion.identity, m_inactiveBuckets[i / BucketSize].transform);
                m_freeInactiveObjects.Push(new PooledPrefab<PooledCascadeObject>(instance, i));
                instance.Initialise();
                m_objects.Add(instance);
            }
            ProcessManager.GetSystemRef<TimeManager>(null, true).InvokeOnValid(TimeManagerValid);
        }

        // Original 06000ea6 stores the manager before subscribing to Environment.
        private void TimeManagerValid(TimeManager timeManager)
        {
            m_timeManager = timeManager;
            m_timeManager.Subscribe(this, TimeCategory.Environment, UpdateOn.Update, "");
        }

        // Original 06000ea7. Lookup failure and pool exhaustion retain their original
        // faults. Object orientation and velocity orientation are separate arguments.
        public void TriggerCascade(CascadeObjectType cascadeType, int amount, Vector3 origin,
            Quaternion rotation, Vector3 startingVelocity, Quaternion objectRotation,
            IReadOnlyCollection<GravitySource> gravitySourcesToAdd = null)
        {
            ProcessManager.GetSystem<DataManager>(null, true).CascadeObjectDefinitions
                .TryGetValue(cascadeType, out CascadeObjectDefinition definition);
            int spawnCount = Mathf.Min(amount, definition.MaximumSpawnedObjects);
            CascadeGroup group = new CascadeGroup(definition.LifetimeSeconds);
            for (int i = 0; i < spawnCount; i++)
            {
                PooledPrefab<PooledCascadeObject> instance = SpawnInstance(origin, objectRotation,
                    Vector3.one, 1f, null);
                Vector3 velocity = Vector3.Slerp(definition.MinLaunchVelocity,
                    definition.MaxLaunchVelocity, definition.LaunchVelocityBySpawnedNumberCurve.Evaluate(i));
                velocity = rotation * Vector3.Scale(velocity, definition.GetRandomDirection())
                    + startingVelocity;
                instance.Instance.OnSpawned(velocity, gravitySourcesToAdd);
                group.CascadeObjects.Add(instance);
            }
            // An empty or negatively limited cascade still creates a timed group.
            m_cascadeGroups.Add(group);
        }

        // Original 06000ea8. Read lifetime again through the live list, and remove a
        // group only after every despawn and enumerator disposal succeeds.
        public void OnUpdate(float deltaTime)
        {
            for (int i = m_cascadeGroups.Count - 1; i >= 0; i--)
            {
                CascadeGroup group = m_cascadeGroups[i];
                group.UpdateTimer(deltaTime);
                if (group.ExpiryTimer >= m_cascadeGroups[i].LifetimeSeconds)
                {
                    foreach (PooledPrefab<PooledCascadeObject> instance in group.CascadeObjects)
                        DespawnInstance(in instance);
                    m_cascadeGroups.RemoveAt(i);
                }
            }
        }

        // Original 06000ea9 unsubscribes first; no extra null guard changes teardown.
        protected override void OnDestroy()
        {
            m_timeManager.Unsubscribe(this, TimeCategory.Environment, UpdateOn.Update);
            base.OnDestroy();
        }

        // Original 06000eaa permits an absent pool manager and a non-cascade pool.
        public static CascadeObjectPool GetCascadeObjectPoolByType(CascadeObjectType cascadeType)
        {
            SystemRef<PrefabPoolManager> manager = ProcessManager.GetSystemRef<PrefabPoolManager>(null, true);
            if (manager.IsNull()) return null;
            ProcessManager.GetSystem<DataManager>(null, true).CascadeObjectDefinitions
                .TryGetValue(cascadeType, out CascadeObjectDefinition definition);
            return manager.Get().GetPoolByType(definition.PrefabPoolType) as CascadeObjectPool;
        }

        // Original 06000eab is supplied by the field initializer and genuine base.
    }
}
