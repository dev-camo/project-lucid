using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using HLCloud.Plugin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Video;

namespace ProjectLucid.Tests
{
    // Low-priority gaps that need real Awake/OnDestroy messages or an active
    // scene object. Each case owns or borrows its registry row through
    // LifecycleSystemRowLease and destroys every object it creates.
    public sealed class OriginalLowGapPlayTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Set<T>(T component, string name, object value) =>
            typeof(T).GetField(name, Instance | BindingFlags.DeclaredOnly).SetValue(component, value);

        // Cloud_MacOS.OnConnectCallback 0x060000b2 (x86-64 0x1a9d8a0), UseFindGameObject
        // branch: three Marshal.PtrToStringAuto calls (0x1a9d93b..955),
        // GameObject.Find (0x1a9d962), a Unity inequality test (0x1a9d989)
        // and a tail call to GameObject.SendMessage(name, data,
        // RequireReceiver) (0x1a9d9b0). A missing object skips the send.
        // Sending the object name or command as data, or sending without the
        // null test, fails.
        [Test]
        public void MacConnectCallbackFindsTheNamedObjectAndSendsTheCommandData()
        {
            bool priorFind = Cloud_MacOS.UseFindGameObject;
            string objectName = "Lucid mac cloud receiver " + Guid.NewGuid().ToString("N");
            GameObject receiver = null;
            var pointers = new List<IntPtr>();
            IntPtr Native(string text) { IntPtr pointer = Marshal.StringToHGlobalAuto(text); pointers.Add(pointer); return pointer; }
            try
            {
                receiver = new GameObject(objectName);
                var probe = receiver.AddComponent<OriginalSendMessageProbe>();
                var invoke = (Action<IntPtr, IntPtr, IntPtr>)Delegate.CreateDelegate(typeof(Action<IntPtr, IntPtr, IntPtr>),
                    typeof(Cloud_MacOS).GetMethod("OnConnectCallback", BindingFlags.Static | BindingFlags.NonPublic));
                Cloud_MacOS.UseFindGameObject = true;
                invoke(Native(objectName), Native("Receive"), Native("payload"));
                Assert.That(probe.Received, Is.EqualTo(new[] { "payload" }));
                Assert.DoesNotThrow(() => invoke(Native(objectName + " missing"), Native("Receive"), Native("ignored")));
                Assert.That(probe.Received, Is.EqualTo(new[] { "payload" }), "a missing object receives nothing");
            }
            finally
            {
                Cloud_MacOS.UseFindGameObject = priorFind;
                foreach (IntPtr pointer in pointers) Marshal.FreeHGlobal(pointer);
                if (receiver != null) UnityEngine.Object.DestroyImmediate(receiver);
            }
        }

