using System.Collections;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.HLUIAnimationInOut clock selection under a zero time
    // scale. Only Time.timeScale is changed and it is restored; the owner is destroyed.
    public sealed class OriginalHLUIAnimationInOutPlayTests
    {
        private const BindingFlags Own = BindingFlags.NonPublic | BindingFlags.Instance;

        // IndependentTimeDelta 0x0600050b @0x1B619B0 returns Time.get_unscaledDeltaTime when +0x2C
        // m_useUnscaledTime is set and Time.get_deltaTime otherwise. At time scale zero the scaled
        // delta is 0 while the unscaled one is not, so a body that ignores the flag, or reads it
        // inverted, fails one of the two calls.
        [UnityTest]
        public IEnumerator ZeroTimeScaleSeparatesTheScaledAndUnscaledDelta()
        {
            float priorScale = Time.timeScale;
            GameObject owner = null;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                yield return null;
                owner = new GameObject("OriginalInOutPlay-delta", typeof(RectTransform));
                owner.SetActive(false);
                HLAlphaInOut alpha = owner.AddComponent<HLAlphaInOut>();
                Assert.That(Time.deltaTime, Is.EqualTo(0f), "The engine reports no scaled time at scale zero.");
                Assume.That(Time.unscaledDeltaTime, Is.GreaterThan(0f));

                Assert.That(Delta(alpha), Is.EqualTo(0f));
                typeof(HLUIAnimationInOut).GetField("m_useUnscaledTime", Own).SetValue(alpha, true);
                Assert.That(Delta(alpha), Is.EqualTo(Time.unscaledDeltaTime));
                Assert.That(Delta(alpha), Is.GreaterThan(0f));
            }
            finally
            {
                Time.timeScale = priorScale;
                if (owner != null) Object.DestroyImmediate(owner);
            }
        }

        private static float Delta(HLUIAnimationInOut target) =>
            (float)typeof(HLUIAnimationInOut).GetMethod("IndependentTimeDelta", Own).Invoke(target, null);
    }
}
