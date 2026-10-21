using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200092b; bodies translated from the 1.10.1 x86-64 binary.
    // The camera page: recentre behaviour, inverted controls, distance and sensitivity.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSettingsCamera : UIContainerSettingsBase
    {
        public readonly Bindable<CameraRecenterHeadingType> CameraRecenterHeading = new Bindable<CameraRecenterHeadingType>();
        public readonly Bindable<bool> CameraInvertedControls = new Bindable<bool>();
        public readonly Bindable<int> CameraConfigurationIndex = new Bindable<int>();
        public readonly Bindable<int> CameraSensitivityIndex = new Bindable<int>();
        public readonly Bindable<Strings> CameraSensitivityString = new Bindable<Strings>();
        public readonly Bindable<bool> CameraSnapBehindCharacterButtonVisible = new Bindable<bool>();
        public readonly Bindable<Strings> CameraDistanceString = new Bindable<Strings>();
        [SerializeField] private List<CameraConfigurationType> m_cameraConfiguration;
        [SerializeField] private List<EnumValuePair<CameraSensitivityType, Strings>> m_cameraSensitivity;
        [SerializeField] private SerializableDictionary<CameraConfigurationType, Strings> m_cameraDistanceStrings =
            new SerializableDictionary<CameraConfigurationType, Strings>(HardlightEnumComparers.CameraConfigurationTypeComparer);
        private int m_cachedCameraDistance;
        private int m_cachedCameraSensitivity;

        // 0x060034c6 (0x67A8A0)
        private void Awake()
        {
            RefreshBinders();
            m_cachedCameraDistance = CameraConfigurationIndex.Value;
            m_cachedCameraSensitivity = CameraSensitivityIndex.Value;
        }

        // 0x060034c7 (0x67A900): changes made on the page are reported once, when it closes.
        private void OnDestroy()
        {
            if (m_cachedCameraDistance != CameraConfigurationIndex.Value)
            {
                CameraConfigurationType previous = m_cameraConfiguration[m_cachedCameraDistance];
                CameraConfigurationType current = m_cameraConfiguration[CameraConfigurationIndex.Value];
                AnalyticsEventCollector.SettingChangedEvent("camera_distance", previous.GetString(), current.GetString());
            }

            if (m_cachedCameraSensitivity == CameraSensitivityIndex.Value)
                return;
            AnalyticsEventCollector.SettingChangedEvent("camera_sensitivity",
                m_cameraSensitivity[m_cachedCameraSensitivity].Value.GetString(), CameraSensitivityString.Value.GetString());
        }

        // 0x060034c8 (0x67AB10)
        protected override void RefreshBinders()
        {
            SaveDataSettings saveDataSettings = m_saveManagerRef.Get().GetSaveDataSettings();
            CameraRecenterHeading.Value = saveDataSettings.CameraRecenterHeadingType;
            CameraInvertedControls.Value = saveDataSettings.CameraInvertedControls;
            CameraSnapBehindCharacterButtonVisible.Value = saveDataSettings.CameraSnapBehindCharacterButtonVisible;
            CameraConfigurationIndex.Value = m_cameraConfiguration.FindIndex(data => data == saveDataSettings.CameraConfiguration);
            CameraSensitivityIndex.Value = m_cameraSensitivity.FindIndex(data => data.Key == saveDataSettings.CameraSensitivity);
            CameraSensitivityString.Value = m_cameraSensitivity[CameraSensitivityIndex.Value].Value;
            if (m_cameraDistanceStrings.TryGetValue(saveDataSettings.CameraConfiguration, out Strings distanceString))
                CameraDistanceString.Value = distanceString;
        }

        // 0x060034c9 (0x67ADC0)
        protected override void ResetToDefaults(SaveDataSettings saveDataSettings)
        {
            saveDataSettings.SetCameraDefaults();
        }

        // 0x060034ca (0x67ADE0): an unchanged heading saves nothing but still refreshes the page. The page names the
        // two headings "Auto" (medium) and "Manual" (anything else) in its analytics.
        private void SetCameraHeadingType(CameraRecenterHeadingType cameraRecenterHeadingType)
        {
            SetSaveData(data =>
            {
                if (data.CameraRecenterHeadingType == cameraRecenterHeadingType)
                    return;
                AnalyticsEventCollector.SettingChangedEvent("camera_recenter_heading_type",
                    data.CameraRecenterHeadingType == CameraRecenterHeadingType.Medium ? "Auto" : "Manual",
                    cameraRecenterHeadingType == CameraRecenterHeadingType.Medium ? "Auto" : "Manual");
                data.CameraRecenterHeadingType = cameraRecenterHeadingType;
            });
        }

        // 0x060034cb (0x67AF00)
        public void Action_SetCameraHeadingTypeNone()
        {
            SetCameraHeadingType(CameraRecenterHeadingType.None);
        }

        // 0x060034cc (0x67AF10)
        public void Action_SetCameraHeadingTypeMedium()
        {
            SetCameraHeadingType(CameraRecenterHeadingType.Medium);
        }

        // 0x060034cd (0x67AF20)
        public void Action_SetCameraControlsNormal()
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("camera_inverted_controls", data.CameraInvertedControls, false);
                data.CameraInvertedControls = false;
            });
        }

        // 0x060034ce (0x67B000)
        public void Action_SetCameraControlsInverted()
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("camera_inverted_controls", data.CameraInvertedControls, true);
                data.CameraInvertedControls = true;
            });
        }

        // 0x060034cf (0x67B0E0)
        public void Action_SetCameraSnapBehindCharacterButtonVisible(bool visible)
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("camera_snap_behind_character_button_visible", data.CameraSnapBehindCharacterButtonVisible, visible);
                data.CameraSnapBehindCharacterButtonVisible = visible;
            });
        }

        // 0x060034d0 (0x67B210): the slider value is rounded to the nearest configuration index (halves to even).
        public void Action_SetCameraConfiguration(float value)
        {
            int index = (int)Math.Round(value);
            SetSaveData(data => data.CameraConfiguration = m_cameraConfiguration[index]);
        }

        // 0x060034d1 (0x67B440)
        public void Action_SetCameraSensitivity(float value)
        {
            int index = (int)Math.Round(value);
            SetSaveData(data => data.CameraSensitivity = m_cameraSensitivity[index].Key);
        }

        // 0x060034d2 (0x67B670)
        public UIContainerSettingsCamera() { }
    }
}
