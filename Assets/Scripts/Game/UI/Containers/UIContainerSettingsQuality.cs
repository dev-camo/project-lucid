using System;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using JetBrains.Annotations;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200093c; bodies translated from the 1.10.1 x86-64 binary.
    // The graphics page: the quality level (a performance profile), the resolution scale and the anti-aliasing level.
    // The chosen quality level becomes the active profile at once; the resolution scale and anti-aliasing level are
    // held by the page and applied, reported and saved when it closes.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerSettingsQuality : UIContainerSettingsBase
    {
        // Original nested types 0x0200093d and 0x0200093e.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        private sealed class MSAASetting
        {
            public int SampleCount;
            [HashEnum(typeof(Strings))]
            public Strings NameFormat;

            // 0x0600351f (0x67ED10): a name that holds a placeholder is formatted with the sample count.
            public string GetLocalisedName()
            {
                string nameFormat = StringTable.GetString(NameFormat);
                if (nameFormat.Contains('{'))
                    return string.Format(nameFormat, SampleCount.ToString());
                return nameFormat;
            }

        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        private sealed class QualityLevel
        {
            public PerformanceProfile Profile;
            [HashEnum(null)]
            public Strings LocalisedName;
        }

        public readonly Bindable<int> QualitySetting = new Bindable<int>();
        public readonly Bindable<string> QualitySettingName = new Bindable<string>();
        public readonly Bindable<string> ResolutionSettingString = new Bindable<string>();
        public readonly Bindable<string> MsaaSettingString = new Bindable<string>();
        [UsedImplicitly]
        [SerializeField] private QualityLevel[] m_qualityLevels_iOS;
        [SerializeField]
        [UsedImplicitly]
        private QualityLevel[] m_qualityLevels_iOS_iPad;
        [UsedImplicitly]
        [SerializeField]
        private QualityLevel[] m_qualityLevels_tvOS;
        [SerializeField] private QualityLevel[] m_qualityLevels_macOS;
        [SerializeField] private MSAASetting[] m_msaaSettings;
        [SerializeField] private UIWidgetSDTSlider m_resolutionSlider;
        [SerializeField] private UIWidgetSDTSlider m_qualitySlider;
        [SerializeField] private UIWidgetSDTSlider m_msaaSlider;
        private QualityLevel[] m_performanceProfiles;
        private ScalablePerformance m_scalablePerformance;
        private float m_resolutionScale;
        private int m_msaaSampleCount;
        private int m_cachedGraphicsQuality;

        // 0x0600350d (0x67DDC0): the page opens on the live render settings and the active performance profile.
        private void Awake()
        {
            m_scalablePerformance = ProcessManager.GetSystem<ScalablePerformance>();
            UniversalRenderPipelineAsset renderPipelineAsset = GetRenderPipelineAsset();
            m_resolutionScale = ProcessManager.GetSystem<VisualQualityManager_SDT>().GameplayRenderScale;
            m_msaaSampleCount = renderPipelineAsset.msaaSampleCount;
            SetPlatformPerformanceProfiles();
            SetCurrentProfile();
        }

        // 0x0600350e (0x67E080): each changed setting is reported (quality, resolution scale, anti-aliasing), the
        // resolution scale and anti-aliasing level are applied to the live rendering and the settings are saved.
        private void OnDestroy()
        {
            if (m_cachedGraphicsQuality != QualitySetting.Value)
                AnalyticsEventCollector.SettingChangedEvent("graphics_quality", m_cachedGraphicsQuality.ToString(), QualitySetting.Value.ToString());

            VisualQualityManager_SDT qualityManager = ProcessManager.GetSystem<VisualQualityManager_SDT>();
            if (qualityManager.GameplayRenderScale != m_resolutionScale)
                AnalyticsEventCollector.SettingChangedEvent("graphics_resolution_scale", qualityManager.GameplayRenderScale.ToString(), m_resolutionScale.ToString());
            qualityManager.GameplayRenderScale = m_resolutionScale;

            UniversalRenderPipelineAsset renderPipelineAsset = GetRenderPipelineAsset();
            if (renderPipelineAsset.msaaSampleCount != m_msaaSampleCount)
                AnalyticsEventCollector.SettingChangedEvent("graphics_anti_aliasing", renderPipelineAsset.msaaSampleCount.ToString(), m_msaaSampleCount.ToString());
            renderPipelineAsset.msaaSampleCount = m_msaaSampleCount;

            SetSaveData(settings =>
            {
                settings.PerformanceProfileName = m_scalablePerformance.ActiveProfile.name;
                settings.RenderScale = m_resolutionScale;
            });
        }

        // 0x0600350f (0x67DFD0): the macOS player is the only platform this build selects; the iOS, iPad and tvOS
        // level lists stay serialized and unread.
        private void SetPlatformPerformanceProfiles()
        {
            m_performanceProfiles = m_qualityLevels_macOS;
        }

        // 0x06003510 (0x67DFF0): the level whose profile is active, or the middle level when none is.
        private void SetCurrentProfile()
        {
            int currentLevel = m_performanceProfiles.Length / 2;
            for (int i = 0; i < m_performanceProfiles.Length; i++)
            {
                if (m_performanceProfiles[i].Profile == m_scalablePerformance.ActiveProfile)
                {
                    currentLevel = i;
                    break;
                }
            }

            m_cachedGraphicsQuality = currentLevel;
            SetQualityValue(currentLevel);
        }

        // 0x06003511 (0x67E600): the slider value is rounded to the nearest level (halves to even). The body is an
        // inlined Mathf.RoundToInt: its _inline method-initialized flag (0x319EAAE) is checked at 0x67E611.
        public void Action_OnQualitySliderUpdated(float qualityValue)
        {
            SetQualityValue(Mathf.RoundToInt(qualityValue));
        }

        // 0x06003512 (0x67E710)
        public void Action_OnResolutionSliderUpdated(float resolutionScale)
        {
            SetResolutionScale(resolutionScale);
        }

        // 0x06003513 (0x67E810): the slider value is rounded to the nearest setting (halves to even); the same inlined
        // Mathf.RoundToInt, its flag checked at 0x67E821.
        public void Action_OnMsaaSliderUpdated(float msaaSetting)
        {
            SetMSAAValue(Mathf.RoundToInt(msaaSetting));
        }

        // 0x06003514 (0x67E970): the saved settings passed in are not used; the page returns to the device default
        // profile and takes the render scale and anti-aliasing the live rendering has.
        protected override void ResetToDefaults(SaveDataSettings saveDataSettings)
        {
            VisualQualityManager_SDT qualityManager = ProcessManager.GetSystem<VisualQualityManager_SDT>();
            qualityManager.SetDefaultPerformanceProfile();
            UniversalRenderPipelineAsset renderPipelineAsset = GetRenderPipelineAsset();
            m_resolutionScale = qualityManager.GameplayRenderScale;
            m_msaaSampleCount = renderPipelineAsset.msaaSampleCount;
            qualityManager.EnableMenuRenderScale();
        }

        // 0x06003515 (0x67EA60)
        protected override void RefreshBinders()
        {
            SetCurrentProfile();
        }

        // 0x06003516 (0x67E3F0): the chosen level's profile takes the device's own level for every feature the device
        // locks, becomes the active profile and is shown, then the resolution and anti-aliasing sliders follow and
        // the menu render scale is restored.
        private void SetQualityValue(int qualityValue)
        {
            QualityLevel level = m_performanceProfiles[qualityValue];
            VisualQualityManager_SDT qualityManager = ProcessManager.GetSystem<VisualQualityManager_SDT>();
            PerformanceProfile defaultProfile = qualityManager.GetDefaultPerformanceProfile();
            foreach (ScalableFeature feature in qualityManager.DeviceLockedFeatures)
            {
                PerformanceProfile.QualityLevel lockedLevel = defaultProfile.GetAttribute(feature).Level;
                level.Profile.GetAttribute(feature).Level = lockedLevel;
            }

            m_scalablePerformance.SetPerformanceProfile(level.Profile);
            m_qualitySlider.SetValueWithoutNotify(qualityValue);
            QualitySetting.Value = qualityValue;
            QualitySettingName.Value = StringTable.GetString(level.LocalisedName);
            UpdateResolutionSlider();
            UpdateMSAASlider();
            qualityManager.EnableMenuRenderScale();
        }

        // 0x06003517 (0x67EAF0)
        private void UpdateResolutionSlider()
        {
            m_resolutionSlider.SetValueWithoutNotify(m_resolutionScale);
            ResolutionSettingString.Value = m_resolutionScale.ToString("0.0");
        }

        // 0x06003518 (0x67EB70)
        private void UpdateMSAASlider()
        {
            int index = GetMsaaSettingIndex(m_msaaSampleCount);
            m_msaaSlider.SetValueWithoutNotify(index);
            MsaaSettingString.Value = m_msaaSettings[index].GetLocalisedName();
        }

        // 0x06003519 (0x67E790)
        private void SetResolutionScale(float resolutionScale)
        {
            m_resolutionScale = resolutionScale;
            UpdateResolutionSlider();
        }

        // 0x0600351a (0x67E940)
        private void SetMSAAValue(int index)
        {
            m_msaaSampleCount = m_msaaSettings[index].SampleCount;
            UpdateMSAASlider();
        }

        // 0x0600351b (0x67EC50): the first setting with the sample count; -1 when there is none.
        private int GetMsaaSettingIndex(int sampleCount)
        {
            return m_msaaSettings.FindIndex(setting => setting.SampleCount == sampleCount);
        }

        // 0x0600351c (0x67DF60)
        private UniversalRenderPipelineAsset GetRenderPipelineAsset()
        {
            return QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        }

        // 0x0600351d (0x67EDB0)
        public UIContainerSettingsQuality() { }
    }
}
