using System.Collections;
using System.Reflection;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Original CoroutineUtils timed waits under a zero time scale. The static
    // iterators are advanced once per real frame by hand; no coroutine host
    // is created. Only Time.timeScale is changed and it is restored.
    public sealed class OriginalCoroutineTimeSourceGapPlayTests
    {
        // WaitForSeconds MoveNext 0x060011df (x86-64 0x1b337a0) subtracts
        // Time.deltaTime (0x1b337ca); WaitForSecondsUnscaled 0x060011e5
        // (x86-64 0x1b338a0) subtracts Time.unscaledDeltaTime (0x1b338ca).
        // At time scale zero the scaled wait never shrinks while the unscaled
        // one ends. Fails if either iterator reads the other delta source.
        [UnityTest]
        public IEnumerator ZeroTimeScaleHoldsTheScaledWaitButEndsTheUnscaledWait()
        {
            float priorScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                const float duration = 0.02f;
                IEnumerator scaled = CoroutineUtils.WaitForSeconds(duration);
                IEnumerator unscaled = CoroutineUtils.WaitForSecondsUnscaled(duration);
                FieldInfo time = scaled.GetType().GetField("time", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                bool unscaledEnded = false;
                int frames = 0;
                float deadline = Time.realtimeSinceStartup + 10f;
                while (!unscaledEnded && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(Time.deltaTime, Is.EqualTo(0f), "The engine reports no scaled time at scale zero.");
                    Assert.That(scaled.MoveNext(), Is.True, "The scaled wait cannot end at time scale zero.");
                    Assert.That(scaled.Current, Is.Null);
                    Assert.That((float)time.GetValue(scaled), Is.EqualTo(duration), "Subtracting zero scaled time keeps the duration.");
                    unscaledEnded = !unscaled.MoveNext();
                    frames++;
                    yield return null;
                }
                Assert.That(unscaledEnded, Is.True, "The unscaled wait ends from real frame time.");
                Assert.That(frames, Is.GreaterThan(1), "The unscaled wait yields before it ends.");
                Assert.That(scaled.MoveNext(), Is.True);
            }
            finally
            {
                Time.timeScale = priorScale;
            }
        }
    }
}
