using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Genuine StackedCamera lifecycle on owned cameras over an isolated
    // ProcessManager registry holding a genuinely constructed CameraStackManager.
    // The registry is restored by reference after success and faults.
    public sealed class OriginalStackedCameraGapTests
    {
        private const BindingFlags OwnFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Reset 0x06000e68 (x86-64 0x726450) stores GetComponent<Camera> (+0x20)
        // and then GetComponent<AudioListener> (+0x28). OnEnable 0x06000e69
        // (0x7264d0) stores the auto-registering typed reference (+0x30) and then
        // InvokeOnValid, so a valid manager loads the camera at once; OnDisable
        // 0x06000e6c (0x726610) unloads only when GetSafe is nonnull. Loading
        // makes the camera the base and re-enables its listener; unloading
        // disables that listener first. For the base camera the manager then
        // reads URP's cameraStack, which faults while the project has no render
        // pipeline asset (its scriptableRenderer is null); that engine-config
        // fault is expected explicitly. Dropping the load or the unload fails.
        [Test]
        public void EnableLoadsAndDisableUnloadsThroughRegisteredManager()
        {
            FieldInfo registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registryField.GetValue(null);
            var allocations = new List<UObject>();
            try
            {
                registryField.SetValue(null, Activator.CreateInstance(registryField.FieldType));
                var manager = new CameraStackManager();
                ProcessManager.RegisterSystem(manager);

                StackedCamera stacked = Stacked(allocations, out Camera camera, out AudioListener listener);
                Assert.That(ReferenceEquals(Own(stacked, "m_camera"), camera), Is.True, "Reset assigns the Camera");
                Assert.That(ReferenceEquals(Own(stacked, "m_audioListener"), listener), Is.True, "Reset assigns the AudioListener");
                Assert.That(manager.TryGetAudioListener(camera, out _), Is.False);

                stacked.gameObject.SetActive(true);
                Assert.That(Own(stacked, "m_cameraStackManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CameraStackManager>()));
                Assert.That(manager.TryGetAudioListener(camera, out AudioListener loaded), Is.True, "OnCameraLoaded ran in OnEnable");
                Assert.That(ReferenceEquals(loaded, listener), Is.True);
                Assert.That(listener.enabled, Is.True, "base camera listener re-enabled");

                if (GraphicsSettings.currentRenderPipeline == null)
                    LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
                stacked.gameObject.SetActive(false);
                Assert.That(listener.enabled, Is.False, "OnCameraUnloaded ran in OnDisable");
            }
            finally
            {
                try { DestroyAll(allocations, allocations.Count - 1); }
                finally { registryField.SetValue(null, previousRegistry); }
            }
        }

        // Control: with no manager, OnEnable leaves the callback pending on an
        // auto-created null row and OnDisable skips the unload without throwing.
        // The pending callback is never cancelled, so registering the manager
        // after disable still loads the disabled camera. A guard that unloads
        // unconditionally would fault on the null manager.
        [Test]
        public void MissingManagerSkipsUnloadAndLateRegistrationStillLoadsDisabledCamera()
        {
            FieldInfo registryField = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRegistry = registryField.GetValue(null);
            var allocations = new List<UObject>();
            try
            {
                registryField.SetValue(null, Activator.CreateInstance(registryField.FieldType));
                StackedCamera stacked = Stacked(allocations, out Camera camera, out AudioListener listener);

                stacked.gameObject.SetActive(true);
                Assert.That(ProcessManager.IsSystemNull<CameraStackManager>(), Is.True);
                Assert.That(listener.enabled, Is.True, "no load without a manager");
                Assert.DoesNotThrow(() => stacked.gameObject.SetActive(false));
                Assert.That(listener.enabled, Is.True, "no unload without a manager");

                var manager = new CameraStackManager();
                ProcessManager.RegisterSystem(manager);
                Assert.That(stacked.gameObject.activeSelf, Is.False);
                Assert.That(manager.TryGetAudioListener(camera, out AudioListener loaded), Is.True, "pending load ran after disable");
                Assert.That(ReferenceEquals(loaded, listener), Is.True);
            }
            finally
            {
                try { DestroyAll(allocations, allocations.Count - 1); }
                finally { registryField.SetValue(null, previousRegistry); }
            }
        }

        private static StackedCamera Stacked(List<UObject> allocations, out Camera camera, out AudioListener listener)
        {
            var owner = new GameObject("Lucid owned stacked camera");
            allocations.Add(owner);
            owner.SetActive(false);
            camera = owner.AddComponent<Camera>();
            listener = owner.AddComponent<AudioListener>();
            StackedCamera stacked = owner.AddComponent<StackedCamera>();
            // Reset is the editor's authoring callback; invoke the genuine body.
            typeof(StackedCamera).GetMethod("Reset", OwnFields).Invoke(stacked, null);
            return stacked;
        }

        private static object Own(object target, string name) => target.GetType().GetField(name, OwnFields).GetValue(target);

        private static void DestroyAll(List<UObject> objects, int index)
        {
            if (index < 0) return;
            try { if (objects[index] != null) UObject.DestroyImmediate(objects[index]); }
            finally { DestroyAll(objects, index - 1); }
        }
    }
}
