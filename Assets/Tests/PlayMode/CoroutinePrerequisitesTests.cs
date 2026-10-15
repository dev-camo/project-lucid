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
            // Awake publishes the static host and registers it; OnDestroy unregisters but keeps both the
            // static and the registry row. Restore the prior static and remove only a row created here.
            FieldInfo instanceField = typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string rowName = ProcessManager.GetDefaultName<CoroutineUtils>();
            // A live host would make this host's registration throw; an empty retained row is reused.
            Assert.That(ProcessManager.IsSystemNull(rowName), Is.True, "requires no live coroutine host");
            object priorInstance = instanceField.GetValue(null);
            bool hadRow = registry.Contains(rowName);
            instanceField.SetValue(null, null);
            var owner = new GameObject("Lucid original coroutine prerequisites");
            try
            {
                var host = owner.AddComponent<CoroutineUtils>();
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
                Object.DestroyImmediate(owner);
                instanceField.SetValue(null, priorInstance);
                if (!hadRow) registry.Remove(rowName);
            }
        }
    }
}
