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
    // Input-type cursor handling of the original ToggleCursorVisibility with a
    // genuine EventSystem/HLInputModule pair. The cursor and App objects stay
    // inactive, so their Start/Awake never run; the App is registered exactly
    // as its own Awake registers itself. Start's waits and subscriptions remain
    // separate scenarios.
    public sealed class ToggleCursorVisibilityInputTypeTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        // Original 0x06002fd3 (x86-64 0x609b70) returns while m_forceReleaseCursor
        // (+0x30) is set; Mouse (0xa79e8ba2) and Keyboard (0xbab8e326) show the
        // cursor iff Storage.GetValue(GameplayControlActive, false, true) is false;
        // every other type hides it. 0x06002fd4 (0x609dd0) stores the flag and
        // shows/hides directly; 0x06002fd6 (0x609cd0) updates the HLInputModule.
        // Fails if the force-release guard is removed, if non-pointer types read
        // gameplay control or the App at all, or if the storeDefault argument is
        // dropped.
        [UnityTest]
        public IEnumerator InputTypeChoosesCursorUnlessForceReleased()
        {
            Require(EventSystem.current == null, "Cursor fixture requires no foreign EventSystem.");
            Require(HLInputModule.OnShutdown == null, "Cursor fixture must not invoke foreign input-module shutdown observers.");
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            var leases = new List<LifecycleSystemRowLease>();
            var partialCaptures = new List<Action>();
            var objects = new List<GameObject>();
            bool previousCursor = Cursor.visible;
            App app = null;
            try
            {
                LifecycleSystemRowLease hostLease = Lease<CoroutineUtils>(leases, partialCaptures, registry, true);
#pragma warning disable CS0618
                Require(CoroutineUtils.Instance == null, "Cursor fixture starts without a utility host.");
#pragma warning restore CS0618
                hostLease.CaptureTypedReference(ProcessManager.GetSystemRef<CoroutineUtils>());
                objects.Add(new GameObject("Lucid cursor fixture coroutine host"));
                objects[0].AddComponent<CoroutineUtils>();

                // Rows the App and cursor field initializers create are owned first.
                LifecycleSystemRowLease saveLease = Lease<SaveManager>(leases, partialCaptures, registry, true);
                LifecycleSystemRowLease transitionLease = Lease<UIScreenTransitionManager>(leases, partialCaptures, registry, true);
                LifecycleSystemRowLease configurationLease = Lease<UIRuntimeConfiguration>(leases, partialCaptures, registry, true);
                LifecycleSystemRowLease appLease = Lease<App>(leases, partialCaptures, registry, false);
                GameObject appObject = Inactive(objects, "Lucid cursor fixture app");
                app = appObject.AddComponent<App>();
                saveLease.CaptureTypedReference(ProcessManager.GetSystemRef<SaveManager>());
                transitionLease.CaptureTypedReference(ProcessManager.GetSystemRef<UIScreenTransitionManager>());
                appLease.CaptureCreatedRow(ProcessManager.RegisterSystem(app));
                ToggleCursorVisibility toggle = Inactive(objects, "Lucid cursor fixture toggle").AddComponent<ToggleCursorVisibility>();
                appLease.CaptureTypedReference(ProcessManager.GetSystemRef<App>());
                configurationLease.CaptureTypedReference(ProcessManager.GetSystemRef<UIRuntimeConfiguration>());

                GameObject eventObject = Inactive(objects, "Lucid cursor fixture event system");
                eventObject.AddComponent<EventSystem>();
                HLInputModule module = eventObject.AddComponent<HLInputModule>();
                typeof(HLInputModule).GetField("m_inputMonitors", Instance).SetValue(module, new BaseInputMonitor[0]);
                typeof(HLInputModule).GetField("m_ForceModuleActive", Instance).SetValue(module, true);
                eventObject.SetActive(true);
                for (int frame = 0; frame < 5 && EventSystem.current.currentInputModule != module; ++frame) yield return null;
                Assert.AreSame(module, EventSystem.current.currentInputModule, "The genuine EventSystem activates the owned module.");
                FieldInfo hardware = typeof(HLInputModule).GetField("m_hardwareCursorIsVisible", Instance);
                Func<bool> visible = () => (bool)hardware.GetValue(module);

                module.SetHardwareCursorVisibility(false);
                InputType(toggle, UIInputType.Mouse);
                Assert.IsTrue(visible(), "Mouse shows the cursor without gameplay control.");
                Assert.IsTrue(app.Storage.GetCollection().ContainsKey(AppFSMKeys.GameplayControlActive), "The missing control value is stored as its default.");
                Assert.IsFalse(app.HasGameplayControl());

                app.Storage.SetValue(AppFSMKeys.GameplayControlActive, true);
                InputType(toggle, UIInputType.Keyboard);
                Assert.IsFalse(visible(), "Keyboard hides the cursor during gameplay control.");

                app.Storage.SetValue(AppFSMKeys.GameplayControlActive, false);
                module.SetHardwareCursorVisibility(true);
                // Non-pointer types branch (0x609bd4) straight to
                // SetCursorVisibility(false) at 0x609c79, never reaching the
                // m_appRef.Get at 0x609be8. Revoke the App for this call only,
                // so any App or storage read faults; then re-register it.
                var appRef = (SystemRef<App>)typeof(ToggleCursorVisibility).GetField("m_appRef", Instance).GetValue(toggle);
                ProcessManager.UnregisterSystem(app);
                try
                {
                    Assert.IsNull(appRef.GetSafe(), "The toggle's App reference is revoked for the Touch call.");
                    InputType(toggle, UIInputType.Touch);
                }
                finally { ProcessManager.RegisterSystem(app); }
                Assert.AreSame(app, appRef.GetSafe());
                Assert.IsFalse(visible(), "Non-pointer types hide the cursor without reading the App.");

                app.Storage.SetValue(AppFSMKeys.GameplayControlActive, true);
                Force(toggle, true);
                Assert.IsTrue(visible(), "Force release shows the cursor.");
                InputType(toggle, UIInputType.Mouse);
                InputType(toggle, UIInputType.Touch);
                Assert.IsTrue(visible(), "Input-type changes are ignored while force-released.");

                Force(toggle, false);
                Assert.IsFalse(visible());
                app.Storage.SetValue(AppFSMKeys.GameplayControlActive, false);
                InputType(toggle, UIInputType.Mouse);
                Assert.IsTrue(visible(), "Clearing force release restores input-type handling.");
            }
            finally
            {
                // Reverse creation order: the module's shutdown stops its polling
                // coroutine on the owned host, which is destroyed last.
                LifecycleSystemRowLease cleanup = leases.Count == 0 ? null : leases[0];
                Action<string, Action> step = (name, action) => { if (cleanup != null) cleanup.RunCleanup(name, action); else action(); };
                // Every success-path capture precedes the first yield, so a typed
                // reference still uncaptured here was left by a constructor or
                // capture that failed before any other frame ran. Capture it
                // without creating one, so the owned row can still be released.
                foreach (Action capture in partialCaptures) step("capture-partial-typed-reference", capture);
                step("unregister-cursor-app", () => { if (app != null) ProcessManager.UnregisterSystem(app); });
                for (int i = objects.Count - 1; i >= 0; --i)
                {
                    GameObject item = objects[i];
                    step("destroy-cursor-fixture-object", () => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
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

        private static LifecycleSystemRowLease Lease<T>(List<LifecycleSystemRowLease> leases, List<Action> partialCaptures,
            IDictionary registry, bool createRow) where T : class, ISystem
        {
            string name = ProcessManager.GetDefaultName<T>();
            var lease = new LifecycleSystemRowLease(registry, name);
            leases.Add(lease);
            partialCaptures.Add(() => lease.CaptureTypedReferenceIfCreated<T>());
            if (createRow) lease.CaptureCreatedRow(ProcessManager.GetSystemRef(name));
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

        private static void InputType(ToggleCursorVisibility toggle, UIInputType type) =>
            typeof(ToggleCursorVisibility).GetMethod("OnInputTypeChanged", Instance).Invoke(toggle, new object[] { type });

        private static void Force(ToggleCursorVisibility toggle, bool release) =>
            typeof(ToggleCursorVisibility).GetMethod("ForceReleaseCursor", Instance).Invoke(toggle, new object[] { release });
    }
}
