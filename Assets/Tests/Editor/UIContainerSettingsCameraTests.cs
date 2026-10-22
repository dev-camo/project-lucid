using System;
using System.Collections.Generic;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.SettingsPageTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsCamera (Game.Runtime 0x0200092b, x86-64 0x67A8A0..0x67BCF8) as a real component on an owned
    // inactive host, talking to a real SaveManager holding the live settings (SettingsPageTestSupport). The serialized
    // lists and dictionary are filled as the authored page holds them: three configurations (near, default, far) and
    // three sensitivities (low, medium, high), each with its own string.
    public sealed class UIContainerSettingsCameraTests
    {
        private static readonly CameraConfigurationType[] Configurations =
            { CameraConfigurationType.Near, CameraConfigurationType.Default, CameraConfigurationType.Far };

        private static readonly (CameraSensitivityType key, Strings value)[] Sensitivities =
        {
            (CameraSensitivityType.Low, Strings.MISSING),
            (CameraSensitivityType.Medium, Strings.DECIMAL_SEPARATOR),
            (CameraSensitivityType.High, Strings.NUMBER_SEPARATOR_SLASH),
        };

        private static UIContainerSettingsCamera NewCamera(Page page)
        {
            var container = Add<UIContainerSettingsCamera>(page);
            Set(container, "m_cameraConfiguration", new List<CameraConfigurationType>(Configurations)); // serialized in the original
            var sensitivity = new List<EnumValuePair<CameraSensitivityType, Strings>>();
            foreach ((CameraSensitivityType key, Strings value) in Sensitivities)
            {
                var pair = new EnumValuePair<CameraSensitivityType, Strings>();
                Set(pair, "m_key", key); // serialized in the original
                Set(pair, "m_value", value); // serialized in the original
                sensitivity.Add(pair);
            }

            Set(container, "m_cameraSensitivity", sensitivity); // serialized in the original
            var distances = Get<Hardlight.SerializableDictionary<CameraConfigurationType, Strings>>(container, "m_cameraDistanceStrings");
            distances[CameraConfigurationType.Near] = Strings.DECIMAL_SEPARATOR;
            distances[CameraConfigurationType.Far] = Strings.NUMBER_SEPARATOR;
            return container;
        }

        // Awake 0x67A8A0: RefreshBinders first, then both index binders are cached.
        [Test]
        public void AwakePublishesTheSettingsThenCachesTheSelectedIndices()
        {
            With(page =>
            {
                page.Settings.CameraConfiguration = CameraConfigurationType.Far;
                page.Settings.CameraSensitivity = CameraSensitivityType.High;
                var container = NewCamera(page);

                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(Get<int>(container, "m_cachedCameraDistance"), Is.EqualTo(2));
                Assert.That(Get<int>(container, "m_cachedCameraSensitivity"), Is.EqualTo(2));
            });
        }

        // RefreshBinders 0x67AB10: heading (+0x24), inverted (+0x28) and snap flag (+0x34) are copied; the configuration
        // and sensitivity binders get the list index whose entry equals the saved one (+0x2C, +0x30); the sensitivity
        // string is that entry's string and the distance string is the dictionary's string for the saved configuration.
        [Test]
        public void RefreshBindersPublishesTheSavedCameraSettings()
        {
            With(page =>
            {
                page.Settings.CameraRecenterHeadingType = CameraRecenterHeadingType.None;
                page.Settings.CameraInvertedControls = true;
                page.Settings.CameraSnapBehindCharacterButtonVisible = true;
                page.Settings.CameraConfiguration = CameraConfigurationType.Far;
                page.Settings.CameraSensitivity = CameraSensitivityType.High;
                var container = NewCamera(page);

                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsCamera, not reachable from the test

                Assert.That(container.CameraRecenterHeading.Value, Is.EqualTo(CameraRecenterHeadingType.None));
                Assert.That(container.CameraInvertedControls.Value, Is.True);
                Assert.That(container.CameraSnapBehindCharacterButtonVisible.Value, Is.True);
                Assert.That(container.CameraConfigurationIndex.Value, Is.EqualTo(2));
                Assert.That(container.CameraSensitivityIndex.Value, Is.EqualTo(2));
                Assert.That(container.CameraSensitivityString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR_SLASH));
                Assert.That(container.CameraDistanceString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR));

                page.Settings.CameraRecenterHeadingType = CameraRecenterHeadingType.Medium;
                page.Settings.CameraInvertedControls = false;
                page.Settings.CameraSnapBehindCharacterButtonVisible = false;
                page.Settings.CameraConfiguration = CameraConfigurationType.Near;
                page.Settings.CameraSensitivity = CameraSensitivityType.Low;
                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsCamera, not reachable from the test

                Assert.That(container.CameraRecenterHeading.Value, Is.EqualTo(CameraRecenterHeadingType.Medium));
                Assert.That(container.CameraInvertedControls.Value, Is.False);
                Assert.That(container.CameraSnapBehindCharacterButtonVisible.Value, Is.False);
                Assert.That(container.CameraConfigurationIndex.Value, Is.EqualTo(0));
                Assert.That(container.CameraSensitivityIndex.Value, Is.EqualTo(0));
                Assert.That(container.CameraSensitivityString.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(container.CameraDistanceString.Value, Is.EqualTo(Strings.DECIMAL_SEPARATOR));
            });
        }

        // A configuration without a dictionary entry leaves the distance string alone (TryGetValue false at 0x67ad79),
        // and one missing from the list publishes FindIndex's -1.
        [Test]
        public void ASavedConfigurationWithoutAnEntryKeepsTheDistanceString()
        {
            With(page =>
            {
                page.Settings.CameraConfiguration = CameraConfigurationType.Default;
                var container = NewCamera(page);
                container.CameraDistanceString.Value = Strings.NUMBER_SEPARATOR_SLASH;
                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsCamera, not reachable from the test
                Assert.That(container.CameraConfigurationIndex.Value, Is.EqualTo(1));
                Assert.That(container.CameraDistanceString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR_SLASH));

                page.Settings.CameraConfiguration = CameraConfigurationType.None;
                Call(container, "RefreshBinders"); // Reflection: protected RefreshBinders of UIContainerSettingsCamera, not reachable from the test
                Assert.That(container.CameraConfigurationIndex.Value, Is.EqualTo(-1));
            });
        }

        // The sensitivity string is read at the found index with no guard, so a sensitivity missing from the list
        // (index -1) faults in List.get_Item.
        [Test]
        public void ASavedSensitivityMissingFromTheListFaults()
        {
            With(page =>
            {
                page.Settings.CameraSensitivity = CameraSensitivityType.VeryHigh;
                var container = NewCamera(page);

                Assert.Throws<ArgumentOutOfRangeException>(() => Call(container, "RefreshBinders")); // Reflection: protected RefreshBinders of UIContainerSettingsCamera, not reachable from the test
                Assert.That(container.CameraSensitivityIndex.Value, Is.EqualTo(-1));
            });
        }

        // OnDestroy 0x67A900: a configuration that differs from the one cached by Awake is reported by its name
        // (HardlightEnumExtensions.GetString 0x19EAAA0) as "camera_distance", old then new.
        [Test]
        public void ClosingThePageReportsAChangedCameraDistance()
        {
            With(page =>
            {
                var container = NewCamera(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                container.CameraConfigurationIndex.Value = 2;

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(page.Reported.Last, Is.EqualTo(("camera_distance", "Default", "Far")));
            });
        }

        // The sensitivity report names the strings: the cached entry's string, then the bound string
        // (Hardlight.Enums.EnumExtentions.GetString 0x19E6FF0).
        [Test]
        public void ClosingThePageReportsAChangedCameraSensitivity()
        {
            With(page =>
            {
                var container = NewCamera(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                container.CameraSensitivityIndex.Value = 2;
                container.CameraSensitivityString.Value = Strings.NUMBER_SEPARATOR_SLASH;

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(page.Reported.Last, Is.EqualTo(("camera_sensitivity", "DECIMAL_SEPARATOR", "NUMBER_SEPARATOR_SLASH")));
            });
        }

        // The new value is the bound string (the binder read at 0x67aab7..0x67aacf), not the list entry at the bound
        // index: index 2 holds NUMBER_SEPARATOR_SLASH but the binder says MISSING.
        [Test]
        public void ClosingThePageReportsTheBoundSensitivityStringNotTheListEntry()
        {
            With(page =>
            {
                var container = NewCamera(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                container.CameraSensitivityIndex.Value = 2;
                container.CameraSensitivityString.Value = Strings.MISSING;

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(page.Reported.Last, Is.EqualTo(("camera_sensitivity", "DECIMAL_SEPARATOR", "MISSING")));
            });
        }

        // The distance is reported first (0x67aa44) and the sensitivity last (the tail call at 0x67ab09), so the last
        // event the stub holds is the sensitivity one.
        [Test]
        public void ClosingThePageWithBothChangedReportsTheSensitivityLast()
        {
            With(page =>
            {
                var container = NewCamera(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                container.CameraConfigurationIndex.Value = 2;
                container.CameraSensitivityIndex.Value = 0;
                container.CameraSensitivityString.Value = Strings.MISSING;

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(page.Reported.Last, Is.EqualTo(("camera_sensitivity", "DECIMAL_SEPARATOR", "MISSING")));
            });
        }

        [Test]
        public void ClosingThePageWithoutChangesReportsNothing()
        {
            With(page =>
            {
                var container = NewCamera(page);
                Call(container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Call(container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(page.Reported.Sent, Is.False);
            });
        }

        // SetCameraHeadingType 0x67ADE0 through the two buttons (0x67AF00 None, 0x67AF10 Medium 0xC9747DCE): the page
        // calls Medium "Auto" and anything else "Manual", reports old then new, stores, saves and refreshes.
        [Test]
        public void TheHeadingButtonsReportAutoAndManualAndSave()
        {
            With(page =>
            {
                var container = NewCamera(page);

                container.Action_SetCameraHeadingTypeNone();
                Assert.That(page.Settings.CameraRecenterHeadingType, Is.EqualTo(CameraRecenterHeadingType.None));
                Assert.That(page.Reported.Last, Is.EqualTo(("camera_recenter_heading_type", "Auto", "Manual")));
                Assert.That(container.CameraRecenterHeading.Value, Is.EqualTo(CameraRecenterHeadingType.None));
                Assert.That(SavedJson(page), Is.Not.Null);

                container.Action_SetCameraHeadingTypeMedium();
                Assert.That(page.Settings.CameraRecenterHeadingType, Is.EqualTo(CameraRecenterHeadingType.Medium));
                Assert.That(page.Reported.Last, Is.EqualTo(("camera_recenter_heading_type", "Manual", "Auto")));
                Assert.That(container.CameraRecenterHeading.Value, Is.EqualTo(CameraRecenterHeadingType.Medium));
            });
        }

        // An unchanged heading returns from the lambda (0x67bb03) before reporting or storing, and the save path still
        // saves and refreshes.
        [Test]
        public void ChoosingTheCurrentHeadingAgainSavesButReportsNothing()
        {
            With(page =>
            {
                var container = NewCamera(page);

                container.Action_SetCameraHeadingTypeMedium();

                Assert.That(page.Reported.Sent, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(container.CameraRecenterHeading.Value, Is.EqualTo(CameraRecenterHeadingType.Medium));
            });
        }

        // The inverted-controls lambdas (0x67B9C0, 0x67B950) report the old value and the constant, then store it. The
        // generic compares first, so an unchanged value is not reported.
        [Test]
        public void TheInvertedControlsButtonsReportAndSave()
        {
            With(page =>
            {
                var container = NewCamera(page);

                container.Action_SetCameraControlsInverted();
                Assert.That(page.Settings.CameraInvertedControls, Is.True);
                Assert.That(page.Reported.Last, Is.EqualTo(("camera_inverted_controls", "False", "True")));
                Assert.That(container.CameraInvertedControls.Value, Is.True);
                Assert.That(SavedJson(page), Is.Not.Null);

                page.Reported.Clear();
                container.Action_SetCameraControlsInverted();
                Assert.That(page.Reported.Sent, Is.False, "unchanged");

                container.Action_SetCameraControlsNormal();
                Assert.That(page.Settings.CameraInvertedControls, Is.False);
                Assert.That(page.Reported.Last, Is.EqualTo(("camera_inverted_controls", "True", "False")));
                Assert.That(container.CameraInvertedControls.Value, Is.False);
            });
        }

        // Action_SetCameraSnapBehindCharacterButtonVisible 0x67B0E0 and its lambda 0x67BB90.
        [Test]
        public void TheSnapBehindButtonVisibilityIsReportedAndSaved()
        {
            With(page =>
            {
                var container = NewCamera(page);

                container.Action_SetCameraSnapBehindCharacterButtonVisible(true);

                Assert.That(page.Settings.CameraSnapBehindCharacterButtonVisible, Is.True);
                Assert.That(page.Reported.Last, Is.EqualTo(("camera_snap_behind_character_button_visible", "False", "True")));
                Assert.That(container.CameraSnapBehindCharacterButtonVisible.Value, Is.True);
                Assert.That(SavedJson(page), Is.Not.Null);
            });
        }

        // Action_SetCameraConfiguration 0x67B210 rounds the slider value with Mathf.RoundToInt (modf, then the half-to-even
        // branch at 0x67b2d8/0x67b2f6) and takes that list entry. 0.5 rounds to 0, 1.5 to 2 and 2.5 to 2; away-from-zero
        // rounding would give 1, 2 and 3. The action itself reports nothing.
        [Test]
        public void TheConfigurationSliderRoundsHalvesToEven()
        {
            With(page =>
            {
                var container = NewCamera(page);

                foreach ((float value, CameraConfigurationType expected) in new[]
                {
                    (0.5f, CameraConfigurationType.Near), (1.5f, CameraConfigurationType.Far), (2.5f, CameraConfigurationType.Far),
                    (0.4f, CameraConfigurationType.Near), (1.6f, CameraConfigurationType.Far), (-0.4f, CameraConfigurationType.Near),
                    (-0.5f, CameraConfigurationType.Near), (1f, CameraConfigurationType.Default),
                })
                {
                    container.Action_SetCameraConfiguration(value);
                    Assert.That(page.Settings.CameraConfiguration, Is.EqualTo(expected), "slider value " + value);
                }

                Assert.That(page.Reported.Sent, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
            });
        }

        [Test]
        public void TheConfigurationSliderRefreshesThePageAfterStoring()
        {
            With(page =>
            {
                var container = NewCamera(page);

                container.Action_SetCameraConfiguration(2f);

                Assert.That(container.CameraConfigurationIndex.Value, Is.EqualTo(2));
                Assert.That(container.CameraDistanceString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR));
            });
        }

        // The sensitivity slider (0x67B440) takes the entry's key the same way.
        [Test]
        public void TheSensitivitySliderRoundsHalvesToEven()
        {
            With(page =>
            {
                var container = NewCamera(page);

                foreach ((float value, CameraSensitivityType expected) in new[]
                {
                    (0.5f, CameraSensitivityType.Low), (1.5f, CameraSensitivityType.High), (2.5f, CameraSensitivityType.High),
                    (0.6f, CameraSensitivityType.Medium), (1f, CameraSensitivityType.Medium),
                })
                {
                    container.Action_SetCameraSensitivity(value);
                    Assert.That(page.Settings.CameraSensitivity, Is.EqualTo(expected), "slider value " + value);
                }

                Assert.That(container.CameraSensitivityIndex.Value, Is.EqualTo(1));
                Assert.That(container.CameraSensitivityString.Value, Is.EqualTo(Strings.DECIMAL_SEPARATOR));
                Assert.That(page.Reported.Sent, Is.False);
            });
        }

        // ResetToDefaults 0x67ADC0 is SetCameraDefaults with its dirty flag set; the public reset then saves.
        [Test]
        public void TheResetButtonRestoresTheCameraDefaults()
        {
            With(page =>
            {
                page.Settings.CameraRecenterHeadingType = CameraRecenterHeadingType.None;
                page.Settings.CameraInvertedControls = true;
                page.Settings.CameraSnapBehindCharacterButtonVisible = true;
                page.Settings.CameraConfiguration = CameraConfigurationType.Far;
                page.Settings.CameraSensitivity = CameraSensitivityType.High;
                var container = NewCamera(page);

                container.Action_ResetToDefaults();

                Assert.That(page.Settings.CameraRecenterHeadingType, Is.EqualTo(CameraRecenterHeadingType.Medium));
                Assert.That(page.Settings.CameraInvertedControls, Is.False);
                Assert.That(page.Settings.CameraSnapBehindCharacterButtonVisible, Is.False);
                Assert.That(page.Settings.CameraConfiguration, Is.EqualTo(CameraConfigurationType.Default));
                Assert.That(page.Settings.CameraSensitivity, Is.EqualTo(CameraSensitivityType.Medium));
                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(container.CameraConfigurationIndex.Value, Is.EqualTo(1));
                Assert.That(container.CameraSensitivityIndex.Value, Is.EqualTo(1));
            });
        }
    }
}
