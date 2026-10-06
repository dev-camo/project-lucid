using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace ProjectLucid.Tests
{
    public sealed class SplashScreenLifecycleTests
    {
        private static void Set(SplashScreen component, string name, object value)
        {
            typeof(SplashScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).SetValue(component, value);
        }

        [UnityTest]
        public IEnumerator OriginalSplashTracksTimelineFramesAndPublishesItsStopCallback()
        {
            FieldInfo registry = typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic);
            object oldRegistry = registry.GetValue(null);
            float oldTimeScale = Time.timeScale;
            GameObject owner = null;
            TimelineAsset timeline = null;
            PlayableDirector director = null;
            try
            {
                registry.SetValue(null, Activator.CreateInstance(registry.FieldType));
                Time.timeScale = 1f;
                owner = new GameObject("Lucid original splash timeline proof");
                owner.SetActive(false);
                director = owner.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.GameTime;
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
                timeline.fixedDuration = 60d;
                director.playableAsset = timeline;
                SplashScreen splash = owner.AddComponent<SplashScreen>();
                int stopped = 0;
                var completion = new UnityEvent();
                completion.AddListener(() => ++stopped);
                Set(splash, "m_playableDirector", director);
                Set(splash, "m_targetVideoPlayers", new List<VideoPlayer>());
                Set(splash, "m_targetSplashScreens", new List<UnityEngine.UI.Image>());
                Set(splash, "m_aspectRatioSplashScreenData", Array.Empty<SplashScreen.AspectRatioSplashScreenData>());
                Set(splash, "m_onDirectorStopped", completion);
                owner.SetActive(true);
                Assert.That(ProcessManager.GetSystem<SplashScreen>(), Is.SameAs(splash), "Original Awake registers the real component.");
                splash.BeginSplashScreen();
                Assert.That(splash.Progress, Is.EqualTo(0f));
                int initialFrame = Time.frameCount;
                director.Play();
                for (int i = 0; i < 10 && splash.Progress <= 0f; ++i) yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
                Assert.That(director.time, Is.GreaterThan(0d), "The real Timeline clock advances.");
                Assert.That(splash.Progress, Is.GreaterThan(0f).And.LessThan(1f), "Original Update reads the Timeline rather than a replacement timer.");
                Assert.That(stopped, Is.EqualTo(0));
                float beforeStop = splash.Progress;
                director.Stop();
                Assert.That(stopped, Is.EqualTo(1), "The real director event invokes the original UnityEvent boundary.");
                Assert.That(splash.Progress, Is.EqualTo(beforeStop), "The original stop callback leaves progress unchanged.");
                yield return null;
                Assert.That(splash.Progress, Is.EqualTo(beforeStop), "Original Update retains its timer when Timeline time resets to zero.");
                Assert.That(stopped, Is.EqualTo(1));
            }
            finally
            {
                try { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
                finally
                {
                    try { if (timeline != null) UnityEngine.Object.DestroyImmediate(timeline); }
                    finally
                    {
                        try { Time.timeScale = oldTimeScale; }
                        finally { registry.SetValue(null, oldRegistry); }
                    }
                }
            }
        }
    }
}
