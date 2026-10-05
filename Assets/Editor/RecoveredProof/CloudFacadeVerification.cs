using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Facade = HLCloud.Cloud;
using Adapter = HLCloud.Plugin.Cloud;

namespace ProjectLucid
{
    // Native-derived facade proof. This is not the original SaveManager or boot.
    public static class CloudFacadeVerification
    {
        private static int checks;
        private static readonly string[] StoreFields = { "s_internalInstance", "SaveHandlers", "ModifySaveHandlers",
            "ResolveConflictHandlers", "SaveSuccessHandlers", "AfterConflictsResolvedHandlers", "LoadHandlers" };

        public static void Run()
        {
            checks = 0;
            object[] previous = StoreFields.Select(name => Field(typeof(HLPropertyStore), name, true).GetValue(null)).ToArray();
            Adapter priorPlugin = (Adapter)Field(typeof(Facade), "m_plugin", true).GetValue(null);
            HLPropertyStore priorStore = ProcessManager.GetSystemSafe<HLPropertyStore>(null, false);
            var objects = new List<GameObject>();
            try
            {
                VerifyContractsAndStringBuilder();
                var adapter = new MemoryCloud();
                Field(typeof(Facade), "m_plugin", true).SetValue(null, adapter);
                var gameObject = new GameObject("Lucid facade proof"); objects.Add(gameObject); gameObject.SetActive(false);
                Facade cloud = gameObject.AddComponent<Facade>();
                Check(Get<bool>(cloud, "m_useFindGameObject") && !Get<bool>(cloud, "m_isInitialised") && !cloud.Synchronised && !cloud.IsDirty,
                    "original constructor flags");
                Invoke(cloud, "Awake");
                Check(adapter.Trace.Count == 0, "existing static plugin skips entire Awake");
                foreach (string name in StoreFields) Field(typeof(HLPropertyStore), name, true).SetValue(null, null);
                var storage = new MemorySave();
                var store = new HLPropertyStore("fixture", 4, "facade-fixture");
                Set(store, "m_propertyFileStorage", storage);
                ProcessManager.RegisterSystem(store, canReplace: true);
                store.LoadFromIdentifier("slot");
                VerifyKeysAndMessages(cloud, adapter);
                VerifyCallbacks(cloud, adapter, store);
                VerifyIterators(cloud, adapter, store, storage);
                Debug.Log("[Project Lucid] Original cloud-facade bounded checks passed: " + checks);
            }
            finally
            {
                foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                ProcessManager.UnregisterSystem<HLPropertyStore>();
                if (priorStore != null) ProcessManager.RegisterSystem(priorStore, canReplace: true);
                for (int i = 0; i < StoreFields.Length; ++i) Field(typeof(HLPropertyStore), StoreFields[i], true).SetValue(null, previous[i]);
                Field(typeof(Facade), "m_plugin", true).SetValue(null, priorPlugin);
            }
        }

        private static void VerifyContractsAndStringBuilder()
        {
            Il2CppSetOptionAttribute[] options = (Il2CppSetOptionAttribute[])Attribute.GetCustomAttributes(typeof(Facade), typeof(Il2CppSetOptionAttribute));
            Check(options.Length == 2 && options.All(x => (bool)x.Value == false) && options.Select(x => x.Option).Contains(Option.NullChecks), "original compiler options");
            string[] serialized = typeof(Facade).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => Attribute.IsDefined(f, typeof(SerializeField))).Select(f => f.Name).ToArray();
            Check(serialized.SequenceEqual(new[] { "m_useGamePlayerID", "m_registerSelfOnAwake", "m_useFindGameObject", "m_synchronisationCooldownSeconds" }), "four original serialized fields");
            OpString owner = OpString.i; owner += "discard";
            Check(ReferenceEquals(owner, OpString.i) && owner.ToString() == "", "owner access reuses and clears original builder");
            OpString worker1 = null, worker2 = null; Exception failure = null;
            var worker = new Thread(() => { try { worker1 = OpString.i; worker1 += "worker"; worker2 = OpString.i; } catch (Exception e) { failure = e; } });
            worker.Start(); worker.Join();
            Check(failure == null && !ReferenceEquals(worker1, worker2) && !ReferenceEquals(owner, worker1) && worker1.ToString() == "worker" && worker2.ToString() == "", "non-owner independent builders");
            CultureInfo culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                uint[] values = { 0, 1, 9, 10, 99, 100, 999, 1000, 999999999, 1000000000, uint.MaxValue };
                foreach (uint value in values)
                {
                    OpString builder = OpString.i; builder += "prefix"; builder += value;
                    Check(builder.ToString() == "prefix" + value.ToString(CultureInfo.InvariantCulture), "unsigned ASCII digits " + value);
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }
            Expect<ArgumentOutOfRangeException>(() => OpString.Create(-1), "capacity error propagates");
        }

