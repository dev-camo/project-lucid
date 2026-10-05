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

        [UnityTest]
        public IEnumerator OriginalLoggingHost_RegisteredCallbacksAndRetainedNativeBridgeLifecycle()
        {
            Assert.IsTrue(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>(), "The bounded runtime fixture starts without the game's real logging host.");
            Type type = typeof(HLUnityCore);
            object previousBridge = Field(type, "s_unityCoreNativeBridge").GetValue(null);
            object previousTracking = Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null);
            bool previousLogging = Debug.unityLogger.logEnabled;
            LogType previousFilter = Debug.unityLogger.filterLogType;
            bool priorConfiguration = !ProcessManager.IsSystemNull<SystemConfiguration>();
            SystemConfiguration createdConfiguration = null;
            var config = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            Field(config.LowMemoryConfig.GetType(), "m_enabled").SetValue(config.LowMemoryConfig, true);
            Field(config.LowMemoryConfig.GetType(), "m_triggerResourcesUnloadAssets").SetValue(config.LowMemoryConfig, false);
            StackableDataHandle handle = null;
            var objects = new List<GameObject>();
            var callbacks = new List<Application.LogCallback>();
            var shutdown = new List<string>();
            SystemRef untyped = ProcessManager.GetSystemRef(typeof(HLUnityCore).ToString());
            SystemRef<HLUnityCore> typed = ProcessManager.GetSystemRef<HLUnityCore>();
            Action<ISystem> onUntyped = value => shutdown.Add("untyped");
            Action<HLUnityCore> onTyped = value => shutdown.Add("typed");
            untyped.OnSystemShutdown += onUntyped;
            typed.OnSystemShutdown += onTyped;
            try
            {
                // Original runtime configuration enumeration registers the base asset type.
                handle = SystemConfiguration.AddConfig<SystemConfigurationAsset>(config);
                if (!priorConfiguration) createdConfiguration = ProcessManager.GetSystem<SystemConfiguration>();
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
            }
            finally
            {
                untyped.OnSystemShutdown -= onUntyped;
                typed.OnSystemShutdown -= onTyped;
                foreach (GameObject item in objects) if (item != null) Object.DestroyImmediate(item);
                // Probe cleanup only: preserve the original runtime's retained
                // callback behavior while isolating the rest of the test suite.
                foreach (Application.LogCallback callback in callbacks) Application.logMessageReceived -= callback;
                Field(type, "s_unityCoreNativeBridge").SetValue(null, previousBridge);
                Field(type, "s_trackingAuthorisationNativeBridge").SetValue(null, previousTracking);
                if (handle != null) ProcessManager.GetSystem<SystemConfiguration>().StackableData.RemoveOverrides(handle);
                if (createdConfiguration != null) ProcessManager.UnregisterSystem(createdConfiguration);
                if (config != null) Object.DestroyImmediate(config);
                Debug.unityLogger.logEnabled = previousLogging;
                Debug.unityLogger.filterLogType = previousFilter;
            }
        }
    }
}
