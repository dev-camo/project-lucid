using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using OriginalSkew = Hardlight.Skew;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.Skew lerp clock under a zero time scale.
    // Only Time.timeScale is changed and it is restored; both owners are destroyed.
    public sealed class OriginalSkewPlayTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // <LerpToSkew>d__7.MoveNext 0x1B4FABC reads Time.get_deltaTime, the scaled
        // clock. At time scale zero the engine-started lerp from OnEnable and a hand-
        // stepped one both keep timer 0 and skew (0, 0) across real frames. Fails for
        // unscaledDeltaTime, which moves X above 0 on the first step.
        [UnityTest]
        public IEnumerator ZeroTimeScaleHoldsTheResetLerpAtZeroSkew()
        {
            float priorScale = Time.timeScale;
            GameObject started = null;
            GameObject driven = null;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                started = Create("OriginalSkewPlay-started", true);
                driven = Create("OriginalSkewPlay-driven", false);
                OriginalSkew startedSkew = started.GetComponent<OriginalSkew>();
                OriginalSkew drivenSkew = driven.GetComponent<OriginalSkew>();
                Assert.That(Time.deltaTime, Is.EqualTo(0f));
                started.SetActive(true);
                driven.SetActive(true);
                IEnumerator lerp = (IEnumerator)typeof(OriginalSkew).GetMethod("LerpToSkew", Own).Invoke(drivenSkew, new object[] { true });
                for (int frame = 0; frame < 2; ++frame)
                {
                    Assert.That(Time.deltaTime, Is.EqualTo(0f), "The engine reports no scaled time at scale zero.");
                    Assert.That(lerp.MoveNext(), Is.True, "frame " + frame);
                    Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(0f), "frame " + frame);
                    yield return null;
                    Assume.That(Time.unscaledDeltaTime, Is.GreaterThan(0f));
                }
                Assert.That(Get(startedSkew, "m_skewX"), Is.EqualTo(0f));
                Assert.That(Get(startedSkew, "m_skewY"), Is.EqualTo(0f));
                Assert.That(Get(drivenSkew, "m_skewX"), Is.EqualTo(0f));
                Assert.That(Get(drivenSkew, "m_skewY"), Is.EqualTo(0f));
                Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(0f));
            }
            finally
            {
                Time.timeScale = priorScale;
                if (started != null) Object.DestroyImmediate(started);
                if (driven != null) Object.DestroyImmediate(driven);
            }
        }

        // Built inactive so the fields are authored before OnEnable runs.
        private static GameObject Create(string name, bool lerpFromZero)
        {
            var owner = new GameObject(name, typeof(RectTransform));
            try
            {
                owner.SetActive(false);
                owner.AddComponent<Image>();
                OriginalSkew skew = owner.AddComponent<OriginalSkew>();
                Set(skew, "m_lerpFromZeroOnEnable", lerpFromZero);
                Set(skew, "m_lerpTime", 0.2f);
                Set(skew, "m_skewX", 2f);
                Set(skew, "m_skewY", 5f);
                return owner;
            }
            catch
            {
                Object.DestroyImmediate(owner);
                throw;
            }
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Own).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Own).SetValue(target, value);
    }
}
