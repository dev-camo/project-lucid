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
    public sealed class LevelManagerSystemsLifecycleTests
    {
        private static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static Dictionary<Type, ISystem> Systems(LevelManagerSystems owner) =>
            (Dictionary<Type, ISystem>)Field(typeof(LevelManagerSystems), "m_managedSystems").GetValue(owner);
        private static Dictionary<Type, GameObject> Objects(LevelManagerSystems owner) =>
            (Dictionary<Type, GameObject>)Field(typeof(LevelManagerSystems), "m_managedGameObjects").GetValue(owner);
        private sealed class CallbackFailure : Exception { }

        private static DictionaryEntry[] Snapshot(IDictionary dictionary)
        {
            var rows = new List<DictionaryEntry>();
            IDictionaryEnumerator iterator = dictionary.GetEnumerator();
            while (iterator.MoveNext()) rows.Add(iterator.Entry);
            return rows.ToArray();
        }

        // Original PrefabPoolManager supplies normal Awake/Update/OnDestroy.
        // Its real SystemRef startup boundary attaches the test observations;
        // no replacement game manager or synthetic component is used.
        [UnityTest]
        public IEnumerator OriginalLevelManagedSystemsLifecycle()
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
                parentOwner = new GameObject("Project Lucid original level ownership parent");
                parentOwner.transform.position = new Vector3(13, -4, 7);
                parentOwner.transform.rotation = Quaternion.Euler(0, 35, 0);
                var owner = new LevelManagerSystems(parentOwner.transform);
                seedOwner = new GameObject("Project Lucid genuine managed prefab seed");
                seedOwner.SetActive(false);
                var seed = seedOwner.AddComponent<PrefabPoolManager>();
                seedOwner.transform.localPosition = new Vector3(2, 3, 4);
                seedOwner.transform.localRotation = Quaternion.Euler(10, 20, 30);
                seedOwner.transform.localScale = new Vector3(2, 3, 4);
                seedOwner.SetActive(true);
                Assert.That(ProcessManager.GetSystemSafe<PrefabPoolManager>(), Is.SameAs(seed), "seed automatic Awake registers the genuine component");
                ProcessManager.UnregisterSystem(seed);
                SystemRef<PrefabPoolManager> reference = ProcessManager.GetSystemRef<PrefabPoolManager>();
                var initialisations = new List<PrefabPoolManager>();
                var shutdowns = new List<PrefabPoolManager>();
                reference.OnSystemStartup += system =>
                {
                    created.Add(system);
                    Assert.That(Objects(owner).ContainsKey(typeof(PrefabPoolManager)), Is.False, "Awake registry notification precedes object ownership publication");
                    system.SubscribeToAction(SystemAction.Initialise, context =>
                    {
                        Assert.That(context, Is.Null);
                        Assert.That(Objects(owner)[typeof(PrefabPoolManager)], Is.SameAs(system.gameObject), "object ownership precedes Initialise");
                        Systems(owner).TryGetValue(typeof(PrefabPoolManager), out ISystem previouslyOwned);
                        Assert.That(previouslyOwned, Is.Not.SameAs(system), "system ownership publication follows Initialise");
                        initialisations.Add(system);
                    });
                    system.SubscribeToAction(SystemAction.Shutdown, context =>
                    {
                        Assert.That(context, Is.Null);
                        Assert.That(system != null, Is.True, "shutdown callback runs before deferred destruction");
                        shutdowns.Add(system);
                    });
                };
                PrefabPoolManager first = owner.AddMonoBehaviour<PrefabPoolManager>();
                Assert.That(first.gameObject.name, Is.EqualTo(typeof(PrefabPoolManager).ToString()));
                Assert.That(first.transform.parent, Is.Null, "AddMonoBehaviour leaves the new object unparented");
                Assert.That(reference.GetSafe(), Is.SameAs(first));
                Assert.That(initialisations, Is.EqualTo(new[] { first }));
                Assert.That(Systems(owner)[typeof(PrefabPoolManager)], Is.SameAs(first));

                int beforeReplacement = Time.frameCount;
                owner.ReplacePrefab(seed);
                PrefabPoolManager clone = reference.GetSafe();
                Assert.That(clone, Is.Not.Null);
                Assert.That(clone, Is.Not.SameAs(first));
                Assert.That(shutdowns, Is.EqualTo(new[] { first }), "old system is shut down before the clone is initialised");
                Assert.That(initialisations, Is.EqualTo(new[] { first, clone }));
                Assert.That(clone.transform.parent, Is.SameAs(parentOwner.transform));
                Assert.That(clone.transform.localPosition, Is.EqualTo(seed.transform.localPosition));
                Assert.That(Quaternion.Angle(clone.transform.localRotation, seed.transform.localRotation), Is.LessThan(0.001f));
                Assert.That(clone.transform.localScale, Is.EqualTo(seed.transform.localScale));
                Assert.That(first != null, Is.True, "Destroy queues the previous object until the frame ends");
                Assert.That(Systems(owner)[typeof(PrefabPoolManager)], Is.SameAs(clone));
                yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(beforeReplacement), "fixture advances a real frame");
                Assert.That(first == null, Is.True, "old object destruction completes during automatic player lifecycle");
                Assert.That(reference.GetSafe(), Is.SameAs(clone), "old OnDestroy does not revoke the replacement's registry entry");

                owner.Shutdown();
                Assert.That(reference.IsNull(), Is.True);
                Assert.That(Systems(owner), Is.Empty);
                Assert.That(Objects(owner), Is.Empty);
                Assert.That(shutdowns, Is.EqualTo(new[] { first, clone }));
                Assert.That(clone != null, Is.True, "shutdown also schedules destruction rather than destroying immediately");
                yield return null;
                Assert.That(clone == null, Is.True);
                Assert.That(seed != null, Is.True, "the supplied prefab is not owned or destroyed by the manager");

                PrefabPoolManager faulted = owner.AddMonoBehaviour<PrefabPoolManager>();
                faulted.SubscribeToAction(SystemAction.Shutdown, _ => { throw new CallbackFailure(); });
                Assert.Throws<CallbackFailure>(owner.Shutdown);
                Assert.That(reference.GetSafe(), Is.SameAs(faulted), "callback failure precedes registry revocation");
                Assert.That(Systems(owner)[typeof(PrefabPoolManager)], Is.SameAs(faulted));
                Assert.That(Objects(owner)[typeof(PrefabPoolManager)], Is.SameAs(faulted.gameObject));
                yield return null;
                Assert.That(faulted != null, Is.True, "failed teardown does not schedule object destruction");
                faulted.UnsubscribeFromAction(SystemAction.Shutdown);
                owner.Shutdown();
                yield return null;
                Assert.That(faulted == null, Is.True, "retry completes the original teardown");
                Assert.That(reference.IsNull(), Is.True);
                Assert.That(parentOwner != null && seedOwner != null, Is.True, "parent and prefab remain outside managed ownership");
            }
            finally
            {
                // Native OnDestroy callbacks must finish inside the isolated
                // registry before the preceding domain state is restored.
                try
                {
                    void DestroyCreated(int index)
                    {
                        if (index >= created.Count) return;
                        try
                        {
                            PrefabPoolManager system = created[index];
                            if (system != null) Object.DestroyImmediate(system.gameObject);
                        }
                        finally { DestroyCreated(index + 1); }
                    }
                    try { DestroyCreated(0); }
                    finally
                    {
                        try { if (seedOwner != null) Object.DestroyImmediate(seedOwner); }
                        finally { if (parentOwner != null) Object.DestroyImmediate(parentOwner); }
                    }
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
