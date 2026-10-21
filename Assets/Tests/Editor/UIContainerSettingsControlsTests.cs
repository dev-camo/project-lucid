using System;
using System.Collections.Generic;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.SettingsPageTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsControls (Game.Runtime 0x02000931, x86-64 0x67BD00..0x67D3C8) as a real component on an owned
    // inactive host, talking to a real SaveManager holding the live settings (SettingsPageTestSupport). The serialized
    // sensitivity lists are filled as the authored page holds them: low, medium and high, each with its own string.
    public sealed class UIContainerSettingsControlsTests
    {
        private static readonly (InputModifierType key, Strings value)[] StickSensitivities =
        {
            (InputModifierType.Low, Strings.MISSING),
            (InputModifierType.Medium, Strings.DECIMAL_SEPARATOR),
            (InputModifierType.High, Strings.NUMBER_SEPARATOR_SLASH),
        };

        private static readonly (CameraSensitivityType key, Strings value)[] CameraSensitivities =
        {
            (CameraSensitivityType.VeryLow, Strings.NUMBER_SEPARATOR),
            (CameraSensitivityType.Medium, Strings.NUMBER_SEPARATOR_SLASH),
            (CameraSensitivityType.VeryHigh, Strings.MISSING),
        };

        private static UIContainerSettingsControls NewControls(Page page)
        {
            var container = Add<UIContainerSettingsControls>(page);
            var stick = new List<EnumValuePair<InputModifierType, Strings>>();
            foreach ((InputModifierType key, Strings value) in StickSensitivities)
            {
                var pair = new EnumValuePair<InputModifierType, Strings>();
                Set(pair, "m_key", key); // serialized in the original
                Set(pair, "m_value", value); // serialized in the original
                stick.Add(pair);
            }

            var camera = new List<EnumValuePair<CameraSensitivityType, Strings>>();
            foreach ((CameraSensitivityType key, Strings value) in CameraSensitivities)
            {
                var pair = new EnumValuePair<CameraSensitivityType, Strings>();
                Set(pair, "m_key", key); // serialized in the original
                Set(pair, "m_value", value); // serialized in the original
                camera.Add(pair);
            }

            Set(container, "m_controlStickSensitivity", stick); // serialized in the original
            Set(container, "m_cameraSecondarySensitivity", camera); // serialized in the original
            return container;
        }

        // Awake 0x67BD00: RefreshBinders first, then both sensitivity index binders are cached.
        [Test]
        public void AwakePublishesTheSettingsThenCachesTheSelectedIndices()
        {
            With(page =>
            {
                page.Settings.ControlStickSensitivity = InputModifierType.High;
                page.Settings.CameraSecondarySensitivity = CameraSensitivityType.VeryLow;
                var container = NewControls(page);

                Call(container, "Awake");

                Assert.That(Get<int>(container, "m_cachedControlStickSensitivity"), Is.EqualTo(2));
                Assert.That(Get<int>(container, "m_cachedCameraSecondarySensitivity"), Is.EqualTo(0));
            });
        }

        // RefreshBinders 0x67BF60: the fixed stick is the absence of the dynamic one (sete at 0x67c066), the air control
        // flag (+0x40) and the two mappings (+0x54, +0x58) are copied, and both sensitivity binders get the index of
        // the entry whose key equals the saved one (+0x38, +0x3C) and that entry's string. The two "can" binders are
        // the constant false (0x67c24d, 0x67c26a).
        [Test]
        public void RefreshBindersPublishesTheSavedControlSettings()
        {
            With(page =>
            {
                page.Settings.DynamicMovementStick = true;
                page.Settings.AirAbilityHold = false;
                page.Settings.HomingAttackMapping = ButtonMappingType.Primary;
                page.Settings.AirStompAttackMapping = ButtonMappingType.All;
                page.Settings.ControlStickSensitivity = InputModifierType.High;
                page.Settings.CameraSecondarySensitivity = CameraSensitivityType.VeryHigh;
                var container = NewControls(page);
                container.CanAdjustControls.Value = true;
                container.CanUseOnScreenControls.Value = true;

                Call(container, "RefreshBinders");

                Assert.That(container.FixedMovementStick.Value, Is.False);
                Assert.That(container.DynamicMovementStick.Value, Is.True);
                Assert.That(container.AirControlContinuous.Value, Is.False);
                Assert.That(container.HomingAttackMapping.Value, Is.EqualTo(ButtonMappingType.Primary));
                Assert.That(container.AirStompAttackMapping.Value, Is.EqualTo(ButtonMappingType.All));
                Assert.That(container.ControlStickSensitivityIndex.Value, Is.EqualTo(2));
                Assert.That(container.ControlStickSensitivityString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR_SLASH));
                Assert.That(container.CameraSecondarySensitivityIndex.Value, Is.EqualTo(2));
                Assert.That(container.CameraSecondarySensitivityString.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(container.CanAdjustControls.Value, Is.False);
                Assert.That(container.CanUseOnScreenControls.Value, Is.False);

                page.Settings.DynamicMovementStick = false;
                page.Settings.AirAbilityHold = true;
                page.Settings.HomingAttackMapping = ButtonMappingType.Secondary;
                page.Settings.AirStompAttackMapping = ButtonMappingType.Primary;
                page.Settings.ControlStickSensitivity = InputModifierType.Low;
                page.Settings.CameraSecondarySensitivity = CameraSensitivityType.VeryLow;
                Call(container, "RefreshBinders");

                Assert.That(container.FixedMovementStick.Value, Is.True);
                Assert.That(container.DynamicMovementStick.Value, Is.False);
                Assert.That(container.AirControlContinuous.Value, Is.True);
                Assert.That(container.HomingAttackMapping.Value, Is.EqualTo(ButtonMappingType.Secondary));
                Assert.That(container.AirStompAttackMapping.Value, Is.EqualTo(ButtonMappingType.Primary));
                Assert.That(container.ControlStickSensitivityIndex.Value, Is.EqualTo(0));
                Assert.That(container.ControlStickSensitivityString.Value, Is.EqualTo(Strings.MISSING));
                Assert.That(container.CameraSecondarySensitivityIndex.Value, Is.EqualTo(0));
                Assert.That(container.CameraSecondarySensitivityString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR));
            });
        }

        // The string is read at the found index with no guard, so a sensitivity missing from its list (index -1) faults
        // in List.get_Item, after the index binder took the -1.
        [Test]
        public void ASavedSensitivityMissingFromItsListFaults()
        {
            With(page =>
            {
                page.Settings.CameraSecondarySensitivity = CameraSensitivityType.High;
                var container = NewControls(page);

                Assert.Throws<ArgumentOutOfRangeException>(() => Call(container, "RefreshBinders"));
                Assert.That(container.CameraSecondarySensitivityIndex.Value, Is.EqualTo(-1));
                Assert.That(container.ControlStickSensitivityIndex.Value, Is.EqualTo(1), "the stick binders were already done");
            });
        }

        // OnDestroy 0x67BD60: a changed stick sensitivity is reported as "control_stick_sensitivity" with the cached
        // entry's string and then the bound string (Hardlight.Enums.EnumExtentions.GetString 0x19E6FF0).
        [Test]
        public void ClosingThePageReportsAChangedStickSensitivity()
        {
            With(page =>
            {
                var container = NewControls(page);
                Call(container, "Awake");
                container.ControlStickSensitivityIndex.Value = 2;
                container.ControlStickSensitivityString.Value = Strings.NUMBER_SEPARATOR_SLASH;

                Call(container, "OnDestroy");

                Assert.That(page.Reported.Last, Is.EqualTo(("control_stick_sensitivity", "DECIMAL_SEPARATOR", "NUMBER_SEPARATOR_SLASH")));
            });
        }

        [Test]
        public void ClosingThePageReportsAChangedSecondaryCameraSensitivity()
        {
            With(page =>
            {
                var container = NewControls(page);
                Call(container, "Awake");
                container.CameraSecondarySensitivityIndex.Value = 0;
                container.CameraSecondarySensitivityString.Value = Strings.NUMBER_SEPARATOR;

                Call(container, "OnDestroy");

                Assert.That(page.Reported.Last, Is.EqualTo(("camera_secondary_sensitivity", "NUMBER_SEPARATOR_SLASH", "NUMBER_SEPARATOR")));
            });
        }

        // The stick sensitivity is reported first (0x67be90) and the camera one last (the tail call at 0x67bf59).
        [Test]
        public void ClosingThePageWithBothChangedReportsTheCameraSensitivityLast()
        {
            With(page =>
            {
                var container = NewControls(page);
                Call(container, "Awake");
                container.ControlStickSensitivityIndex.Value = 0;
                container.ControlStickSensitivityString.Value = Strings.MISSING;
                container.CameraSecondarySensitivityIndex.Value = 2;
                container.CameraSecondarySensitivityString.Value = Strings.MISSING;

                Call(container, "OnDestroy");

                Assert.That(page.Reported.Last, Is.EqualTo(("camera_secondary_sensitivity", "NUMBER_SEPARATOR_SLASH", "MISSING")));
            });
        }

        [Test]
        public void ClosingThePageWithoutChangesReportsNothing()
        {
            With(page =>
            {
                var container = NewControls(page);
                Call(container, "Awake");

                Call(container, "OnDestroy");

                Assert.That(page.Reported.Sent, Is.False);
            });
        }

        // The mapping buttons: 0x67C3E0/F0/400 pass All 0x5240CCA8, Primary 0xD7FFEB63 and Secondary 0xA06E9505 to
        // SetHomingAttackMapping 0x67C2B0, whose lambda (0x67D150) reports the old and new mapping by name, stores it
        // and saves; the air stomp buttons (0x67C540/50/60 and 0x67D1D0) do the same for the stomp mapping.
        [Test]
        public void TheHomingAttackMappingButtonsReportStoreAndSave()
        {
            With(page =>
            {
                var container = NewControls(page);

                container.Action_SetHomingAttackMappingAll();
                Assert.That(page.Settings.HomingAttackMapping, Is.EqualTo(ButtonMappingType.All));
                Assert.That(page.Reported.Last, Is.EqualTo(("homing_attack_mapping", "Secondary", "All")));
                Assert.That(container.HomingAttackMapping.Value, Is.EqualTo(ButtonMappingType.All));
                Assert.That(SavedJson(page), Is.Not.Null);

                container.Action_SetHomingAttackMappingPrimary();
                Assert.That(page.Settings.HomingAttackMapping, Is.EqualTo(ButtonMappingType.Primary));
                Assert.That(page.Reported.Last, Is.EqualTo(("homing_attack_mapping", "All", "Primary")));

                container.Action_SetHomingAttackMappingSecondary();
                Assert.That(page.Settings.HomingAttackMapping, Is.EqualTo(ButtonMappingType.Secondary));
                Assert.That(page.Reported.Last, Is.EqualTo(("homing_attack_mapping", "Primary", "Secondary")));
                Assert.That(container.HomingAttackMapping.Value, Is.EqualTo(ButtonMappingType.Secondary));
                Assert.That(page.Settings.AirStompAttackMapping, Is.EqualTo(ButtonMappingType.Secondary), "the stomp mapping is separate");
            });
        }

        [Test]
        public void TheAirStompMappingButtonsReportStoreAndSave()
        {
            With(page =>
            {
                var container = NewControls(page);

                container.Action_SetAirStompAttackMappingPrimary();
                Assert.That(page.Settings.AirStompAttackMapping, Is.EqualTo(ButtonMappingType.Primary));
                Assert.That(page.Reported.Last, Is.EqualTo(("air_stomp_attack_mapping", "Secondary", "Primary")));
                Assert.That(container.AirStompAttackMapping.Value, Is.EqualTo(ButtonMappingType.Primary));
                Assert.That(SavedJson(page), Is.Not.Null);

                container.Action_SetAirStompAttackMappingAll();
                Assert.That(page.Settings.AirStompAttackMapping, Is.EqualTo(ButtonMappingType.All));
                Assert.That(page.Reported.Last, Is.EqualTo(("air_stomp_attack_mapping", "Primary", "All")));

                container.Action_SetAirStompAttackMappingSecondary();
                Assert.That(page.Settings.AirStompAttackMapping, Is.EqualTo(ButtonMappingType.Secondary));
                Assert.That(page.Reported.Last, Is.EqualTo(("air_stomp_attack_mapping", "All", "Secondary")));
                Assert.That(page.Settings.HomingAttackMapping, Is.EqualTo(ButtonMappingType.Secondary), "the homing mapping is separate");
            });
        }

        [Test]
        public void ChoosingTheCurrentMappingAgainSavesButReportsNothing()
        {
            With(page =>
            {
                var container = NewControls(page);

                container.Action_SetHomingAttackMappingSecondary();

                Assert.That(page.Reported.Sent, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
            });
        }

        // The stick style buttons (0x67C570 with lambda 0x67CFD0, 0x67C650 with 0x67D040) both report under
        // "dynamic_stick_movement" and store the dynamic flag as the constant false or true.
        [Test]
        public void TheMovementStickStyleButtonsReportTheDynamicFlag()
        {
            With(page =>
            {
                var container = NewControls(page);

                container.Action_SetDynamicMovementControls();
                Assert.That(page.Settings.DynamicMovementStick, Is.True);
                Assert.That(page.Reported.Last, Is.EqualTo(("dynamic_stick_movement", "False", "True")));
                Assert.That(container.DynamicMovementStick.Value, Is.True);
                Assert.That(container.FixedMovementStick.Value, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);

                container.Action_SetFixedMovementControls();
                Assert.That(page.Settings.DynamicMovementStick, Is.False);
                Assert.That(page.Reported.Last, Is.EqualTo(("dynamic_stick_movement", "True", "False")));
                Assert.That(container.DynamicMovementStick.Value, Is.False);
                Assert.That(container.FixedMovementStick.Value, Is.True);
            });
        }

        // Action_SetControlStickSensitivity 0x67C730 rounds the slider value with Mathf.RoundToInt (modf, then the half-to-even
        // branch) and takes that entry's key. 0.5 rounds to 0, 1.5 to 2 and 2.5 to 2; away-from-zero rounding would give
        // 1, 2 and 3. The action itself reports nothing.
        [Test]
        public void TheStickSensitivitySliderRoundsHalvesToEven()
        {
            With(page =>
            {
                var container = NewControls(page);

                foreach ((float value, InputModifierType expected) in new[]
                {
                    (0.5f, InputModifierType.Low), (1.5f, InputModifierType.High), (2.5f, InputModifierType.High),
                    (0.4f, InputModifierType.Low), (1.6f, InputModifierType.High), (1f, InputModifierType.Medium),
                })
                {
                    container.Action_SetControlStickSensitivity(value);
                    Assert.That(page.Settings.ControlStickSensitivity, Is.EqualTo(expected), "slider value " + value);
                }

                Assert.That(container.ControlStickSensitivityIndex.Value, Is.EqualTo(1));
                Assert.That(container.ControlStickSensitivityString.Value, Is.EqualTo(Strings.DECIMAL_SEPARATOR));
                Assert.That(page.Reported.Sent, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
            });
        }

        // The secondary camera slider (0x67C960) does the same for its own list and setting.
        [Test]
        public void TheSecondaryCameraSensitivitySliderRoundsHalvesToEven()
        {
            With(page =>
            {
                var container = NewControls(page);

                foreach ((float value, CameraSensitivityType expected) in new[]
                {
                    (0.5f, CameraSensitivityType.VeryLow), (1.5f, CameraSensitivityType.VeryHigh), (2.5f, CameraSensitivityType.VeryHigh),
                    (0.6f, CameraSensitivityType.Medium), (1f, CameraSensitivityType.Medium),
                })
                {
                    container.Action_SetCameraSecondarySensitivity(value);
                    Assert.That(page.Settings.CameraSecondarySensitivity, Is.EqualTo(expected), "slider value " + value);
                }

                Assert.That(container.CameraSecondarySensitivityIndex.Value, Is.EqualTo(1));
                Assert.That(container.CameraSecondarySensitivityString.Value, Is.EqualTo(Strings.NUMBER_SEPARATOR_SLASH));
                Assert.That(page.Reported.Sent, Is.False);
            });
        }

        // Action_SetAirAbilityHold 0x67CB90 and its lambda 0x67D350.
        [Test]
        public void TheAirAbilityHoldIsReportedStoredAndSaved()
        {
            With(page =>
            {
                var container = NewControls(page);

                container.Action_SetAirAbilityHold(false);

                Assert.That(page.Settings.AirAbilityHold, Is.False);
                Assert.That(page.Reported.Last, Is.EqualTo(("air_control_continuous", "True", "False")));
                Assert.That(container.AirControlContinuous.Value, Is.False);
                Assert.That(SavedJson(page), Is.Not.Null);
            });
        }

        // ResetToDefaults 0x67C290 is SetControlsDefaults with its dirty flag set; the public reset then saves.
        [Test]
        public void TheResetButtonRestoresTheControlDefaults()
        {
            With(page =>
            {
                page.Settings.DynamicMovementStick = true;
                page.Settings.AirAbilityHold = false;
                page.Settings.HomingAttackMapping = ButtonMappingType.All;
                page.Settings.AirStompAttackMapping = ButtonMappingType.Primary;
                page.Settings.ControlStickSensitivity = InputModifierType.High;
                page.Settings.CameraSecondarySensitivity = CameraSensitivityType.VeryHigh;
                var container = NewControls(page);

                container.Action_ResetToDefaults();

                Assert.That(page.Settings.DynamicMovementStick, Is.False);
                Assert.That(page.Settings.AirAbilityHold, Is.True);
                Assert.That(page.Settings.HomingAttackMapping, Is.EqualTo(ButtonMappingType.Secondary));
                Assert.That(page.Settings.AirStompAttackMapping, Is.EqualTo(ButtonMappingType.Secondary));
                Assert.That(page.Settings.ControlStickSensitivity, Is.EqualTo(InputModifierType.Medium));
                Assert.That(page.Settings.CameraSecondarySensitivity, Is.EqualTo(CameraSensitivityType.Medium));
                Assert.That(SavedJson(page), Is.Not.Null);
                Assert.That(container.FixedMovementStick.Value, Is.True);
                Assert.That(container.ControlStickSensitivityIndex.Value, Is.EqualTo(1));
                Assert.That(container.CameraSecondarySensitivityIndex.Value, Is.EqualTo(1));
            });
        }
    }
}
