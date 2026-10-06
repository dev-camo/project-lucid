using System.Collections;
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
            var owner = new GameObject("Lucid original coroutine prerequisites");
            try
            {
                var host = owner.AddComponent<CoroutineUtils>();
                Assert.That(host, Is.Not.Null);
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
            }
        }
    }
}
