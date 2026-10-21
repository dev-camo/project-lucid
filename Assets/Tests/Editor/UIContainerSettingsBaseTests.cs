using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using static ProjectLucid.Tests.SettingsPageTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsBase (Game.Runtime 0x0200092a, x86-64 0x679940..0x67A898, shared generic bodies 0x9DB980,
    // 0x9DBA90 and 0x9DBBC0) through SettingsContainerBaseProbe, which logs the hooks the base calls. The page is a real
    // component on an owned inactive host, talking to a real SaveManager that holds the live settings.
    public sealed class UIContainerSettingsBaseTests
    {
        // SetSaveData 0x679940: the callback gets the save manager's live settings (+0x90), then RequestSaveSettings
        // (0x5F0250) saves them, DoOnAfterSave (slot 9) runs, and RefreshBinders (slot 8, a tail call) runs last.
        [Test]
        public void SettingsAreEditedThenSavedThenTheHooksRun()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);
                SaveDataSettings seen = null;
                probe.Edit(settings =>
                {
                    seen = settings;
                    settings.GameSpeed = 0.5f;
                    probe.Log.Add(settings.HasChangesToSave() ? "callback:unsaved" : "callback:saved");
                });

                Assert.That(seen, Is.SameAs(page.Settings));
                Assert.That(probe.Log, Is.EqualTo(new[] { "callback:unsaved", "after-save:saved", "refresh" }));
                StringAssert.Contains("\"m_gameSpeed\":0.5", SavedJson(page));
            });
        }

        // The flag the shipped callers leave at its default (0x67999e tests it before RequestSaveSettings).
        [Test]
        public void NotSavingStillRefreshesTheBinders()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);
                probe.Edit(settings => settings.GameSpeed = 0.25f, false);

                Assert.That(probe.Log, Is.EqualTo(new[] { "refresh" }));
                Assert.That(page.Settings.HasChangesToSave(), Is.True, "the edit is not saved");
                Assert.That(SavedJson(page), Is.Null);
                Assert.That(page.Settings.GameSpeed, Is.EqualTo(0.25f));
            });
        }

        // Action_ResetToDefaults 0x67A760: ResetToDefaults (slot 7) takes the live settings, then the save path runs with
        // a callback that refreshes the binders (<Action_ResetToDefaults>b__3_0 0x67A880, a tail call of slot 8) before
        // the save, DoOnAfterSave and the final RefreshBinders.
        [Test]
        public void ResettingToDefaultsResetsThenRefreshesSavesAndRefreshesAgain()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);
                probe.OnReset = settings => settings.GameSpeed = 0.75f;

                probe.Action_ResetToDefaults();

                Assert.That(probe.ResetArguments.Count, Is.EqualTo(1));
                Assert.That(probe.ResetArguments[0], Is.SameAs(page.Settings));
                Assert.That(probe.Log, Is.EqualTo(new[] { "reset", "refresh", "after-save:saved", "refresh" }));
                StringAssert.Contains("\"m_gameSpeed\":0.75", SavedJson(page));
            });
        }

        // DoOnAfterSave 0x67A1D0 is a bare return.
        [Test]
        public void TheBaseAfterSaveHookDoesNothing()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);
                page.Settings.GameSpeed = 0.5f;

                probe.BaseDoOnAfterSave();

                Assert.That(probe.Log, Is.Empty);
                Assert.That(page.Settings.HasChangesToSave(), Is.True);
                Assert.That(SavedJson(page), Is.Null);
            });
        }

        // The bool body 0x9DB980 boxes both values, compares them with Equals and reports their ToString texts only
        // when they differ (SettingChangedEvent 0x50BC80).
        [Test]
        public void ABooleanChangeIsReportedAndAnUnchangedOneIsNot()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);

                probe.Report("bool_setting", true, true);
                Assert.That(page.Reported.Sent, Is.False);

                probe.Report("bool_setting", false, true);
                Assert.That(page.Reported.Last, Is.EqualTo(("bool_setting", "False", "True")));

                page.Reported.Clear();
                probe.Report("bool_setting", true, false);
                Assert.That(page.Reported.Last, Is.EqualTo(("bool_setting", "True", "False")));
            });
        }

        // The Int32Enum body 0x9DBA90 reports the enum names (System.Enum.ToString).
        [Test]
        public void AnEnumChangeIsReportedByName()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);

                probe.Report("enum_setting", AccessibilityFailState.Invincible, AccessibilityFailState.Invincible);
                Assert.That(page.Reported.Sent, Is.False);

                probe.Report("enum_setting", AccessibilityFailState.Default, AccessibilityFailState.NoFail);
                Assert.That(page.Reported.Last, Is.EqualTo(("enum_setting", "Default", "NoFail")));
            });
        }

        // The fully shared body 0x9DBBC0 serves every other argument type, with the constrained Equals and ToString
        // calls of the same shape (an int and a float here).
        [Test]
        public void OtherValueTypesAreComparedAndReportedTheSameWay()
        {
            With(page =>
            {
                var probe = Add<SettingsContainerBaseProbe>(page);

                probe.Report("int_setting", 3, 3);
                probe.Report("float_setting", 1.5f, 1.5f);
                Assert.That(page.Reported.Sent, Is.False);

                probe.Report("int_setting", 3, 4);
                Assert.That(page.Reported.Last, Is.EqualTo(("int_setting", "3", "4")));

                probe.Report("float_setting", 1.5f, 2.25f);
                Assert.That(page.Reported.Last, Is.EqualTo(("float_setting", "1.5", "2.25")));
            });
        }
    }
}
