using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // ToggleCursorVisibility gameplay callback and module check against genuine
    // EventSystems. The toggle stays inactive, so Start never subscribes or waits;
    // no App is registered, since neither path reads it. Rows created by the
    // toggle's field initialisers and the input module's coroutine host are owned.
    public sealed class ToggleCursorVisibilityGameplayTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // OnGameplayControlActive 0x06002fd5, x86-64 0x609de0, is `xor sil, 1` and a
        // tail jump to SetCursorVisibility: it never reads m_forceReleaseCursor.
        // SetCursorVisibility 0x06002fd6, x86-64 0x609cd0, isinst-casts
        // EventSystem.current.currentInputModule to HLInputModule (0x609d2a..0x609daf)
        // and returns on Unity-null (0x609d76..0x609d83) before
        // SetHardwareCursorVisibility and Cursor.visible (0x609d8e..0x609d9b).
        // Catches a force-release guard in the gameplay callback, and writing
        // Cursor.visible for a module that is not an HLInputModule.
        [UnityTest]
        public IEnumerator GameplayCallbackIgnoresForceReleaseAndOtherModulesLeaveTheCursor()
        {
            Require(EventSystem.current == null, "Cursor fixture requires no foreign EventSystem.");
            Require(HLInputModule.OnShutdown == null, "Cursor fixture must not invoke foreign input-module shutdown observers.");
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            var leases = new List<LifecycleSystemRowLease>();
            var objects = new List<GameObject>();
            bool previousCursor = Cursor.visible;
            try
            {
                LifecycleSystemRowLease hostLease = Lease<CoroutineUtils>(leases, registry);
#pragma warning disable CS0618
                Require(CoroutineUtils.Instance == null, "Cursor fixture starts without a utility host.");
#pragma warning restore CS0618
                hostLease.CaptureTypedReference(ProcessManager.GetSystemRef<CoroutineUtils>());
                var host = new GameObject("Lucid cursor gameplay coroutine host");
                objects.Add(host);
                host.AddComponent<CoroutineUtils>();

                LifecycleSystemRowLease appLease = Lease<App>(leases, registry);
                LifecycleSystemRowLease configurationLease = Lease<UIRuntimeConfiguration>(leases, registry);
                ToggleCursorVisibility toggle = Inactive(objects, "Lucid cursor gameplay toggle").AddComponent<ToggleCursorVisibility>();
                appLease.CaptureTypedReference(ProcessManager.GetSystemRef<App>());
                configurationLease.CaptureTypedReference(ProcessManager.GetSystemRef<UIRuntimeConfiguration>());

                // A genuine non-HL module: the cursor and its visibility stay untouched.
                GameObject plainObject = Inactive(objects, "Lucid cursor gameplay plain event system");
                plainObject.AddComponent<EventSystem>();
                StandaloneInputModule plain = plainObject.AddComponent<StandaloneInputModule>();
                plainObject.SetActive(true);
                for (int frame = 0; frame < 5 && EventSystem.current.currentInputModule != plain; ++frame) yield return null;
                Assert.AreSame(plain, EventSystem.current.currentInputModule);
                bool shown = Cursor.visible;
                SetCursor(toggle, !shown);
                Assert.AreEqual(shown, Cursor.visible, "A non-HL module leaves Cursor.visible unchanged.");
                Gameplay(toggle, shown);
                Assert.AreEqual(shown, Cursor.visible);
                objects.Remove(plainObject);
                UnityEngine.Object.DestroyImmediate(plainObject);
                Assert.IsTrue(EventSystem.current == null);

                GameObject eventObject = Inactive(objects, "Lucid cursor gameplay event system");
                eventObject.AddComponent<EventSystem>();
                HLInputModule module = eventObject.AddComponent<HLInputModule>();
                typeof(HLInputModule).GetField("m_inputMonitors", Instance).SetValue(module, new BaseInputMonitor[0]);
                typeof(HLInputModule).GetField("m_ForceModuleActive", Instance).SetValue(module, true);
                eventObject.SetActive(true);
                for (int frame = 0; frame < 5 && EventSystem.current.currentInputModule != module; ++frame) yield return null;
                Assert.AreSame(module, EventSystem.current.currentInputModule);

                typeof(ToggleCursorVisibility).GetMethod("ForceReleaseCursor", Instance).Invoke(toggle, new object[] { true });
                Assert.IsTrue(module.GetHardwareCursorVisibility(), "Force release shows the cursor.");
                Gameplay(toggle, true);
                Assert.IsFalse(module.GetHardwareCursorVisibility(), "Gameplay control hides the cursor despite force release.");
                Assert.IsTrue((bool)typeof(ToggleCursorVisibility).GetField("m_forceReleaseCursor", Instance).GetValue(toggle));
                Gameplay(toggle, false);
                Assert.IsTrue(module.GetHardwareCursorVisibility());
            }
            finally
            {
                // Reverse creation order: the module's shutdown stops its polling
                // coroutine on the owned host, which is destroyed last.
                LifecycleSystemRowLease cleanup = leases.Count == 0 ? null : leases[0];
                Action<string, Action> step = (name, action) => { if (cleanup != null) cleanup.RunCleanup(name, action); else action(); };
                for (int i = objects.Count - 1; i >= 0; --i)
                {
                    GameObject item = objects[i];
                    step("destroy-cursor-gameplay-object", () => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                step("restore-cursor-visibility", () => Cursor.visible = previousCursor);
                for (int i = leases.Count - 1; i >= 0; --i) leases[i].Dispose();
            }
            foreach (LifecycleSystemRowLease lease in leases)
            {
                Assert.IsEmpty(lease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
                Assert.IsEmpty(lease.DiagnosticFailures);
            }
        }

        private static LifecycleSystemRowLease Lease<T>(List<LifecycleSystemRowLease> leases, IDictionary registry) where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            var lease = new LifecycleSystemRowLease(registry, name);
            leases.Add(lease);
            lease.CaptureCreatedRow(ProcessManager.GetSystemRef(name));
            return lease;
        }

        private static GameObject Inactive(List<GameObject> objects, string name)
        {
            var created = new GameObject(name);
            created.SetActive(false);
            objects.Add(created);
            return created;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Gameplay(ToggleCursorVisibility toggle, bool on) =>
            typeof(ToggleCursorVisibility).GetMethod("OnGameplayControlActive", Instance).Invoke(toggle, new object[] { on });

        private static void SetCursor(ToggleCursorVisibility toggle, bool on) =>
            typeof(ToggleCursorVisibility).GetMethod("SetCursorVisibility", Instance).Invoke(toggle, new object[] { on });
    }
}
