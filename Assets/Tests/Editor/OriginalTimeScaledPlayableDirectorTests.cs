using System;
using NUnit.Framework;
using HardlightProject;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static ProjectLucid.Tests.TimeScaledTestHarness;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimeScaledPlayableDirector (TypeDef 0x0200080d, x86-64 0x5FD440..0x5FD77F). The component
    // and its required PlayableDirector are real, on an owned inactive host that never wakes. The TimeManager is a
    // real component holding the Environment category at scale 2, registered into its absent row before the
    // component's field initialiser takes the typed reference. The Evaluate branch of OnUpdate needs a live graph
    // and is not covered.
    public sealed class OriginalTimeScaledPlayableDirectorTests
    {
        private static void WithDirector(Action<Scene, TimeScaledPlayableDirector, PlayableDirector> body) => WithRegisteredManager(scene =>
        {
            GameObject host = scene.Owned.Host("Lucid owned time scaled director", false);
            var scaled = host.AddComponent<TimeScaledPlayableDirector>();
            Set(scaled, "m_timeCategory", scene.Category); // serialized in the original
            body(scene, scaled, host.GetComponent<PlayableDirector>());
        });

        // 0x5fd460: the sibling director is looked up with GetComponent and stored.
        [Test]
        public void ResetFindsTheSiblingDirector()
        {
            WithDirector((scene, scaled, director) =>
            {
                Set(scaled, "m_playableDirector", null);
                Call(scaled, "Reset");
                Assert.That(Get<PlayableDirector>(scaled, "m_playableDirector"), Is.SameAs(director));
            });
        }

        // 0x5fd4b0: manual time update (3), a graph rebuild, then Play only when play-on-awake is set.
        [Test]
        public void AwakeSwitchesToManualUpdateAndPlaysOnlyWhenAsked()
        {
            WithDirector((scene, scaled, director) =>
            {
                Set(scaled, "m_playableDirector", director);
                Call(scaled, "Awake");
                Assert.That(director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
                Assert.That(Subscribed(scene, UpdateOn.Update), Is.EqualTo(0));
                Set(scaled, "m_playOnAwake", true); // serialized in the original
                Call(scaled, "Awake");
                Assert.That(Subscribed(scene, UpdateOn.Update), Is.EqualTo(1));
            });
        }

        // 0x5fd4f0: unsubscribe, time = 0, subscribe (Update, empty path). A second Play reuses the entry.
        [Test]
        public void PlayRestartsFromZeroAndSubscribesOnce()
        {
            WithDirector((scene, scaled, director) =>
            {
                Set(scaled, "m_playableDirector", director);
                director.time = 3.0;
                scaled.Play();
                Assert.That(director.time, Is.EqualTo(0.0));
                Assert.That(Subscribed(scene, UpdateOn.Update), Is.EqualTo(1));
                scaled.Play();
                Assert.That(Subscribed(scene, UpdateOn.Update), Is.EqualTo(1));
            });
        }

        // 0x5fd5b0: unsubscribe, then time = 0.
        [Test]
        public void StopUnsubscribesAndRewinds()
        {
            WithDirector((scene, scaled, director) =>
            {
                Set(scaled, "m_playableDirector", director);
                scaled.Play();
                director.time = 2.0;
                scaled.Stop();
                Assert.That(Subscribed(scene, UpdateOn.Update), Is.EqualTo(0));
                Assert.That(director.time, Is.EqualTo(0.0));
            });
        }

        // 0x5fd620: the time grows by the delta, and a duration that is not above it (0x5fd686 ucomisd duration,
        // time; jbe) ends the playback: unsubscribe and rewind to zero.
        [Test]
        public void ReachingTheAssetsDurationEndsThePlayback()
        {
            WithDirector((scene, scaled, director) =>
            {
                var timeline = scene.Owned.Track(ScriptableObject.CreateInstance<TimelineAsset>());
                Assert.That(timeline.duration, Is.EqualTo(0.0), "an empty timeline");
                Set(scaled, "m_playableDirector", director);
                director.playableAsset = timeline;
                scaled.Play();
                scaled.OnUpdate(0.5f);
                Assert.That(Subscribed(scene, UpdateOn.Update), Is.EqualTo(0));
                Assert.That(director.time, Is.EqualTo(0.0));
            });
        }
    }
}
