using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    public static class PrefabPoolVerification
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Prefab pool verification: " + label);
            checks++;
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void Set<T>(object value, string name, T data) => Field(typeof(PrefabPool<ProceduralMesh>), name).SetValue(value, data);
        private static T Get<T>(object value, string name) => (T)Field(typeof(PrefabPool<ProceduralMesh>), name).GetValue(value);
        private static void Call(object value, Type type, string name) =>
            type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(value, null);
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); }
            catch (TargetInvocationException e) when (e.InnerException is T) { Check(true, label); return; }
            catch (T) { Check(true, label); return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + label);
        }

        // Runs only genuine readonly value/delegate APIs and metadata. No Unity
        // object constructor bypass, generic component fixture, or pool substitute.
        public static int RunManaged()
        {
            checks = 0;
            var empty = default(PooledPrefab<object>);
            Check(empty.Index == 0, "default index is zero, not the -1 sentinel");
            Check(empty.Instance == null, "default instance is null");
            object value = new object();
            foreach (int index in new[] { -1, -2, 0, 1, 49, 50, int.MinValue, int.MaxValue })
            {
                var pooled = new PooledPrefab<object>(value, index);
                Check(pooled.Index == index, "constructor retains unrestricted index");
                Check(ReferenceEquals(pooled.Instance, value), "constructor retains exact instance");
                PooledPrefab boxed = pooled;
                Check(boxed.Index == index, "actual interface dispatch retains index");
            }
            var number = new PooledPrefab<int>(23, 7);
            Check(number.Instance == 23 && number.Index == 7, "pooled value type generic is unconstrained");
            Check(Convert.ToInt32(Enum.Parse(typeof(ObjectUpdateType), "Enable")) == 0 &&
                Convert.ToInt32(Enum.Parse(typeof(ObjectUpdateType), "Disable")) == 1, "original compiled enum values");
            int notifications = 0;
            ObjectUpdateType last = default;
            UpdateSpawnedObjectCallback callback = update => { notifications++; last = update; };
            callback(ObjectUpdateType.Disable);
            Check(notifications == 1 && last == ObjectUpdateType.Disable, "real delegate retains update value");
            callback(ObjectUpdateType.Enable);
            Check(notifications == 2 && last == ObjectUpdateType.Enable, "real delegate repeat dispatch");
            Check(typeof(IPrefabPool).GetMethods().Length == 8, "eight actual interface contracts");
            Check(typeof(PooledPrefab).GetMethods().Length == 1, "one actual pooled interface contract");
            var typeParameter = typeof(PrefabPool<>).GetGenericArguments()[0];
            var constraints = typeParameter.GetGenericParameterConstraints();
            Check(constraints.Length == 1 && constraints[0] == typeof(Component), "real component type constraint");
            Check(typeParameter.GenericParameterAttributes == GenericParameterAttributes.None, "no invented new or reference constraint");
            Check(typeof(PooledPrefab<>).GetGenericArguments()[0].GetGenericParameterConstraints().Length == 0,
                "pooled struct type parameter has no constraint");
            var indexField = typeof(PooledPrefab<object>).GetField("<Index>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var instanceField = typeof(PooledPrefab<object>).GetField("<Instance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(indexField.IsInitOnly && instanceField.IsInitOnly, "both value fields are readonly");
            Check(typeof(PrefabPoolManager).GetInterfaces().Length == 1 && typeof(PrefabPoolManager).GetInterfaces()[0] == typeof(ISystem),
                "manager implements genuine empty system contract");
            Check(typeof(PrefabPool<Transform>).GetInterfaces().Length == 1 && typeof(PrefabPool<Transform>).GetInterfaces()[0] == typeof(IPrefabPool),
                "pool implements original pool contract");
            Check(typeof(ProceduralMesh_Pool).BaseType == typeof(PrefabPool<ProceduralMesh>), "real concrete original pool base");
            return checks;
        }

        // Use the original nongeneric HLS pool and component with an isolated
        // registry, preserving every prior SystemRef and callback row. EditMode
        // invokes original lifecycle entries explicitly; PlayMode checks normal messages.
        public static int RunEngine()
        {
            checks = 0;
            var registry = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registry.GetValue(null);
            GameObject managerOwner = null, poolOwner = null, prefabOwner = null, otherOwner = null;
            PrefabPoolType key = null;
            ProceduralMesh_Pool pool = null, other = null;
            PrefabPoolManager manager = null;
            try
            {
                registry.SetValue(null, Activator.CreateInstance(registry.FieldType));
                managerOwner = new GameObject("Project Lucid genuine prefab manager fixture");
                manager = managerOwner.AddComponent<PrefabPoolManager>();
                Call(manager, typeof(PrefabPoolManager), "Awake");
                Check(ProcessManager.GetSystem<PrefabPoolManager>() == manager, "original Awake entry registers manager");
                var style = (GUIStyle)Field(typeof(PrefabPoolManager), "m_style").GetValue(manager);
                Check(style.fontSize == 15 && style.normal.textColor == Color.white, "original cached style values");
                Check(!manager.GUIEnabled, "manager starts with disabled diagnostics");
                key = ScriptableObject.CreateInstance<PrefabPoolType>();
                key.name = "Project Lucid pool fixture key";
                prefabOwner = new GameObject("Project Lucid genuine procedural prefab");
                var prefab = prefabOwner.AddComponent<ProceduralMesh>();
                Check(prefab.FinalScale == Vector3.one, "real component constructor sets unit final scale");
                poolOwner = new GameObject("Project Lucid genuine procedural pool");
                poolOwner.SetActive(false);
                pool = poolOwner.AddComponent<ProceduralMesh_Pool>();
                Check(Get<int>(pool, "m_initialObjectCount") == 10 && pool.FadeTimeSeconds == 0f, "real inherited and descendant constructor defaults");
                Set(pool, "m_objectPoolType", key);
                Set(pool, "m_prefab", prefab);
                Set(pool, "m_initialObjectCount", 2);
                poolOwner.SetActive(true);
                Call(pool, typeof(PrefabPool<ProceduralMesh>), "Awake");
                Check(pool.MaxObjects == 2 && pool.FreeObjects == 2 && pool.UsedObjects == 0, "original Awake entry initializes and instantiates two originals");
                Check(manager.GetPoolByType(key) == pool, "pool startup callback registers original pool");
                Check(pool.HasFreeInstance() && !pool.HasOverflowed && pool.MaxUsedObjects == 0, "initial availability and diagnostics");
                var inactive = Get<List<GameObject>>(pool, "m_inactiveBuckets");
                var active = Get<List<GameObject>>(pool, "m_activeBuckets");
                Check(inactive.Count == 1 && active.Count == 1, "initial 50-object bucket rounding");
                Check(!inactive[0].activeSelf && active[0].activeSelf, "native inactive/active bucket state");
                int callbacks = 0;
                Set(pool, "<SpawnDespawnCallbackEnabled>k__BackingField", true);
                var first = pool.SpawnInstance(new Vector3(2, 3, 4), Quaternion.Euler(0, 25, 0), new Vector3(3, 4, 5), 2f, _ => callbacks++);
                Check(first.Index == 1 && first.Instance != null, "inactive stack pops original second instance");
                Check(first.Instance.transform.parent == active[0].transform, "inactive spawn reparents to active bucket");
                Check(first.Instance.transform.position == new Vector3(2, 3, 4), "spawn assigns world position");
                Check(Quaternion.Angle(first.Instance.transform.rotation, Quaternion.Euler(0, 25, 0)) < 0.01f, "spawn assigns world rotation");
                Check(first.Instance.transform.localScale == new Vector3(6, 8, 10), "spawn multiplies local scale");
                Check(callbacks == 0 && first.Instance.FinalScale == Vector3.one, "empty original base hook does not invoke callback or alter component scale");
                var second = pool.SpawnInstance(Vector3.zero, Quaternion.identity, Vector3.one);
                Check(second.Index == 0 && second.Instance != first.Instance, "inactive stack pops distinct first instance");
                Check(pool.UsedObjects == 2 && pool.FreeObjects == 0 && pool.MaxUsedObjects == 2, "used count and maximum after two spawns");
                var exhausted = pool.SpawnInstance(Vector3.zero, Quaternion.identity, Vector3.one);
                Check(exhausted.Index == 0 && exhausted.Instance == null, "exhausted nonresizing pool returns genuine default value");
                Check(!pool.HasOverflowed && !pool.HasFreeInstance(), "exhaustion does not mark overflow");
                pool.MarkOverflow(); pool.MarkOverflow();
                Check(pool.HasOverflowed && pool.MaxObjects == 2, "explicit overflow marking is idempotent and does not resize");
                pool.DespawnInstance(in first);
                Check(first.Instance.transform.parent == active[0].transform && pool.FreeObjects == 1, "despawn queues active instance without immediate reparent");
                var reused = pool.SpawnInstance(Vector3.right, Quaternion.identity, Vector3.one);
                Check(reused.Instance == first.Instance && reused.Index == first.Index, "active free stack is reused first");
                pool.DespawnInstance(in reused);
                Call(pool, typeof(PrefabPool<ProceduralMesh>), "LateUpdate");
                Check(reused.Instance.transform.parent == inactive[0].transform, "LateUpdate moves active free instance to inactive bucket");
                Check(Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeActiveObjects").Count == 0, "LateUpdate drains active stack");
                var sentinel = new PooledPrefab<ProceduralMesh>(null, -1);
                pool.MakeActive(in sentinel); pool.MakeInactive(in sentinel);
                pool.DespawnInstance(in sentinel); pool.OnDestroyDespawnInstance(in sentinel);
                Check(pool.FreeObjects == 1, "only -1 sentinel is ignored by these operations");
                PooledPrefab foreign = new PooledPrefab<Transform>(second.Instance.transform, second.Index);
                pool.DespawnInstance(in foreign);
                Check(pool.FreeObjects == 1, "foreign generic boxed value is ignored");
                PooledPrefab matching = second;
                ((IPrefabPool)pool).DespawnInstance(in matching);
                Check(pool.FreeObjects == 2 && pool.UsedObjects == 0, "actual explicit interface forwards matching boxed value");
                pool.DespawnInstance(in second);
                Check(pool.FreeObjects == 3 && pool.UsedObjects == -1, "duplicate despawn is retained without deduplication");
                Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeActiveObjects").Clear();
                var nullInstance = new PooledPrefab<ProceduralMesh>(null, 0);
                pool.DespawnInstance(in nullInstance);
                Check(pool.FreeObjects == 2, "typed despawn accepts null instance without engine access");
                Throws<NullReferenceException>(() => Call(pool, typeof(PrefabPool<ProceduralMesh>), "LateUpdate"), "LateUpdate null-instance native fault");
                Check(Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeActiveObjects").Count == 0 &&
                    Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeInactiveObjects").Count == 2, "LateUpdate pushes inactive before transform fault");
                Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeInactiveObjects").Clear();
                Set(pool, "m_resizeWhenFull", true);
                var expanded = pool.SpawnInstance(Vector3.zero, Quaternion.identity, Vector3.one);
                Check(expanded.Index == 2 && expanded.Instance != null && pool.MaxObjects == 3, "resizing creates one genuine instance");
                Check(inactive.Count == 1 && active.Count == 1, "expansion within existing bucket retains one pair");
                var oldInactive = Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeInactiveObjects");
                Set(pool, "m_initialObjectCount", -1);
                Throws<ArgumentOutOfRangeException>(() => Call(pool, typeof(PrefabPool<ProceduralMesh>), "Initialise"), "negative initial capacity faults at first stack allocation");
                Check(ReferenceEquals(oldInactive, Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeInactiveObjects")), "first allocation failure retains previous stacks");
                otherOwner = new GameObject("Project Lucid stale original pool registration");
                otherOwner.SetActive(false);
                other = otherOwner.AddComponent<ProceduralMesh_Pool>();
                Set(other, "m_objectPoolType", key);
                manager.RegisterPool(other);
                manager.UnregisterPool(pool);
                Check(manager.GetPoolByType(key) == other, "unregister stale instance preserves current registered pool");
                manager.UnregisterPool(other);
                Check(manager.GetPoolByType(key) == null, "unregister exact registered instance removes row");
                Throws<ArgumentNullException>(() => manager.GetPoolByType(null), "BCL dictionary null key failure remains");
                return checks;
            }
            finally
            {
                try
                {
                    // Destroy real spawned children first and empty the genuine
                    // list before EditMode pool teardown; its native Destroy
                    // body is checked separately by the automatic PlayMode fixture.
                    if (pool != null)
                    {
                        var objects = Get<List<ProceduralMesh>>(pool, "m_objects");
                        if (objects != null) { foreach (var item in objects) if (item != null) Object.DestroyImmediate(item.gameObject); objects.Clear(); }
                        Set(pool, "m_prefabPoolManager", manager);
                        if (objects != null) Call(pool, typeof(PrefabPool<ProceduralMesh>), "OnDestroy");
                    }
                    if (other != null)
                    {
                        Set(other, "m_objects", new List<ProceduralMesh>());
                        Set(other, "m_prefabPoolManager", manager);
                        Call(other, typeof(PrefabPool<ProceduralMesh>), "OnDestroy");
                    }
                }
                finally
                {
                    try { if (otherOwner != null) Object.DestroyImmediate(otherOwner); }
                    finally
                    {
                        try { if (poolOwner != null) Object.DestroyImmediate(poolOwner); }
                        finally
                        {
                            try { if (prefabOwner != null) Object.DestroyImmediate(prefabOwner); }
                            finally
                            {
                                try
                                {
                                    try { if (manager != null) Call(manager, typeof(PrefabPoolManager), "OnDestroy"); }
                                    finally { if (managerOwner != null) Object.DestroyImmediate(managerOwner); }
                                }
                                finally { try { if (key != null) Object.DestroyImmediate(key); } finally { registry.SetValue(null, previousRegistry); } }
                            }
                        }
                    }
                }
            }
        }
    }
}
