using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // ApplicationTransitionFontsReady.DoUpdate 0x0600084d, x86-64 0x663500, against
    // an owned inactive App in an owned registry row. App.Awake never runs, so the
    // only construction effects are its field initialisers' two reference rows.
    public sealed class OriginalFontReadinessUpdateTests : OwnedGraphNamesFixture
    {
        public OriginalFontReadinessUpdateTests() : base("Lucid-font-readiness-update-") { }

        // GetSystemRef<App> 0x663583; Storage.GetValue<bool>(FontsFallbackPreloadReady
        // static +0x350, false, true) 0x66365e; App re-read 0x663671; GetValue<bool>
        // (FontsChangeLanguageReady +0x360, false, true) 0x66371e; `and al, r14b`
        // 0x663721 with no branch between the reads. The user argument is unused.
        // Catches && short-circuiting (second key never stored) and storeDefault false.
        [Test]
        public void UpdateReadsAndStoresBothKeysEagerlyFromTheRegisteredApp()
        {
            IDictionary registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            string[] fieldRows = { ProcessManager.GetDefaultName<SaveManager>(), ProcessManager.GetDefaultName<UIScreenTransitionManager>() };
            // App's field initialisers auto-register these rows and cache one typed
            // reference in each. An earlier lookup may have left a row unoccupied;
            // snapshot such a borrowed row's object and whole typed cache so cleanup
            // removes only what this fixture added and leaves the row as found.
            Func<object, bool>[] typedIsNull =
            {
                o => o is SystemRef<SaveManager> r && r.IsNull(),
                o => o is SystemRef<UIScreenTransitionManager> r && r.IsNull(),
            };
            bool[] existed = new bool[fieldRows.Length];
            object[] borrowedRows = new object[fieldRows.Length];
            var borrowedCaches = new Dictionary<object, object>[fieldRows.Length];
            for (int i = 0; i < fieldRows.Length; i++)
            {
                existed[i] = registry.Contains(fieldRows[i]);
                if (!existed[i]) continue;
                Assert.IsTrue(ProcessManager.IsSystemNull(fieldRows[i]), "Occupied row " + fieldRows[i]);
                borrowedRows[i] = registry[fieldRows[i]];
                borrowedCaches[i] = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in TypedCache(borrowedRows[i])) borrowedCaches[i].Add(entry.Key, entry.Value);
            }
            GameObject host = null;
            try
            {
                using (var appRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<App>()))
                {
                    host = new GameObject("Owned inactive font-readiness App");
                    host.SetActive(false);
                    App app = host.AddComponent<App>();
                    Assert.IsFalse(host.activeInHierarchy);
                    appRow.Register(app);
                    IGraphStorage storage = app.Storage;
                    var machine = new FiniteStateMachine("Lucid-font-readiness-update-" + Guid.NewGuid().ToString("N"),
                        skipAddToManager: true);
                    IFSMTransition transition = ApplicationTransitionFontsReady.ConstructInstance(machine,
                        new FSMIdentifier(110010104), null);
                    var context = new FSMUpdateContext(0f, FSMUpdateType.Update);

                    Assert.IsFalse(storage.HasValue<bool>(AppFSMKeys.FontsFallbackPreloadReady));
                    Assert.IsFalse(storage.HasValue<bool>(AppFSMKeys.FontsChangeLanguageReady));
                    Assert.IsFalse(transition.Update(null, context));
                    Assert.IsTrue(storage.TryGetValue(AppFSMKeys.FontsFallbackPreloadReady, out bool preload));
                    Assert.IsFalse(preload);
                    Assert.IsTrue(storage.TryGetValue(AppFSMKeys.FontsChangeLanguageReady, out bool change),
                        "The second key is read and its default stored even though the first is false");
                    Assert.IsFalse(change);

                    storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true);
                    Assert.IsFalse(transition.Update(null, context));
                    storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, false);
                    storage.SetValue(AppFSMKeys.FontsFallbackPreloadReady, true);
                    Assert.IsFalse(transition.Update(null, context));
                    storage.SetValue(AppFSMKeys.FontsChangeLanguageReady, true);
                    Assert.IsTrue(transition.Update(null, context));
                    Assert.AreSame(storage, app.Storage);
                }
            }
            finally
            {
                try { if (host != null) DestroyWithoutAppShutdown(host); }
                finally
                {
                    // Remove the field-initialiser rows created above and the typed
                    // references added to borrowed rows before checking that none
                    // held a system and every borrowed row matches its snapshot.
                    var unoccupied = new List<KeyValuePair<string, bool>>();
                    for (int i = 0; i < fieldRows.Length; i++)
                    {
                        if (!registry.Contains(fieldRows[i])) continue;
                        unoccupied.Add(new KeyValuePair<string, bool>(fieldRows[i], ProcessManager.IsSystemNull(fieldRows[i])));
                        IDictionary cache = TypedCache(registry[fieldRows[i]]);
                        foreach (DictionaryEntry entry in new Hashtable(cache))
                        {
                            if (existed[i] && borrowedCaches[i].ContainsKey(entry.Key)) continue;
                            if (existed[i]) cache.Remove(entry.Key);
                            unoccupied.Add(new KeyValuePair<string, bool>(fieldRows[i] + " typed " + entry.Key, typedIsNull[i](entry.Value)));
                        }
                        if (!existed[i]) registry.Remove(fieldRows[i]);
                    }
                    foreach (KeyValuePair<string, bool> reference in unoccupied) Assert.IsTrue(reference.Value, reference.Key);
                    for (int i = 0; i < fieldRows.Length; i++)
                    {
                        if (!existed[i]) continue;
                        Assert.IsTrue(registry.Contains(fieldRows[i]), fieldRows[i]);
                        Assert.AreSame(borrowedRows[i], registry[fieldRows[i]], fieldRows[i]);
                        IDictionary cache = TypedCache(borrowedRows[i]);
                        Assert.AreEqual(borrowedCaches[i].Count, cache.Count, fieldRows[i]);
                        foreach (KeyValuePair<object, object> entry in borrowedCaches[i])
                            Assert.AreSame(entry.Value, cache[entry.Key], fieldRows[i] + " typed " + entry.Key);
                    }
                }
            }
        }

        // ProcessManager.SystemInfo.SystemRefDictionary: the row's typed reference cache.
        private static IDictionary TypedCache(object row) => (IDictionary)row.GetType()
            .GetField("SystemRefDictionary", BindingFlags.Public | BindingFlags.Instance).GetValue(row);

        // Unity sends OnDestroy only to objects that were once active (and App has no
        // ExecuteAlways for EditMode), so this never activated App must not reach
        // OnDestroy 0x06000561 and its global ProcessSystemAction(Shutdown/AppShutdown).
        // Every dispatch clears the scratch
        // s_actionList and TryGetOrNew adds a missing action row, so an unchanged
        // lookup plus a surviving owned sentinel in the list proves no dispatch ran.
        private static void DestroyWithoutAppShutdown(GameObject host)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var lookup = (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager)
                .GetField("s_systemActionLookup", flags).GetValue(null);
            var pending = (List<Action<object>>)typeof(ProcessManager).GetField("s_actionList", flags).GetValue(null);
            FieldInfo inProgress = typeof(ProcessManager).GetField("s_systemActionInProgress", flags);
            var rows = new List<KeyValuePair<SystemAction, KeyValuePair<Dictionary<ISystem, Action<object>>, int>>>();
            foreach (KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>> row in lookup)
                rows.Add(new KeyValuePair<SystemAction, KeyValuePair<Dictionary<ISystem, Action<object>>, int>>(row.Key,
                    new KeyValuePair<Dictionary<ISystem, Action<object>>, int>(row.Value, row.Value.Count)));
            Action<object>[] listed = pending.ToArray();
            object flag = inProgress.GetValue(null);
            int capacity = pending.Capacity;
            Action<object> sentinel = _ => { };
            bool sentinelKept;
            pending.Add(sentinel);
            try { UnityEngine.Object.DestroyImmediate(host); }
            finally
            {
                sentinelKept = pending.Count == listed.Length + 1 && ReferenceEquals(pending[listed.Length], sentinel);
                pending.Remove(sentinel);
                if (pending.Count <= capacity) pending.Capacity = capacity;
            }
            Assert.IsTrue(sentinelKept, "No Shutdown/AppShutdown dispatch cleared s_actionList");
            CollectionAssert.AreEqual(listed, pending);
            Assert.AreEqual(flag, inProgress.GetValue(null));
            Assert.AreEqual(rows.Count, lookup.Count, "No action row was added");
            foreach (var row in rows)
            {
                Assert.IsTrue(lookup.TryGetValue(row.Key, out Dictionary<ISystem, Action<object>> callbacks), row.Key.ToString());
                Assert.AreSame(row.Value.Key, callbacks, row.Key.ToString());
                Assert.AreEqual(row.Value.Value, callbacks.Count, row.Key.ToString());
            }
        }
    }
}
