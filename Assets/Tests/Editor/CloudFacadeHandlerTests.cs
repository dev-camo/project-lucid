using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Facade = HLCloud.Cloud;

namespace ProjectLucid.Tests
{
    // Property-store handler lifecycle and save modification at the original
    // cloud facade. The components stay inactive, so no Awake/plugin work runs.
    public sealed class CloudFacadeHandlerTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly string[] StoreEvents = { "SaveHandlers", "ModifySaveHandlers", "ResolveConflictHandlers",
            "SaveSuccessHandlers", "AfterConflictsResolvedHandlers", "LoadHandlers" };

        private static void RequireUnownedPropertyStore()
        {
            if (typeof(HLPropertyStore).GetField("s_internalInstance", Static).GetValue(null) != null ||
                HLPropertyStore.IsThereAnySaveHandler || HLPropertyStore.IsThereAnyModifySaveHandler ||
                HLPropertyStore.IsThereAnyResolveConflictHandler || HLPropertyStore.IsThereAnySaveSuccessHandler ||
                HLPropertyStore.IsThereAnyAfterConflictsResolvedHandler || HLPropertyStore.IsThereAnyLoadHandler)
                throw new InvalidOperationException("Cloud fixture requires no property-store singleton or foreign store handlers.");
        }

        // Original 0x06000039 (x86-64 0x1a9a350) adds load, after-conflicts,
        // modify-save and save-success handlers. Original 0x0600003b (x86-64
        // 0x1a9a620) removes load, then calls RemoveSaveHandler (0x1a9a79e),
        // modify-save and save-success: the after-conflicts handler stays
        // subscribed. Fails if shutdown is "fixed" to remove the registered
        // after-conflicts handler, or if any of the four removals is lost.
        [Test]
        public void ShutdownRemovesSaveHandlerAndLeavesAfterConflictsHandlerSubscribed()
        {
            RequireUnownedPropertyStore();
            FieldInfo afterConflictsField = typeof(HLPropertyStore).GetField("AfterConflictsResolvedHandlers", Static);
            GameObject owner = null;
            Facade cloud = null;
            try
            {
                owner = new GameObject("Lucid cloud handler lifecycle proof");
                owner.SetActive(false);
                cloud = owner.AddComponent<Facade>();
                Invoke(cloud, "OnInitialise");
                Assert.IsTrue(HLPropertyStore.IsThereAnyLoadHandler);
                Assert.IsTrue(HLPropertyStore.IsThereAnyAfterConflictsResolvedHandler);
                Assert.IsTrue(HLPropertyStore.IsThereAnyModifySaveHandler);
                Assert.IsTrue(HLPropertyStore.IsThereAnySaveSuccessHandler);
                Assert.IsFalse(HLPropertyStore.IsThereAnySaveHandler, "Initialisation never adds a SaveHandler.");

                // Shutdown builds SaveHandler(this.OnPropertyStoreSave) (0x1a9a782)
                // and passes it to RemoveSaveHandler (0x1a9a79e). Add an owned
                // delegate for that same method and target through the public
                // API, so only equal-delegate removal can clear it; an unrelated
                // sentinel would survive even a correct removal.
                var ownedSave = (HLPropertyStore.SaveHandler)Delegate.CreateDelegate(typeof(HLPropertyStore.SaveHandler), cloud,
                    typeof(Facade).GetMethod("OnPropertyStoreSave", Instance));
                HLPropertyStore.AddSaveHandler(ownedSave);
                Assert.IsTrue(HLPropertyStore.IsThereAnySaveHandler, "The owned SaveHandler is present before shutdown.");

                Invoke(cloud, "OnShutdown");
                Assert.IsFalse((bool)typeof(Facade).GetField("m_isInitialised", Instance).GetValue(cloud));
                Assert.IsFalse(HLPropertyStore.IsThereAnyLoadHandler);
                Assert.IsFalse(HLPropertyStore.IsThereAnyModifySaveHandler);
                Assert.IsFalse(HLPropertyStore.IsThereAnySaveSuccessHandler);
                Assert.IsFalse(HLPropertyStore.IsThereAnySaveHandler, "Shutdown removes this cloud's OnPropertyStoreSave SaveHandler.");
                var retained = (Delegate)afterConflictsField.GetValue(null);
                Assert.IsNotNull(retained, "The original shutdown leaves its after-conflicts handler subscribed.");
                Delegate[] handlers = retained.GetInvocationList();
                Assert.AreEqual(1, handlers.Length);
                Assert.AreSame(cloud, handlers[0].Target);
                Assert.AreEqual("OnPropertyStoreSave", handlers[0].Method.Name);
            }
            finally
            {
                try
                {
                    // Release only this facade's handlers, including any left by a
                    // failed step, through each event's own remove accessor; the
                    // backing fields are never written directly.
                    foreach (string name in StoreEvents)
                    {
                        var current = (Delegate)typeof(HLPropertyStore).GetField(name, Static).GetValue(null);
                        if (current == null || cloud == null) continue;
                        MethodInfo remove = typeof(HLPropertyStore).GetEvent(name, Static).GetRemoveMethod(true);
                        foreach (Delegate handler in current.GetInvocationList())
                            if (ReferenceEquals(handler.Target, cloud)) remove.Invoke(null, new object[] { handler });
                    }
                }
                finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
            }
            RequireUnownedPropertyStore();
        }

