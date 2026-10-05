using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded original Splash cleanup/shutdown state proof, not an authored boot or whole-game run.
    public static class BootPersistenceStateVerification
    {
        private static int checks;
        private static readonly string[] StoreStatics = { "s_internalInstance", "SaveHandlers", "ModifySaveHandlers", "ResolveConflictHandlers", "SaveSuccessHandlers", "AfterConflictsResolvedHandlers", "LoadHandlers" };
        private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action callback, string message) where T : Exception
        { checks++; try { callback(); } catch (T) { return; } throw new InvalidOperationException(message); }
        private static string Name() => "ProjectLucid-Boot-state-proof-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FieldInfo Static(string name) => typeof(HLPropertyStore).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        private static HLPropertyStore Store(MemoryStorage memory, bool loaded = true)
        {
            foreach (string name in StoreStatics) Static(name).SetValue(null, null);
            var store = new HLPropertyStore("fixture-only", 0, "ProjectLucid-Boot-state-proof");
            typeof(HLPropertyStore).GetField("m_propertyFileStorage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(store, memory);
            if (loaded) store.LoadFromIdentifier("probe");
            memory.Calls.Clear();
            return store;
        }
        public static void Run()
        {
            Execute(); Debug.Log("Original Boot cleanup/shutdown verified checks=" + checks);
        }
        public static int RunManaged() { Execute(); return checks; }
        private static void Execute()
        {
            checks = 0;
            object[] previous = StoreStatics.Select(name => Static(name).GetValue(null)).ToArray();
            HLPropertyStore registered = ProcessManager.GetSystemSafe<HLPropertyStore>(autoRegister: false);
            try { VerifyKeysAndFactories(); VerifyCleanup(); VerifyCurrentStorageAndSingleton(); VerifyShutdown(); }
            finally
            {
                ProcessManager.UnregisterSystem<HLPropertyStore>();
                if (registered != null) ProcessManager.RegisterSystem(registered);
                for (int i = 0; i < StoreStatics.Length; i++) Static(StoreStatics[i]).SetValue(null, previous[i]);
            }
        }
        private static IFSMState Cleanup() => ApplicationStateBootCleanup.ConstructInstance(Machine(), Name(), "invalid unused json");
        private static IFSMState Shutdown() => ApplicationStateBootShutdown.ConstructInstance(Machine(), Name(), null);
        private static void VerifyKeysAndFactories()
        {
            FieldInfo[] keys = typeof(AppFSMKeys).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Check(keys.Length == 67 && keys.All(field => field.IsInitOnly && field.FieldType == typeof(GraphStorageKey)), "all original67 key identities/readonly types");
            foreach (FieldInfo field in keys)
                Check(((GraphStorageKey)field.GetValue(null)).Equals(new GraphStorageKey(field.Name == "SceneOpKey" ? "SceneOperation" : field.Name, 0, 0)), "original key name/zero node/zero graph scope: " + field.Name);
            Check(AppFSMKeys.SceneOpKey.Equals(new GraphStorageKey("SceneOperation", 0, 0)), "SceneOpKey uses complete original enum registry");
            Check(AppFSMKeys.LoadedLevels.Equals(new GraphStorageKey("LoadedLevels", 0, 0)), "original loaded-levels key scope");
            Check(AppFSMKeys.StateEvents.Equals(new GraphStorageKey("StateEvents", 0, 0)), "original event-list key scope");
            Check(AppFSMKeys.BootHLPropertyStoreList.Equals(new GraphStorageKey("BootHLPropertyStoreList", 0, 0)), "original boot property snapshot key scope");
            var machine = Machine(); FSMIdentifier id = Name();
            var cleanup = ApplicationStateBootCleanup.ConstructInstance(machine, id, "malformed");
            Check(cleanup.StateId == id.Id && cleanup.FSMId == machine.FSMId && cleanup is ApplicationStateBootCleanup, "cleanup original factory ignores JSON and retains name roundtrip");
            var shutdown = ApplicationStateBootShutdown.ConstructInstance(machine, id, "malformed");
            Check(shutdown.StateId == id.Id && shutdown.GetStateTransitions().Count == 0, "shutdown original factory ignores JSON");
            Check(typeof(ApplicationStateBootCleanup).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "cleanup adds no synthetic fields");
            Check(typeof(ApplicationStateBootShutdown).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "shutdown adds no synthetic fields");
        }
        private static void VerifyCleanup()
        {
            foreach (int condition in new[] { 0, 1, 2, 3 })
            {
                var memory = new MemoryStorage(); var store = Store(memory);
                ProcessManager.RegisterSystem(store, canReplace: true);
                store.AddProperty("HL_First_Boot", true);
                var user = new FSMUser();
                if (condition != 0)
                {
                    var properties = new HLPropertyList();
                    if (condition != 3) properties.AddProperty("HL_First_Boot", condition == 1);
                    user.Storage.SetValue(AppFSMKeys.BootHLPropertyStoreList, properties);
                }
                int saves = 0;
                HLPropertyStore.AddSaveHandler(properties => { saves++; Check(properties.AsBool("HL_First_Boot", true) == false, "first boot flag written before save callback"); });
                Cleanup().OnEnter(user, null);
                bool expectedSave = condition != 2;
                Check(saves == (expectedSave ? 1 : 0), "missing/true/default snapshot saves; false snapshot suppresses save");
                Check(store.GetPropertyValue("HL_First_Boot") == (expectedSave ? "False" : "True"), "cleanup preserves original conditional flag write");
                Check(!user.Storage.HasValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList), "successful cleanup removes snapshot");
                Check(memory.Calls.SequenceEqual(expectedSave ? new[] { "save", "backup" } : Array.Empty<string>()), "cleanup uses immediate primary/backup store flow");
            }
            var failingMemory = new MemoryStorage(); var failingStore = Store(failingMemory);
            ProcessManager.RegisterSystem(failingStore, canReplace: true);
            var failingUser = new FSMUser(); failingUser.Storage.SetValue(AppFSMKeys.BootHLPropertyStoreList, new HLPropertyList());
            HLPropertyStore.AddSaveHandler(properties => throw new MarkerException());
            Throws<MarkerException>(() => Cleanup().OnEnter(failingUser, null), "save callback error propagates");
            Check(failingUser.Storage.HasValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList) && !failingStore.CanSave, "save error prevents snapshot removal and keeps original Saving state");
            var nullMemory = new MemoryStorage(); var nullStore = Store(nullMemory);
            ProcessManager.RegisterSystem(nullStore, canReplace: true);
            var nullUser = new FSMUser(); nullUser.Storage.SetValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList, null);
            Throws<NullReferenceException>(() => Cleanup().OnEnter(nullUser, null), "present null snapshot is not repaired");
            Check(nullUser.Storage.HasValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList) && nullMemory.Calls.Count == 0, "null snapshot failure retains key and skips save");
        }
        private static void VerifyCurrentStorageAndSingleton()
        {
            var memory = new MemoryStorage(); var store = Store(memory);
            ProcessManager.RegisterSystem(store, canReplace: true);
            var first = new FSMStorage(); var second = new FSMStorage();
            var properties = new HLPropertyList(); properties.AddProperty("HL_First_Boot", false);
            first.SetValue(AppFSMKeys.BootHLPropertyStoreList, properties); second.SetValue(AppFSMKeys.BootHLPropertyStoreList, properties);
            var user = new SwitchingUser(first, second);
            Cleanup().OnEnter(user, null);
            Check(user.Reads == 2 && first.HasValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList) && !second.HasValue<HLPropertyList>(AppFSMKeys.BootHLPropertyStoreList), "cleanup re-reads current storage for removal");
            var registeredMemory = new MemoryStorage(); var registered = Store(registeredMemory);
            ProcessManager.RegisterSystem(registered, canReplace: true);
            var currentMemory = new MemoryStorage(); var current = Store(currentMemory);
            Cleanup().OnEnter(new FSMUser(), null);
            Check(registered.GetPropertyValue("HL_First_Boot") == "False" && current.GetPropertyValue("HL_First_Boot") == "", "flag write uses registered store");
            Check(registeredMemory.Calls.Count == 0 && currentMemory.Calls.SequenceEqual(new[] { "save", "backup" }), "immediate save independently uses current singleton");
        }
        private static void VerifyShutdown()
        {
            var memory = new MemoryStorage(); var store = Store(memory);
            SystemRef untyped = ProcessManager.RegisterSystem(store, canReplace: true);
            var typed = ProcessManager.GetSystemRef<HLPropertyStore>();
            bool notified = false;
            Action<ISystem> observe = previous => { notified = true; Check(ReferenceEquals(previous, store) && Static("s_internalInstance").GetValue(null) == null, "store shutdown completes before unregistration notification"); };
            untyped.OnSystemShutdown += observe;
            try { Shutdown().OnEnter(new ThrowingUser(), null); }
            finally { untyped.OnSystemShutdown -= observe; }
            Check(notified && untyped.IsNull() && typed.IsNull() && ProcessManager.IsSystemNull<HLPropertyStore>(), "successful shutdown revokes original registry references");
            Check(memory.Calls.Count == 0, "shutdown performs no replacement save or storage write");
            store = Store(memory); untyped = ProcessManager.RegisterSystem(store, canReplace: true);
            Action<ISystem> fail = previous => throw new MarkerException();
            untyped.OnSystemShutdown += fail;
            try { Throws<MarkerException>(() => Shutdown().OnEnter(null, null), "unregistration callback error propagates"); }
            finally { untyped.OnSystemShutdown -= fail; }
            Check(Static("s_internalInstance").GetValue(null) == null && !untyped.IsNull(), "later unregistration error retains earlier shutdown and unreleased reference");
        }
        private sealed class MarkerException : Exception { }
        private sealed class SwitchingUser : IGraphUser
        {
            private readonly IGraphStorage first, second; public int Reads;
            public SwitchingUser(IGraphStorage first, IGraphStorage second) { this.first = first; this.second = second; }
            public IGraphStorage Storage => ++Reads == 1 ? first : second;
            public void DestroyUser() { }
        }
        private sealed class ThrowingUser : IGraphUser
        {
            public IGraphStorage Storage => throw new MarkerException();
            public void DestroyUser() { }
        }
        private sealed class MemoryStorage : IHLSaveMethod
        {
            public readonly List<string> Calls = new List<string>();
            public bool TryGetSaveVersion(string identifier, out long version) { version = 0; return false; }
            public IReadOnlyList<string> GetAllSaveIdentifiers() => Array.Empty<string>();
            public bool SaveData(HLPropertyStore.FileType type, StringBuilder content, string identifier) { Calls.Add("save"); return true; }
            public bool BackupData(string identifier) { Calls.Add("backup"); return true; }
            public void WipeSaveFile(string identifier) { }
            public void WipeAllSaveFiles() { }
            public string LoadData(HLPropertyStore.FileType type, string identifier) => null;
        }
    }
}