        private static string Key(string name, string id = "slot") => id + HLPropertyStore.GetCRC(name).ToString(CultureInfo.InvariantCulture);
        private static void VerifyKeysAndMessages(Facade cloud, MemoryCloud adapter)
        {
            Check((string)Invoke(cloud, "GetCloudKey", uint.MaxValue, "slot") == "slot4294967295", "key has no separator");
            Check((string)Invoke(cloud, "GetCloudKey", 0u, null) == "0", "null identifier appends nothing");
            object[] found = { "12slot12", "12", null };
            Check((bool)InvokeArgs(cloud, "TryGetCloudSaveIdentifier", found) && (string)found[2] == "slot", "identifier removes every embedded CRC occurrence");
            object[] absent = { "slot", "12", null };
            Check(!(bool)InvokeArgs(cloud, "TryGetCloudSaveIdentifier", absent) && (string)absent[2] == HLPropertyStore.DefaultSaveIdentifier, "identifier out default");
            Invoke(cloud, "SetCloudValueFromProperty", "value", "rings", "slot");
            Check(adapter.Trace.Count == 0, "unsynchronized setter computes key but skips write");
            Set(cloud, "<Synchronised>k__BackingField", true);
            Invoke(cloud, "SetCloudValueFromProperty", "value", "rings", "slot");
            Check(adapter.Values[Key("rings")] == "value", "synchronized setter retains key/value");
            Set(cloud, "m_useFindGameObject", false);
            cloud.Native_CloudDidChange("older"); cloud.Native_CloudDidChange("newer");
            Check(!adapter.Trace.Contains("message:newer"), "queued mode defers latest message");
            Invoke(cloud, "OnUpdate", (object)null);
            Check(adapter.Trace.Last() == "message:newer" && Get<string>(cloud, "m_cacheCloudDidChangeMessage") == "", "update consumes only latest cache");
            cloud.Native_CloudDidChange("retry"); adapter.FailRead = "message";
            Expect<InvalidOperationException>(() => Invoke(cloud, "ConsumeCloudDidChangeMessage"), "message callback error propagates");
            Check(Get<string>(cloud, "m_cacheCloudDidChangeMessage") == "retry", "failed consume retains message");
            adapter.FailRead = null; Set(cloud, "m_useFindGameObject", true); cloud.Native_CloudDidChange("direct");
            Check(adapter.Trace.Last() == "message:direct", "legacy mode directly dispatches");
            string crc = HLPropertyStore.GetCRC("rings").ToString();
            adapter.Values.Clear(); adapter.Values["a" + crc] = ""; adapter.Values["b" + crc] = "";
            var identifiers = (IReadOnlyList<string>)Invoke(cloud, "GetAllSaveIdentifiers", "rings");
            Check(identifiers.Count == 2 && identifiers.Contains("a") && identifiers.Contains("b"), "cloud identifier enumeration");
            adapter.Values.Clear();
            Check(ReferenceEquals(identifiers, Invoke(cloud, "GetAllSaveIdentifiers", "rings")) && identifiers.Count == 0, "returned identifier list aliases next scan");
            adapter.Values["prefix-slot-middle"] = "delete"; adapter.Values["other"] = "keep"; cloud.ResetData("slot");
            Check(!adapter.Values.ContainsKey("prefix-slot-middle") && adapter.Values.ContainsKey("other"), "reset substring matching without synchronization");
        }

