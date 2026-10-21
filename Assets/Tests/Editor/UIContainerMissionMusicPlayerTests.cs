using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionMusicPlayer (Game.Runtime 0x020008de; OnEnable 0x63D800, Update 0x63DF80, Action_* 0x63E0B0..0x63E210,
    // SetupAudioBindings 0x63E290) over a real AudioManager with a background audio source (OwnedAudioServices). Not covered:
    // everything that needs the MusicTrackManager (Awake, OnDestroy, SelectTrack, Action_PlayNext, Action_PlayPrevious,
    // Action_ToggleShuffle, Play, OnMusicTrackChanged and the WaitForMusic iterator), because a real MusicTrackManager subscribes
    // to the save, level and mission managers on construction; the toggles, the playing animations and the repeat-on branch of
    // Action_ToggleRepeat, which need a UIToggle; and Seek, which needs a clip on the source.
    public sealed class UIContainerMissionMusicPlayerTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            internal Owned Owned;
            internal OwnedAudioServices Services;
            internal AudioSource Source;
            internal UIContainerMissionMusicPlayer Container;
            internal UIWidgetButtonMusic Button;
            internal Button UnityButton;
        }

        private static void Run(bool withBackgroundSource, Action<Rig> body) => Owned.Run(owned =>
        {
            using (var services = new OwnedAudioServices())
            {
                var rig = new Rig { Owned = owned, Services = services };
                services.RegisterAudioManager();
                if (withBackgroundSource) rig.Source = services.AddSource(HLAudioSourceIdentifier.Background);
                rig.Container = owned.Host("Lucid mission music player", false).AddComponent<UIContainerMissionMusicPlayer>();
                GameObject buttonHost = owned.Host("Lucid music button", false);
                rig.UnityButton = buttonHost.AddComponent<Button>();
                rig.Button = buttonHost.AddComponent<UIWidgetButtonMusic>();
                Set(rig.Button, "m_button", rig.UnityButton); // Reflection: serialized references and lists
                Set(rig.Container, "m_playerButtons", new List<UIWidgetButtonMusic> { rig.Button });
                Set(rig.Container, "m_playerToggles", new List<UIWidgetToggleMusicPlayer>());
                body(rig);
            }
        });

        // OnEnable ends by starting the WaitForMusic coroutine, which the inactive host refuses.
        private static void Enable(Rig rig)
        {
            LogAssert.Expect(LogType.Error, RefusedCoroutine);
            Call(rig.Container, "OnEnable");
            LogAssert.NoUnexpectedReceived();
        }

        // OnEnable 0x63D800: with the background source (0x48085690) every player control is unlocked, the source is kept
        // and the wait for the playing music starts.
        [Test]
        public void EnablingUnlocksTheControlsAndKeepsTheBackgroundSource()
        {
            Run(true, rig =>
            {
                rig.Button.Lock();
                Assert.That(rig.Button.LockValue.Value, Is.True);
                Assert.That(rig.UnityButton.interactable, Is.False);

                Enable(rig);

                Assert.That(rig.Button.LockValue.Value, Is.False);
                Assert.That(rig.UnityButton.interactable, Is.True);
                Assert.That(Get<HLAudioSourceData>(rig.Container, "m_audioSourceData"), Is.SameAs(rig.Services.Source(HLAudioSourceIdentifier.Background)));
            });
        }

        // Without the source the whole body is skipped (je 0x63DA3C): nothing is unlocked and no coroutine is started.
        [Test]
        public void WithoutTheBackgroundSourceEnablingDoesNothing()
        {
            Run(false, rig =>
            {
                rig.Button.Lock();

                Call(rig.Container, "OnEnable");

                LogAssert.NoUnexpectedReceived();
                Assert.That(rig.Button.LockValue.Value, Is.True);
                Assert.That(Get<HLAudioSourceData>(rig.Container, "m_audioSourceData"), Is.Null);
            });
        }

        // Action_StartSeek 0x63E110 mutes the source and Action_FinishSeek 0x63E170 unmutes it (the time argument is unused).
        [Test]
        public void SeekingMutesTheSourceUntilTheSeekFinishes()
        {
            Run(true, rig =>
            {
                Enable(rig);
                Assert.That(rig.Source.mute, Is.False);

                rig.Container.Action_StartSeek();
                Assert.That(rig.Source.mute, Is.True);

                rig.Container.Action_FinishSeek(12.5f);
                Assert.That(rig.Source.mute, Is.False);
            });
        }

        // Action_ToggleRepeat 0x63E0C0 stores the flag and the source's looping, and only a repeat that is on reaches the shuffle
        // toggle (cmp byte [rbx+0xC8], 0 at 0x63E0DE), so turning it off needs none.
        [Test]
        public void TurningRepeatOffClearsTheLoopingWithoutTouchingTheShuffleToggle()
        {
            Run(true, rig =>
            {
                Enable(rig);
                rig.Source.loop = true;
                Set(rig.Container, "m_isRepeating", true); // Reflection: the flag the repeat-on branch (not covered) writes

                rig.Container.Action_ToggleRepeat(false);

                Assert.That(rig.Source.loop, Is.False);
                Assert.That(Get<bool>(rig.Container, "m_isRepeating"), Is.False);
            });
        }

        // Action_TogglePlay 0x63E0B0 calls Play only for a true flag; Play would fault here (no current track).
        [Test]
        public void SwitchingPlayOffDoesNothing()
        {
            Run(true, rig => Assert.DoesNotThrow(() => rig.Container.Action_TogglePlay(false)));
        }

        // Update 0x63DF80 formats the source time as "{0:00}:{1:00}" (minutes, seconds) and publishes it as the slider value; a
        // source with no clip is at time 0.
        [Test]
        public void UpdateShowsTheSourceTime()
        {
            Run(true, rig =>
            {
                Enable(rig);
                rig.Container.TrackCurrentTimeFormatted.Value = "stale";
                rig.Container.TrackSliderValue.Value = 7f;

                Call(rig.Container, "Update");

                Assert.That(rig.Container.TrackCurrentTimeFormatted.Value, Is.EqualTo("00:00"));
                Assert.That(rig.Container.TrackSliderValue.Value, Is.EqualTo(0f));
            });
        }

        // SetupAudioBindings 0x63E290: the clip length is the total time and is formatted as minutes and seconds.
        [Test]
        public void TheClipLengthIsTheTotalTimeFormattedAsMinutesAndSeconds()
        {
            Run(true, rig =>
            {
                foreach ((int seconds, string expected) in new[] { (75, "01:15"), (5, "00:05") })
                {
                    AudioClip clip = AudioClip.Create("lucid mission music " + seconds, seconds * 8000, 1, 8000, false);
                    rig.Services.Own(clip);

                    Call(rig.Container, "SetupAudioBindings", new HLAudioClipHandler.LoadedAudioParameters { Clip = clip });

                    Assert.That(rig.Container.TrackTotalTime.Value, Is.EqualTo((float)seconds), expected);
                    Assert.That(rig.Container.TrackTotalTimeFormatted.Value, Is.EqualTo(expected));
                }
            });
        }
    }
}
