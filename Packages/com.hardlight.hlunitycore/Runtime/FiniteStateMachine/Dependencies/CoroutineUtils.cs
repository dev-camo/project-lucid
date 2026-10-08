using System;
using System.Collections;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime CoroutineUtils, 0x020002b1. The authored
    // iterator bodies retain all sixteen original generated owners. Scheduling,
    // Unity lifetime and time getters require genuine engine validation.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CoroutineUtils : MonoBehaviour, ISystem
    {
        private static CoroutineUtils s_instance;

        // 0x06001170: publish before registration; destroy only this component.
        private void Awake()
        {
            if (s_instance != null) { Destroy(this); return; }
            s_instance = this;
            ProcessManager.RegisterSystem(this);
        }

        // 0x06001171: every instance unregisters; the static field is retained.
        private void OnDestroy() => ProcessManager.UnregisterSystem(this);

        // 0x06001172: no lazy host creation or missing-host guard.
        public static Coroutine RunCoroutine(IEnumerator coroutine) => s_instance.StartCoroutine(coroutine);

        // 0x06001173: capture the host before allocating the iterator.
        public static void OnFrameEnd(Action action)
        {
            CoroutineUtils host = s_instance;
            host.StartCoroutine(host.EndOfFrameCoroutine(action));
        }

        // 0x06001174; MoveNext 0x060011a3: one yield, then unguarded action.
        private IEnumerator EndOfFrameCoroutine(Action action)
        {
            yield return new WaitForEndOfFrame();
            action();
        }

        // 0x06001175.
        public static void OnNextFrame(Action action)
        {
            CoroutineUtils host = s_instance;
            host.StartCoroutine(host.NextFrameCoroutine(action));
        }

        // 0x06001176; MoveNext 0x060011b5: terminal before user callback.
        private IEnumerator NextFrameCoroutine(Action action)
        {
            yield return null;
            action();
        }

        // 0x06001177: both shipped architectures pass exactly two frames.
        public static Coroutine WaitForUI(Action action)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(WaitNumberOfFramesCoroutine(action, 2));
        }

        // 0x06001178.
        public static void WaitNumberOfFrames(Action action, int numberOfFramesToWait)
        {
            CoroutineUtils host = s_instance;
            host.StartCoroutine(WaitNumberOfFramesCoroutine(action, numberOfFramesToWait));
        }

        // 0x06001179; MoveNext 0x060011eb: signed count, no validation.
        public static IEnumerator WaitNumberOfFramesCoroutine(Action action, int numberOfFramesToWait)
        {
            int frameCount = 0;
            while (frameCount < numberOfFramesToWait)
            {
                yield return null;
                frameCount++;
            }
            action();
        }

        // 0x0600117a.
        public static Coroutine Delay(Action action, float seconds)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.DelayCoroutine(action, seconds));
        }

        // 0x0600117b; MoveNext 0x06001197: always yields a new wait first.
        private IEnumerator DelayCoroutine(Action action, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        // 0x0600117c.
        public static Coroutine Delay(Action action, WaitForSeconds waitForSeconds)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.DelayCoroutine(action, waitForSeconds));
        }

        // 0x0600117d; MoveNext 0x0600119d: retains even a null wait reference.
        private IEnumerator DelayCoroutine(Action action, WaitForSeconds waitForSeconds)
        {
            yield return waitForSeconds;
            action();
        }

        // 0x0600117e.
        public static void OnFixedUpdate(Action action)
        {
            CoroutineUtils host = s_instance;
            host.StartCoroutine(host.FixedUpdateCoroutine(action));
        }

        // 0x0600117f; MoveNext 0x060011a9.
        private IEnumerator FixedUpdateCoroutine(Action action)
        {
            yield return new WaitForFixedUpdate();
            action();
        }

        // 0x06001180.
        public static Coroutine OnPredicateActive(Action action, Func<bool> predicate)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.PredicateActiveCoroutine(action, predicate));
        }

        // 0x06001181: Unity host guard precedes ordinary handle guard. Clear
        // only after successful StopCoroutine, using a fresh static host read.
        public static void StopUtilCoroutine(ref Coroutine coroutine)
        {
            if (s_instance == null || coroutine == null) return;
            s_instance.StopCoroutine(coroutine);
            coroutine = null;
        }

        // 0x06001182; MoveNext 0x060011bb: false yields; true invokes once.
        private IEnumerator PredicateActiveCoroutine(Action action, Func<bool> predicate)
        {
            while (!predicate()) yield return null;
            action();
        }

        // 0x06001183.
        public static Coroutine Predicate(Action action, Func<bool> predicate)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.PredicateCoroutine(action, predicate));
        }

        // 0x06001184; MoveNext 0x060011c7: callback precedes each null yield.
        private IEnumerator PredicateCoroutine(Action action, Func<bool> predicate)
        {
            while (predicate())
            {
                action();
                yield return null;
            }
        }

        // 0x06001185.
        public static Coroutine Update(Action action)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.UpdateCoroutine(action));
        }

        // 0x06001186; MoveNext 0x060011d3.
        private IEnumerator UpdateCoroutine(Action action)
        {
            while (true)
            {
                action();
                yield return null;
            }
        }

        // 0x06001187.
        public static Coroutine Interval(Action action, float interval)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.IntervalCoroutine(action, interval));
        }

        // 0x06001188; MoveNext 0x060011af: allocate once before first action.
        private IEnumerator IntervalCoroutine(Action action, float interval)
        {
            WaitForSeconds wait = new WaitForSeconds(interval);
            while (true)
            {
                action();
                yield return wait;
            }
        }

        // 0x06001189.
        public static Coroutine PredicateInterval(Action action, Func<bool> predicate, float interval)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.PredicateIntervalCoroutine(action, predicate, interval));
        }

        // 0x0600118a; MoveNext 0x060011cd: allocate before predicate check.
        private IEnumerator PredicateIntervalCoroutine(Action action, Func<bool> predicate, float interval)
        {
            WaitForSeconds wait = new WaitForSeconds(interval);
            while (predicate())
            {
                action();
                yield return wait;
            }
        }

        // 0x0600118b.
        public static Coroutine PredicateActiveInterval(Func<bool> predicateToStart, Action action, Func<bool> predicate, float interval)
        {
            CoroutineUtils host = s_instance;
            return host.StartCoroutine(host.PredicateActiveIntervalCoroutine(predicateToStart, action, predicate, interval));
        }

        // 0x0600118c; MoveNext 0x060011c1: the original waits WHILE the start
        // predicate is true, then yields the timed wait before the main loop.
        private IEnumerator PredicateActiveIntervalCoroutine(Func<bool> predicateToStart, Action action, Func<bool> predicate, float interval)
        {
            while (predicateToStart()) yield return null;
            WaitForSeconds wait = new WaitForSeconds(interval);
            yield return wait;
            while (predicate())
            {
                action();
                yield return wait;
            }
        }

        // 0x0600118d; MoveNext 0x060011d9: two time reads on first advance.
        public static IEnumerator WaitForRealSeconds(float time)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < start + time) yield return null;
        }

        // 0x0600118e; MoveNext 0x060011df: subtract before null yield, preserving
        // mutable time and ordered-positive comparison (NaN ends the iterator).
        public static IEnumerator WaitForSeconds(float time)
        {
            while (time > 0)
            {
                time -= Time.deltaTime;
                yield return null;
            }
        }

        // 0x0600118f; MoveNext 0x060011e5.
        public static IEnumerator WaitForSecondsUnscaled(float time)
        {
            while (time > 0)
            {
                time -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Original property0x170001e1/getter0x06001190, no lazy host creation.
        [Obsolete("Instance is obsolete and only exists for backwards compatability. Please use the public static methods instead.")]
        public static CoroutineUtils Instance => s_instance;

        // 0x06001191; MoveNext0x060011f1: genuine Unity null equality.
        [Obsolete("WaitOnInstance is obsolete and only exists for backwards compatability. Please use a SystemRef instead.")]
        public static IEnumerator WaitOnInstance()
        {
            while (Instance == null) yield return null;
        }

        // 0x06001192.
        [Obsolete("NotNull is obsolete and only exists for backwards compatability. Please use a SystemRef instead.")]
        public static bool NotNull() => Instance != null;

        // 0x06001193.
        [Obsolete("IsNull is obsolete and only exists for backwards compatability. Please use a SystemRef instead.")]
        public static bool IsNull() => Instance == null;

        // 0x06001194: MonoBehaviour base constructor only.
        public CoroutineUtils() { }
    }
}