        private static void VerifyCallbacks(Facade cloud, MemoryCloud adapter, HLPropertyStore store)
        {
            adapter.Values.Clear();
            HLPropertyList list = store.GetPropertyListFromSave("slot");
            list.AddProperty("rings", "local"); list.AddProperty("other", "local-other");
            cloud.AddPropertyKeyToSync("rings"); adapter.Values[Key("rings")] = "cloud";
            adapter.Values[Key("slot")] = "30";
            Invoke(cloud, "OnInitialise", (object)null);
            Check(Get<bool>(cloud, "m_isInitialised") && HLPropertyStore.IsThereAnyLoadHandler && HLPropertyStore.IsThereAnyAfterConflictsResolvedHandler,
                "initialization immediate-load and subscriptions");
            Dictionary<string, string> changes = Get<Dictionary<string, string>>(cloud, "ChangedCloudKeys");
            Check(changes[Key("rings")] == "cloud" && Get<HashSet<string>>(cloud, "KeysToSync").Contains("slot"), "load names version key and reads subscribed properties");
            changes.Clear(); changes[Key("rings")] = " "; changes[Key("other")] = "ignored";
            Set(cloud, "m_cacheChangedVersion", true); object[] args = { list, false };
            Check((bool)InvokeArgs(cloud, "ModifySaveHandler", args) && (bool)args[1] && list.AsString("rings") == " " && list.AsString("other") == "local-other" && !cloud.IsDirty,
                "existing subscribed values only, whitespace accepted, version resolution and cache clear");
            changes[Key("rings")] = ""; Set(cloud, "m_cacheChangedVersion", true); args = new object[] { list, false };
            Check(!(bool)InvokeArgs(cloud, "ModifySaveHandler", args) && (bool)args[1] && !cloud.IsDirty, "empty change skips mutation but completes version cleanup");
            changes[Key("rings")] = "retained"; Set(cloud, "m_cacheChangedSave", true); args = new object[] { list, false };
            Check(!(bool)InvokeArgs(cloud, "ModifySaveHandler", args) && cloud.IsDirty && !(bool)args[1], "cached save guard retains pending changes");
            Check(cloud.ApplySaveChange() && !cloud.ApplySaveChange(), "apply returns and clears only cached flag");
            changes.Clear(); Set(cloud, "<Synchronised>k__BackingField", false); adapter.Trace.Clear();
            Invoke(cloud, "SaveSuccessHandler", new HLPropertyList());
            Check(cloud.Synchronised && adapter.Trace.SequenceEqual(new[] { "sync" }), "empty successful save synchronizes and establishes flag");
            adapter.Trace.Clear(); changes["dirty"] = "x"; Invoke(cloud, "SaveSuccessHandler", list);
            Check(adapter.Trace.Count == 0, "dirty success handler skips upload and sync"); changes.Clear();
            Invoke(cloud, "OnShutdown", (object)null);
            Check(!Get<bool>(cloud, "m_isInitialised") && !HLPropertyStore.IsThereAnyLoadHandler && !HLPropertyStore.IsThereAnyModifySaveHandler &&
                !HLPropertyStore.IsThereAnySaveSuccessHandler && HLPropertyStore.IsThereAnyAfterConflictsResolvedHandler, "shutdown retains original mismatched after-conflicts subscription");
            long before = DateTime.Now.Ticks; Check(HLPropertyStore.SaveImmediate(), "retained callback still permits store write");
            Check(long.Parse(list.AsString("slot")) >= before, "after-conflicts callback writes local tick version after shutdown");
        }

