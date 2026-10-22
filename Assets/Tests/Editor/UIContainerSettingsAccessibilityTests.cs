using System.Collections.Generic;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.SettingsPageTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsAccessibility (Game.Runtime 0x02000923, x86-64 0x6796A0..0x67A5F0) as a real component on an owned
    // inactive host (no Awake runs, so each case calls the original methods itself), talking to a real SaveManager
    // holding the live settings and a real TimeManager (SettingsPageTestSupport). The setting-changed analytics event is
    // read from the shared stub of the offline provider.
    public sealed class UIContainerSettingsAccessibilityTests
    {
        private static readonly Dictionary<Strings, string> PercentFormat =
            new Dictionary<Strings, string> { { Strings.SETTINGS_SLIDER_LABEL_PERCENT, "{0}%" } };

        // Awake 0x6796A0: RefreshBinders (slot 8) first, then the game speed binder's value is cached, so the cache
        // holds the saved speed rather than the empty binder.
        [Test]
        public void AwakePublishesTheSavedSettingsThenCachesTheGameSpeed()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                page.Settings.GameSpeed = 0.5f;
                var container = Add<UIContainerSettingsAccessibility>(page);

                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(container.GameSpeed.Value, Is.EqualTo(0.5f));
                Assert.That(Get<float>(container, "m_cachedGameSpeed"), Is.EqualTo(0.5f));
            }));
        }

        // RefreshBinders 0x6799E0: the time manager's limits (+0x28, +0x2C) and the saved settings (+0x5C game speed,
        // +0x60 fail state, +0x64 toggle, +0x65 subtitles, +0x88 skip) fill the binders. The percentage string formats
        // the localised label with (int)(speed * 100) where mulss and cvttss2si round to single precision (0x679ae3):
        // 0.57f * 100f is 57.0f, so it reads 57 where a double product would truncate to 56.
        [Test]
        public void RefreshBindersPublishesTheSettingsAndTheTimeLimits()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                Set(page.Time, "m_gameSpeedMin", 0.35f); // serialized in the original
                Set(page.Time, "m_gameSpeedMax", 0.9f); // serialized in the original
                page.Settings.GameSpeed = 0.57f;
                page.Settings.FailState = AccessibilityFailState.NoFail;
                page.Settings.GameSpeedToggleEnabled = true;
                page.Settings.SubtitlesEnabled = false;
                page.Settings.SkipFTUE = true;
                var container = Add<UIContainerSettingsAccessibility>(page);

                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsAccessibility, not reachable from the test

                Assert.That(container.GameSpeed.Value, Is.EqualTo(0.57f));
                Assert.That(container.GameSpeedMin.Value, Is.EqualTo(0.35f));
                Assert.That(container.GameSpeedMax.Value, Is.EqualTo(0.9f));
                Assert.That(container.GameSpeedPercentageString.Value, Is.EqualTo("57%"));
                Assert.That(container.FailStateType.Value, Is.EqualTo(AccessibilityFailState.NoFail));
                Assert.That(container.GameSpeedToggle.Value, Is.True);
                Assert.That(container.SubtitlesToggle.Value, Is.False);
                Assert.That(container.SkipFTUEToggle.Value, Is.True);

                page.Settings.GameSpeed = 0.29f;
                page.Settings.GameSpeedToggleEnabled = false;
                page.Settings.SubtitlesEnabled = true;
                page.Settings.SkipFTUE = false;
                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsAccessibility, not reachable from the test

                Assert.That(container.GameSpeedPercentageString.Value, Is.EqualTo("29%"), "0.29f * 100f is 29.0f");
                Assert.That(container.GameSpeedToggle.Value, Is.False);
                Assert.That(container.SubtitlesToggle.Value, Is.True);
                Assert.That(container.SkipFTUEToggle.Value, Is.False);
            }));
        }

        // The percentage truncates (cvttss2si 0x679aeb) rather than rounds: 0.556f * 100f is 55.6 in single precision,
        // which reads 55 where rounding would give 56.
        [Test]
        public void ThePercentageTruncatesTheSpeedProduct()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                page.Settings.GameSpeed = 0.556f;
                var container = Add<UIContainerSettingsAccessibility>(page);

                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsAccessibility, not reachable from the test

                Assert.That(container.GameSpeedPercentageString.Value, Is.EqualTo("55%"));
            }));
        }

        // Action_SetGameSpeed 0x679BE0: the speed is stored without a save (the save path is entered with
        // saveImmediately false), the binders refresh and the page remembers to save on exit.
        [Test]
        public void SettingTheGameSpeedStoresItAndLeavesTheSaveForExit()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);

                container.Action_SetGameSpeed(0.4f);

                Assert.That(page.Settings.GameSpeed, Is.EqualTo(0.4f));
                Assert.That(page.Settings.HasChangesToSave(), Is.True);
                Assert.That(SavedJson(page), Is.Null);
                Assert.That(container.GameSpeed.Value, Is.EqualTo(0.4f), "the binders are refreshed");
                Assert.That(Get<bool>(container, "m_saveOnExit"), Is.True);
                Assert.That(page.Reported.Sent, Is.False);
            }));
        }

        // OnDestroy 0x6796E0: a pending speed is saved through the save path (DoOnAfterSave 0x67A1C0 clears the flag),
        // then a speed that differs from the one cached by Awake is reported with "0.0" formatting.
        [Test]
        public void ClosingThePageSavesAPendingGameSpeedAndReportsTheChange()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                container.Action_SetGameSpeed(0.4f);

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                StringAssert.Contains("\"m_gameSpeed\":0.4", SavedJson(page));
                Assert.That(page.Settings.HasChangesToSave(), Is.False);
                Assert.That(Get<bool>(container, "m_saveOnExit"), Is.False);
                Assert.That(page.Reported.Last, Is.EqualTo(("game_speed", "1.0", "0.4")));
            }));
        }

        // The save is conditional on the pending flag (0x67974a) and the report on a changed speed (0x679821).
        [Test]
        public void ClosingThePageWithoutChangesSavesAndReportsNothing()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                page.Settings.GameSpeed = 1f; // dirties the settings without changing the speed

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(SavedJson(page), Is.Null);
                Assert.That(page.Settings.HasChangesToSave(), Is.True);
                Assert.That(page.Reported.Sent, Is.False);
            }));
        }

        // A pending save alone does not report: the speed set back to the cached one is saved but not reported.
        [Test]
        public void ClosingThePageSavesButDoesNotReportAnUnchangedSpeed()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                container.Action_SetGameSpeed(1f);

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(page.Reported.Sent, Is.False);
            }));
        }

        // ResetToDefaults (slot 7) 0x679BB0: the accessibility defaults (SetAccessibilityDefaults with its dirty flag set)
        // and the pending-save flag cleared; the skip flag is not one of them.
        [Test]
        public void ResettingTheAccessibilitySettingsRestoresTheirDefaultsAndClearsThePendingSave()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                page.Settings.FailState = AccessibilityFailState.Invincible;
                page.Settings.GameSpeedToggleEnabled = true;
                page.Settings.SubtitlesEnabled = false;
                page.Settings.SkipFTUE = true;
                var container = Add<UIContainerSettingsAccessibility>(page);
                container.Action_SetGameSpeed(0.4f);
                page.Settings.MarkSaved();

                Call(container, "ResetToDefaults", page.Settings); // Reflection: protected ResetToDefaults of UIContainerSettingsAccessibility, not reachable from the test

                Assert.That(page.Settings.GameSpeed, Is.EqualTo(1f));
                Assert.That(page.Settings.FailState, Is.EqualTo(AccessibilityFailState.Default));
                Assert.That(page.Settings.GameSpeedToggleEnabled, Is.False);
                Assert.That(page.Settings.SubtitlesEnabled, Is.True);
                Assert.That(page.Settings.SkipFTUE, Is.True);
                Assert.That(page.Settings.HasChangesToSave(), Is.True, "marked dirty");
                Assert.That(Get<bool>(container, "m_saveOnExit"), Is.False);
            }));
        }

        // The public reset (Action_ResetToDefaults 0x67A760) resets, saves and refreshes the binders.
        [Test]
        public void TheResetButtonSavesTheDefaultsAndRefreshesThePage()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                page.Settings.GameSpeed = 0.3f;
                page.Settings.SubtitlesEnabled = false;
                var container = Add<UIContainerSettingsAccessibility>(page);

                container.Action_ResetToDefaults();

                Assert.That(page.Settings.GameSpeed, Is.EqualTo(1f));
                Assert.That(page.Settings.SubtitlesEnabled, Is.True);
                StringAssert.Contains("\"m_gameSpeed\":1", SavedJson(page));
                Assert.That(container.GameSpeed.Value, Is.EqualTo(1f));
                Assert.That(container.SubtitlesToggle.Value, Is.True);
                Assert.That(container.GameSpeedPercentageString.Value, Is.EqualTo("100%"));
            }));
        }

        // Action_SetSubtitlesEnabled 0x679CF0: saved at once; DoOnAfterSave clears a pending game speed save, as the
        // flag is shared.
        [Test]
        public void SettingSubtitlesSavesAtOnceAndClearsThePendingSpeedSave()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);
                container.Action_SetGameSpeed(0.4f);

                container.Action_SetSubtitlesEnabled(false);

                Assert.That(page.Settings.SubtitlesEnabled, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(container.SubtitlesToggle.Value, Is.False);
                Assert.That(Get<bool>(container, "m_saveOnExit"), Is.False);
                Assert.That(page.Reported.Sent, Is.False);
            }));
        }

        // SetFailStateType 0x679E10 through the three buttons (0x679F40 Default 0x23E4013A, 0x679F50 NoFail 0x303AE339,
        // 0x679F60 Invincible 0xCF291B17): the change is reported by name, the state saved and the binder refreshed.
        [Test]
        public void TheFailStateButtonsSaveTheirStateAndReportTheChange()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);

                container.Action_SetFailStateNoFail();
                Assert.That(page.Settings.FailState, Is.EqualTo(AccessibilityFailState.NoFail));
                Assert.That(page.Reported.Last, Is.EqualTo(("fail_state", "Default", "NoFail")));
                Assert.That(container.FailStateType.Value, Is.EqualTo(AccessibilityFailState.NoFail));
                Assert.That(SavedJson(page), Is.Not.Null);

                container.Action_SetFailStateInvincible();
                Assert.That(page.Settings.FailState, Is.EqualTo(AccessibilityFailState.Invincible));
                Assert.That(page.Reported.Last, Is.EqualTo(("fail_state", "NoFail", "Invincible")));
                Assert.That(container.FailStateType.Value, Is.EqualTo(AccessibilityFailState.Invincible));

                container.Action_SetFailStateDefault();
                Assert.That(page.Settings.FailState, Is.EqualTo(AccessibilityFailState.Default));
                Assert.That(page.Reported.Last, Is.EqualTo(("fail_state", "Invincible", "Default")));
                Assert.That(container.FailStateType.Value, Is.EqualTo(AccessibilityFailState.Default));
            }));
        }

        [Test]
        public void ChoosingTheCurrentFailStateAgainSavesButReportsNothing()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);

                container.Action_SetFailStateDefault();

                Assert.That(page.Reported.Sent, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
            }));
        }

        // The toggle lambda 0x67A670: the change is reported, stored and saved; switching the game speed off tells the
        // time manager to apply the speed again (+0xB0 GameSpeedApplied = true), switching it on leaves it alone.
        [Test]
        public void TheGameSpeedToggleIsReportedSavedAndReappliedWhenSwitchedOff()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);
                page.Time.GameSpeedApplied = false;

                container.Action_SetGameSpeedToggleEnabled(true);
                Assert.That(page.Settings.GameSpeedToggleEnabled, Is.True);
                Assert.That(page.Reported.Last, Is.EqualTo(("game_speed_toggle_enabled", "False", "True")));
                Assert.That(container.GameSpeedToggle.Value, Is.True);
                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(page.Time.GameSpeedApplied, Is.False, "switching on leaves the time manager alone");

                container.Action_SetGameSpeedToggleEnabled(false);
                Assert.That(page.Settings.GameSpeedToggleEnabled, Is.False);
                Assert.That(page.Reported.Last, Is.EqualTo(("game_speed_toggle_enabled", "True", "False")));
                Assert.That(page.Time.GameSpeedApplied, Is.True);
            }));
        }

        // Action_SetSkipFTUE 0x67A0A0 stores and saves the flag; nothing is reported.
        [Test]
        public void SkippingTheIntroductionIsSavedWithoutAReport()
        {
            WithTime(page => OwnedStringTable.Run(PercentFormat, () =>
            {
                var container = Add<UIContainerSettingsAccessibility>(page);

                container.Action_SetSkipFTUE(true);

                Assert.That(page.Settings.SkipFTUE, Is.True);
                Assert.That(container.SkipFTUEToggle.Value, Is.True);
                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(page.Reported.Sent, Is.False);
            }));
        }
    }
}
