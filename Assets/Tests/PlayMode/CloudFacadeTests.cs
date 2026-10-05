using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HLCloud.Plugin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Facade = HLCloud.Cloud;
using Adapter = HLCloud.Plugin.Cloud;

namespace ProjectLucid.Tests
{
    // Live callback scheduling at the original facade. Full save/game boot and
    // desktop-service acceptance remain separate required scenarios.
    public sealed class CloudFacadeTests
    {
        [UnityTest]
        public IEnumerator OriginalCloudFacade_OverlappingCallbacksAndShutdown()
        {
            Adapter previousPlugin = (Adapter)Field(typeof(Facade), "m_plugin", true).GetValue(null);
            object previousStoreInstance = Field(typeof(HLPropertyStore), "s_internalInstance", true).GetValue(null);
            HLPropertyStore previousStore = ProcessManager.GetSystemSafe<HLPropertyStore>(null, false);
            GameObject gameObject = null;
            try
            {
                ProcessManager.UnregisterSystem<HLPropertyStore>();
                var adapter = new MemoryCloud();
                Field(typeof(Facade), "m_plugin", true).SetValue(null, adapter);
                gameObject = new GameObject("Lucid live cloud facade proof"); gameObject.SetActive(false);
                Facade cloud = gameObject.AddComponent<Facade>();
                gameObject.SetActive(true); // Existing plugin makes original Awake return.
                adapter.Values["mutated"] = "first-result"; adapter.Values["second"] = "second-result";
                string versionKey = "slot" + HLPropertyStore.GetCRC("slot"); adapter.Values[versionKey] = "11";
                string[] firstKeys = { "initial" };
                ((ICloud)cloud).OnCloudChange(firstKeys, ChangeReason.ServerChange);
                Coroutine first = Get<Coroutine>(cloud, "m_cloudChangeCoroutine"); Assert.IsNotNull(first);
                ((ICloud)cloud).OnCloudChange(new[] { "second" }, ChangeReason.AccountChange);
                Coroutine second = Get<Coroutine>(cloud, "m_cloudChangeCoroutine");
                Assert.IsNotNull(second); Assert.AreNotSame(first, second, "Each callback overwrites the shared handle.");
                firstKeys[0] = "mutated";
                yield return null;
                Assert.IsEmpty(adapter.Reads, "Callbacks wait for a property-store system before reading keys.");

                var store = new HLPropertyStore("fixture-only", 4, "live-facade-fixture");
                Field(typeof(HLPropertyStore), "m_activeSaveIdentifier").SetValue(store, "slot");
                store.AddProperty("slot", "10"); // No file I/O; readiness does not require loaded.
                ProcessManager.RegisterSystem(store);
                Assert.IsFalse(store.IsLoaded);
                yield return null;
                Assert.IsEmpty(adapter.Reads, "Store presence alone does not satisfy the initialization gate.");
                Set(cloud, "m_isInitialised", true);
                for (int i = 0; i < 5; ++i) yield return null;
                Dictionary<string, string> changes = Get<Dictionary<string, string>>(cloud, "ChangedCloudKeys");
                Assert.AreEqual("first-result", changes["mutated"], "The original first coroutine survives the second callback and retains its array reference.");
                Assert.AreEqual("second-result", changes["second"]);
                Assert.IsFalse(adapter.Reads.Contains("initial"));
                Assert.AreEqual(2, adapter.Reads.Count(x => x == versionKey));
                Assert.IsNull(Get<Coroutine>(cloud, "m_cloudChangeCoroutine"));
                Assert.IsTrue(cloud.CloudChangeInformation.IsValid);
                Assert.AreEqual(10, cloud.CloudChangeInformation.LocalVersion);
                Assert.AreEqual(11, cloud.CloudChangeInformation.CloudVersion);
                Assert.AreSame(changes, cloud.CloudChangeInformation.ChangedKeys);
                Assert.IsTrue(Get<bool>(cloud, "m_cacheChangedVersion"));
                Assert.IsFalse(cloud.ApplySaveChange(), "A cloud notification does not invent a cached-save flag.");

                Set(cloud, "m_isInitialised", false); adapter.Values["third"] = "after-uninitialized-shutdown";
                ((ICloud)cloud).OnCloudChange(new[] { "third" }, ChangeReason.InitialSyncChange);
                Coroutine third = Get<Coroutine>(cloud, "m_cloudChangeCoroutine");
                Shutdown(cloud);
                Assert.AreSame(third, Get<Coroutine>(cloud, "m_cloudChangeCoroutine"), "Uninitialized shutdown returns before cancellation.");
                Set(cloud, "m_isInitialised", true);
                for (int i = 0; i < 5; ++i) yield return null;
                Assert.AreEqual("after-uninitialized-shutdown", changes["third"]);

                Set(cloud, "m_isInitialised", false); adapter.Values["fourth"] = "must-not-read";
                ((ICloud)cloud).OnCloudChange(new[] { "fourth" }, ChangeReason.QuotaViolationChange);
                Set(cloud, "m_isInitialised", true); Shutdown(cloud);
                Assert.IsNull(Get<Coroutine>(cloud, "m_cloudChangeCoroutine"), "Initialized shutdown stops and clears the currently stored handle.");
                Assert.IsFalse(Get<bool>(cloud, "m_isInitialised"));
                Set(cloud, "m_isInitialised", true);
                for (int i = 0; i < 5; ++i) yield return null;
                Assert.IsFalse(changes.ContainsKey("fourth")); Assert.IsFalse(adapter.Reads.Contains("fourth"));
            }
            finally
            {
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
                ProcessManager.UnregisterSystem<HLPropertyStore>();
                if (previousStore != null) ProcessManager.RegisterSystem(previousStore, canReplace: true);
                Field(typeof(HLPropertyStore), "s_internalInstance", true).SetValue(null, previousStoreInstance);
                Field(typeof(Facade), "m_plugin", true).SetValue(null, previousPlugin);
            }
        }

        private static FieldInfo Field(Type type, string name, bool isStatic = false) => type.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
        private static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static void Shutdown(Facade cloud) => typeof(Facade).GetMethod("OnShutdown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(cloud, new object[] { null });
        private sealed class MemoryCloud : Adapter
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public readonly List<string> Reads = new List<string>();
            public override void InitializeWithGameObjectName(string n, bool id) { }
            public override bool Synchronize() => true;
            public override HashSet<string> RetrieveAllCloudKeys(string separator) => new HashSet<string>(Values.Keys);
            public override string StringForKey(string key) { Reads.Add(key); return Values.TryGetValue(key, out string value) ? value : ""; }
            public override void SetStringForKey(string value, string key) { Values[key] = value; }
            public override void RemoveForKey(string key) { Values.Remove(key); }
            public override void CloudDidChange(string message) => throw new NotSupportedException();
            public override float FloatForKey(string key) => throw new NotSupportedException();
            public override void SetFloatForKey(float value, string key) => throw new NotSupportedException();
            public override int IntForKey(string key) => throw new NotSupportedException();
            public override void SetIntForKey(int value, string key) => throw new NotSupportedException();
            public override bool BoolForKey(string key) => throw new NotSupportedException();
            public override void SetBoolForKey(bool value, string key) => throw new NotSupportedException();
        }
    }
}
