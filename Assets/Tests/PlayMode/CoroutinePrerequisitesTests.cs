using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class CoroutinePrerequisitesTests
    {
        [UnityTest]
        public IEnumerator OriginalCoroutineHelpers_NestedTwoFramesAndBothStopOverloads()
        {
            // The lease admits only an absent CoroutineUtils row, so no borrowed typed references,
            // observers or deferred InvokeOnValid work can be consumed, and removes only its own cleared row.
            var rowLease = new LifecycleSystemRowLease(LifecycleSystemRowLease.SharedRegistry, ProcessManager.GetDefaultName<CoroutineUtils>());
            using (rowLease)
            {
                // Awake's duplicate guard is a Unity-null test, so a destroyed prior host is admitted
                // without clearing the static; a live one would be bypassed, so reject it unchanged.
                FieldInfo instanceField = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
                object priorInstance = instanceField.GetValue(null);
                Assert.That((priorInstance as CoroutineUtils) == null, Is.True, "requires no live coroutine host singleton");
                GameObject owner = null;
                CoroutineUtils host = null;
                try
                {
                    rowLease.CaptureCreatedRow(ProcessManager.GetSystemRef(ProcessManager.GetDefaultName<CoroutineUtils>()));
                    owner = new GameObject("Lucid original coroutine prerequisites");
                    host = owner.AddComponent<CoroutineUtils>();
                    Assert.That(host, Is.Not.Null);
                    Assert.That(instanceField.GetValue(null), Is.SameAs(host), "Awake publishes the owned host");
                    int calls = 0;
                    int actionFrame = -1;
                    int firstFrame = Time.frameCount;
                    IEnumerator outer = host.WaitForUI(() => { calls++; actionFrame = Time.frameCount; });
                    host.StartCoroutine(outer);
                    Assert.That(calls, Is.Zero, "nested two-frame child starts without callback");
                    yield return null;
                    Assert.That(calls, Is.Zero, "first frame remains suspended");
                    for (int i = 0; i < 4 && calls == 0; i++) yield return null;
                    Assert.That(calls, Is.EqualTo(1), "genuine Unity nested IEnumerator scheduling calls action");
                    Assert.That(actionFrame - firstFrame, Is.EqualTo(2), "original two-frame dependency");
                    yield return null;
                    Assert.That(calls, Is.EqualTo(1), "completed nested callback remains one-shot");

                    int stoppedEnumeratorCalls = 0;
                    IEnumerator enumerator = CoroutineUtils.WaitNumberOfFramesCoroutine(() => stoppedEnumeratorCalls++, 4);
                    host.StartCoroutine(enumerator);
                    host.SafeStopCoroutine(ref enumerator);
                    Assert.That(enumerator, Is.Null, "original IEnumerator overload clears only after engine stop");
                    int stoppedHandleCalls = 0;
                    Coroutine handle = host.StartCoroutine(CoroutineUtils.WaitNumberOfFramesCoroutine(() => stoppedHandleCalls++, 4));
                    host.SafeStopCoroutine(ref handle);
                    Assert.That(handle, Is.Null, "original Coroutine overload clears only after engine stop");
                    for (int i = 0; i < 5; i++) yield return null;
                    Assert.That(stoppedEnumeratorCalls, Is.Zero, "IEnumerator identity stop cancels original callback");
                    Assert.That(stoppedHandleCalls, Is.Zero, "Coroutine handle stop cancels original callback");
                }
                finally
                {
                    // OnDestroy (0x06001171, x86-64 0x1b31630) only unregisters and keeps the static; restore it
                    // only while this host still owns it. The host is destroyed by now, so Unity's overloaded
                    // null would hide it; compare CLR references instead.
                    rowLease.RunCleanup("destroy-coroutine-owner", () => { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); });
                    rowLease.RunCleanup("restore-coroutine-singleton", () =>
                    {
                        object current = instanceField.GetValue(null);
                        if (!ReferenceEquals(host, null) && ReferenceEquals(current, host)) instanceField.SetValue(null, priorInstance);
                        else if (!ReferenceEquals(current, priorInstance))
                            throw new InvalidOperationException("Coroutine singleton ownership changed");
                    });
                }
            }
            Assert.IsEmpty(rowLease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
            Assert.IsEmpty(rowLease.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
        }
    }
}
