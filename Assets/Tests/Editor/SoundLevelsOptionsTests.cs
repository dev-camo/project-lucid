using System;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // SoundLevelsOptionsValues (Game.Runtime 0x02000946; ctor 0x67FE90, ApplyToSettings 0x67F9B0) and
    // UIContainerSoundLevelsOptions (0x02000944; Setup 0x67FD10, GetValues 0x67F920, the actions 0x67FEB0..0x67FFC0) as a
    // real component on an owned inactive host. Every level is a different number so a swapped pair shows.
    public sealed class SoundLevelsOptionsTests
    {
        private static UIContainerSoundLevelsOptionsParameters Parameters(Action onSave = null, Action<float> onMaster = null,
            Action<float> onMusic = null, Action<float> onSfx = null, Action<float> onVoice = null) =>
            new UIContainerSoundLevelsOptionsParameters(0.1f, 0.2f, 0.3f, 0.4f, onSave, onMaster, onMusic, onSfx, onVoice);

        // 0x67FE90 stores the four arguments at offsets 0, 4, 8 and 0xC, which the four getters read back.
        [Test]
        public void TheValuesHoldTheFourLevelsInTheirOwnOrder()
        {
            var values = new SoundLevelsOptionsValues(0.1f, 0.2f, 0.3f, 0.4f);

            Assert.That(values.MasterVolume, Is.EqualTo(0.1f));
            Assert.That(values.MusicVolume, Is.EqualTo(0.2f));
            Assert.That(values.SfxVolume, Is.EqualTo(0.3f));
            Assert.That(values.VoiceVolume, Is.EqualTo(0.4f));
        }

        // ApplyToSettings 0x67F9B0 calls set_MasterVolume, set_MusicVolume, set_SfxVolume and set_VoiceVolume in that order,
        // each with its own level; the setters mark the settings changed.
        [Test]
        public void ApplyingTheValuesWritesEachLevelToItsOwnSettingAndMarksThemChanged()
        {
            var settings = new SaveDataSettings();
            settings.MarkSaved();
            Assert.That(settings.HasChangesToSave(), Is.False);

            new SoundLevelsOptionsValues(0.1f, 0.2f, 0.3f, 0.4f).ApplyToSettings(settings);

            Assert.That(settings.MasterVolume, Is.EqualTo(0.1f));
            Assert.That(settings.MusicVolume, Is.EqualTo(0.2f));
            Assert.That(settings.SfxVolume, Is.EqualTo(0.3f));
            Assert.That(settings.VoiceVolume, Is.EqualTo(0.4f));
            Assert.That(settings.HasChangesToSave(), Is.True);
        }

        // 0x67F9B7: the null test branches to il2cpp_codegen_raise_null_reference_exception.
        [Test]
        public void ApplyingTheValuesToNoSettingsFaults()
        {
            Assert.Throws<NullReferenceException>(() => new SoundLevelsOptionsValues(0.1f, 0.2f, 0.3f, 0.4f).ApplyToSettings(null));
        }

        // Setup 0x67FD10 sets the four bindables from the parameters; GetValues 0x67F920 reads them back in the same order.
        [Test]
        public void SetupPublishesTheLevelsAndGetValuesReadsThemBack()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid sound levels options", false).AddComponent<UIContainerSoundLevelsOptions>();

                container.Setup(Parameters());

                Assert.That(container.MasterVolume.Value, Is.EqualTo(0.1f));
                Assert.That(container.MusicVolume.Value, Is.EqualTo(0.2f));
                Assert.That(container.SfxVolume.Value, Is.EqualTo(0.3f));
                Assert.That(container.VoiceVolume.Value, Is.EqualTo(0.4f));

                container.MusicVolume.Value = 0.6f;
                SoundLevelsOptionsValues values = container.GetValues();
                Assert.That((values.MasterVolume, values.MusicVolume, values.SfxVolume, values.VoiceVolume), Is.EqualTo((0.1f, 0.6f, 0.3f, 0.4f)));
            });
        }

        // Setup stores the save, music and sfx callbacks (0x67FD9D, 0x67FDAE, 0x67FDBD) before it sets the bindables and the
        // master and voice callbacks (0x67FE34, 0x67FE67) after them. A listener of the master bindable that presses the
        // sfx, music and voice toggles therefore reaches the new sfx and music callbacks but not a voice callback.
        [Test]
        public void SetupStoresTheMusicAndSfxCallbacksBeforeItSetsTheBindablesAndTheOthersAfter()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid sound levels options", false).AddComponent<UIContainerSoundLevelsOptions>();
                var reached = new List<string>();
                container.MasterVolume.AddListener(() =>
                {
                    container.Action_OnSfxVolumeToggle(0.7f);
                    container.Action_OnMusicVolumeToggle(0.8f);
                    container.Action_OnVoiceVolumeToggle(0.9f);
                });

                container.Setup(Parameters(null, null, value => reached.Add("music"), value => reached.Add("sfx"), value => reached.Add("voice")));

                Assert.That(reached, Is.EqualTo(new[] { "sfx", "music" }));
            });
        }

        // The toggle actions 0x67FED0, 0x67FF20, 0x67FF70 and 0x67FFC0 set their own bindable first and then call their own
        // callback with the value, so the callback already sees the new bindable value.
        [Test]
        public void EachToggleSetsItsBindableBeforeCallingItsOwnCallback()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid sound levels options", false).AddComponent<UIContainerSoundLevelsOptions>();
                var calls = new List<string>();
                container.Setup(Parameters(null,
                    value => calls.Add("master " + value + " bound " + container.MasterVolume.Value),
                    value => calls.Add("music " + value + " bound " + container.MusicVolume.Value),
                    value => calls.Add("sfx " + value + " bound " + container.SfxVolume.Value),
                    value => calls.Add("voice " + value + " bound " + container.VoiceVolume.Value)));

                container.Action_OnMasterVolumeToggle(0.5f);
                container.Action_OnMusicVolumeToggle(0.6f);
                container.Action_OnSfxVolumeToggle(0.7f);
                container.Action_OnVoiceVolumeToggle(0.8f);

                Assert.That(calls, Is.EqualTo(new[]
                {
                    "master " + 0.5f + " bound " + 0.5f,
                    "music " + 0.6f + " bound " + 0.6f,
                    "sfx " + 0.7f + " bound " + 0.7f,
                    "voice " + 0.8f + " bound " + 0.8f,
                }));
            });
        }

        // The toggles test their callback for null (0x67FEF6, 0x67FF46, 0x67FF96, 0x67FFE6) and still keep the value.
        [Test]
        public void ATogglePressedWithoutACallbackStillSetsItsBindable()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid sound levels options", false).AddComponent<UIContainerSoundLevelsOptions>();
                container.Setup(Parameters());

                container.Action_OnMasterVolumeToggle(0.5f);
                container.Action_OnMusicVolumeToggle(0.6f);
                container.Action_OnSfxVolumeToggle(0.7f);
                container.Action_OnVoiceVolumeToggle(0.8f);

                SoundLevelsOptionsValues values = container.GetValues();
                Assert.That((values.MasterVolume, values.MusicVolume, values.SfxVolume, values.VoiceVolume), Is.EqualTo((0.5f, 0.6f, 0.7f, 0.8f)));
            });
        }

        // Action_OnSaveClick 0x67FEB0 jumps straight to the delegate with no null test.
        [Test]
        public void TheSaveClickCallsItsCallbackAndFaultsWithoutOne()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid sound levels options", false).AddComponent<UIContainerSoundLevelsOptions>();
                int saves = 0;
                container.Setup(Parameters(() => saves++));

                container.Action_OnSaveClick();
                Assert.That(saves, Is.EqualTo(1));

                container.Setup(Parameters());
                Assert.Throws<NullReferenceException>(container.Action_OnSaveClick);
                Assert.That(saves, Is.EqualTo(1));
            });
        }
    }
}