        // Original 0x06000045 (x86-64 0x1a9b6f0): a cached save or empty change
        // set returns false first; each non-empty change is matched to an
        // existing property by cloud key, requires that property's name in
        // KeysToSync (0x1a9b9b4), and assigns only a differing value. A cached
        // version sets conflictsResolved and clears itself; the changes are
        // always cleared. Fails if the KeysToSync gate, the empty-value skip,
        // the early returns or the final clear is removed or reordered.
        [Test]
        public void ModifySaveMutatesOnlySubscribedChangedPropertiesAndConsumesChanges()
        {
            RequireUnownedPropertyStore();
            HLPropertyStore store = null;
            GameObject owner = null;
            FieldInfo singleton = typeof(HLPropertyStore).GetField("s_internalInstance", Static);
            try
            {
                using (var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<HLPropertyStore>()))
                {
                    store = new HLPropertyStore("fixture-only", 0, "cloud-modify-save-fixture"); // No file I/O.
                    row.Register(store);
                    owner = new GameObject("Lucid cloud modify-save proof");
                    owner.SetActive(false);
                    Facade cloud = owner.AddComponent<Facade>();
                    string id = store.GetSaveIdentifier();
                    var changes = (Dictionary<string, string>)typeof(Facade).GetField("ChangedCloudKeys", Instance).GetValue(cloud);
                    FieldInfo cachedVersion = typeof(Facade).GetField("m_cacheChangedVersion", Instance);
                    FieldInfo cachedSave = typeof(Facade).GetField("m_cacheChangedSave", Instance);
                    foreach (string key in new[] { "changed", "same", "empty" }) cloud.AddPropertyKeyToSync(key);

                    var list = new HLPropertyList();
                    list.AddProperty("changed", "old");
                    list.AddProperty("same", "same");
                    list.AddProperty("empty", "keep");
                    list.AddProperty("unsubscribed", "local");
                    changes[CloudKey(id, "changed")] = "new";
                    changes[CloudKey(id, "same")] = "same";
                    changes[CloudKey(id, "empty")] = "";
                    changes[CloudKey(id, "unsubscribed")] = "remote";
                    changes[id + "no-such-property"] = "orphan";

                    // No recovered writer sets the cached-save flag; the native
                    // branch at 0x1a9b80e is still exercised directly.
                    cachedSave.SetValue(cloud, true);
                    cachedVersion.SetValue(cloud, true);
                    bool conflictsResolved = false;
                    Assert.IsFalse(ModifySave(cloud, list, ref conflictsResolved));
                    Assert.IsFalse(conflictsResolved);
                    Assert.AreEqual(5, changes.Count, "A cached save returns before consuming changes.");
                    Assert.IsTrue((bool)cachedVersion.GetValue(cloud));
                    Assert.AreEqual("old", list.AsString("changed", null));

                    cachedSave.SetValue(cloud, false);
                    Assert.IsTrue(ModifySave(cloud, list, ref conflictsResolved));
                    Assert.AreEqual("new", list.AsString("changed", null));
                    Assert.AreEqual("same", list.AsString("same", null));
                    Assert.AreEqual("keep", list.AsString("empty", null), "Empty cloud values are skipped.");
                    Assert.AreEqual("local", list.AsString("unsubscribed", null), "Only KeysToSync properties are modified.");
                    Assert.AreEqual(4, list.Properties.Count, "Missing properties are never added.");
                    Assert.IsTrue(conflictsResolved);
                    Assert.IsFalse((bool)cachedVersion.GetValue(cloud));
                    Assert.AreEqual(0, changes.Count);

                    // Empty change set: the Count test (0x1a9b82e) branches out
                    // before GetSystemSafe (0x1a9b863), whose result is then
                    // dereferenced unchecked. Revoke the owned store for this
                    // call only, so a reordered lookup faults on null; the
                    // retained row is re-acquired by the same owner.
                    cachedVersion.SetValue(cloud, true);
                    conflictsResolved = false;
                    bool emptyResult;
                    ProcessManager.UnregisterSystem(ProcessManager.GetDefaultName<HLPropertyStore>());
                    try
                    {
                        Assert.IsTrue(ProcessManager.IsSystemNull<HLPropertyStore>());
                        emptyResult = ModifySave(cloud, list, ref conflictsResolved);
                    }
                    finally { row.Register(store); }
                    Assert.IsFalse(emptyResult);
                    Assert.IsFalse(conflictsResolved);
                    Assert.IsTrue((bool)cachedVersion.GetValue(cloud));

                    // Unchanged values report false but still resolve and clear.
                    changes[CloudKey(id, "same")] = "same";
                    Assert.IsFalse(ModifySave(cloud, list, ref conflictsResolved));
                    Assert.IsTrue(conflictsResolved);
                    Assert.IsFalse((bool)cachedVersion.GetValue(cloud));
                    Assert.AreEqual(0, changes.Count);
                }
            }
            finally
            {
                try { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
                finally { if (store != null && ReferenceEquals(singleton.GetValue(null), store)) singleton.SetValue(null, null); }
            }
            RequireUnownedPropertyStore();
        }

        // Cloud key text is the save identifier followed by the decimal CRC.
        private static string CloudKey(string id, string name) => id + HLPropertyStore.GetCRC(name);

        private static void Invoke(Facade cloud, string name) =>
            typeof(Facade).GetMethod(name, Instance).Invoke(cloud, new object[] { null });

        private static bool ModifySave(Facade cloud, HLPropertyList list, ref bool conflictsResolved)
        {
            object[] arguments = { list, conflictsResolved };
            bool result = (bool)typeof(Facade).GetMethod("ModifySaveHandler", Instance).Invoke(cloud, arguments);
            conflictsResolved = (bool)arguments[1];
            return result;
        }
    }
}
