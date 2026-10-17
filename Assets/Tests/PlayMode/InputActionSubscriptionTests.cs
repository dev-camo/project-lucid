using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Release timing of the original action subscription on a real coroutine
    // host. Callbacks are invoked directly; ControlMapping input is not driven.
    public sealed class InputActionSubscriptionTests
    {
        private sealed class Recording : InputActionSubscription
        {
            public readonly List<string> Trace = new List<string>();
            public int UpFrame = -1;
            // No modifiers and no validator: nonzero values pass ApplyModifiers.
            public Recording() : base(new GameControlsBinding(GameInput.Up), null) { }
            protected override void OnDown(float value) => Trace.Add(FormattableString.Invariant($"down:{value}"));
            protected override void OnHeld(float value) => Trace.Add(FormattableString.Invariant($"held:{value}"));
            protected override void OnUp(float value) { Trace.Add(FormattableString.Invariant($"up:{value}")); UpFrame = Time.frameCount; }
            protected override void OnDisabled() => Trace.Add("disabled");
        }

        // The original .cctor (IL 1417285300002B80C30300042A, recorded in
        // ControllerActionCurrentFacts) stores
        // ProcessManager.GetSystemRef<InputSystem>(null, true) in
        // m_inputSystemRef, creating the 'HardlightProject.InputSystem' row on
        // the type's first use and holding its typed reference for the whole
        // domain. A test cannot remove that row: m_inputSystemRef would keep
        // the orphaned reference and never see a later InputSystem. Run the
        // initializer once for the fixture, before any test case starts, so
        // each case finds the row already in place and leaves it as found.
        [OneTimeSetUp]
        public void InitializeTheCachedInputSystemReference()
        {
            RuntimeHelpers.RunClassConstructor(typeof(InputActionSubscription).TypeHandle);
            Assert.IsNotNull(typeof(InputActionSubscription).GetField("m_inputSystemRef", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null),
                "The type initializer caches its InputSystem reference.");
        }

        // Original 060023c1 (x86-64 0x58ab40) captures the value in display
        // class 020006aa, clears m_inputDown (+0x20), then either clears
        // m_inputHeld (+0x21) and tail-calls OnUp, or passes the closure to
        // CoroutineUtils.OnNextFrame. Fails if the unheld release calls OnUp
        // synchronously, or if m_inputDown is cleared only inside the deferred
        // callback (a press in the same frame would then report held, not down).
        [UnityTest]
        public IEnumerator UnheldReleaseDefersOnUpToTheNextFrameAfterClearingDown()
        {
            var rowLease = new LifecycleSystemRowLease(LifecycleSystemRowLease.SharedRegistry, ProcessManager.GetDefaultName<CoroutineUtils>());
            using (rowLease)
            {
#pragma warning disable CS0618
                Assert.IsTrue(CoroutineUtils.Instance == null, "The bounded test starts without a utility host.");
#pragma warning restore CS0618
                GameObject hostObject = null;
                try
                {
                    rowLease.CaptureCreatedRow(ProcessManager.GetSystemRef(ProcessManager.GetDefaultName<CoroutineUtils>()));
                    rowLease.CaptureTypedReference(ProcessManager.GetSystemRef<CoroutineUtils>());
                    hostObject = new GameObject("Lucid input subscription coroutine host");
                    hostObject.AddComponent<CoroutineUtils>();

                    var subscription = new Recording();
                    Held(subscription, 1f);
                    int releaseFrame = Time.frameCount;
                    Up(subscription, 0.5f);
                    CollectionAssert.AreEqual(new[] { "down:1" }, subscription.Trace, "An unheld release does not call OnUp synchronously.");
                    Held(subscription, 2f);
                    CollectionAssert.AreEqual(new[] { "down:1", "down:2" }, subscription.Trace, "The down flag is cleared before deferral.");
                    for (int frame = 0; frame < 3 && subscription.UpFrame < 0; ++frame) yield return null;
                    CollectionAssert.AreEqual(new[] { "down:1", "down:2", "up:0.5" }, subscription.Trace, "The deferred OnUp keeps its captured value.");
                    Assert.AreEqual(releaseFrame + 1, subscription.UpFrame, "The deferred OnUp runs on the following Unity frame.");

                    // Held path: the release is synchronous and resets both flags.
                    subscription.Trace.Clear();
                    Held(subscription, 3f);
                    Up(subscription, 4f);
                    Held(subscription, 5f);
                    CollectionAssert.AreEqual(new[] { "held:3", "up:4", "down:5" }, subscription.Trace);
                    int heldReleaseFrame = Time.frameCount;
                    Assert.AreEqual(heldReleaseFrame, subscription.UpFrame);
                    yield return null;
                    yield return null;
                    CollectionAssert.AreEqual(new[] { "held:3", "up:4", "down:5" }, subscription.Trace, "A held release schedules no later OnUp.");
                }
                finally
                {
                    rowLease.RunCleanup("destroy-coroutine-host", () => { if (hostObject != null) UnityEngine.Object.DestroyImmediate(hostObject); });
                }
            }
            Assert.IsEmpty(rowLease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
            Assert.IsEmpty(rowLease.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
        }

        private static void Held(InputActionSubscription subscription, float value) => Callback("OnCallbackHeld", subscription, value);
        private static void Up(InputActionSubscription subscription, float value) => Callback("OnCallbackUp", subscription, value);
        private static void Callback(string name, InputActionSubscription subscription, float value) =>
            typeof(InputActionSubscription).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(subscription, new object[] { value });
    }
}
