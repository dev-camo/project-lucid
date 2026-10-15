using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Original PrefabPoolManager components inside an isolated ProcessManager
    // registry, as in LevelManagerSystemsLifecycleTests; only the shutdown
    // callbacks observe the owner's two ownership dictionaries.
    public sealed class OriginalLevelManagerSystemsGapPlayTests
    {
        private static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static Dictionary<Type, ISystem> Systems(LevelManagerSystems owner) =>
            (Dictionary<Type, ISystem>)Field(typeof(LevelManagerSystems), "m_managedSystems").GetValue(owner);
        private static Dictionary<Type, GameObject> Objects(LevelManagerSystems owner) =>
            (Dictionary<Type, GameObject>)Field(typeof(LevelManagerSystems), "m_managedGameObjects").GetValue(owner);

        private static DictionaryEntry[] Snapshot(IDictionary dictionary)
        {
            var rows = new List<DictionaryEntry>();
            IDictionaryEnumerator iterator = dictionary.GetEnumerator();
            while (iterator.MoveNext()) rows.Add(iterator.Entry);
            return rows.ToArray();
        }

        // ReplacePrefab 0x0600249b reaches RemoveManagedGameObject 0x060024a0
        // (shared x86-64 0x95c340): TryGetValue on the object row 0x95c406,
        // Object.Destroy 0x95c430 and Dictionary.Remove 0x95c446 all precede the
        // system-row TryGetValue 0x95c460 and RemoveManagedSystem 0x95c47a.
        // Shutdown 0x0600249f (x86-64 0x5921d0) finishes the system loop before
        // destroying the owned objects. Removing the system before the object
        // row, or destroying objects before system teardown, fails here.
        [UnityTest]
        public IEnumerator ShutdownCallbacksSeeTheObjectRowOnlyDuringFullTeardown()
        {
            FieldInfo[] fields = new[] { "s_systemDictionary", "s_systemActionLookup", "s_actionList", "s_systemActionInProgress" }
                .Select(name => Field(typeof(ProcessManager), name)).ToArray();
            object[] prior = fields.Select(field => field.GetValue(null)).ToArray();
            DictionaryEntry[] priorRegistry = Snapshot((IDictionary)prior[0]);
            DictionaryEntry[] priorActions = Snapshot((IDictionary)prior[1]);
            object[] priorList = ((IList)prior[2]).Cast<object>().ToArray();
            var created = new List<PrefabPoolManager>();
            GameObject seedOwner = null, parentOwner = null;
            try
            {
                ((IDictionary)prior[0]).Clear();
                ((IDictionary)prior[1]).Clear();
                ((IList)prior[2]).Clear();
                fields[3].SetValue(null, false);
                parentOwner = new GameObject("Project Lucid level ownership gap parent");
                var owner = new LevelManagerSystems(parentOwner.transform);
                seedOwner = new GameObject("Project Lucid level ownership gap seed");
                PrefabPoolManager seed = seedOwner.AddComponent<PrefabPoolManager>();
                ProcessManager.UnregisterSystem(seed);
                SystemRef<PrefabPoolManager> reference = ProcessManager.GetSystemRef<PrefabPoolManager>();
                Type type = typeof(PrefabPoolManager);
                var observed = new List<string>();
                reference.OnSystemStartup += system =>
                {
                    created.Add(system);
                    system.SubscribeToAction(SystemAction.Shutdown, _ => observed.Add(string.Format(
                        "{0} object-row={1} system-row={2} alive={3}", created.IndexOf(system),
                        Objects(owner).ContainsKey(type), Systems(owner).ContainsKey(type), system != null)));
                };

                PrefabPoolManager first = owner.AddMonoBehaviour<PrefabPoolManager>();
                owner.ReplacePrefab(seed);
                PrefabPoolManager clone = reference.GetSafe();
                Assert.That(created, Is.EqualTo(new[] { first, clone }));
                Assert.That(observed, Is.EqualTo(new[] { "0 object-row=False system-row=True alive=True" }),
                    "replacement drops the object row before the old system's shutdown");
                Assert.That(Objects(owner)[type], Is.SameAs(clone.gameObject));
                Assert.That(first != null, Is.True, "the old object's destruction is deferred");

                owner.Shutdown();
                Assert.That(observed[1], Is.EqualTo("1 object-row=True system-row=True alive=True"),
                    "full shutdown tears systems down while their objects are still owned");
                Assert.That(Objects(owner), Is.Empty);
                Assert.That(Systems(owner), Is.Empty);
                Assert.That(clone != null, Is.True, "Shutdown schedules rather than performs destruction");
                yield return null;
                Assert.That(first == null && clone == null, Is.True);
                Assert.That(seed != null, Is.True);
            }
            finally
            {
                // OnDestroy callbacks finish inside the isolated registry before
                // the preceding domain state is restored.
                try
                {
                    foreach (PrefabPoolManager system in created)
                        if (system != null) Object.DestroyImmediate(system.gameObject);
                    if (seedOwner != null) Object.DestroyImmediate(seedOwner);
                    if (parentOwner != null) Object.DestroyImmediate(parentOwner);
                }
                finally
                {
                    IDictionary registry = (IDictionary)prior[0], actions = (IDictionary)prior[1];
                    registry.Clear();
                    foreach (DictionaryEntry row in priorRegistry) registry.Add(row.Key, row.Value);
                    actions.Clear();
                    foreach (DictionaryEntry row in priorActions) actions.Add(row.Key, row.Value);
                    IList list = (IList)prior[2];
                    list.Clear();
                    foreach (object callback in priorList) list.Add(callback);
                    fields[3].SetValue(null, prior[3]);
                }
            }
        }
    }
}
