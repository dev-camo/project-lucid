using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Bounded original wrapper/host proof. Generated silent clips are owned by
    // this test. No audible output, headless isPlaying parity, Actor bootstrap,
    // supplied content, or full-game scenario is asserted here.
    public sealed class AudioSourceLifecycleTests
    {
        private const BindingFlags OwnStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags OwnInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private sealed class VolumeSteps : IEnumerator, IDisposable
        {
            private readonly HLAudioSourceData source;
            private readonly List<string> trace;
            private readonly string name;
            private readonly float first;
            private readonly float final;
            private readonly bool endless;
            public int MoveCalls;
            public int DisposeCalls;

            public VolumeSteps(HLAudioSourceData source, List<string> trace, string name,
                float first, float final, bool endless)
            {
                this.source = source; this.trace = trace; this.name = name;
                this.first = first; this.final = final; this.endless = endless;
            }
            public object Current => null;
            public bool MoveNext()
            {
                ++MoveCalls;
                if (MoveCalls == 1)
                {
                    trace.Add(name + ":enter");
                    source.SetVolume(first);
                    return true;
                }
                if (endless) return true;
                if (MoveCalls == 2)
                {
                    trace.Add(name + ":complete");
                    source.SetVolume(final);
                }
                return false;
            }
            public void Reset() { throw new NotSupportedException(); }
            public void Dispose() { ++DisposeCalls; }
        }

        private static IEnumerator NestedFade(VolumeSteps child, List<string> trace)
        {
            yield return child;
            trace.Add("outer:complete");
        }

        [UnityTest]
        public IEnumerator OriginalAudioSourceDelaysFadesAndUntrackedOneShotsUseTheRealHost()
        {
            FieldInfo registry = typeof(ProcessManager).GetField("s_systemDictionary", OwnStatic);
            FieldInfo singleton = typeof(CoroutineUtils).GetField("s_instance", OwnStatic);
            FieldInfo currentFadeField = typeof(HLAudioSourceData).GetField("m_currentFade", OwnInstance);
            FieldInfo oneShotFlag = typeof(HLAudioSourceData).GetField("m_playingOneShot", OwnInstance);
            FieldInfo unusedOneShotHandle = typeof(HLAudioSourceData).GetField("m_oneShotIsPlayingRoutine", OwnInstance);
            MethodInfo performFade = typeof(HLAudioSourceData).GetMethod("PerformFade", OwnInstance);
            Assert.That(registry, Is.Not.Null);
            Assert.That(singleton, Is.Not.Null);
            Assert.That(currentFadeField, Is.Not.Null);
            Assert.That(oneShotFlag, Is.Not.Null);
            Assert.That(unusedOneShotHandle, Is.Not.Null);
            Assert.That(performFade, Is.Not.Null);
            object oldRegistry = registry.GetValue(null), oldSingleton = singleton.GetValue(null);
            float oldTimeScale = Time.timeScale;
            float oldCaptureDeltaTime = Time.captureDeltaTime;
            GameObject hostObject = null, sourceObject = null;
            CoroutineUtils host = null;
            AudioClip shortClip = null, longClip = null;
            SystemRef untyped = null;
            SystemRef<CoroutineUtils> typed = null;
            var shutdown = new List<string>();
            Action<ISystem> untypedShutdown = value => shutdown.Add("untyped");
            Action<CoroutineUtils> typedShutdown = value => shutdown.Add("typed");
            try
            {
                // Real ProcessManager rows and genuine Awake registration remain
                // in use; old registry/host state is isolated and restored.
                registry.SetValue(null, Activator.CreateInstance(registry.FieldType));
                singleton.SetValue(null, null);
                Time.timeScale = 1f;
                // Advance the real Unity scaled clock consistently in batch mode.
                // Eight unconstrained fast frames need not exceed a clip duration.
                Time.captureDeltaTime = 1f / 60f;
                untyped = ProcessManager.GetSystemRef(typeof(CoroutineUtils).ToString());
                typed = ProcessManager.GetSystemRef<CoroutineUtils>();
                untyped.OnSystemShutdown += untypedShutdown;
                typed.OnSystemShutdown += typedShutdown;
                hostObject = new GameObject("Lucid original audio coroutine host proof");
                host = hostObject.AddComponent<CoroutineUtils>();
                Assert.That(untyped.GetSafe(), Is.SameAs(host));
                Assert.That(typed.GetSafe(), Is.SameAs(host));

                int calls = 0, callbackFrame = -1;
                int scheduledFrame = Time.frameCount;
                Coroutine delayed = CoroutineUtils.Delay(() => { ++calls; callbackFrame = Time.frameCount; }, 0f);
                Assert.That(delayed, Is.Not.Null);
                Assert.That(calls, Is.Zero, "Even zero seconds yields the original WaitForSeconds first.");
                for (int frame = 0; frame < 8 && calls == 0; ++frame) yield return null;
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(callbackFrame, Is.GreaterThan(scheduledFrame), "A genuine Unity frame must advance.");
                yield return null;
                yield return null;
                Assert.That(calls, Is.EqualTo(1), "Completed callback does not run again.");
                CoroutineUtils.StopUtilCoroutine(ref delayed);
                Assert.That(delayed, Is.Null);

                int cancelledCalls = 0;
                Coroutine cancelled = CoroutineUtils.Delay(() => ++cancelledCalls, 0f);
                CoroutineUtils.StopUtilCoroutine(ref cancelled);
                Assert.That(cancelled, Is.Null);

                sourceObject = new GameObject("Lucid owned silent audio source proof");
                AudioSource engineSource = sourceObject.AddComponent<AudioSource>();
                engineSource.playOnAwake = false;
                engineSource.mute = true;
                var source = new HLAudioSourceData(HLAudioSourceIdentifier.Ui, engineSource,
                    false, HLAudioSourceTransition.Fade, default(AudioFadeTransitionSettings));
                object fade = currentFadeField.GetValue(source);
                FieldInfo fadeHandle = fade.GetType().GetField("FadeCoroutine", OwnInstance);
                FieldInfo startingVolume = fade.GetType().GetField("StartingVolume", OwnInstance);
                Assert.That(fadeHandle, Is.Not.Null);
                Assert.That(startingVolume, Is.Not.Null);

                source.SetVolume(0.75f);
                var trace = new List<string>();
                var first = new VolumeSteps(source, trace, "first", 0.25f, 0f, true);
                source.StartFade(first);
                Assert.That(fadeHandle.GetValue(fade), Is.Not.Null);
                Assert.That(startingVolume.GetValue(fade), Is.EqualTo(0.75f));
                for (int frame = 0; frame < 4 && first.MoveCalls == 0; ++frame) yield return null;
                Assert.That(first.MoveCalls, Is.GreaterThan(0), "Unity advances the nested authored fade argument.");
                Assert.That(source.GetVolume(), Is.EqualTo(0.25f));
                source.StopFade();
                Assert.That(fadeHandle.GetValue(fade), Is.Null);
                Assert.That(source.GetVolume(), Is.EqualTo(0.75f), "Stop restores the original captured start volume.");
                int stoppedCalls = first.MoveCalls;
                yield return null;
                yield return null;
                Assert.That(first.MoveCalls, Is.EqualTo(stoppedCalls), "The original host no longer advances stopped work.");

                source.SetVolume(0.625f);
                var replaced = new VolumeSteps(source, trace, "replaced", 0.125f, 0f, true);
                source.StartFade(replaced);
                for (int frame = 0; frame < 4 && replaced.MoveCalls == 0; ++frame) yield return null;
                Assert.That(replaced.MoveCalls, Is.GreaterThan(0));
                Assert.That(source.GetVolume(), Is.EqualTo(0.125f));
                var replacement = new VolumeSteps(source, trace, "replacement", 0.375f, 0f, true);
                source.StartFade(replacement);
                Assert.That(startingVolume.GetValue(fade), Is.EqualTo(0.625f),
                    "Replacement stops/restores the old fade before capturing the new start.");
                int replacedCalls = replaced.MoveCalls;
                for (int frame = 0; frame < 4 && replacement.MoveCalls == 0; ++frame) yield return null;
                Assert.That(replacement.MoveCalls, Is.GreaterThan(0));
                Assert.That(source.GetVolume(), Is.EqualTo(0.375f));
                yield return null;
                yield return null;
                Assert.That(replaced.MoveCalls, Is.EqualTo(replacedCalls));
                source.StopFade();
                Assert.That(fadeHandle.GetValue(fade), Is.Null);
                Assert.That(source.GetVolume(), Is.EqualTo(0.625f));

                trace.Clear();
                source.SetVolume(0.5f);
                var finite = new VolumeSteps(source, trace, "child", 0.375f, 0.125f, false);
                source.StartFade(NestedFade(finite, trace));
                Assert.That(startingVolume.GetValue(fade), Is.EqualTo(0.5f));
                for (int frame = 0; frame < 8 && fadeHandle.GetValue(fade) != null; ++frame) yield return null;
                Assert.That(fadeHandle.GetValue(fade), Is.Null, "Normal nested completion clears the original handle.");
                Assert.That(finite.MoveCalls, Is.EqualTo(2));
                CollectionAssert.AreEqual(new[] { "child:enter", "child:complete", "outer:complete" }, trace);
                Assert.That(source.GetVolume(), Is.EqualTo(0.125f), "Normal completion does not restore the start volume.");
                source.StopFade();
                Assert.That(source.GetVolume(), Is.EqualTo(0.125f), "A completed fade has no active stop/restoration.");

                // Directly inspect the genuine wrapper iterator's authored empty
                // Dispose on a real source/real Coroutine handle. This does not
                // claim that Unity calls IDisposable when stopping a coroutine.
                int markerCalls = 0;
                Coroutine marker = CoroutineUtils.Delay(() => ++markerCalls, 0f);
                fadeHandle.SetValue(fade, marker);
                source.SetVolume(0.25f);
                var untouched = new VolumeSteps(source, trace, "untouched", 0f, 0f, true);
                IEnumerator wrapper = (IEnumerator)performFade.Invoke(source, new object[] { untouched });
                Assert.That(wrapper.MoveNext(), Is.True);
                Assert.That(wrapper.Current, Is.SameAs(untouched));
                ((IDisposable)wrapper).Dispose();
                Assert.That(fadeHandle.GetValue(fade), Is.SameAs(marker), "Original Dispose leaves the retained handle intact.");
                Assert.That(source.GetVolume(), Is.EqualTo(0.25f));
                Assert.That(untouched.MoveCalls, Is.Zero);
                Assert.That(untouched.DisposeCalls, Is.Zero, "The wrapper does not dispose its retained child.");
                Assert.That(wrapper.MoveNext(), Is.False, "Empty Dispose does not alter the original resume state.");
                Assert.That(fadeHandle.GetValue(fade), Is.Null);
                CoroutineUtils.StopUtilCoroutine(ref marker);
                Assert.That(marker, Is.Null);

                // Silent generated PCM clips exercise the original untracked
                // WaitForSeconds callback, independently of audible playback.
                shortClip = AudioClip.Create("Lucid owned short silent one-shot", 96, 1, 48000, false);
                longClip = AudioClip.Create("Lucid owned long silent one-shot", 480000, 1, 48000, false);
                Assert.That(shortClip, Is.Not.Null);
                Assert.That(longClip, Is.Not.Null);
                Assert.That(shortClip.length, Is.GreaterThan(0f).And.LessThan(0.01f));
                Assert.That(longClip.length, Is.EqualTo(10f));
                source.PlayOneShot(shortClip, 0f);
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.True);
                source.Stop();
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.True, "Stop does not immediately clear the untracked flag.");
                for (int frame = 0; frame < 8 && source.IsPlayingOrLoadingOneShot(); ++frame) yield return null;
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.False, "The original completion still runs after Stop.");
                source.PlayOneShot(shortClip, 0f);
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.True);
                source.Pause();
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.True, "Pause does not immediately clear the untracked flag.");
                for (int frame = 0; frame < 8 && source.IsPlayingOrLoadingOneShot(); ++frame) yield return null;
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.False, "The original completion still runs after Pause.");
                int oneShotFrame = Time.frameCount;
                source.PlayOneShot(shortClip, 0f);
                source.PlayOneShot(longClip, 0f);
                float longDeadline = Time.time + longClip.length;
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.True);
                Assert.That(unusedOneShotHandle.GetValue(source), Is.Null, "The original field is not assigned by PlayOneShot.");
                for (int frame = 0; frame < 120 && source.IsPlayingOrLoadingOneShot() && Time.time < longDeadline; ++frame)
                    yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(oneShotFrame));
                Assert.That(Time.time, Is.LessThan(longDeadline), "The newer long completion is still pending.");
                Assert.That(source.IsPlayingOrLoadingOneShot(), Is.False,
                    "An older short completion clears the shared flag while the newer long delay remains pending.");
                Assert.That((bool)oneShotFlag.GetValue(source), Is.False);
                Assert.That(markerCalls, Is.Zero);
                Assert.That(cancelledCalls, Is.Zero);

                int destroyedHostCalls = 0;
                CoroutineUtils.Delay(() => ++destroyedHostCalls, 0f);
                UnityEngine.Object.Destroy(hostObject);
                yield return null;
                yield return null;
                Assert.That(host == null, Is.True);
                Assert.That(untyped.IsNull() && typed.IsNull(), Is.True);
                CollectionAssert.AreEqual(new[] { "untyped", "typed" }, shutdown,
                    "Original OnDestroy unregisters untyped before cached typed references.");
                Assert.That(destroyedHostCalls, Is.Zero, "Destroy cancels the host's pending zero-delay work.");
                Assert.That(markerCalls, Is.Zero);
                Assert.That(cancelledCalls, Is.Zero);
            }
            finally
            {
                try
                {
                    if (untyped != null) untyped.OnSystemShutdown -= untypedShutdown;
                    if (typed != null) typed.OnSystemShutdown -= typedShutdown;
                }
                finally
                {
                    try { if (host != null) host.StopAllCoroutines(); }
                    finally
                    {
                        try { if (hostObject != null) UnityEngine.Object.DestroyImmediate(hostObject); }
                        finally
                        {
                            try { if (sourceObject != null) UnityEngine.Object.DestroyImmediate(sourceObject); }
                            finally
                            {
                                try { if (shortClip != null) UnityEngine.Object.DestroyImmediate(shortClip); }
                                finally
                                {
                                    try { if (longClip != null) UnityEngine.Object.DestroyImmediate(longClip); }
                                    finally
                                    {
                                        try { Time.timeScale = oldTimeScale; }
                                        finally
                                        {
                                            try { Time.captureDeltaTime = oldCaptureDeltaTime; }
                                            finally
                                            {
                                                try { singleton.SetValue(null, oldSingleton); }
                                                finally { registry.SetValue(null, oldRegistry); }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