        // SplashScreen.OnDestroy 0x0600062e (x86-64 0x53bb00) removes each
        // player's error listener (0x53bbb0) and then calls
        // targetTexture.Release (0x53bbba/0x53bbc4) with no null test. A real
        // VideoPlayer without a target texture therefore faults during the
        // real OnDestroy message, which Unity logs. A null-guarded release
        // logs nothing and fails the expectation. OnDestroy does not
        // unregister, so the fixture unregisters its own row.
        [Test]
        public void DestroyFaultsReleasingAMissingVideoTargetTexture()
        {
            string name = ProcessManager.GetDefaultName<SplashScreen>();
            var rowLease = new LifecycleSystemRowLease(LifecycleSystemRowLease.SharedRegistry, name);
            using (rowLease)
            {
                GameObject owner = null;
                try
                {
                    rowLease.CaptureCreatedRow(ProcessManager.GetSystemRef(name));
                    // GetSystemSafe caches a typed reference in the row, so the
                    // lease owns it before the registration check reads it.
                    SystemRef<SplashScreen> typed = ProcessManager.GetSystemRef<SplashScreen>();
                    rowLease.CaptureTypedReference(typed);
                    owner = new GameObject("Lucid splash destroy proof");
                    owner.SetActive(false);
                    var director = owner.AddComponent<PlayableDirector>();
                    director.playOnAwake = false;
                    var player = owner.AddComponent<VideoPlayer>();
                    player.playOnAwake = false;
                    Assert.That(player.targetTexture, Is.Null);
                    SplashScreen splash = owner.AddComponent<SplashScreen>();
                    Set(splash, "m_playableDirector", director);
                    Set(splash, "m_targetVideoPlayers", new List<VideoPlayer> { player });
                    Set(splash, "m_targetSplashScreens", new List<UnityEngine.UI.Image>());
                    Set(splash, "m_aspectRatioSplashScreenData", Array.Empty<SplashScreen.AspectRatioSplashScreenData>());
                    owner.SetActive(true);
                    Assert.That(ProcessManager.GetSystemSafe<SplashScreen>(), Is.SameAs(splash), "the real Awake registered");
                    Assert.That(ProcessManager.GetSystemRef<SplashScreen>(), Is.SameAs(typed), "the check reused the owned typed reference");
                    LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException"));
                    UnityEngine.Object.DestroyImmediate(owner);
                    owner = null;
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    rowLease.RunCleanup("destroy-splash-owner", () => { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); });
                    rowLease.RunCleanup("unregister-splash-row", () => ProcessManager.UnregisterSystem(name));
                }
            }
            Assert.IsEmpty(rowLease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
            Assert.IsEmpty(rowLease.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
        }

        private static Delegate[] ProfileHandlers(PerformanceProfile profile) =>
            ((Delegate)typeof(PerformanceProfile).GetField("OnProfileUpdated", Instance).GetValue(profile))?.GetInvocationList()
            ?? Array.Empty<Delegate>();

        // ScalablePerformance.Awake 0x06000d9b (x86-64 0x1b11020) registers
        // (0x1b11066) and then tail-calls SetPerformanceProfile with
        // m_defaultSetting (0x1b11078). OnDestroy 0x06000d9c (0x1b11690) removes the
        // profile edit handler (0x1b11715) before tail-calling
        // ProcessManager.UnregisterSystem (0x1b1173f). Real startup and
        // shutdown notifications observe both boundaries. Applying the default
        // before registering, or unregistering before detaching, fails.
        // PerformanceDependency..cctor (x86-64 0x1b10280) calls
        // GetSystemRef<ScalablePerformance>(null, true) (0x1b102e1), so an
        // earlier case can leave this row and its typed reference for the rest
        // of the run. An idle row is borrowed; one with observers or pending
        // work is still refused.
        [Test]
        public void AwakeRegistersBeforeApplyingAndDestroyDetachesBeforeUnregistering()
        {
            string name = ProcessManager.GetDefaultName<ScalablePerformance>();
            var rowLease = LifecycleSystemRowLease.AdmitAbsentOrIdle(LifecycleSystemRowLease.SharedRegistry, name);
            using (rowLease)
            {
                GameObject owner = null;
                PerformanceProfile profile = null;
                SystemRef<ScalablePerformance> typed = null;
                var trace = new List<string>();
                ScalablePerformance manager = null;
                Action<ScalablePerformance> startup = system =>
                    trace.Add("startup active=" + (system.ActiveProfile == null ? "none" : system.ActiveProfile.name));
                Action<ScalablePerformance> shutdown = system =>
                    trace.Add("shutdown profile-handlers=" + ProfileHandlers(profile).Count(handler => ReferenceEquals(handler.Target, manager)));
                try
                {
                    rowLease.CaptureCreatedRow(ProcessManager.GetSystemRef(name));
                    typed = ProcessManager.GetSystemRef<ScalablePerformance>();
                    rowLease.CaptureTypedReference(typed);
                    typed.OnSystemStartup += startup;
                    typed.OnSystemShutdown += shutdown;
                    profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                    profile.name = "Lucid default profile";
                    owner = new GameObject("Lucid scalable performance proof");
                    owner.SetActive(false);
                    manager = owner.AddComponent<ScalablePerformance>();
                    Set(manager, "m_performanceProfiles", new List<PerformanceProfile> { profile });
                    Set(manager, "m_defaultSetting", profile);
                    owner.SetActive(true);
                    Assert.That(trace, Is.EqualTo(new[] { "startup active=none" }), "registration precedes the default profile");
                    Assert.That(manager.ActiveProfile, Is.SameAs(profile));
                    Assert.That(ProfileHandlers(profile).Count(handler => ReferenceEquals(handler.Target, manager)), Is.EqualTo(1));
                    UnityEngine.Object.DestroyImmediate(owner);
                    owner = null;
                    Assert.That(trace, Is.EqualTo(new[] { "startup active=none", "shutdown profile-handlers=0" }),
                        "the edit handler is removed before unregistration");
                    Assert.That(typed.GetSafe(), Is.Null);
                }
                finally
                {
                    rowLease.RunCleanup("destroy-performance-owner", () => { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); });
                    rowLease.RunCleanup("detach-performance-startup", () => { if (typed != null) typed.OnSystemStartup -= startup; });
                    rowLease.RunCleanup("detach-performance-shutdown", () => { if (typed != null) typed.OnSystemShutdown -= shutdown; });
                    rowLease.RunCleanup("destroy-performance-profile", () => { if (profile != null) UnityEngine.Object.DestroyImmediate(profile); });
                }
            }
            Assert.IsEmpty(rowLease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
            Assert.IsEmpty(rowLease.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
        }
    }
}