        private static void VerifyIterators(Facade cloud, MemoryCloud adapter, HLPropertyStore store, MemorySave storage)
        {
            Set(cloud, "m_isInitialised", false);
            IEnumerator wait = (IEnumerator)Invoke(cloud, "WaitUntilReady");
            Check(wait.MoveNext() && wait.Current == null, "readiness waits for initialization");
            Set(cloud, "m_isInitialised", true); Check(!wait.MoveNext(), "ready iterator finishes");
            Dictionary<string, string> changes = Get<Dictionary<string, string>>(cloud, "ChangedCloudKeys");
            changes.Clear(); changes["old"] = "retained"; store.AddProperty("slot", "10");
            adapter.Values.Clear(); adapter.Values[Key("slot")] = "11"; adapter.Values["mutable"] = "result";
            string[] keys = { "original" };
            IEnumerator callback = (IEnumerator)Invoke(cloud, "OnCloudChangeWaitForRequiredSystems", keys, HLCloud.Plugin.ChangeReason.AccountChange);
            Check(callback.MoveNext() && callback.Current is IEnumerator && changes.Count == 1, "callback always yields readiness child before mutation");
            keys[0] = "mutable"; Check(!((IEnumerator)callback.Current).MoveNext(), "ready child has no null yields");
            ((IDisposable)callback).Dispose(); Check(!callback.MoveNext(), "original no-op Dispose retains callback continuation");
            Check(changes["mutable"] == "result" && changes["old"] == "retained" && cloud.CloudChangeInformation.IsValid && cloud.CloudChangeInformation.LocalVersion == 10 &&
                cloud.CloudChangeInformation.CloudVersion == 11 && ReferenceEquals(cloud.CloudChangeInformation.ChangedKeys, changes), "changed array retained, versions and dictionary alias");
            var info = cloud.CloudChangeInformation; changes.Clear(); Check(info.ChangedKeys.Count == 0, "previous info observes later cache clear");
            storage.Identifiers = new[] { "slot", "other" }; adapter.Values.Clear(); adapter.Values["third" + HLPropertyStore.GetCRC("rings")] = "new";
            storage.Trace.Clear(); IEnumerator all = cloud.SyncAllSaveIdentifiers("rings");
            Check(all.MoveNext() && all.Current is IEnumerator, "all-ID synchronization defers behind one child");
            Check(!((IEnumerator)all.Current).MoveNext() && !all.MoveNext(), "all-ID loop finishes synchronously after readiness");
            Check(store.GetSaveIdentifier() == "third" && storage.Trace.Where(x => x.StartsWith("save:")).SequenceEqual(new[] { "save:slot", "save:other", "save:third" }),
                "all IDs loaded/saved without restoring selection");
        }

        private static FieldInfo Field(Type type, string name, bool isStatic = false) => type.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
        private static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => InvokeArgs(target, name, args);
        private static object InvokeArgs(object target, string name, object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsByRef || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static void Check(bool value, string message) { ++checks; if (!value) throw new InvalidOperationException("Cloud facade proof: " + message); }
        private static void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { Check(true, message); return; }
            throw new InvalidOperationException("Cloud facade proof expected " + typeof(T).Name + ": " + message);
        }

        private sealed class MemoryCloud : Adapter
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public readonly List<string> Trace = new List<string>();
            public string FailRead;
            public override void InitializeWithGameObjectName(string n, bool id) { Trace.Add("init"); }
            public override bool Synchronize() { Trace.Add("sync"); return true; }
            public override HashSet<string> RetrieveAllCloudKeys(string separator) { return new HashSet<string>(Values.Keys); }
            public override string StringForKey(string key) { Trace.Add("read:" + key); if (key == FailRead) throw new InvalidOperationException("fixture read"); return Values.TryGetValue(key, out string value) ? value : ""; }
            public override void SetStringForKey(string value, string key) { Trace.Add("write:" + key); Values[key] = value; }
            public override void RemoveForKey(string key) { Trace.Add("remove:" + key); Values.Remove(key); }
            public override void CloudDidChange(string message) { if (FailRead == "message") throw new InvalidOperationException("fixture message"); Trace.Add("message:" + message); }
            public override float FloatForKey(string key) => throw new NotSupportedException();
            public override void SetFloatForKey(float value, string key) => throw new NotSupportedException();
            public override int IntForKey(string key) => throw new NotSupportedException();
            public override void SetIntForKey(int value, string key) => throw new NotSupportedException();
            public override bool BoolForKey(string key) => throw new NotSupportedException();
            public override void SetBoolForKey(bool value, string key) => throw new NotSupportedException();
        }
        private sealed class MemorySave : IHLSaveMethod
        {
            public IReadOnlyList<string> Identifiers;
            public readonly List<string> Trace = new List<string>();
            public bool TryGetSaveVersion(string id, out long version) { version = 0; return false; }
            public IReadOnlyList<string> GetAllSaveIdentifiers() => Identifiers;
            public bool SaveData(HLPropertyStore.FileType type, StringBuilder content, string id) { Trace.Add("save:" + id); return true; }
            public bool BackupData(string id) { return true; }
            public void WipeSaveFile(string id) { }
            public void WipeAllSaveFiles() { }
            public string LoadData(HLPropertyStore.FileType type, string id) { Trace.Add("load:" + id); return null; }
        }
    }
}
