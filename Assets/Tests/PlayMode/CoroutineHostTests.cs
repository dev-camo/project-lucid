using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // A bounded runtime proof of the original utility host only. Passing this
    // test does not establish the game's bootstrap, saving or gameplay scenarios.
    public sealed class CoroutineHostTests
    {
        [UnityTest]
        public IEnumerator OriginalCoroutineHost_StartStopAndRegistryLifecycle()
        {
            var objects = new List<GameObject>();
            SystemRef untyped = ProcessManager.GetSystemRef(typeof(CoroutineUtils).ToString());
            SystemRef<CoroutineUtils> typed = ProcessManager.GetSystemRef<CoroutineUtils>();
            var shutdown = new List<string>();
            Action<ISystem> untypedShutdown = value => shutdown.Add("untyped");
            Action<CoroutineUtils> typedShutdown = value => shutdown.Add("typed");
            untyped.OnSystemShutdown += untypedShutdown;
            typed.OnSystemShutdown += typedShutdown;
            try
            {
#pragma warning disable CS0618
                Assert.IsTrue(CoroutineUtils.Instance == null, "The bounded test starts without a utility host.");
                var firstObject = new GameObject("Lucid coroutine host proof");
                objects.Add(firstObject);
                CoroutineUtils first = firstObject.AddComponent<CoroutineUtils>();
                Assert.AreSame(first, CoroutineUtils.Instance);
                Assert.AreSame(first, untyped.GetSafe());
                Assert.AreSame(first, typed.GetSafe());

                var trace = new List<string>();
                Coroutine stopped = CoroutineUtils.RunCoroutine(TraceFrames(trace));
                Assert.IsNotNull(stopped);
                CollectionAssert.AreEqual(new[] { "start" }, trace, "StartCoroutine executes the first iterator step immediately.");
                CoroutineUtils.StopUtilCoroutine(ref stopped);
                Assert.IsNull(stopped, "A successful stop clears the caller's handle.");
                yield return null;
                yield return null;
                CollectionAssert.AreEqual(new[] { "start" }, trace, "Stopped work never advances to the next frame.");

                trace.Clear();
                Coroutine running = CoroutineUtils.RunCoroutine(TraceFrames(trace));
                yield return null;
                yield return null;
                yield return null;
                CollectionAssert.AreEqual(new[] { "start", "resume", "finish" }, trace, "The original host schedules the Unity iterator across frames.");
                CoroutineUtils.StopUtilCoroutine(ref running);
                Assert.IsNull(running);

                var duplicateObject = new GameObject("Lucid duplicate host proof");
                objects.Add(duplicateObject);
                CoroutineUtils duplicate = duplicateObject.AddComponent<CoroutineUtils>();
                Assert.AreSame(first, CoroutineUtils.Instance);
                yield return null;
                Assert.IsTrue(duplicate == null, "Awake destroys the duplicate component.");
                Assert.IsTrue(duplicateObject != null, "Duplicate rejection retains its GameObject.");
                Assert.AreSame(first, typed.GetSafe(), "Duplicate OnDestroy cannot unregister another host by type/name.");
                Assert.IsEmpty(shutdown);

                Coroutine stale = CoroutineUtils.RunCoroutine(EndlessFrames());
                Coroutine captured = stale;
                UnityEngine.Object.Destroy(firstObject);
                yield return null;
                Assert.IsTrue(CoroutineUtils.Instance == null);
                Assert.IsTrue(untyped.IsNull() && typed.IsNull());
                CollectionAssert.AreEqual(new[] { "untyped", "typed" }, shutdown, "Registry revokes untyped before typed cached references.");
                CoroutineUtils.StopUtilCoroutine(ref stale);
                Assert.AreSame(captured, stale, "Missing Unity host leaves the caller's stale handle unchanged.");

                var replacementObject = new GameObject("Lucid replacement host proof");
                objects.Add(replacementObject);
                CoroutineUtils replacement = replacementObject.AddComponent<CoroutineUtils>();
                Assert.AreSame(replacement, CoroutineUtils.Instance);
                Assert.AreSame(replacement, untyped.GetSafe());
                Assert.AreSame(replacement, typed.GetSafe(), "Unregistration retains cached references for a replacement host.");
                UnityEngine.Object.Destroy(replacementObject);
                yield return null;
                CollectionAssert.AreEqual(new[] { "untyped", "typed", "untyped", "typed" }, shutdown);
#pragma warning restore CS0618
            }
            finally
            {
                untyped.OnSystemShutdown -= untypedShutdown;
                typed.OnSystemShutdown -= typedShutdown;
                foreach (GameObject item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static IEnumerator TraceFrames(List<string> trace)
        {
            trace.Add("start");
            yield return null;
            trace.Add("resume");
            yield return null;
            trace.Add("finish");
        }
        private static IEnumerator EndlessFrames() { while (true) yield return null; }
    }
}
