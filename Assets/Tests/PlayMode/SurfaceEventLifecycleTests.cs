using System;
using System.Collections;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif

namespace ProjectLucid.Tests
{
    public sealed class SurfaceEventLifecycleTests
    {
        private static FieldInfo Field(string name) => typeof(SurfaceEvent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        [UnityTest]
        public IEnumerator AuthoredStayListenersRunOnFixedFramesAndReentryKeepsEarlierRoutine()
        {
#if UNITY_EDITOR
            GameObject owned = null, target = null;
            SurfaceEvent surface = null;
            float previousScale = Time.timeScale, previousFixed = Time.fixedDeltaTime;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                Time.timeScale = 1f; Time.fixedDeltaTime = 1f / 60f; Time.captureDeltaTime = 1f / 60f;
                owned = new GameObject("Lucid genuine surface coroutine verification");
                target = new GameObject("Lucid genuine persistent callback target");
                surface = owned.AddComponent<SurfaceEvent>();
                var enter = new UnityEvent(); var stay = new UnityEvent(); var exit = new UnityEvent();
                Field("m_onEnter").SetValue(surface, enter); Field("m_onStay").SetValue(surface, stay); Field("m_onExit").SetValue(surface, exit);
                int entered = 0, stayed = 0, exited = 0; bool firstSawNoHandle = false, exitSawNoHandle = false;
                Coroutine priorCallbackHandle = null;
                enter.AddListener(() => ++entered);
                stay.AddListener(() => { ++stayed; if (stayed == 1) firstSawNoHandle = Field("m_stayCoroutine").GetValue(surface) == null;
                    else priorCallbackHandle = (Coroutine)Field("m_stayCoroutine").GetValue(surface); });
                exit.AddListener(() => { ++exited; exitSawNoHandle = Field("m_stayCoroutine").GetValue(surface) == null; });
                // Use an actual built-in UnityEngine.Object receiver and Unity's
                // persistent-call authoring API; no invented event host/provider.
                UnityEventTools.AddBoolPersistentListener(stay, target.SetActive, false);
                Assert.That(stay.GetPersistentEventCount(), Is.EqualTo(1));
                surface.OnSurfaceEnter();
                Coroutine older = (Coroutine)Field("m_stayCoroutine").GetValue(surface);
                Assert.That(entered, Is.EqualTo(1));
                Assert.That(stayed, Is.EqualTo(1));
                Assert.That(firstSawNoHandle, Is.True);
                Assert.That(target.activeSelf, Is.False);
                Assert.That(older, Is.Not.Null);
                target.SetActive(true); int before = stayed;
                for (int frame = 0; frame < 120 && stayed == before; ++frame) yield return null;
                Assert.That(stayed, Is.GreaterThan(before));
                Assert.That(target.activeSelf, Is.False);
                before = stayed; surface.OnSurfaceEnter();
                Coroutine latest = (Coroutine)Field("m_stayCoroutine").GetValue(surface);
                Assert.That(stayed, Is.EqualTo(before + 1));
                Assert.That(priorCallbackHandle, Is.SameAs(older));
                Assert.That(latest, Is.Not.Null.And.Not.SameAs(older));
                surface.OnSurfaceExit();
                Assert.That(exited, Is.EqualTo(1));
                Assert.That(exitSawNoHandle, Is.True);
                Assert.That(Field("m_stayCoroutine").GetValue(surface), Is.Null);
                before = stayed;
                for (int frame = 0; frame < 120 && stayed == before; ++frame) yield return null;
                Assert.That(stayed, Is.GreaterThan(before), "the original earlier coroutine survives re-entry/exit");
                surface.StopCoroutine(older); before = stayed;
                for (int frame = 0; frame < 4; ++frame) yield return null;
                Assert.That(stayed, Is.EqualTo(before), "both original handles are now stopped");
                surface.OnSurfaceEnter(); var badExit = new UnityEvent();
                badExit.AddListener(() => { throw new InvalidOperationException("fixture exit fault"); });
                Field("m_onExit").SetValue(surface, badExit);
                Assert.Throws<InvalidOperationException>(() => surface.OnSurfaceExit());
                Assert.That(Field("m_stayCoroutine").GetValue(surface), Is.Null, "stop and clear precede callback fault");
                before = stayed;
                for (int frame = 0; frame < 4; ++frame) yield return null;
                Assert.That(stayed, Is.EqualTo(before));
            }
            finally
            {
                try { if (surface != null) surface.StopAllCoroutines(); }
                finally
                {
                    try { if (owned != null) UnityEngine.Object.DestroyImmediate(owned); }
                    finally
                    {
                        try { if (target != null) UnityEngine.Object.DestroyImmediate(target); }
                        finally { Time.captureDeltaTime = previousCapture; Time.fixedDeltaTime = previousFixed; Time.timeScale = previousScale; }
                    }
                }
            }
#else
            Assert.Ignore("This authored persistent-call fixture requires the real Unity Editor event authoring API.");
            yield break;
#endif
        }
    }
}
