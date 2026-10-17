using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Bounded real-engine lifecycle proof of the recovered original logging
    // host. Studio native-plugin parity and authored App startup are separate.
    public sealed class LoggingHostTests
    {
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static Application.LogCallback Callback(HLUnityCore core) => (Application.LogCallback)Delegate.CreateDelegate(typeof(Application.LogCallback), core, typeof(HLUnityCore).GetMethod("OnHandleLog", BindingFlags.NonPublic | BindingFlags.Instance));

        // Field-only admission of the logging-host row before any reference or
        // activation: a null row can still hold foreign observers and deferred
        // work that the host's registration would run and consume.
        internal static LifecycleSystemRowLease AdmitHostRow(IDictionary registry) => AdmitHostRow(registry, null);

        private static LifecycleSystemRowLease AdmitHostRow(IDictionary registry, Action<string> reportCleanupFailure) =>
            LifecycleSystemRowLease.AdmitAbsentOrIdle(registry, ProcessManager.GetDefaultName<HLUnityCore>(), reportCleanupFailure);

        [UnityTest]
        public IEnumerator OriginalLoggingHost_RegisteredCallbacksAndRetainedNativeBridgeLifecycle()
        {
            // The fixture reports every cleanup failure once, below.
            LifecycleSystemRowLease hostRow = AdmitHostRow(LifecycleSystemRowLease.SharedRegistry, message => { });
            OwnedConfiguration configuration;
            try
            {
                Assert.IsTrue(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>(), "The bounded runtime fixture starts without the game's real logging host.");
                configuration = new OwnedConfiguration();
            }
            catch
            {
                hostRow.Dispose();
                throw;
            }
            Type type = typeof(HLUnityCore);
            object previousBridge = Field(type, "s_unityCoreNativeBridge").GetValue(null);
            object previousTracking = Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null);
            bool previousLogging = Debug.unityLogger.logEnabled;
            LogType previousFilter = Debug.unityLogger.filterLogType;
            var config = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            Field(config.LowMemoryConfig.GetType(), "m_enabled").SetValue(config.LowMemoryConfig, true);
            Field(config.LowMemoryConfig.GetType(), "m_triggerResourcesUnloadAssets").SetValue(config.LowMemoryConfig, false);
            var cleanup = new List<Exception>();
            var diagnostics = new List<Exception>();
            bool completed = false;
            var objects = new List<GameObject>();
            var callbacks = new List<Application.LogCallback>();
            var shutdown = new List<string>();
            SystemRef untyped = null;
            SystemRef<HLUnityCore> typed = null;
            Action<ISystem> onUntyped = value => shutdown.Add("untyped");
            Action<HLUnityCore> onTyped = value => shutdown.Add("typed");
            try
            {
                untyped = ProcessManager.GetSystemRef(typeof(HLUnityCore).ToString());
                hostRow.CaptureCreatedRow(untyped);
                typed = ProcessManager.GetSystemRef<HLUnityCore>();
                hostRow.CaptureTypedReference(typed);
                untyped.OnSystemShutdown += onUntyped;
                typed.OnSystemShutdown += onTyped;
                // Original runtime configuration enumeration registers the base asset type.
                configuration.Add(config);
                Field(type, "s_unityCoreNativeBridge").SetValue(null, null);
                Field(type, "s_trackingAuthorisationNativeBridge").SetValue(null, null);
                Debug.unityLogger.logEnabled = true;
                Debug.unityLogger.filterLogType = LogType.Error;
                var firstObject = new GameObject("Lucid genuine original logging host");
                objects.Add(firstObject);
                HLUnityCore first = firstObject.AddComponent<HLUnityCore>();
                callbacks.Add(Callback(first));
                Assert.AreSame(first, HLUnityCore.Instance, "Unity automatically dispatches the original Awake.");
                Assert.AreSame(first, typed.GetSafe());
                Assert.AreSame(first, untyped.GetSafe());
                Assert.AreSame(config, Field(type, "m_unityCoreConfigurationAsset").GetValue(first));
                object bridge = Field(type, "s_unityCoreNativeBridge").GetValue(null);
                object tracking = Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null);
                Assert.IsInstanceOf<HLUnityCoreNativeBridge>(bridge);
                Assert.IsInstanceOf<HLTrackingAuthorisationNativeBridgeStub>(tracking);

                var errors = new List<string>();
                var firstExceptions = new List<string>();
                first.AddLogErrorHandler((message, code) => errors.Add(message + ":" + code));
                first.AddLogExceptionHandler((message, stack, code) => firstExceptions.Add(message + ":" + code));
                HLOutput.LogError("live-route");
                CollectionAssert.AreEqual(new[] { "live-route:0" }, errors);
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: lucid-live-before-destroy"));
                Debug.LogException(new InvalidOperationException("lucid-live-before-destroy"));
                Assert.AreEqual(1, firstExceptions.Count, "The actual Unity logMessageReceived event reaches the genuine original OnHandleLog callback.");
                StringAssert.Contains("lucid-live-before-destroy", firstExceptions[0]);
                StringAssert.EndsWith(":0", firstExceptions[0]);
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: lucid-live-output-exception"));
                HLOutput.LogException(new InvalidOperationException("lucid-live-output-exception"), firstObject);
                Assert.AreEqual(3, firstExceptions.Count, "Original output exception reports through the actual Unity log event and then its direct registered-host transport.");
                StringAssert.Contains("lucid-live-output-exception", firstExceptions[1]);
                Assert.AreEqual("lucid-live-output-exception:0", firstExceptions[2], "Original output direct route uses Message and code0, rather than HResult.");

                errors.Clear();
                var duplicateObject = new GameObject("Lucid genuine original duplicate logging host");
                objects.Add(duplicateObject);
                HLUnityCore duplicate = duplicateObject.AddComponent<HLUnityCore>();
                callbacks.Add(Callback(duplicate));
                Assert.AreSame(first, HLUnityCore.Instance);
                Assert.AreSame(bridge, Field(type, "s_unityCoreNativeBridge").GetValue(null));
                Assert.AreEqual(2, errors.Count, "Original base Awake emits both registered duplicate diagnostics.");
                StringAssert.StartsWith("Duplicate singletons found of type: Hardlight.HLUnityCore,", errors[0]);
                StringAssert.Contains(duplicateObject.name, errors[0]);
                Assert.AreEqual("Destroying New Object: " + duplicateObject.name + ":0", errors[1]);
                yield return null;
                Assert.IsTrue(duplicate == null && duplicateObject == null, "Original MonoSingleton duplicate handling destroys the entire new GameObject.");
                Assert.AreSame(first, typed.GetSafe());
                Assert.IsEmpty(shutdown, "A duplicate cannot unregister the existing host.");

                Object.Destroy(firstObject);
                yield return null;
                Assert.IsTrue(first == null && HLUnityCore.IsNull());
                Assert.IsTrue(typed.IsNull() && untyped.IsNull());
                CollectionAssert.AreEqual(new[] { "untyped", "typed" }, shutdown);
                Assert.AreSame(bridge, Field(type, "s_unityCoreNativeBridge").GetValue(null), "Original OnDestroy leaves the native bridge assigned.");
                Assert.AreSame(tracking, Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null));
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: lucid-live-after-destroy"));
                Debug.LogException(new InvalidOperationException("lucid-live-after-destroy"));
                Assert.AreEqual(4, firstExceptions.Count, "Original OnDestroy deliberately retains the Unity log callback; the managed destroyed wrapper still receives it.");
                StringAssert.Contains("lucid-live-after-destroy", firstExceptions[3]);

                var replacementObject = new GameObject("Lucid genuine original replacement logging host");
                objects.Add(replacementObject);
                HLUnityCore replacement = replacementObject.AddComponent<HLUnityCore>();
                callbacks.Add(Callback(replacement));
                Assert.AreSame(replacement, HLUnityCore.Instance);
                Assert.AreSame(replacement, typed.GetSafe());
                Assert.AreSame(replacement, untyped.GetSafe());
                Assert.AreSame(bridge, Field(type, "s_unityCoreNativeBridge").GetValue(null));
                var replacementErrors = new List<string>();
                var replacementExceptions = new List<string>();
                replacement.AddLogErrorHandler((message, code) => replacementErrors.Add(message + ":" + code));
                replacement.AddLogExceptionHandler((message, stack, code) => replacementExceptions.Add(message + ":" + code));
                HLOutput.LogError("replacement-route");
                CollectionAssert.AreEqual(new[] { "replacement-route:0" }, replacementErrors, "Registry routing uses the replacement host.");
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: lucid-live-with-replacement"));
                Debug.LogException(new InvalidOperationException("lucid-live-with-replacement"));
                Assert.AreEqual(5, firstExceptions.Count);
                Assert.IsEmpty(replacementExceptions, "Existing bridge makes original EnsureInitialised return before installing a new Unity log callback.");
                Object.Destroy(replacementObject);
                yield return null;
                CollectionAssert.AreEqual(new[] { "untyped", "typed", "untyped", "typed" }, shutdown);
                Assert.IsTrue(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>());
                completed = true;
            }
            finally
            {
                // Every step is attempted; none may replace a body failure.
                Step(cleanup, () => { if (untyped != null) untyped.OnSystemShutdown -= onUntyped; });
                Step(cleanup, () => { if (typed != null) typed.OnSystemShutdown -= onTyped; });
                foreach (GameObject item in objects) Step(cleanup, () => { if (item != null) Object.DestroyImmediate(item); });
                // Probe cleanup only: preserve the original runtime's retained
                // callback behavior while isolating the rest of the test suite.
                foreach (Application.LogCallback callback in callbacks) Step(cleanup, () => Application.logMessageReceived -= callback);
                Step(cleanup, () => Field(type, "s_unityCoreNativeBridge").SetValue(null, previousBridge));
                Step(cleanup, () => Field(type, "s_trackingAuthorisationNativeBridge").SetValue(null, previousTracking));
                Step(cleanup, () => cleanup.AddRange(configuration.Release()));
                Step(cleanup, () => { if (config != null) Object.DestroyImmediate(config); });
                Step(cleanup, () => Debug.unityLogger.logEnabled = previousLogging);
                Step(cleanup, () => Debug.unityLogger.filterLogType = previousFilter);
                Step(cleanup, () =>
                {
                    hostRow.Dispose();
                    foreach (LifecycleSystemRowLease.FailureRecord record in hostRow.CleanupFailures) cleanup.Add(record.Failure);
                });
                // As in LifecycleSystemRowLease, a failing diagnostic is recorded rather than
                // thrown, so the body failure stays primary.
                if (!completed)
                    foreach (Exception failure in cleanup)
                    {
                        try { TestContext.Error.WriteLine("Logging host cleanup also failed: " + failure); }
                        catch (Exception diagnosticFailure) { diagnostics.Add(diagnosticFailure); }
                    }
            }
            Assert.IsEmpty(cleanup, "Every logging host cleanup step completes.");
        }

        private static void Step(List<Exception> failures, Action action)
        {
            try { action(); }
            catch (Exception failure) { failures.Add(failure); }
        }

        // Owns at most one SystemConfiguration row and every override of the assets it adds.
        // An absent row is leased and removed once cleared; a live one is borrowed. A dormant
        // row is refused before AddConfig, because its typed cache may still hold references
        // that AddConfig would replace.
        internal sealed class OwnedConfiguration
        {
            private readonly IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            private readonly string name = ProcessManager.GetDefaultName<SystemConfiguration>();
            private readonly LifecycleSystemRowLease lease;
            private readonly List<SystemConfigurationAsset> assets = new List<SystemConfigurationAsset>();
            private readonly List<Exception> captureFailures = new List<Exception>();
            private bool captured;
            private bool released;

            internal OwnedConfiguration()
            {
                if (!registry.Contains(name)) lease = new LifecycleSystemRowLease(registry, name);
                else if (!RowReference().IsValid()) throw new InvalidOperationException("The fixture refuses a dormant configuration row.");
            }

            internal bool OwnsRow => lease != null;

            // AddOverride stores the override before notifying listeners, so a throwing
            // listener leaves an override without a returned handle; Release finds it by asset.
            internal StackableDataHandle Add(SystemConfigurationAsset asset)
            {
                assets.Add(asset);
                StackableDataHandle handle;
                try { handle = SystemConfiguration.AddConfig<SystemConfigurationAsset>(asset); }
                catch
                {
                    try { CaptureCreatedRow(); }
                    catch (Exception failure) { captureFailures.Add(failure); }
                    throw;
                }
                CaptureCreatedRow();
                return handle;
            }

            internal List<Exception> Release()
            {
                var failures = new List<Exception>(captureFailures);
                if (released) return failures;
                released = true;
                SystemConfiguration system = null;
                List<StackableDataHandle> handles = null;
                Step(failures, () => system = ProcessManager.IsSystemNull(name) ? null : ProcessManager.GetSystem<SystemConfiguration>());
                // A failed scan is recorded like any other step, so unregistration and the lease still run.
                if (system != null) Step(failures, () => handles = (ScanOverride ?? OverridesOf)(system.StackableData));
                if (handles != null)
                    foreach (StackableDataHandle handle in handles)
                        Step(failures, () => system.StackableData.RemoveOverrides(handle));
                if (lease != null)
                {
                    if (system != null) Step(failures, () => ProcessManager.UnregisterSystem(system));
                    Step(failures, () =>
                    {
                        lease.Dispose();
                        foreach (LifecycleSystemRowLease.FailureRecord record in lease.CleanupFailures) failures.Add(record.Failure);
                    });
                }
                return failures;
            }

            // Fault-injection seam for the ownership controls only; null uses the real scan.
            internal Func<StackableData, List<StackableDataHandle>> ScanOverride;

            internal bool HoldsOverride(StackableData data) => OverridesOf(data).Count != 0;

            private List<StackableDataHandle> OverridesOf(StackableData data)
            {
                // Read-only scan of the override table for containers holding an owned asset, matched by
                // reference: Unity equality would also match a destroyed owned asset to a foreign null value.
                var table = (System.Collections.Specialized.OrderedDictionary)typeof(StackableData)
                    .GetField("m_dataDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
                var handles = new List<StackableDataHandle>();
                foreach (DictionaryEntry entry in table)
                    if (entry.Value is Dictionary<int, StackableData.StackableDataContainer> containers)
                        foreach (StackableData.StackableDataContainer container in containers.Values)
                            if (container is StackableData.StackableDataContainer<SystemConfigurationAsset> typed && assets.Exists(asset => ReferenceEquals(asset, typed.Value)))
                            {
                                handles.Add((StackableDataHandle)entry.Key);
                                break;
                            }
                return handles;
            }

            private void CaptureCreatedRow()
            {
                if (lease == null || captured || !registry.Contains(name)) return;
                captured = true;
                lease.CaptureCreatedRow(RowReference());
            }

            private SystemRef RowReference() => (SystemRef)registry[name].GetType().GetField("SystemRef").GetValue(registry[name]);
        }
    }

    // Ownership controls for LoggingHostTests.OwnedConfiguration. Each needs an absent configuration
    // row so it can own every row it touches. In the standard PlayMode context, HLOutput's
    // RuntimeInitializeOnLoadMethod (GetConfig<HLUnityCoreConfigurationAsset>) has already created a
    // live, startup-owned row. Each synchronous control therefore sets that exact row object aside and
    // restores it unchanged; a dormant pre-existing row is refused instead.
    public sealed class LoggingHostConfigurationOwnershipTests
    {
        private object setAsideRow;
        private SystemRef setAsideReference;
        private ISystem setAsideSystem;
        private IDictionary setAsideCache;
        private List<KeyValuePair<object, object>> setAsideEntries;

        [SetUp]
        public void SetAsideStartupConfigurationRow()
        {
            setAsideRow = null;
            if (!Registry.Contains(ConfigurationName)) return;
            object row = Registry[ConfigurationName];
            var reference = (SystemRef)Field(row, "SystemRef");
            Assert.IsTrue(reference.IsValid(), "A dormant configuration row is refused, not set aside.");
            setAsideRow = row;
            setAsideReference = reference;
            setAsideSystem = reference.GetSafe();
            setAsideCache = (IDictionary)Field(row, "SystemRefDictionary");
            setAsideEntries = new List<KeyValuePair<object, object>>();
            foreach (DictionaryEntry entry in setAsideCache) setAsideEntries.Add(new KeyValuePair<object, object>(entry.Key, entry.Value));
            Registry.Remove(ConfigurationName);
        }

        [TearDown]
        public void RestoreStartupConfigurationRow()
        {
            if (setAsideRow == null) return;
            object row = setAsideRow;
            setAsideRow = null;
            // Never overwrite: a row left by a control is a failure to report, not something to replace.
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The control left a configuration row in the set-aside slot.");
            Registry.Add(ConfigurationName, row);
            Assert.AreSame(setAsideReference, Field(row, "SystemRef"));
            Assert.IsTrue(setAsideReference.IsValid());
            Assert.AreSame(setAsideSystem, setAsideReference.GetSafe());
            Assert.AreSame(setAsideCache, Field(row, "SystemRefDictionary"));
            Assert.AreEqual(setAsideEntries.Count, setAsideCache.Count);
            foreach (KeyValuePair<object, object> entry in setAsideEntries) Assert.AreSame(entry.Value, setAsideCache[entry.Key]);
        }

        private static object Field(object row, string name) =>
            row.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(row);

        private static IDictionary Registry => LifecycleSystemRowLease.SharedRegistry;
        private static string ConfigurationName => ProcessManager.GetDefaultName<SystemConfiguration>();

        private static void WithOwnedLiveConfiguration(Action<SystemConfiguration> body)
        {
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The control owns the configuration row.");
            var outer = new LifecycleSystemRowLease(Registry, ConfigurationName);
            var live = new SystemConfiguration();
            try
            {
                outer.CaptureCreatedRow(ProcessManager.RegisterSystem(live));
                body(live);
            }
            finally
            {
                if (ReferenceEquals(ProcessManager.IsSystemNull(ConfigurationName) ? null : ProcessManager.GetSystem<SystemConfiguration>(), live))
                    ProcessManager.UnregisterSystem(live);
                outer.Dispose();
            }
            Assert.IsEmpty(outer.CleanupFailures);
            Assert.IsFalse(Registry.Contains(ConfigurationName));
        }

        // Catches leaving the cleared row behind (the original leak) or a second run tripping over the first.
        [Test]
        public void OwnedConfigurationRemovesAnAbsentRowOnEveryRun()
        {
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The control owns the configuration row.");
            for (int run = 0; run < 2; run++)
            {
                var asset = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
                var owned = new LoggingHostTests.OwnedConfiguration();
                List<Exception> failures;
                try
                {
                    Assert.IsTrue(owned.OwnsRow);
                    Assert.IsNotNull(owned.Add(asset));
                    Assert.IsFalse(ProcessManager.IsSystemNull<SystemConfiguration>());
                }
                finally
                {
                    failures = owned.Release();
                    Object.DestroyImmediate(asset);
                }
                Assert.IsEmpty(failures);
                Assert.IsFalse(Registry.Contains(ConfigurationName), "Run " + run + " removes its cleared row.");
            }
        }

        // Catches unregistering a borrowed configuration, or removing overrides the fixture does not own.
        [Test]
        public void OwnedConfigurationBorrowsALiveRowAndRemovesOnlyItsOwnOverride()
        {
            WithOwnedLiveConfiguration(live =>
            {
                var foreign = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
                var asset = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
                StackableDataHandle foreignHandle = SystemConfiguration.AddConfig<SystemConfigurationAsset>(foreign, 1);
                try
                {
                    object row = Registry[ConfigurationName];
                    var owned = new LoggingHostTests.OwnedConfiguration();
                    Assert.IsFalse(owned.OwnsRow);
                    owned.Add(asset);
                    Assert.IsTrue(owned.HoldsOverride(live.StackableData));
                    Assert.IsEmpty(owned.Release());
                    Assert.IsFalse(owned.HoldsOverride(live.StackableData));
                    Assert.AreSame(row, Registry[ConfigurationName]);
                    Assert.AreSame(live, ProcessManager.GetSystem<SystemConfiguration>());
                    Assert.AreSame(foreign, live.StackableData.GetOverride<SystemConfigurationAsset>(foreignHandle, 1));
                }
                finally
                {
                    live.StackableData.RemoveOverrides(foreignHandle);
                    Object.DestroyImmediate(asset);
                    Object.DestroyImmediate(foreign);
                }
            });
        }

        // Catches borrowing or replacing a dormant row instead of refusing it before AddConfig.
        [Test]
        public void OwnedConfigurationRefusesADormantRowWithoutTouchingIt()
        {
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The control owns the configuration row.");
            var seed = new LifecycleSystemRowLease(Registry, ConfigurationName);
            try
            {
                SystemRef dormant = ProcessManager.GetSystemRef(ConfigurationName);
                seed.CaptureCreatedRow(dormant);
                object row = Registry[ConfigurationName];
                Assert.Throws<InvalidOperationException>(() => new LoggingHostTests.OwnedConfiguration());
                Assert.AreSame(row, Registry[ConfigurationName]);
                Assert.IsTrue(dormant.IsNull());
            }
            finally { seed.Dispose(); }
            Assert.IsEmpty(seed.CleanupFailures);
            Assert.IsFalse(Registry.Contains(ConfigurationName));
        }

        // Injected cleanup failure before the row is released: a throwing listener during override
        // removal must not stop unregistration and row removal, and must be reported, not swallowed.
        [Test]
        public void OwnedConfigurationStillReleasesItsRowAfterAnOverrideRemovalFailure()
        {
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The control owns the configuration row.");
            var asset = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            var owned = new LoggingHostTests.OwnedConfiguration();
            var sentinel = new InvalidOperationException("lucid-injected-cleanup-failure");
            SystemConfiguration created = null;
            Action<int> fail = id => throw sentinel;
            List<Exception> failures;
            try
            {
                owned.Add(asset);
                created = ProcessManager.GetSystem<SystemConfiguration>();
                created.StackableData.OnDataUpdated += fail;
            }
            finally
            {
                failures = owned.Release();
                if (created != null) created.StackableData.OnDataUpdated -= fail;
                Object.DestroyImmediate(asset);
            }
            Assert.AreEqual(1, failures.Count);
            Assert.AreSame(sentinel, failures[0]);
            Assert.IsFalse(owned.HoldsOverride(created.StackableData), "RemoveOverrides drops the entry before notifying.");
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The owned row is still removed.");
        }

        // AddOverride stores the override before notifying listeners. A listener that throws on a
        // borrowed live configuration leaves an override without a handle; Release must still remove it.
        [Test]
        public void OwnedConfigurationRemovesAnOverrideWhoseAddConfigThrew()
        {
            WithOwnedLiveConfiguration(live =>
            {
                var asset = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
                var sentinel = new InvalidOperationException("lucid-injected-add-failure");
                int notifications = 0;
                Action<int> failOnce = id => { if (notifications++ == 0) throw sentinel; };
                live.StackableData.OnDataUpdated += failOnce;
                try
                {
                    var owned = new LoggingHostTests.OwnedConfiguration();
                    Assert.AreSame(sentinel, Assert.Throws<InvalidOperationException>(() => owned.Add(asset)));
                    Assert.IsTrue(owned.HoldsOverride(live.StackableData), "The override was stored before the listener threw.");
                    Assert.IsEmpty(owned.Release());
                    Assert.IsFalse(owned.HoldsOverride(live.StackableData));
                    Assert.AreSame(live, ProcessManager.GetSystem<SystemConfiguration>());
                }
                finally
                {
                    live.StackableData.OnDataUpdated -= failOnce;
                    Object.DestroyImmediate(asset);
                }
            });
        }

        // Injected scan failure on an owned row: Release must record it and still unregister the
        // created system and remove its row. Catches an unguarded scan exiting Release early.
        [Test]
        public void OwnedConfigurationStillReleasesItsRowAfterAnOverrideScanFailure()
        {
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The control owns the configuration row.");
            var asset = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            var owned = new LoggingHostTests.OwnedConfiguration();
            var sentinel = new InvalidOperationException("lucid-injected-scan-failure");
            List<Exception> failures;
            try
            {
                owned.Add(asset);
                owned.ScanOverride = data => throw sentinel;
            }
            finally
            {
                failures = owned.Release();
                Object.DestroyImmediate(asset);
            }
            Assert.AreEqual(1, failures.Count);
            Assert.AreSame(sentinel, failures[0]);
            Assert.IsFalse(Registry.Contains(ConfigurationName), "The owned row is still removed.");
        }
    }
}
