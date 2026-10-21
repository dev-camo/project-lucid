using Hardlight;
using HardlightProject;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declarations of the settings pages and their widgets against the DummyDll metadata (type flags, field and
    // method order and flags, IL2CPP options in their authored order, base types). The compiler-generated members are
    // the lambdas that capture only this (<Action_ResetToDefaults>b__3_0 and the like), whose numeric suffix is the
    // member ordinal that fixes their names.
    public sealed class SettingsPageDeclarationTests
    {
        // UIContainerSettingsBase, TypeDef 0x0200092a.
        [Test]
        public void UIContainerSettingsBaseMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsBase), 0x00100081,
                new (string, int)[]
                {
                    ("m_saveManagerRef", 0x0024),
                },
                new (string, int)[]
                {
                    ("SendAnalyticsEventOnSettingChanged", 0x0084),
                    ("SetSaveData", 0x0084),
                    ("Action_ResetToDefaults", 0x0086),
                    ("ResetToDefaults", 0x05c4),
                    ("RefreshBinders", 0x05c4),
                    ("DoOnAfterSave", 0x01c4),
                    (".ctor", 0x1884),
                    ("<Action_ResetToDefaults>b__3_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsBase), 2, 1);
            Assert.That(typeof(UIContainerSettingsBase).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerSettings, TypeDef 0x02000922.
        [Test]
        public void UIContainerSettingsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettings), 0x00100101,
                new (string, int)[]
                {
                    ("m_debugSettings", 0x0001),
                    ("IsInMetaGame", 0x0026),
                    ("IsInGameplay", 0x0026),
                    ("GraphicsSettingsActive", 0x0026),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSettings), 2, 1);
            Assert.That(typeof(UIContainerSettings).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerSettingsAccessibility, TypeDef 0x02000923.
        [Test]
        public void UIContainerSettingsAccessibilityMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsAccessibility), 0x00100001,
                new (string, int)[]
                {
                    ("GameSpeed", 0x0026),
                    ("GameSpeedMin", 0x0026),
                    ("GameSpeedMax", 0x0026),
                    ("GameSpeedPercentageString", 0x0026),
                    ("FailStateType", 0x0026),
                    ("GameSpeedToggle", 0x0026),
                    ("SubtitlesToggle", 0x0026),
                    ("SkipFTUEToggle", 0x0026),
                    ("m_timeManagerRef", 0x0021),
                    ("m_cachedGameSpeed", 0x0001),
                    ("m_saveOnExit", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("RefreshBinders", 0x00c4),
                    ("ResetToDefaults", 0x00c4),
                    ("Action_SetGameSpeed", 0x0086),
                    ("Action_SetSubtitlesEnabled", 0x0086),
                    ("SetFailStateType", 0x0081),
                    ("Action_SetFailStateDefault", 0x0086),
                    ("Action_SetFailStateNoFail", 0x0086),
                    ("Action_SetFailStateInvincible", 0x0086),
                    ("Action_SetGameSpeedToggleEnabled", 0x0086),
                    ("Action_SetSkipFTUE", 0x0086),
                    ("DoOnAfterSave", 0x00c4),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsAccessibility), 2, 1);
            Assert.That(typeof(UIContainerSettingsAccessibility).BaseType, Is.EqualTo(typeof(UIContainerSettingsBase)));
        }

        // UIContainerSettingsCamera, TypeDef 0x0200092b.
        [Test]
        public void UIContainerSettingsCameraMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsCamera), 0x00100001,
                new (string, int)[]
                {
                    ("CameraRecenterHeading", 0x0026),
                    ("CameraInvertedControls", 0x0026),
                    ("CameraConfigurationIndex", 0x0026),
                    ("CameraSensitivityIndex", 0x0026),
                    ("CameraSensitivityString", 0x0026),
                    ("CameraSnapBehindCharacterButtonVisible", 0x0026),
                    ("CameraDistanceString", 0x0026),
                    ("m_cameraConfiguration", 0x0001),
                    ("m_cameraSensitivity", 0x0001),
                    ("m_cameraDistanceStrings", 0x0001),
                    ("m_cachedCameraDistance", 0x0001),
                    ("m_cachedCameraSensitivity", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("RefreshBinders", 0x00c4),
                    ("ResetToDefaults", 0x00c4),
                    ("SetCameraHeadingType", 0x0081),
                    ("Action_SetCameraHeadingTypeNone", 0x0086),
                    ("Action_SetCameraHeadingTypeMedium", 0x0086),
                    ("Action_SetCameraControlsNormal", 0x0086),
                    ("Action_SetCameraControlsInverted", 0x0086),
                    ("Action_SetCameraSnapBehindCharacterButtonVisible", 0x0086),
                    ("Action_SetCameraConfiguration", 0x0086),
                    ("Action_SetCameraSensitivity", 0x0086),
                    (".ctor", 0x1886),
                    ("<Action_SetCameraControlsNormal>b__19_0", 0x0081),
                    ("<Action_SetCameraControlsInverted>b__20_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsCamera), 1, 2);
            Assert.That(typeof(UIContainerSettingsCamera).BaseType, Is.EqualTo(typeof(UIContainerSettingsBase)));
        }

        // UIContainerSettingsControls, TypeDef 0x02000931.
        [Test]
        public void UIContainerSettingsControlsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsControls), 0x00100001,
                new (string, int)[]
                {
                    ("FixedMovementStick", 0x0026),
                    ("DynamicMovementStick", 0x0026),
                    ("CanAdjustControls", 0x0026),
                    ("CanUseOnScreenControls", 0x0026),
                    ("ControlStickSensitivityIndex", 0x0026),
                    ("ControlStickSensitivityString", 0x0026),
                    ("CameraSecondarySensitivityIndex", 0x0026),
                    ("CameraSecondarySensitivityString", 0x0026),
                    ("AirControlContinuous", 0x0026),
                    ("HomingAttackMapping", 0x0026),
                    ("AirStompAttackMapping", 0x0026),
                    ("m_controlStickSensitivity", 0x0001),
                    ("m_cameraSecondarySensitivity", 0x0001),
                    ("m_cachedControlStickSensitivity", 0x0001),
                    ("m_cachedCameraSecondarySensitivity", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("RefreshBinders", 0x00c4),
                    ("ResetToDefaults", 0x00c4),
                    ("SetHomingAttackMapping", 0x0081),
                    ("Action_SetHomingAttackMappingAll", 0x0086),
                    ("Action_SetHomingAttackMappingPrimary", 0x0086),
                    ("Action_SetHomingAttackMappingSecondary", 0x0086),
                    ("SetAirStompAttackMapping", 0x0081),
                    ("Action_SetAirStompAttackMappingAll", 0x0086),
                    ("Action_SetAirStompAttackMappingPrimary", 0x0086),
                    ("Action_SetAirStompAttackMappingSecondary", 0x0086),
                    ("Action_SetFixedMovementControls", 0x0086),
                    ("Action_SetDynamicMovementControls", 0x0086),
                    ("Action_SetControlStickSensitivity", 0x0086),
                    ("Action_SetCameraSecondarySensitivity", 0x0086),
                    ("Action_SetAirAbilityHold", 0x0086),
                    (".ctor", 0x1886),
                    ("<Action_SetFixedMovementControls>b__27_0", 0x0081),
                    ("<Action_SetDynamicMovementControls>b__28_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsControls), 1, 2);
            Assert.That(typeof(UIContainerSettingsControls).BaseType, Is.EqualTo(typeof(UIContainerSettingsBase)));
        }

        // UISettingsBackgroundImage, TypeDef 0x0200084b.
        [Test]
        public void UISettingsBackgroundImageMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UISettingsBackgroundImage), 0x00100001,
                new (string, int)[]
                {
                    ("m_backgroundImage", 0x0001),
                    ("m_backgroundImageColourOnMainMenu", 0x0001),
                },
                new (string, int)[]
                {
                    ("Reset", 0x0081),
                    ("Awake", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UISettingsBackgroundImage), 2, 1);
            Assert.That(typeof(UISettingsBackgroundImage).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

        // UIWidgetSDTCheckbox, TypeDef 0x020009c2.
        [Test]
        public void UIWidgetSDTCheckboxMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTCheckbox), 0x00100101,
                new (string, int)[]
                {
                    ("m_openEvents", 0x0001),
                    ("m_closeEvents", 0x0001),
                },
                new (string, int)[]
                {
                    ("Action_Toggle", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetSDTCheckbox), 2, 1);
            Assert.That(typeof(UIWidgetSDTCheckbox).BaseType, Is.EqualTo(typeof(UIWidget)));
        }

        // UIWidgetSDTLanguageSelect, TypeDef 0x020009c3.
        [Test]
        public void UIWidgetSDTLanguageSelectMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTLanguageSelect), 0x00100101,
                new (string, int)[]
                {
                    ("m_languageIsChosen", 0x0006),
                    ("m_language", 0x0001),
                    ("SelectionEvent", 0x0006),
                },
                new (string, int)[]
                {
                    ("Action_SetLanguage", 0x0086),
                    ("SetCachedLanguage", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetSDTLanguageSelect), 1, 2);
            Assert.That(typeof(UIWidgetSDTLanguageSelect).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

        // UIWidgetSDTSlider, TypeDef 0x0200084f.
        [Test]
        public void UIWidgetSDTSliderMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetSDTSlider), 0x00100001,
                new (string, int)[]
                {
                    ("m_slider", 0x0001),
                    ("m_prefabToSpawn", 0x0001),
                    ("m_pipParent", 0x0001),
                    ("m_emptyPipColour", 0x0001),
                    ("m_fillPipColour", 0x0001),
                    ("m_sliderPips", 0x0021),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("SetValue", 0x0086),
                    ("SetValueWithoutNotify", 0x0086),
                    ("OnSliderValueChanged", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetSDTSlider), 1, 2);
            Assert.That(typeof(UIWidgetSDTSlider).BaseType, Is.EqualTo(typeof(UIWidget)));
        }

        // Attributes beside the options: the components the checkbox and the background image require, the language
        // field's enum hash drawer and the slider's iterator state machine.
        [Test]
        public void TheOtherAttributesMatchTheOriginalMetadata()
        {
            var image = (RequireComponent)typeof(UISettingsBackgroundImage).GetCustomAttributes(typeof(RequireComponent), false).Single();
            Assert.That(image.m_Type0, Is.EqualTo(typeof(Image)));
            var toggle = (RequireComponent)typeof(UIWidgetSDTCheckbox).GetCustomAttributes(typeof(RequireComponent), false).Single();
            Assert.That(toggle.m_Type0, Is.EqualTo(typeof(Toggle)));
            Assert.That(AttributeTypes(Field(typeof(UIWidgetSDTLanguageSelect), "m_language")),
                Is.EqualTo(new[] { typeof(Hardlight.Utils.HashEnumAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(typeof(UIWidgetSDTSlider).GetMethod("Start", Declared)),
                Is.EqualTo(new[] { typeof(System.Runtime.CompilerServices.IteratorStateMachineAttribute) }));
        }
    }
}
