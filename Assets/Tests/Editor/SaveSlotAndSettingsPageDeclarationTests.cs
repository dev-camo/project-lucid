using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using JetBrains.Annotations;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declarations of the save slot screen, its widget and parameters, and the language, legal, notifications and quality
    // settings pages against the DummyDll metadata (type flags, field and method order and flags, IL2CPP options in their
    // authored order, base types, nested and compiler-generated types). The numeric suffix of a generated name is the member
    // ordinal of the method that holds the closure, which the nested types and fields before it fix.
    public sealed class SaveSlotAndSettingsPageDeclarationTests
    {
        private const BindingFlags NestedPrivate = BindingFlags.NonPublic;

        // UIContainerSaveDataViewParameters, TypeDef 0x020009dc.
        [Test]
        public void UIContainerSaveDataViewParametersMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSaveDataViewParameters), 0x00100001,
                new (string, int)[]
                {
                    ("<SaveData>k__BackingField", 0x0021),
                    ("<Progress>k__BackingField", 0x0021),
                },
                new (string, int)[]
                {
                    ("get_SaveData", 0x0886),
                    ("get_Progress", 0x0886),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSaveDataViewParameters), 1, 2);
            Assert.That(typeof(UIContainerSaveDataViewParameters).BaseType, Is.EqualTo(typeof(UIWidgetToggleParameters)));
        }

        // UIWidgetToggleSaveData, TypeDef 0x020009db.
        [Test]
        public void UIWidgetToggleSaveDataMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetToggleSaveData), 0x00100001,
                new (string, int)[]
                {
                    ("m_playTimeStringId", 0x0001),
                    ("NumberOfOrbs", 0x0026),
                    ("NumberOfOrnaments", 0x0026),
                    ("LastedUpdatedTimestamp", 0x0026),
                    ("PlayTimeTimestamp", 0x0026),
                    ("SlotEmpty", 0x0026),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("Select", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIWidgetToggleSaveData), 2, 1);
            Assert.That(typeof(UIWidgetToggleSaveData).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
            Assert.That(AttributeTypes(Field(typeof(UIWidgetToggleSaveData), "m_playTimeStringId")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(HashEnumAttribute) }));
        }

        // UIContainerSaveSlots, TypeDef 0x02000920, and its closure class 0x02000921.
        [Test]
        public void UIContainerSaveSlotsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSaveSlots), 0x00100001,
                new (string, int)[]
                {
                    ("m_selectSaveUIEvent", 0x0001),
                    ("m_saveSlotToggles", 0x0001),
                    ("ShowLoading", 0x0026),
                    ("m_appRef", 0x0021),
                    ("m_saveManagerRef", 0x0021),
                    ("m_navigationManagerRef", 0x0021),
                    ("m_progressionManagerRef", 0x0021),
                    ("m_selectedSlotIndex", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnSaveManagerValid", 0x0081),
                    ("SetActiveSaveSlotIndex", 0x0081),
                    ("Action_SelectSave", 0x0086),
                    ("TryNavigateToWidget", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSaveSlots), 1, 2);
            Assert.That(typeof(UIContainerSaveSlots).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerSaveSlots).GetNestedType("<>c__DisplayClass9_0", NestedPrivate), 0x00100103,
                new (string, int)[]
                {
                    ("slotIndex", 0x0006),
                    ("<>4__this", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("<OnSaveManagerValid>b__0", 0x0083),
                });
        }

        // UIContainerSettingsLanguage, TypeDef 0x02000938, and its static lambda cache 0x02000939.
        [Test]
        public void UIContainerSettingsLanguageMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsLanguage), 0x00100001,
                new (string, int)[]
                {
                    ("m_languageSelectionButtons", 0x0001),
                    ("m_appRef", 0x0021),
                    ("m_languageToApply", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("CacheLanguageChoice", 0x0086),
                    ("OnDestroy", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsLanguage), 1, 2);
            Assert.That(typeof(UIContainerSettingsLanguage).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerSettingsLanguage).GetNestedType("<>c", NestedPrivate), 0x00102103,
                new (string, int)[]
                {
                    ("<>9", 0x0036),
                    ("<>9__5_0", 0x0016),
                },
                new (string, int)[]
                {
                    (".cctor", 0x1891),
                    (".ctor", 0x1886),
                    ("<OnDestroy>b__5_0", 0x0083),
                });
        }

        // UIContainerSettingsLegal, TypeDef 0x0200093a.
        [Test]
        public void UIContainerSettingsLegalMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsLegal), 0x00100001,
                new (string, int)[]
                {
                    ("m_levelSelectIdentifier", 0x0001),
                    ("m_mainMenuIdentifier", 0x0001),
                    ("CanShowAnalyticsConsentButton", 0x0026),
                    ("CanShowCreditsButton", 0x0026),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsLegal), 2, 1);
            Assert.That(typeof(UIContainerSettingsLegal).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerSettingsNotifications, TypeDef 0x0200093b.
        [Test]
        public void UIContainerSettingsNotificationsMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsNotifications), 0x00100001,
                new (string, int)[]
                {
                    ("m_notificationsPermission", 0x0001),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    ("Action_SetNotificationPermission", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsNotifications), 1, 2);
            Assert.That(typeof(UIContainerSettingsNotifications).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // UIContainerSettingsQuality, TypeDef 0x0200093c, its nested types 0x0200093d..0x0200093f and the closure of OnDestroy.
        [Test]
        public void UIContainerSettingsQualityMatchesTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerSettingsQuality), 0x00100001,
                new (string, int)[]
                {
                    ("QualitySetting", 0x0026),
                    ("QualitySettingName", 0x0026),
                    ("ResolutionSettingString", 0x0026),
                    ("MsaaSettingString", 0x0026),
                    ("m_qualityLevels_iOS", 0x0001),
                    ("m_qualityLevels_iOS_iPad", 0x0001),
                    ("m_qualityLevels_tvOS", 0x0001),
                    ("m_qualityLevels_macOS", 0x0001),
                    ("m_msaaSettings", 0x0001),
                    ("m_resolutionSlider", 0x0001),
                    ("m_qualitySlider", 0x0001),
                    ("m_msaaSlider", 0x0001),
                    ("m_performanceProfiles", 0x0001),
                    ("m_scalablePerformance", 0x0001),
                    ("m_resolutionScale", 0x0001),
                    ("m_msaaSampleCount", 0x0001),
                    ("m_cachedGraphicsQuality", 0x0001),
                },
                new (string, int)[]
                {
                    ("Awake", 0x0081),
                    ("OnDestroy", 0x0081),
                    ("SetPlatformPerformanceProfiles", 0x0081),
                    ("SetCurrentProfile", 0x0081),
                    ("Action_OnQualitySliderUpdated", 0x0086),
                    ("Action_OnResolutionSliderUpdated", 0x0086),
                    ("Action_OnMsaaSliderUpdated", 0x0086),
                    ("ResetToDefaults", 0x00c4),
                    ("RefreshBinders", 0x00c4),
                    ("SetQualityValue", 0x0081),
                    ("UpdateResolutionSlider", 0x0081),
                    ("UpdateMSAASlider", 0x0081),
                    ("SetResolutionScale", 0x0081),
                    ("SetMSAAValue", 0x0081),
                    ("GetMsaaSettingIndex", 0x0081),
                    ("GetRenderPipelineAsset", 0x0081),
                    (".ctor", 0x1886),
                    ("<OnDestroy>b__20_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsQuality), 2, 1);
            Assert.That(typeof(UIContainerSettingsQuality).BaseType, Is.EqualTo(typeof(UIContainerSettingsBase)));

            AssertMembers(typeof(UIContainerSettingsQuality).GetNestedType("MSAASetting", NestedPrivate), 0x00102103,
                new (string, int)[]
                {
                    ("SampleCount", 0x0006),
                    ("NameFormat", 0x0006),
                },
                new (string, int)[]
                {
                    ("GetLocalisedName", 0x0086),
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(UIContainerSettingsQuality).GetNestedType("QualityLevel", NestedPrivate), 0x00102103,
                new (string, int)[]
                {
                    ("Profile", 0x0006),
                    ("LocalisedName", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                });
            AssertMembers(typeof(UIContainerSettingsQuality).GetNestedType("<>c__DisplayClass33_0", NestedPrivate), 0x00100103,
                new (string, int)[]
                {
                    ("sampleCount", 0x0006),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("<GetMsaaSettingIndex>b__0", 0x0083),
                });
            AssertIl2CppOptions(typeof(UIContainerSettingsQuality).GetNestedType("MSAASetting", NestedPrivate), 2, 1);
            AssertIl2CppOptions(typeof(UIContainerSettingsQuality).GetNestedType("QualityLevel", NestedPrivate), 2, 1);
        }

        // Attributes beside the options: the platform level lists keep their authored attribute order, the enum hash drawers
        // of the two nested types (the quality level's has no enum type).
        [Test]
        public void TheQualityPageAttributesMatchTheOriginalMetadata()
        {
            System.Type page = typeof(UIContainerSettingsQuality);
            Assert.That(AttributeTypes(Field(page, "m_qualityLevels_iOS")), Is.EqualTo(new[] { typeof(UsedImplicitlyAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(page, "m_qualityLevels_iOS_iPad")), Is.EqualTo(new[] { typeof(SerializeField), typeof(UsedImplicitlyAttribute) }));
            Assert.That(AttributeTypes(Field(page, "m_qualityLevels_tvOS")), Is.EqualTo(new[] { typeof(UsedImplicitlyAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(page, "m_qualityLevels_macOS")), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(page.GetNestedType("MSAASetting", NestedPrivate), "NameFormat")),
                Is.EqualTo(new[] { typeof(HashEnumAttribute) }));
            FieldInfo levelName = Field(page.GetNestedType("QualityLevel", NestedPrivate), "LocalisedName");
            Assert.That(AttributeTypes(levelName), Is.EqualTo(new[] { typeof(HashEnumAttribute) }));
        }
    }
}
