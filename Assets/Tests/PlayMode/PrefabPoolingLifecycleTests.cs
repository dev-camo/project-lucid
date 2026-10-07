using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class PrefabPoolingLifecycleTests
    {
        private static FieldInfo Field(string name) => typeof(PrefabPool<ProceduralMesh>).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static T Get<T>(object pool, string name) => (T)Field(name).GetValue(pool);
        private static void Set(object pool, string name, object data) => Field(name).SetValue(pool, data);
        private static void Check(bool value, string label, ref int checks)
        { if (!value) throw new InvalidOperationException(label); checks++; }

        // Exercise normal Unity messages and deferred destruction using the
        // original concrete pool, component, and registry.
        [UnityTest]
        public IEnumerator OriginalPrefabPool_AutomaticLateUpdateAndDestroy()
        {
            int checks = 0;
            var registry = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registry.GetValue(null);
            GameObject managerOwner = null, poolOwner = null, prefabOwner = null;
            PrefabPoolType key = null;
            ProceduralMesh_Pool pool = null;
            ProceduralMesh[] spawned = null;
            try
            {
                registry.SetValue(null, Activator.CreateInstance(registry.FieldType));
                managerOwner = new GameObject("Project Lucid original prefab manager frame fixture");
                var manager = managerOwner.AddComponent<PrefabPoolManager>();
                Check(ProcessManager.GetSystemRef<PrefabPoolManager>().GetSafe() == manager, "automatic manager Awake registration", ref checks);
                key = ScriptableObject.CreateInstance<PrefabPoolType>();
                prefabOwner = new GameObject("Project Lucid original procedural prefab frame fixture");
                var prefab = prefabOwner.AddComponent<ProceduralMesh>();
                poolOwner = new GameObject("Project Lucid original procedural pool frame fixture");
                poolOwner.SetActive(false);
                pool = poolOwner.AddComponent<ProceduralMesh_Pool>();
                Set(pool, "m_objectPoolType", key);
                Set(pool, "m_prefab", prefab);
                Set(pool, "m_initialObjectCount", 2);
                poolOwner.SetActive(true);
                Check(pool.MaxObjects == 2 && pool.FreeObjects == 2, "automatic original Awake creates actual instances", ref checks);
                Check(manager.GetPoolByType(key) == pool, "original pool startup callback registers before frames", ref checks);
                var inactive = Get<List<GameObject>>(pool, "m_inactiveBuckets")[0];
                var active = Get<List<GameObject>>(pool, "m_activeBuckets")[0];
                Check(!inactive.activeSelf && active.activeSelf, "original buckets retain distinct active state", ref checks);
                var instance = pool.SpawnInstance(Vector3.one, Quaternion.identity, Vector3.one);
                Check(instance.Instance != null && instance.Instance.gameObject.activeInHierarchy, "real spawn is active through bucket parent", ref checks);
                pool.DespawnInstance(in instance);
                Check(instance.Instance.transform.parent == active.transform, "despawn defers reparent until LateUpdate", ref checks);
                yield return null;
                Check(instance.Instance.transform.parent == inactive.transform && Get<Stack<PooledPrefab<ProceduralMesh>>>(pool, "m_freeActiveObjects").Count == 0,
                    "actual automatic LateUpdate reparents and drains", ref checks);
                var reused = pool.SpawnInstance(Vector3.zero, Quaternion.identity, Vector3.one);
                Check(reused.Instance == instance.Instance && reused.Index == instance.Index, "next spawn reuses exact returned instance", ref checks);
                spawned = Get<List<ProceduralMesh>>(pool, "m_objects").ToArray();
                UObj.Destroy(poolOwner);
                Check(manager.GetPoolByType(key) == pool, "deferred owner destruction retains registration until callback", ref checks);
                yield return null;
                yield return null;
                Check(pool == null && poolOwner == null, "original owner is destroyed on actual frames", ref checks);
                Check(spawned.Length == 2 && spawned[0] == null && spawned[1] == null, "original OnDestroy destroys both owned instances", ref checks);
                Check(manager != null && manager.GetPoolByType(key) == null, "final pool unregister leaves real manager alive", ref checks);
                Assert.AreEqual(12, checks, "bounded original prefab pool frame checks");
            }
            finally
            {
                try
                {
                    // On setup failure, avoid invoking Destroy from EditMode or
                    // retaining a poisoned registry row after a setup failure.
                    if (pool != null)
                    {
                        var objects = Get<List<ProceduralMesh>>(pool, "m_objects");
                        if (objects != null)
                        {
                            foreach (var item in objects) if (item != null) UObj.DestroyImmediate(item.gameObject);
                            objects.Clear();
                        }
                    }
                    if (spawned != null) foreach (var item in spawned) if (item != null) UObj.DestroyImmediate(item.gameObject);
                }
                finally
                {
                    try { if (poolOwner != null) UObj.DestroyImmediate(poolOwner); }
                    finally
                    {
                        try { if (prefabOwner != null) UObj.DestroyImmediate(prefabOwner); }
                        finally
                        {
                            try { if (managerOwner != null) UObj.DestroyImmediate(managerOwner); }
                            finally { try { if (key != null) UObj.DestroyImmediate(key); } finally { registry.SetValue(null, previousRegistry); } }
                        }
                    }
                }
            }
        }
    }
}
