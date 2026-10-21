using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.TimeScaledTestHarness;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OrbDetectorUI (x86-64 0x693080..0x693CAF) and its OrbDetectorLevel (0x693CB0..0x693CFF) over a real
    // UIContainerGameplayIsland whose public bindables are the proximity inputs. The detector is a real
    // time scaled component on an inactive host; the engine's per-frame call is the public OnUpdate.
    // A ping is read from the owned AudioManager's diagnostics: a declared clip sent to the Sfx source that was never
    // added reports the missing source twice, the second time naming the clip (as OriginalAudioComponentPlaybackTests).
    public sealed class OrbDetectorUITests
    {
        // Editor callbacks run InternalValidate (0x693170) when the component is added, and the original enumerates
        // m_levels with no null test while Edit Mode AddComponent leaves the list null, so the fault is expected.
        private static OrbDetectorUI AddDetector(Scene scene, UIContainerGameplayIsland island)
        {
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));
            var detector = Add<OrbDetectorUI>(scene, scene.Owned.Host("Lucid orb detector", false));
            Set(detector, "m_gameplayIsland", island);
            return detector;
        }

        private static UIContainerGameplayIsland AddIsland(Scene scene) =>
            scene.Owned.Host("Lucid gameplay island", false).AddComponent<UIContainerGameplayIsland>();

        private static OrbDetectorUI.OrbDetectorLevel NewLevel(float minProximity, float repeatSeconds, params GameObject[] enabledWhileActive)
        {
            var level = new OrbDetectorUI.OrbDetectorLevel();
            Set(level, "m_minProximity", minProximity);
            Set(level, "m_enabledWhileActive", new List<GameObject>(enabledWhileActive));
            Set(level, "m_audioRepeatFrequency", repeatSeconds);
            return level;
        }

        private static GameObject[] Marks(Scene scene, int count)
        {
            var marks = new GameObject[count];
            for (int index = 0; index < count; index++) marks[index] = scene.Owned.Host("Lucid level mark " + index);
            return marks;
        }

        private static bool[] Active(GameObject[] marks)
        {
            var active = new bool[marks.Length];
            for (int index = 0; index < marks.Length; index++) active[index] = marks[index].activeSelf;
            return active;
        }

        private static OrbDetectorUI Detector(Scene scene, UIContainerGameplayIsland island, float[] minimums, float repeatSeconds, GameObject[] marks)
        {
            OrbDetectorUI detector = AddDetector(scene, island);
            var levels = new List<OrbDetectorUI.OrbDetectorLevel>();
            for (int index = 0; index < minimums.Length; index++) levels.Add(NewLevel(minimums[index], repeatSeconds, marks[index]));
            Set(detector, "m_levels", levels);
            // OnEnable (0x6932B0) ends by showing no level; SetLevel is the real path to that state.
            Call(detector, "SetLevel", -1);
            return detector;
        }

        private static void Tick(OrbDetectorUI detector, UIContainerGameplayIsland island, float weightedProximity, float deltaTime = 0.016f)
        {
            island.WeightedProximity.Value = weightedProximity;
            detector.OnUpdate(deltaTime);
        }

        // InternalUpdate 0x693600 walks the levels from the bottom while each next minimum is at or below the proximity
        // (ucomiss/jbe 0x69382d, so equality qualifies), and SetLevel 0x6934F0 shows every level up to the chosen one.
        // Rows: weighted proximity, then which of the three levels are shown.
        [Test]
        public void TheShownLevelsFollowTheWeightedProximity()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                GameObject[] marks = Marks(scene, 3);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.25f, 0.5f, 0.75f }, 1f, marks);
                Assert.That(Active(marks), Is.EqualTo(new[] { false, false, false }), "no level before any proximity");

                Tick(detector, island, 0.1f);
                Assert.That(Active(marks), Is.EqualTo(new[] { false, false, false }), "below the first minimum");
                Tick(detector, island, 0.25f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, false, false }), "equal to the first minimum qualifies");
                Tick(detector, island, 0.5f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, true, false }));
                Tick(detector, island, 0.4f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, false, false }), "a lower proximity hides the higher levels again");
                Tick(detector, island, 0.99f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, true, true }));
                Tick(detector, island, 0f);
                Assert.That(Active(marks), Is.EqualTo(new[] { false, false, false }));
            });
        }

        // The walk stops at the first level whose minimum is above the proximity (0x69382d jbe falls out at the first
        // failing level), so a later, lower minimum is never reached.
        [Test]
        public void TheLevelWalkStopsAtTheFirstMinimumAboveTheProximity()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                GameObject[] marks = Marks(scene, 2);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.5f, 0.25f }, 1f, marks);
                Tick(detector, island, 0.3f);
                Assert.That(Active(marks), Is.EqualTo(new[] { false, false }), "0.25 <= 0.3 is not reached past the 0.5 level");
                Tick(detector, island, 0.6f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, true }));
            });
        }

        // Mathf.Approximately(last, current) (0x69378c..0x6937d6) leaves the levels alone, so an unchanged or
        // epsilon-different proximity does not run SetLevel. SetLevel also runs only when the walked level differs
        // from the current one (cmp m_currentLevel, r14d; jne 0x69382f), so a real change that lands on the same
        // level index leaves the shown levels alone too, and a change of level index refreshes them.
        [Test]
        public void AnUnchangedProximityLeavesTheShownLevelsAlone()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                GameObject[] marks = Marks(scene, 2);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.25f, 0.5f }, 1f, marks);
                Tick(detector, island, 0.5f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, true }));

                marks[0].SetActive(false);
                Tick(detector, island, 0.5f);
                Assert.That(marks[0].activeSelf, Is.False, "same proximity: SetLevel is not run");
                Tick(detector, island, 0.5000001f);
                Assert.That(marks[0].activeSelf, Is.False, "within the relative tolerance of 1e-6");
                Tick(detector, island, 0.51f);
                Assert.That(marks[0].activeSelf, Is.False, "a real change that keeps the level index does not run SetLevel (0x69382f)");
                Tick(detector, island, 0.3f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, false }), "a new level index runs SetLevel");
            });
        }

        // The walk's compare is ucomiss min, w; jbe (0x6937ea..0x69382d), taken when min <= w or the compare is
        // unordered, so a NaN proximity climbs to the last level (an ordered <= would stay at no level).
        [Test]
        public void ANaNProximityShowsEveryLevel()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                GameObject[] marks = Marks(scene, 3);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.25f, 0.5f, 0.75f }, 1f, marks);
                Tick(detector, island, float.NaN);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, true, true }));
            });
        }

        // While the orb is being collected (collection bindable at +0x58, read at 0x69364e) the proximity is taken as zero,
        // the music restore starts (0x693666..0x693670) and the shown levels drop to none.
        [Test]
        public void CollectingAnOrbHidesEveryLevelAndStartsTheMusicRestore()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                GameObject[] marks = Marks(scene, 2);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.25f, 0.5f }, 1f, marks);
                Tick(detector, island, 0.9f);
                Assert.That(Active(marks), Is.EqualTo(new[] { true, true }));

                island.CollectionInProgress.Value = true;
                detector.OnUpdate(0.016f);
                Assert.That(Active(marks), Is.EqualTo(new[] { false, false }));
                Assert.That(Get<bool>(detector, "m_restoringMusicAfterOrbCollection"), Is.True);
                Assert.That(Get<float>(detector, "m_timeSinceOrbCollection"), Is.EqualTo(0f));
            });
        }

        // After collection the restore runs until the restore curve's last key time: the elapsed time is added first
        // (0x6936b5) and the restore ends once it is not below that time (ucomiss/jb 0x693717), so reaching it ends it.
        [Test]
        public void TheMusicRestoreEndsWhenTheElapsedTimeReachesTheLastKey()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                OrbDetectorUI detector = Detector(scene, island, new float[0], 1f, new GameObject[0]);
                Set(detector, "m_orbCollectionLowPassRestore", new AnimationCurve(new Keyframe(0f, 500f), new Keyframe(2f, 22000f)));
                island.CollectionInProgress.Value = true;
                detector.OnUpdate(0.016f);
                island.CollectionInProgress.Value = false;

                detector.OnUpdate(1f);
                Assert.That(Get<bool>(detector, "m_restoringMusicAfterOrbCollection"), Is.True, "1 < 2");
                detector.OnUpdate(0.5f);
                Assert.That(Get<bool>(detector, "m_restoringMusicAfterOrbCollection"), Is.True, "1.5 < 2");
                detector.OnUpdate(0.5f);
                Assert.That(Get<bool>(detector, "m_restoringMusicAfterOrbCollection"), Is.False, "2 is not below 2");
                Assert.That(Get<float>(detector, "m_timeSinceOrbCollection"), Is.EqualTo(2f));
            });
        }

        // In a level, time accumulates (0x693847) and once it reaches the level's repeat period (jb 0x69387b, so equality
        // pings) the period is subtracted for as long as it still fits (the jae loop 0x6938a0). A ping is not heard here
        // (no AudioManager), but the leftover time is the same. Rows: delta per update, then the leftover time.
        [Test]
        public void ThePingTimerKeepsTheRemainderOfTheRepeatPeriod()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                GameObject[] marks = Marks(scene, 1);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.5f }, 0.25f, marks);
                Tick(detector, island, 0.6f, 0.016f);
                Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0f), "entering the level restarts the ping timer (0x69359e)");

                detector.OnUpdate(0.125f);
                Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0.125f));
                detector.OnUpdate(0.125f);
                Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0f), "exactly one period: the ping is due and the timer wraps to zero");
                detector.OnUpdate(0.8f);
                Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0.05f).Within(1e-5f), "0.8 holds three periods of 0.25 and leaves 0.05");
            });
        }

        // With no level shown (-1) the ping timer does not run (0x69383d).
        [Test]
        public void NoPingTimerRunsWithoutALevel()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                OrbDetectorUI detector = Detector(scene, island, new[] { 0.5f }, 0.25f, Marks(scene, 1));
                Tick(detector, island, 0.1f, 0.3f);
                Tick(detector, island, 0.1f, 0.3f);
                Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0f));
            });
        }

        // IsCollectingOrb 0x6938D0 returns the island's collection bindable.
        [Test]
        public void IsCollectingOrbReadsTheIslandBindable()
        {
            WithManager(scene =>
            {
                UIContainerGameplayIsland island = AddIsland(scene);
                OrbDetectorUI detector = Detector(scene, island, new float[0], 1f, new GameObject[0]);
                Assert.That(Call(detector, "IsCollectingOrb"), Is.EqualTo(false));
                island.CollectionInProgress.Value = true;
                Assert.That(Call(detector, "IsCollectingOrb"), Is.EqualTo(true));
            });
        }

        // A music source with an AudioSource and, when asked, an AudioLowPassFilter, added to the owned AudioManager.
        private static AudioLowPassFilter AddMusic(OwnedAudioServices services, AudioManager audio, bool withFilter)
        {
            GameObject host = services.Host("music");
            AudioSource source = host.AddComponent<AudioSource>();
            AudioLowPassFilter filter = withFilter ? host.AddComponent<AudioLowPassFilter>() : null;
            audio.AddSource(new HLAudioSourceData(HLAudioSourceIdentifier.Background, source, false, HLAudioSourceTransition.None, default));
            return filter;
        }

        private static OrbDetectorUI MusicDetector(Scene scene, UIContainerGameplayIsland island, AudioManager audio, HLAudioSourceIdentifier musicSource,
            float[] minimums = null, GameObject[] marks = null)
        {
            OrbDetectorUI detector = Detector(scene, island, minimums ?? new float[0], 1f, marks ?? new GameObject[0]);
            Set(detector, "m_gameplayMusicAudioSource", musicSource);
            // The cutoff is 1000 at proximity 0 and 21000 at proximity 1; the restore curve falls from 20000 to 500 over two seconds.
            Set(detector, "m_orbProximityMusicLowPassCutoff", AnimationCurve.Linear(0f, 1000f, 1f, 21000f));
            Set(detector, "m_orbCollectionLowPassRestore", AnimationCurve.Linear(0f, 20000f, 2f, 500f));
            Call(detector, "OnAudioManagerValid", audio);
            return detector;
        }

        // SetMusicEffects 0x693330: the filter is found once through AudioManager.TryGetAudioSourceData (0x6933f9) and
        // cached; the proximity is clamped to 0..1 (minss/cmpltss 0x693460..0x69346c) before the cutoff curve is read.
        [Test]
        public void TheMusicLowPassFollowsTheClampedProximity()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    AudioLowPassFilter filter = AddMusic(services, audio, true);
                    UIContainerGameplayIsland island = AddIsland(scene);
                    OrbDetectorUI detector = MusicDetector(scene, island, audio, HLAudioSourceIdentifier.Background);

                    Call(detector, "SetMusicEffects", 0.5f);
                    Assert.That(Get<AudioLowPassFilter>(detector, "m_musicLowPassFilter"), Is.SameAs(filter));
                    Assert.That(filter.enabled, Is.True);
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(11000f).Within(0.5f));
                    Call(detector, "SetMusicEffects", 3f);
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(21000f).Within(0.5f), "clamped down to 1");
                    Call(detector, "SetMusicEffects", -2f);
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(1000f).Within(0.5f), "clamped up to 0");
                }
            });
        }

        // While the restore runs the filter is switched off and the lower of the restore curve and the proximity cutoff
        // is applied (minss 0x693497); when the restore ends the filter is on again and only the proximity cutoff applies.
        [Test]
        public void TheMusicRestoreSwitchesTheFilterOffAndAppliesTheLowerCutoff()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    AudioLowPassFilter filter = AddMusic(services, audio, true);
                    UIContainerGameplayIsland island = AddIsland(scene);
                    OrbDetectorUI detector = MusicDetector(scene, island, audio, HLAudioSourceIdentifier.Background);

                    island.CollectionInProgress.Value = true;
                    detector.OnUpdate(0.016f);
                    Assert.That(filter.enabled, Is.False, "restoring: the filter is off");
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(20000f).Within(0.5f), "restore(0) 20000 is below the cutoff(1) 21000");

                    island.CollectionInProgress.Value = false;
                    island.DistanceProximity.Value = 1f;
                    detector.OnUpdate(1f);
                    Assert.That(filter.enabled, Is.False);
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(10250f).Within(0.5f), "restore(1) 10250 is below the cutoff(1) 21000");

                    island.DistanceProximity.Value = 0.5f;
                    detector.OnUpdate(1f);
                    Assert.That(filter.enabled, Is.True, "the restore ended at two seconds");
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(11000f).Within(0.5f));
                }
            });
        }

        // HLAudioSourceData's constructor reads the filter with GetComponent, so a source without one has a null
        // LowPassFilter and SetMusicEffects returns at the null test (0x693435) before HasLowPassFilter is read;
        // the m_noLowPassFilterAvailable store (0x6934d8) cannot be reached through it. Nothing is remembered for a
        // filterless source, an unknown source, or a missing audio manager.
        [Test]
        public void ASourceWithoutAFilterOrAnUnknownSourceRemembersNothing()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    AddMusic(services, audio, false);
                    UIContainerGameplayIsland island = AddIsland(scene);

                    OrbDetectorUI plain = MusicDetector(scene, island, audio, HLAudioSourceIdentifier.Background);
                    Call(plain, "SetMusicEffects", 0.5f);
                    Assert.That(Get<bool>(plain, "m_noLowPassFilterAvailable"), Is.False);
                    Assert.That(Get<AudioLowPassFilter>(plain, "m_musicLowPassFilter") == null, Is.True);

                    OrbDetectorUI unknown = MusicDetector(scene, island, audio, HLAudioSourceIdentifier.Ambient);
                    Call(unknown, "SetMusicEffects", 0.5f);
                    Assert.That(Get<bool>(unknown, "m_noLowPassFilterAvailable"), Is.False, "no such source: nothing is remembered");

                    OrbDetectorUI silent = Detector(scene, island, new float[0], 1f, new GameObject[0]);
                    Set(silent, "m_gameplayMusicAudioSource", HLAudioSourceIdentifier.Background);
                    Call(silent, "SetMusicEffects", 0.5f);
                    Assert.That(Get<bool>(silent, "m_noLowPassFilterAvailable"), Is.False, "no audio manager yet: nothing is remembered");
                }
            });
        }

        // The cached audio manager is the one OnAudioManagerValid (0x693290) receives; SetLevel then pings through it.
        // Here only the stored reference is read.
        [Test]
        public void OnAudioManagerValidKeepsTheManager()
        {
            WithManager(scene =>
            {
                OrbDetectorUI detector = Detector(scene, AddIsland(scene), new float[0], 1f, new GameObject[0]);
                var host = scene.Owned.Host("Lucid audio manager", false).AddComponent<AudioManager>();
                Call(detector, "OnAudioManagerValid", host);
                Assert.That(Get<AudioManager>(detector, "m_audioManager"), Is.SameAs(host));
            });
        }

        private static string[] PingAtMissingSfx(string clipName) => new[]
        {
            "Cannot load clip, the source Sfx does not exist.:0",
            "Cannot play one shot of audio clip " + clipName + ", the source Sfx does not exist.:0",
        };

        private static void SetClips(OrbDetectorUI detector, params HLAudioClipIdentifier[] clips)
        {
            var levels = Get<List<OrbDetectorUI.OrbDetectorLevel>>(detector, "m_levels");
            for (int index = 0; index < clips.Length; index++) Set(levels[index], "m_audioClip", clips[index]);
        }

        // SetLevel 0x6934F0 keeps overwriting the reached level while it walks (cmovle r15, 0x693568), so the level it
        // pings is the last one shown, and it pings only when the level changes (je 0x6935ba skips an unchanged level).
        [Test]
        public void EnteringALevelPingsTheLastReachedLevelOnce()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    services.Clip((HLAudioClipIdentifier)0x1A01, "lucid-orb-a");
                    services.Clip((HLAudioClipIdentifier)0x1A02, "lucid-orb-b");
                    UIContainerGameplayIsland island = AddIsland(scene);
                    OrbDetectorUI detector = Detector(scene, island, new[] { 0.25f, 0.5f }, 1f, Marks(scene, 2));
                    Set(detector, "m_audioSource", HLAudioSourceIdentifier.Sfx);
                    SetClips(detector, (HLAudioClipIdentifier)0x1A01, (HLAudioClipIdentifier)0x1A02);
                    Call(detector, "OnAudioManagerValid", audio);

                    Tick(detector, island, 0.6f);
                    Assert.That(services.Log.Errors, Is.EqualTo(PingAtMissingSfx("lucid-orb-b")), "the last reached level pings, not the first");
                    services.Log.Errors.Clear();
                    Tick(detector, island, 0.6f);
                    Call(detector, "SetLevel", 1);
                    Assert.That(services.Log.Errors, Is.Empty, "an unchanged level does not ping again");
                }
            });
        }

        // PlayAudio 0x6938F0 returns at each of four guards: no audio manager (0x693942), no ping source (0x69394b),
        // no clip on the level (0x693951) and an orb being collected (0x69396a).
        [Test]
        public void ThePingNeedsAManagerASourceAClipAndNoOrbCollection()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    services.Clip((HLAudioClipIdentifier)0x1A01, "lucid-orb-a");
                    UIContainerGameplayIsland island = AddIsland(scene);
                    OrbDetectorUI detector = Detector(scene, island, new[] { 0.25f }, 1f, Marks(scene, 1));
                    SetClips(detector, (HLAudioClipIdentifier)0x1A01);
                    var level = Get<List<OrbDetectorUI.OrbDetectorLevel>>(detector, "m_levels")[0];
                    Set(detector, "m_audioSource", HLAudioSourceIdentifier.Sfx);

                    Call(detector, "PlayAudio", level);
                    Assert.That(services.Log.Errors, Is.Empty, "no audio manager yet");
                    Call(detector, "OnAudioManagerValid", audio);
                    Call(detector, "PlayAudio", level);
                    Assert.That(services.Log.Errors, Is.EqualTo(PingAtMissingSfx("lucid-orb-a")), "all four guards pass");

                    services.Log.Errors.Clear();
                    island.CollectionInProgress.Value = true;
                    Call(detector, "PlayAudio", level);
                    Assert.That(services.Log.Errors, Is.Empty, "an orb is being collected");
                    island.CollectionInProgress.Value = false;
                    Set(detector, "m_audioSource", HLAudioSourceIdentifier.None);
                    Call(detector, "PlayAudio", level);
                    Assert.That(services.Log.Errors, Is.Empty, "no ping source");
                    Set(detector, "m_audioSource", HLAudioSourceIdentifier.Sfx);
                    Set(level, "m_audioClip", HLAudioClipIdentifier.None);
                    Call(detector, "PlayAudio", level);
                    Assert.That(services.Log.Errors, Is.Empty, "no clip on the level");
                }
            });
        }

        // OnEnable 0x6932B0 (stores 0x6932c0..0x6932fb) forgets the cached filter and clears the no-filter flag, the
        // restore state and the timers, then SetMusicEffects(0) (cutoff 1000) and SetLevel(-1) hide every level.
        [Test]
        public void EnablingResetsTheMusicEffectsAndHidesEveryLevel()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    AudioLowPassFilter filter = AddMusic(services, audio, true);
                    UIContainerGameplayIsland island = AddIsland(scene);
                    GameObject[] marks = Marks(scene, 1);
                    OrbDetectorUI detector = MusicDetector(scene, island, audio, HLAudioSourceIdentifier.Background, new[] { 0.25f }, marks);

                    Tick(detector, island, 0.3f);
                    Tick(detector, island, 0.3f, 0.3f);
                    island.CollectionInProgress.Value = true;
                    detector.OnUpdate(0.016f);
                    island.CollectionInProgress.Value = false;
                    Tick(detector, island, 0.3f);
                    Tick(detector, island, 0.3f, 0.3f);
                    // No path sets this flag (a filterless source returns before the store), so it is written to see it cleared.
                    Set(detector, "m_noLowPassFilterAvailable", true);
                    Assert.That(Get<bool>(detector, "m_restoringMusicAfterOrbCollection"), Is.True);
                    Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0.3f).Within(1e-5f));
                    Assert.That(filter.enabled, Is.False);

                    Call(detector, "OnEnable");
                    Assert.That(filter.enabled, Is.True);
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(1000f).Within(0.5f));
                    Assert.That(Get<bool>(detector, "m_noLowPassFilterAvailable"), Is.False);
                    Assert.That(Get<bool>(detector, "m_restoringMusicAfterOrbCollection"), Is.False);
                    Assert.That(Get<float>(detector, "m_timeSinceAudio"), Is.EqualTo(0f));
                    Assert.That(Get<float>(detector, "m_timeSinceOrbCollection"), Is.EqualTo(0f));
                    Assert.That(Get<int>(detector, "m_currentLevel"), Is.EqualTo(-1));
                    Assert.That(Active(marks), Is.EqualTo(new[] { false }));
                }
            });
        }

        // OnDisable 0x6935D0 ends with SetMusicEffects(0) (0x6935ec), dropping the cutoff to the proximity-zero value.
        [Test]
        public void DisablingResetsTheMusicCutoff()
        {
            WithManager(scene =>
            {
                using (var services = new OwnedAudioServices())
                {
                    AudioManager audio = services.RegisterAudioManager();
                    AudioLowPassFilter filter = AddMusic(services, audio, true);
                    OrbDetectorUI detector = MusicDetector(scene, AddIsland(scene), audio, HLAudioSourceIdentifier.Background);
                    Call(detector, "OnEnable");
                    Call(detector, "SetMusicEffects", 1f);
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(21000f).Within(0.5f));
                    Call(detector, "OnDisable");
                    Assert.That(filter.cutoffFrequency, Is.EqualTo(1000f).Within(0.5f));
                }
            });
        }
    }
}
