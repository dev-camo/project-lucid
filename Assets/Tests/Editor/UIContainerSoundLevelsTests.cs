using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.SettingsPageTestSupport;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSoundLevels (Game.Runtime 0x02000943; Start 0x67F2E0, Refresh 0x67F3D0, Action_OnExit 0x67F690, the
    // toggles 0x67FA40..0x67FC70, LoadSettingsSaveData 0x67F3B0) over its options page, a real SaveManager holding the live
    // settings (SettingsPageTestSupport), a genuine App and an AudioManager in its row. The AudioManager is the
    // unawakened component, so it has no mixer: a toggle stores its volume and then faults at GetMixer.
    //
    // Not covered: the mixer calls the toggles end in (UpdateAudioSettings 0x67FAD0 and the four copies inlined in the
    // toggles). They need an AudioMixer asset, which Edit Mode cannot create; AudioMixerLifecycleTests covers the
    // HLAudioMixerDefinition setters they call.
    public sealed class UIContainerSoundLevelsTests
    {
        private sealed class Rig
        {
            internal Owned Owned;
            internal Page Page;
            internal App App;
            internal AudioManager Audio;
            internal UIContainerSoundLevels Container;
            internal UIContainerSoundLevelsOptions Options;
            internal UIModernEvent SaveEvent;
        }

        // The stored levels are four different numbers so a swapped pair shows.
        private static void Run(Action<Rig> body) => With(page =>
        {
            Owned owned = page.Owned;
            page.Settings.MasterVolume = 0.11f;
            page.Settings.MusicVolume = 0.22f;
            page.Settings.SfxVolume = 0.33f;
            page.Settings.VoiceVolume = 0.44f;
            page.Settings.MarkSaved();
            owned.LeaseRow<UIScreenTransitionManager>(); // App's field initialiser
            var rig = new Rig { Owned = owned, Page = page };
            rig.App = owned.Register(owned.Host("Lucid sound levels app", false).AddComponent<App>());
            rig.Audio = owned.Register(owned.Host("Lucid sound levels audio", false).AddComponent<AudioManager>());
            rig.Container = Add<UIContainerSoundLevels>(page);
            rig.Options = Add<UIContainerSoundLevelsOptions>(page);
            rig.SaveEvent = NewEvent(owned, "save-sound-levels");
            Set(rig.Container, "m_soundLevelsOptionsContainer", rig.Options); // serialized in the original
            Set(rig.Container, "m_saveEvent", rig.SaveEvent);
            body(rig);
        });

        private static (float master, float music, float sfx, float voice) Shown(UIContainerSoundLevelsOptions options) =>
            (options.MasterVolume.Value, options.MusicVolume.Value, options.SfxVolume.Value, options.VoiceVolume.Value);

        private static (float master, float music, float sfx, float voice) Remembered(UIContainerSoundLevels container) =>
            (Get<float>(container, "m_masterVolume"), Get<float>(container, "m_musicVolume"),
                Get<float>(container, "m_sfxVolume"), Get<float>(container, "m_voiceVolume"));

        // Start 0x67F2E0 takes the AudioManager and SaveManager (0x67F346, 0x67F36C), keeps the manager's live settings
        // (+0x90 at 0x67F385) and calls Refresh, which remembers the four stored levels (the 16 bytes copied at 0x67F463) and
        // hands them to the options page.
        [Test]
        public void StartTakesTheServicesAndShowsTheStoredLevels()
        {
            Run(rig =>
            {
                Call(rig.Container, "Start");

                Assert.That(Get<AudioManager>(rig.Container, "m_audioManager"), Is.SameAs(rig.Audio));
                Assert.That(Get<SaveManager>(rig.Container, "m_saveManager"), Is.SameAs(rig.Page.Save));
                Assert.That(Get<SaveDataSettings>(rig.Container, "m_existingSaveData"), Is.SameAs(rig.Page.Settings));
                Assert.That(Remembered(rig.Container), Is.EqualTo((0.11f, 0.22f, 0.33f, 0.44f)));
                Assert.That(Shown(rig.Options), Is.EqualTo((0.11f, 0.22f, 0.33f, 0.44f)));
            });
        }

        // LoadSettingsSaveData 0x67F3B0 reads the live settings of the SaveManager the page took (+0x58, +0x90).
        [Test]
        public void LoadSettingsReadsTheLiveSettingsOfTheSaveManager()
        {
            Run(rig =>
            {
                Call(rig.Container, "Start");

                Assert.That(Call(rig.Container, "LoadSettingsSaveData"), Is.SameAs(rig.Page.Settings));
            });
        }

        // The four toggles 0x67FA40, 0x67FB50, 0x67FBE0 and 0x67FC70 each store the level in their own field (+0x60, +0x64,
        // +0x68, +0x6C) and keep the other three before they go to the mixer; the options page's toggle actions are wired to them.
        [Test]
        public void EachToggleStoresItsOwnLevelAndGoesOnToTheMixer()
        {
            (Action<UIContainerSoundLevelsOptions> press, (float, float, float, float) expected)[] cases =
            {
                (options => options.Action_OnMasterVolumeToggle(0.5f), (0.5f, 0.22f, 0.33f, 0.44f)),
                (options => options.Action_OnMusicVolumeToggle(0.6f), (0.11f, 0.6f, 0.33f, 0.44f)),
                (options => options.Action_OnSfxVolumeToggle(0.7f), (0.11f, 0.22f, 0.7f, 0.44f)),
                (options => options.Action_OnVoiceVolumeToggle(0.8f), (0.11f, 0.22f, 0.33f, 0.8f)),
            };
            foreach (var testCase in cases)
            {
                Run(rig =>
                {
                    Call(rig.Container, "Start");

                    // No mixer exists on the unawakened AudioManager, so the toggle faults once its fields are stored.
                    Assert.Throws<NullReferenceException>(() => testCase.press(rig.Options));

                    Assert.That(Remembered(rig.Container), Is.EqualTo(testCase.expected));
                    Assert.That(Shown(rig.Options), Is.EqualTo(testCase.expected), "the options page took the value before the callback");
                });
            }
        }

        // Action_ResetToDefaults 0x67FA10 resets the audio settings of the live settings (SetAudioDefaults(true) at 0x67FA24,
        // which marks them changed) and refreshes the page, which shows and remembers the defaults.
        [Test]
        public void ResettingToDefaultsRestoresFullVolumeInTheLiveSettingsAndThePage()
        {
            Run(rig =>
            {
                Call(rig.Container, "Start");
                Assert.That(rig.Page.Settings.HasChangesToSave(), Is.False);

                rig.Container.Action_ResetToDefaults();

                Assert.That(rig.Page.Settings.MasterVolume, Is.EqualTo(1f));
                Assert.That(rig.Page.Settings.MusicVolume, Is.EqualTo(1f));
                Assert.That(rig.Page.Settings.SfxVolume, Is.EqualTo(1f));
                Assert.That(rig.Page.Settings.VoiceVolume, Is.EqualTo(1f));
                Assert.That(rig.Page.Settings.HasChangesToSave(), Is.True);
                Assert.That(Remembered(rig.Container), Is.EqualTo((1f, 1f, 1f, 1f)));
                Assert.That(Shown(rig.Options), Is.EqualTo((1f, 1f, 1f, 1f)));
            });
        }

        // Action_OnExit 0x67F690 reads the levels the options page shows (0x67F759..0x67F7A4), copies the live settings into a
        // new SaveDataSettings (CopyFrom 0x67F7CF), applies the levels to the copy, stores the copy under GameSettings in the
        // App's storage (0x67F8C9) and publishes the save event as an OnClick (event type 8, 0x67F8DD). The live settings are
        // not changed. The options page's save click is wired to it. The broadcast type is Both (ecx = 3 at 0x67F8FD), so the
        // callback bound to the save event runs as well as the unbound ones.
        [Test]
        public void SavingTheLevelsStoresACopyOfTheSettingsAndPublishesTheSaveEvent()
        {
            Run(rig =>
            {
                rig.Page.Settings.CameraInvertedControls = true;
                Call(rig.Container, "Start");
                rig.Options.MasterVolume.Value = 0.9f;
                rig.Options.MusicVolume.Value = 0.8f;
                rig.Options.SfxVolume.Value = 0.7f;
                rig.Options.VoiceVolume.Value = 0.6f;
                var received = new List<(UIModernEventType, UIModernEvent)>();
                var bound = new List<UIModernEvent>();

                Publishing(rig, received, bound, () => rig.Options.Action_OnSaveClick());

                SaveDataSettings stored = rig.App.Storage.GetValueOnly<SaveDataSettings>(AppFSMKeys.GameSettings);
                Assert.That(stored, Is.Not.SameAs(rig.Page.Settings));
                Assert.That((stored.MasterVolume, stored.MusicVolume, stored.SfxVolume, stored.VoiceVolume), Is.EqualTo((0.9f, 0.8f, 0.7f, 0.6f)));
                Assert.That(stored.CameraInvertedControls, Is.True, "the rest of the settings are copied");
                Assert.That((rig.Page.Settings.MasterVolume, rig.Page.Settings.VoiceVolume), Is.EqualTo((0.11f, 0.44f)));
                Assert.That(received, Is.EqualTo(new[] { (UIModernEventType.OnClick, rig.SaveEvent) }));
                Assert.That(bound, Is.EqualTo(new[] { rig.SaveEvent }), "the callback bound to the save event");
            });
        }

        // Publishes through a genuine message exchange in its row, with a subscriber to every event type and one bound to the
        // save event for OnClick.
        private static void Publishing(Rig rig, List<(UIModernEventType, UIModernEvent)> received, List<UIModernEvent> bound, Action action)
        {
            string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var row = rig.Owned.Lease(new VerificationFixtureOwnership.RegistryRow(name));
            var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
            row.Register(exchange);
            var unsubscribe = new List<Action>();
            foreach (UIModernEventType type in Enum.GetValues(typeof(UIModernEventType)))
            {
                UIModernEventType subscribed = type;
                var message = new UIModernMessage(type);
                MessageCallback<UIModernEvent> callback = (in UIModernEvent value) => received.Add((subscribed, value));
                exchange.SubscribeToMessage(in message, callback);
                unsubscribe.Add(() => exchange.UnsubscribeFromMessage(in message, callback));
            }
            var onClick = new UIModernMessage(UIModernEventType.OnClick);
            UIModernEvent target = rig.SaveEvent;
            MessageCallback<UIModernEvent> boundCallback = (in UIModernEvent value) => bound.Add(value);
            exchange.SubscribeToMessage(in onClick, in target, boundCallback);
            unsubscribe.Add(() => exchange.UnsubscribeFromMessage(in onClick, in target, boundCallback));
            try
            {
                action();
            }
            finally
            {
                foreach (Action step in unsubscribe) step();
            }
        }
    }
}
