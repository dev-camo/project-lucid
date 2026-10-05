using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight.Utils;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded native-derived iterator checks. The live host test separately
    // verifies Unity scheduling; neither establishes the game's startup flow.
    public static class CoroutineCallbackVerification
    {
        public static void Run()
        {
            int checks = 0;
            var objects = new List<GameObject>();
            Action<bool, string> check = (good, name) =>
            {
                ++checks;
                if (!good) throw new InvalidOperationException("Coroutine callback proof: " + name);
            };
            try
            {
                var item = new GameObject("Lucid deferred callback proof");
                objects.Add(item);
                item.SetActive(false);
                CoroutineUtils host = item.AddComponent<CoroutineUtils>();
                MethodInfo factory = typeof(CoroutineUtils).GetMethod("NextFrameCoroutine", BindingFlags.NonPublic | BindingFlags.Instance);
                check(factory != null && factory.ReturnType == typeof(IEnumerator), "original private instance factory");
                Func<Action, IEnumerator> create = action => (IEnumerator)factory.Invoke(host, new object[] { action });

                int calls = 0;
                IEnumerator iterator = create(() => ++calls);
                check(calls == 0 && iterator.Current == null, "factory neither invokes nor advances");
                check(iterator.MoveNext() && iterator.Current == null && calls == 0, "first step yields null before invocation");
                check(ReferenceEquals(iterator.Current, ((IEnumerator<object>)iterator).Current), "both Current interfaces retain the same value");
                ((IDisposable)iterator).Dispose();
                check(!iterator.MoveNext() && calls == 1, "native no-op Dispose retains pending continuation");
                check(!iterator.MoveNext() && calls == 1 && iterator.Current == null, "completion never invokes twice");
                bool resetFailed = false;
                try { iterator.Reset(); } catch (NotSupportedException) { resetFailed = true; }
                check(resetFailed && !iterator.MoveNext(), "original Reset throws without restarting");

                IEnumerator disposedBeforeStart = create(() => ++calls);
                ((IDisposable)disposedBeforeStart).Dispose();
                check(disposedBeforeStart.MoveNext() && calls == 1, "Dispose before start is also inert");
                check(!disposedBeforeStart.MoveNext() && calls == 2, "disposed unstarted iterator still resumes once");

                IEnumerator reentrant = null;
                bool nested = true;
                reentrant = create(() => { ++calls; nested = reentrant.MoveNext(); });
                check(reentrant.MoveNext() && calls == 2, "reentrant fixture defers callback");
                check(!reentrant.MoveNext() && !nested && calls == 3, "iterator completes state before calling user code");

                var failure = new InvalidOperationException("fixture callback");
                IEnumerator throwing = create(() => { ++calls; throw failure; });
                check(throwing.MoveNext() && calls == 3, "throwing fixture defers callback");
                Exception caught = null;
                try { throwing.MoveNext(); } catch (Exception error) { caught = error; }
                check(ReferenceEquals(caught, failure) && calls == 4, "callback failure propagates unchanged");
                check(!throwing.MoveNext() && calls == 4, "failed callback is never retried");

                IEnumerator missing = create(null);
                check(missing.MoveNext() && missing.Current == null, "null action survives the first yield");
                bool nullFailed = false;
                try { missing.MoveNext(); } catch (NullReferenceException) { nullFailed = true; }
                check(nullFailed && !missing.MoveNext(), "null action fails on resume and retains completed state");
                Debug.Log("[Project Lucid] Original deferred-callback bounded checks passed: " + checks);
            }
            finally
            {
                foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }
    }
}
