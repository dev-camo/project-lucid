using System.Collections;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UserEditableUIFlipper (02000996) against its 1.10.1 x86-64 bodies (0x698980..0x698C60). The flipper is a component
    // on an owned inactive host, so every StartCoroutine it makes is refused by the engine with its own error, which
    // each case expects: that error is the observable proof that the flip update was started. The iterator is stepped
    // directly. Not covered: the flip of an active component list, whose UpdateAfterParentFlipped needs a component
    // that is active in the hierarchy and so receives Unity's own Awake.
    public sealed class UserEditableUIFlipperTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private static UserEditableUIFlipperProbe NewFlipper(Services services)
        {
            var settings = new SaveDataSettings();
            Set(services.Save, "m_saveSettings", settings); // loaded by the save flow in the original
            var flipper = services.Owned.Host("Lucid owned flipper", false).AddComponent<UserEditableUIFlipperProbe>();
            flipper.RunOnSaveManagerValid(services.Save);
            return flipper;
        }

        // DoInitialise 0x6989B0, OnEditFlip 0x698AA0 and OnEditReset 0x698B50 each run the base body and then start the
        // delayed flip update.
        [Test]
        public void InitialiseFlipAndResetEachStartTheDelayedUpdate()
        {
            With(services =>
            {
                UserEditableUIFlipperProbe flipper = NewFlipper(services);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                flipper.RunDoInitialise();

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                flipper.RunEditFlip();
                Assert.That(flipper.Flipped, Is.True, "the base toggled the flip first");

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                flipper.RunEditReset();
                Assert.That(flipper.Flipped, Is.False, "the base cleared the flip first");
            });
        }

        // OnEditConfirm 0x698B20 stores the flip in the settings (set_OnScreenControlsFlipped) and then runs the base,
        // which publishes the settings object to the app storage; the second confirm stores the cleared flip.
        [Test]
        public void ConfirmStoresTheFlipInTheSettingsBeforeTheyArePublished()
        {
            With(services =>
            {
                UserEditableUIFlipperProbe flipper = NewFlipper(services);
                SaveDataSettings settings = services.Save.GetSaveDataSettings();
                Assert.That(settings.OnScreenControlsFlipped, Is.False);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                flipper.RunEditFlip();
                flipper.RunEditConfirm();
                Assert.That(settings.OnScreenControlsFlipped, Is.True);
                Assert.That(ProcessManager.GetSystem<App>().Storage.TryGetValue(AppFSMKeys.GameSettings, out SaveDataSettings stored), Is.True);
                Assert.That(stored, Is.SameAs(settings));

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                flipper.RunEditFlip();
                flipper.RunEditConfirm();
                Assert.That(settings.OnScreenControlsFlipped, Is.False);
            });
        }

        // MoveNext 0x698C80: state 0 yields a new WaitForEndOfFrame; state 1 sets the layout group's reverseArrangement
        // from the flip flag and finishes the list (components inactive in the hierarchy are skipped, and calling
        // UpdateAfterParentFlipped on this unprepared one would fault), then ends.
        [Test]
        public void DelayedUpdateReversesTheLayoutAtTheEndOfTheFrame()
        {
            With(services =>
            {
                UserEditableUIFlipperProbe flipper = NewFlipper(services);
                var layout = services.Owned.Host("Lucid owned flip layout", false).AddComponent<HorizontalLayoutGroup>();
                var inactive = services.Owned.Host("Lucid owned flipped component", false).AddComponent<UserEditableUIComponentController>();
                Set(flipper, "m_flipLayoutGroup", layout); // serialized in the original
                Set(flipper, "m_editableComponents", new[] { inactive });
                layout.reverseArrangement = true;

                var unflipped = (IEnumerator)Call(flipper, "UpdateFlipDelayed");
                Assert.That(layout.reverseArrangement, Is.True, "nothing runs before the first MoveNext");
                Assert.That(unflipped.MoveNext(), Is.True);
                Assert.That(unflipped.Current, Is.InstanceOf<WaitForEndOfFrame>());
                Assert.That(layout.reverseArrangement, Is.True, "still waiting");
                Assert.That(unflipped.MoveNext(), Is.False);
                Assert.That(layout.reverseArrangement, Is.False, "the flip flag is off");
                Assert.That(unflipped.MoveNext(), Is.False, "a finished iterator does nothing more");

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                flipper.RunEditFlip();
                var flipped = (IEnumerator)Call(flipper, "UpdateFlipDelayed");
                flipped.MoveNext();
                flipped.MoveNext();
                Assert.That(layout.reverseArrangement, Is.True, "the flip flag is on");
                Assert.Throws<System.NotSupportedException>(flipped.Reset);
            });
        }
    }
}
