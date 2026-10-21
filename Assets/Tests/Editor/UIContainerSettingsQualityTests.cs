using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.SettingsPageTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsQuality and its nested MSAASetting, QualityLevel and closure types (Game.Runtime 0x0200093c..0x0200093f,
    // x86-64 0x67DDC0..0x67EFBF) as a real component on an owned inactive host, over the real services it reads: a
    // ScalablePerformance holding three authored quality levels (low, medium, high profiles), a VisualQualityManager_SDT, the
    // DataManager, the SaveManager of SettingsPageTestSupport, a GUICameraManager singleton owning the menu camera, and an owned
    // UniversalRenderPipelineAsset installed as the quality level's pipeline (the previous one is put back afterwards).
    // Each profile holds a device-locked feature (levels -200, 0, 200 for low, medium, high) and a free one (-100, 0, 100); the
    // high profile alone holds the render scale feature (low).
    // The fixture swaps the global QualitySettings.renderPipeline for the owned asset (the project's own is none) and puts it
    // back; this needs a clean native run with ProjectSettings unchanged and no unexpected error logs.
    // Not covered: the platform lists other than macOS, which this build never reads (SetPlatformPerformanceProfiles 0x67DFD0).
    public sealed class UIContainerSettingsQualityTests
    {
        private static readonly Type LevelType = typeof(UIContainerSettingsQuality).GetNestedType("QualityLevel", BindingFlags.NonPublic);
        private static readonly Type MsaaType = typeof(UIContainerSettingsQuality).GetNestedType("MSAASetting", BindingFlags.NonPublic);
        private static readonly MethodInfo SetGuiCameraInstance = typeof(MonoSingleton<GUICameraManager>).GetProperty("Instance").GetSetMethod(true);

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string>
        {
            { Strings.ACT_1, "Low text" }, { Strings.ACT_2, "Medium text" }, { Strings.ACT_3, "High text" },
            { Strings.LANGUAGE_ENGLISH, "Off" }, { Strings.LANGUAGE_ENGLISH_UK, "{0}x MSAA" },
            { Strings.LANGUAGE_ENGLISH_AUS, "{0}x AA" }, { Strings.LANGUAGE_FRENCH, "{0}x Plus" },
            { Strings.ZONE_1, "first {0}" }, { Strings.ZONE_2, "second {0}" },
        };

        private sealed class Rig
        {
            public Page Page;
            public UIContainerSettingsQuality Container;
            public ScalablePerformance Performance;
            public VisualQualityManager_SDT Quality;
            public PerformanceProfile Low, Medium, High;
            public ScalableFeature Locked, Free, RenderScale;
            public UniversalRenderPipelineAsset Pipeline;
            public Camera MenuCamera;
            public Slider QualitySlider, ResolutionSlider, MsaaSlider;
        }

        private static PerformanceProfile NewProfile(Owned owned, string guid, string name, PerformanceProfile.QualityLevel locked,
            PerformanceProfile.QualityLevel free, ScalableFeature lockedFeature, ScalableFeature freeFeature)
        {
            PerformanceProfile profile = owned.Track(ScriptableObject.CreateInstance<PerformanceProfile>());
            Set(profile, "m_guid", guid); // serialized in the original, as are the attributes
            profile.name = name;
            Set(profile, "m_attributes", new List<PerformanceAttribute>
            {
                new PerformanceAttribute { Feature = lockedFeature, Level = locked },
                new PerformanceAttribute { Feature = freeFeature, Level = free },
            });
            return profile;
        }

        private static Array Levels(params (PerformanceProfile profile, Strings name)[] levels)
        {
            Array array = Array.CreateInstance(LevelType, levels.Length);
            for (int index = 0; index < levels.Length; index++)
            {
                object level = Activator.CreateInstance(LevelType);
                Set(level, "Profile", levels[index].profile);
                Set(level, "LocalisedName", levels[index].name);
                array.SetValue(level, index);
            }

            return array;
        }

        private static Array Msaa(params (int samples, Strings format)[] settings)
        {
            Array array = Array.CreateInstance(MsaaType, settings.Length);
            for (int index = 0; index < settings.Length; index++)
            {
                object setting = Activator.CreateInstance(MsaaType);
                Set(setting, "SampleCount", settings[index].samples);
                Set(setting, "NameFormat", settings[index].format);
                array.SetValue(setting, index);
            }

            return array;
        }

        private static Slider NewSlider(Owned owned, out UIWidgetSDTSlider widget, float maximum, bool wholeNumbers)
        {
            widget = owned.Host("Lucid quality slider", false).AddComponent<UIWidgetSDTSlider>();
            Slider slider = widget.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = maximum;
            slider.wholeNumbers = wholeNumbers;
            Set(widget, "m_slider", slider); // serialized in the original
            return slider;
        }

        // Taken before anything the services read: the settings page, the data manager, the performance and quality managers
        // (the active profile is the medium one), the menu camera, the pipeline asset and the container itself. With awake set,
        // the container's Awake has run, as it does when the page opens.
        private static void Run(bool awake, Action<Owned, Rig> body) => OwnedStringTable.Run(Texts, () => With(page =>
        {
            Owned owned = page.Owned;
            var rig = new Rig { Page = page };
            if (GUICameraManager.Instance != null) throw new InvalidOperationException("Fixture requires no live GUICameraManager instance");
            owned.Register(owned.Host("Lucid quality data manager", false).AddComponent<DataManager>());

            rig.Locked = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            rig.Locked.ID = "lucid-locked";
            rig.Free = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            rig.Free.ID = "lucid-free";
            rig.RenderScale = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            rig.RenderScale.ID = "lucid-render-scale";
            var switching = owned.Track(ScriptableObject.CreateInstance<ScalableFeature>());
            switching.ID = "lucid-switching";
            rig.Low = NewProfile(owned, "lucid-low", "lucid-low-profile", PerformanceProfile.QualityLevel.VeryLow,
                PerformanceProfile.QualityLevel.Low, rig.Locked, rig.Free);
            rig.Medium = NewProfile(owned, "lucid-medium", "lucid-medium-profile", PerformanceProfile.QualityLevel.Medium,
                PerformanceProfile.QualityLevel.Medium, rig.Locked, rig.Free);
            rig.High = NewProfile(owned, "lucid-high", "lucid-high-profile", PerformanceProfile.QualityLevel.VeryHigh,
                PerformanceProfile.QualityLevel.High, rig.Locked, rig.Free);
            Get<List<PerformanceAttribute>>(rig.High, "m_attributes").Add(
                new PerformanceAttribute { Feature = rig.RenderScale, Level = PerformanceProfile.QualityLevel.Low });

            rig.Performance = owned.Register(owned.Host("Lucid scalable performance", false).AddComponent<ScalablePerformance>());
            rig.Performance.SetPerformanceProfile(rig.Medium);
            rig.Quality = owned.Register(owned.Host("Lucid visual quality manager", false).AddComponent<VisualQualityManager_SDT>());
            // Serialized in the original: the device-locked features, the "graphics switching" requirement the profile setter
            // tests, and the render scale of each quality level (the high profile's low level gives 0.5).
            Set(rig.Quality, "m_deviceLockedFeatures", new[] { rig.Locked });
            Set(rig.Quality, "m_allowGraphicsQualitySwitching",
                new PerformanceAttribute { Feature = switching, Level = PerformanceProfile.QualityLevel.Low });
            Set(rig.Quality, "m_renderScaleFeatureConfig", rig.RenderScale);
            Set(rig.Quality, "m_defaultRenderScaleConfigurations", new[]
            {
                NewRenderScale(PerformanceProfile.QualityLevel.Low, 0.5f),
                NewRenderScale(PerformanceProfile.QualityLevel.Medium, 0.8f),
                NewRenderScale(PerformanceProfile.QualityLevel.High, 0.6f),
            });
            rig.Quality.GameplayRenderScale = 0.7f;

            rig.MenuCamera = owned.Host("Lucid menu camera", false).AddComponent<Camera>();
            rig.MenuCamera.enabled = false;
            var gui = owned.Host("Lucid gui camera manager", false).AddComponent<GUICameraManager>();
            Set(gui, "m_menuCamera", rig.MenuCamera); // serialized in the original
            owned.Register(gui);
            SetGuiCameraInstance.Invoke(null, new object[] { gui });
            owned.Lease(new ReleaseAction(() => SetGuiCameraInstance.Invoke(null, new object[] { null })));

            // The pipeline is put back before the owned asset is destroyed.
            RenderPipelineAsset prior = QualitySettings.renderPipeline;
            rig.Pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            owned.Lease(new ReleaseAction(() =>
            {
                QualitySettings.renderPipeline = prior;
                UnityEngine.Object.DestroyImmediate(rig.Pipeline);
            }));
            QualitySettings.renderPipeline = rig.Pipeline;
            rig.Pipeline.msaaSampleCount = 2;
            rig.Pipeline.renderScale = 0.6f;

            // The container's field initialisers cache the SaveManager reference from its row.
            rig.Container = owned.Host("Lucid quality page", false).AddComponent<UIContainerSettingsQuality>();
            // Serialized in the original: three levels, four anti-aliasing settings and three sliders.
            Set(rig.Container, "m_qualityLevels_macOS", Levels((rig.Low, Strings.ACT_1), (rig.Medium, Strings.ACT_2), (rig.High, Strings.ACT_3)));
            Set(rig.Container, "m_msaaSettings", Msaa((1, Strings.LANGUAGE_ENGLISH), (2, Strings.LANGUAGE_ENGLISH_UK),
                (4, Strings.LANGUAGE_ENGLISH_AUS), (8, Strings.LANGUAGE_FRENCH)));
            rig.QualitySlider = NewSlider(owned, out UIWidgetSDTSlider qualityWidget, 2f, true);
            rig.ResolutionSlider = NewSlider(owned, out UIWidgetSDTSlider resolutionWidget, 1f, false);
            rig.MsaaSlider = NewSlider(owned, out UIWidgetSDTSlider msaaWidget, 3f, true);
            Set(rig.Container, "m_qualitySlider", qualityWidget);
            Set(rig.Container, "m_resolutionSlider", resolutionWidget);
            Set(rig.Container, "m_msaaSlider", msaaWidget);

            if (awake) Call(rig.Container, "Awake");
            body(owned, rig);
        }));

        private static RenderScaleConfiguration NewRenderScale(PerformanceProfile.QualityLevel level, float scale)
        {
            var configuration = new RenderScaleConfiguration();
            Set(configuration, "m_qualityLevel", level); // serialized in the original
            Set(configuration, "m_renderScale", scale);
            return configuration;
        }

        private static void SetMsaa(Rig rig, Array settings) => Set(rig.Container, "m_msaaSettings", settings);

        // Awake 0x67DDC0: the page opens on the active performance profile (the medium one, at index 1), the quality manager's
        // render scale and the pipeline's anti-aliasing, and shows them (SetQualityValue 0x67E3F0).
        [Test]
        public void AwakeOpensOnTheActiveProfileAndTheLiveRenderSettings()
        {
            Run(false, (owned, rig) =>
            {
                rig.Performance.SetPerformanceProfile(rig.High);
                rig.Pipeline.msaaSampleCount = 4;

                Call(rig.Container, "Awake");

                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(2));
                Assert.That(rig.Container.QualitySettingName.Value, Is.EqualTo("High text"));
                Assert.That(rig.QualitySlider.value, Is.EqualTo(2f));
                Assert.That(rig.Container.ResolutionSettingString.Value, Is.EqualTo("0.7"));
                Assert.That(rig.ResolutionSlider.value, Is.EqualTo(0.7f).Within(1e-6f));
                Assert.That(rig.Container.MsaaSettingString.Value, Is.EqualTo("4x AA"));
                Assert.That(rig.MsaaSlider.value, Is.EqualTo(2f));
                Assert.That(rig.Performance.ActiveProfile, Is.SameAs(rig.High));
            });
        }

        // The loop of SetCurrentProfile (0x67DFF0..0x67E05A) stops at the first level whose profile equals the active one
        // (by GUID): of two levels sharing a profile identity the first is shown.
        [Test]
        public void TheFirstLevelHoldingTheActiveProfileIsShown()
        {
            Run(false, (owned, rig) =>
            {
                PerformanceProfile twin = NewProfile(owned, "lucid-low", "lucid-twin-profile", PerformanceProfile.QualityLevel.Low,
                    PerformanceProfile.QualityLevel.Low, rig.Locked, rig.Free);
                Set(rig.Container, "m_qualityLevels_macOS", Levels((rig.Low, Strings.ACT_1), (rig.Medium, Strings.ACT_2), (twin, Strings.ACT_3)));
                rig.Performance.SetPerformanceProfile(rig.Low);

                Call(rig.Container, "Awake");

                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(0), "not the later level of the same profile");
                Assert.That(rig.Container.QualitySettingName.Value, Is.EqualTo("Low text"));
            });
        }

        // No level holds the active profile (0x67E04D): the middle level, the count halved, is shown.
        [Test]
        public void WithoutAMatchingLevelTheMiddleLevelIsShown()
        {
            Run(false, (owned, rig) =>
            {
                PerformanceProfile other = NewProfile(owned, "lucid-other", "lucid-other-profile", PerformanceProfile.QualityLevel.High,
                    PerformanceProfile.QualityLevel.High, rig.Locked, rig.Free);
                rig.Performance.SetPerformanceProfile(other);

                Call(rig.Container, "Awake");

                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(1));
                Assert.That(rig.Performance.ActiveProfile, Is.SameAs(rig.Medium), "the shown level's profile becomes the active one");
                Assert.That(Get<List<PerformanceAttribute>>(rig.Medium, "m_attributes")[0].Level, Is.EqualTo(PerformanceProfile.QualityLevel.High),
                    "the device-locked level of the previous profile is carried over");
            });
        }

        // The loop over the device-locked features (0x67E4B0..0x67E4E6) reads the default profile's level first and then writes it
        // to the chosen profile's attribute. The default profile is the active one, so the locked level follows the last choice
        // while the free feature keeps its own.
        [Test]
        public void ChoosingALevelGivesItsProfileTheDefaultProfilesLockedLevels()
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnQualitySliderUpdated(0f);

                Assert.That(rig.Performance.ActiveProfile, Is.SameAs(rig.Low));
                Assert.That(Get<List<PerformanceAttribute>>(rig.Low, "m_attributes")[0].Level, Is.EqualTo(PerformanceProfile.QualityLevel.Medium),
                    "the medium profile's locked level replaces very low");
                Assert.That(Get<List<PerformanceAttribute>>(rig.Low, "m_attributes")[1].Level, Is.EqualTo(PerformanceProfile.QualityLevel.Low));

                rig.Container.Action_OnQualitySliderUpdated(2f);

                Assert.That(Get<List<PerformanceAttribute>>(rig.High, "m_attributes")[0].Level, Is.EqualTo(PerformanceProfile.QualityLevel.Medium),
                    "the active low profile now holds the medium locked level");
                Assert.That(Get<List<PerformanceAttribute>>(rig.High, "m_attributes")[1].Level, Is.EqualTo(PerformanceProfile.QualityLevel.High));
            });
        }

        // Action_OnQualitySliderUpdated 0x67E600 rounds the slider value to the nearest level, halves to even.
        [TestCase(0.5f, 0, TestName = "ALowHalfRoundsDownToTheEvenLevel")]
        [TestCase(1.5f, 2, TestName = "AMiddleHalfRoundsUpToTheEvenLevel")]
        [TestCase(2.5f, 2, TestName = "AHighHalfRoundsDownToTheEvenLevel")]
        [TestCase(1.4f, 1, TestName = "ABelowHalfValueRoundsDown")]
        public void TheQualitySliderValueIsRoundedHalvesToEven(float value, int expected)
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnQualitySliderUpdated(value);

                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(expected));
                Assert.That(rig.QualitySlider.value, Is.EqualTo((float)expected));
            });
        }

        // SetQualityValue 0x67E3F0 shows the level's name and the menu render scale (EnableMenuRenderScale 0x5F6270): the
        // pipeline's scale becomes 1 and the menu camera is switched on, whatever they were.
        [Test]
        public void ChoosingALevelShowsItsNameAndRestoresTheMenuRenderScale()
        {
            Run(true, (owned, rig) =>
            {
                rig.Pipeline.renderScale = 0.6f;
                rig.MenuCamera.enabled = false;

                rig.Container.Action_OnQualitySliderUpdated(0f);

                Assert.That(rig.Container.QualitySettingName.Value, Is.EqualTo("Low text"));
                Assert.That(rig.Pipeline.renderScale, Is.EqualTo(1f));
                Assert.That(rig.MenuCamera.enabled, Is.True);
            });
        }

        // Action_OnResolutionSliderUpdated 0x67E710 stores the scale, moves the slider without notifying and shows the scale
        // with one decimal (the literal "0.0", rounded). The quality manager keeps its own scale until the page closes.
        [Test]
        public void TheResolutionSliderIsShownWithOneDecimal()
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnResolutionSliderUpdated(0.86f);

                Assert.That(rig.Container.ResolutionSettingString.Value, Is.EqualTo("0.9"));
                Assert.That(rig.ResolutionSlider.value, Is.EqualTo(0.86f).Within(1e-6f));
                Assert.That(rig.Quality.GameplayRenderScale, Is.EqualTo(0.7f));
            });
        }

        // Action_OnMsaaSliderUpdated 0x67E810 rounds the value (halves to even) to a setting, takes its sample count (+0x10) and
        // shows its name (UpdateMSAASlider 0x67EB70): the name format is filled with the sample count only when it has a
        // placeholder (0x67EBFF String.Contains('{')). The pipeline keeps its anti-aliasing until the page closes.
        [TestCase(0.5f, 0, "Off", TestName = "ALowHalfSelectsTheFirstAntiAliasingSetting")]
        [TestCase(2.5f, 2, "4x AA", TestName = "AHighHalfSelectsTheEvenAntiAliasingSetting")]
        [TestCase(3f, 3, "8x Plus", TestName = "AWholeValueSelectsThatAntiAliasingSetting")]
        public void TheAntiAliasingSliderSelectsASettingAndShowsItsName(float value, int index, string name)
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnMsaaSliderUpdated(value);

                Assert.That(rig.MsaaSlider.value, Is.EqualTo((float)index));
                Assert.That(rig.Container.MsaaSettingString.Value, Is.EqualTo(name));
                Assert.That(rig.Pipeline.msaaSampleCount, Is.EqualTo(2));
            });
        }

        // GetMsaaSettingIndex 0x67EC50 finds the first setting with the sample count.
        [Test]
        public void TwoSettingsWithTheSameSampleCountShowTheFirst()
        {
            Run(false, (owned, rig) =>
            {
                SetMsaa(rig, Msaa((2, Strings.ZONE_1), (2, Strings.ZONE_2), (4, Strings.LANGUAGE_ENGLISH_AUS)));

                Call(rig.Container, "Awake");

                Assert.That(rig.Container.MsaaSettingString.Value, Is.EqualTo("first 2"));
                Assert.That(rig.MsaaSlider.value, Is.EqualTo(0f));
            });
        }

        // MSAASetting.GetLocalisedName 0x67ED10 returns a name without a placeholder as it is.
        [Test]
        public void ANameWithoutAPlaceholderIsNotFormatted()
        {
            Run(false, (owned, rig) =>
            {
                object setting = Msaa((8, Strings.LANGUAGE_ENGLISH)).GetValue(0);
                object formatted = Msaa((8, Strings.LANGUAGE_ENGLISH_UK)).GetValue(0);

                Assert.That(Call(setting, "GetLocalisedName"), Is.EqualTo("Off"));
                Assert.That(Call(formatted, "GetLocalisedName"), Is.EqualTo("8x MSAA"));
            });
        }

        // OnDestroy 0x67E080 compares each shown setting with the one the page opened on. Nothing changed: nothing is reported,
        // and the settings are saved with the active profile's name and the render scale (<OnDestroy>b__20_0 0x67EF30).
        [Test]
        public void ClosingUnchangedReportsNothingAndSavesTheProfileAndScale()
        {
            Run(true, (owned, rig) =>
            {
                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Page.Reported.Sent, Is.False);
                Assert.That(rig.Page.Settings.PerformanceProfileName, Is.EqualTo("lucid-medium-profile"));
                Assert.That(rig.Page.Settings.RenderScale, Is.EqualTo(0.7f));
                Assert.That(SavedJson(rig.Page), Does.Contain("lucid-medium-profile"));
                Assert.That(rig.Pipeline.msaaSampleCount, Is.EqualTo(2));
                Assert.That(rig.Quality.GameplayRenderScale, Is.EqualTo(0.7f));
            });
        }

        // The quality level is reported with the level the page opened on and the shown one (0x67E138..0x67E18F).
        [Test]
        public void ClosingWithAnotherLevelReportsTheQuality()
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnQualitySliderUpdated(2f);

                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Page.Reported.Last, Is.EqualTo(("graphics_quality", "1", "2")));
                Assert.That(rig.Page.Settings.PerformanceProfileName, Is.EqualTo("lucid-high-profile"));
            });
        }

        // The resolution scale is reported with the quality manager's scale and the page's (0x67E248..0x67E299), and the
        // manager takes the page's value either way (0x67E1DC, 0x67E2A3).
        [Test]
        public void ClosingWithAnotherResolutionScaleReportsItAndAppliesIt()
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnResolutionSliderUpdated(0.9f);

                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Page.Reported.Last, Is.EqualTo(("graphics_resolution_scale", "0.7", "0.9")));
                Assert.That(rig.Quality.GameplayRenderScale, Is.EqualTo(0.9f));
                Assert.That(rig.Page.Settings.RenderScale, Is.EqualTo(0.9f));
            });
        }

        // The anti-aliasing is reported with the pipeline's sample count and the page's (0x67E2DD..0x67E32C), and the pipeline
        // takes the page's value (0x67E334).
        [Test]
        public void ClosingWithAnotherAntiAliasingReportsItAndAppliesIt()
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnMsaaSliderUpdated(2f);

                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Page.Reported.Last, Is.EqualTo(("graphics_anti_aliasing", "2", "4")));
                Assert.That(rig.Pipeline.msaaSampleCount, Is.EqualTo(4));
            });
        }

        // The three settings are checked in the order quality, resolution scale, anti-aliasing, so the last report is the
        // anti-aliasing one; all three are applied.
        [Test]
        public void ClosingWithEverythingChangedReportsTheAntiAliasingLast()
        {
            Run(true, (owned, rig) =>
            {
                rig.Container.Action_OnQualitySliderUpdated(2f);
                rig.Container.Action_OnResolutionSliderUpdated(0.9f);
                rig.Container.Action_OnMsaaSliderUpdated(3f);

                Call(rig.Container, "OnDestroy");

                Assert.That(rig.Page.Reported.Last, Is.EqualTo(("graphics_anti_aliasing", "2", "8")));
                Assert.That(rig.Quality.GameplayRenderScale, Is.EqualTo(0.9f));
                Assert.That(rig.Pipeline.msaaSampleCount, Is.EqualTo(8));
                Assert.That(rig.Page.Settings.PerformanceProfileName, Is.EqualTo("lucid-high-profile"));
            });
        }

        // RefreshBinders 0x67EA60 follows the active profile: the level holding it is shown again.
        [Test]
        public void RefreshingShowsTheLevelOfTheActiveProfile()
        {
            Run(true, (owned, rig) =>
            {
                rig.Performance.SetPerformanceProfile(rig.High);

                Call(rig.Container, "RefreshBinders");

                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(2));
                Assert.That(rig.Container.QualitySettingName.Value, Is.EqualTo("High text"));
            });
        }

        // ResetToDefaults 0x67E970 returns to the device default profile (the active one here; its render scale feature level
        // low gives 0.5), takes the scale and the pipeline's anti-aliasing, restores the menu render scale, and the reset button
        // (the base class's Action_ResetToDefaults) then refreshes the page from them.
        [Test]
        public void ResettingReturnsToTheDefaultProfileScaleAndAntiAliasing()
        {
            Run(false, (owned, rig) =>
            {
                rig.Performance.SetPerformanceProfile(rig.High);
                Call(rig.Container, "Awake");
                rig.Container.Action_OnResolutionSliderUpdated(0.9f);
                rig.Container.Action_OnMsaaSliderUpdated(0f);
                rig.Pipeline.renderScale = 0.6f;
                rig.MenuCamera.enabled = false;

                rig.Container.Action_ResetToDefaults();

                Assert.That(rig.Quality.GameplayRenderScale, Is.EqualTo(0.5f));
                Assert.That(rig.Container.ResolutionSettingString.Value, Is.EqualTo("0.5"));
                Assert.That(rig.Container.MsaaSettingString.Value, Is.EqualTo("2x MSAA"), "the pipeline's own anti-aliasing");
                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(2));
                Assert.That(rig.Pipeline.renderScale, Is.EqualTo(1f));
                Assert.That(rig.MenuCamera.enabled, Is.True);
            });
        }

        // The constructor 0x67EDB0 builds the four binders in declaration order, each its own object.
        [Test]
        public void TheConstructorBuildsFourSeparateBinders()
        {
            Run(false, (owned, rig) =>
            {
                object[] binders =
                {
                    rig.Container.QualitySetting, rig.Container.QualitySettingName, rig.Container.ResolutionSettingString,
                    rig.Container.MsaaSettingString,
                };
                Assert.That(binders, Is.All.Not.Null);
                Assert.That(binders, Is.Unique);
                Assert.That(rig.Container.QualitySetting.Value, Is.EqualTo(0));
            });
        }
    }
}
