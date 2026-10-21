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
    // Original Game.Runtime 0x02000931; bodies translated from the 1.10.1 x86-64 binary.
    // The controls page: movement stick style, stick and camera sensitivity, air ability hold and button mappings.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerSettingsControls : UIContainerSettingsBase
    {
        public readonly Bindable<bool> FixedMovementStick = new Bindable<bool>();
        public readonly Bindable<bool> DynamicMovementStick = new Bindable<bool>();
        public readonly Bindable<bool> CanAdjustControls = new Bindable<bool>();
        public readonly Bindable<bool> CanUseOnScreenControls = new Bindable<bool>();
        public readonly Bindable<int> ControlStickSensitivityIndex = new Bindable<int>();
        public readonly Bindable<Strings> ControlStickSensitivityString = new Bindable<Strings>();
        public readonly Bindable<int> CameraSecondarySensitivityIndex = new Bindable<int>();
        public readonly Bindable<Strings> CameraSecondarySensitivityString = new Bindable<Strings>();
        public readonly Bindable<bool> AirControlContinuous = new Bindable<bool>();
        public readonly Bindable<ButtonMappingType> HomingAttackMapping = new Bindable<ButtonMappingType>();
        public readonly Bindable<ButtonMappingType> AirStompAttackMapping = new Bindable<ButtonMappingType>();
        [SerializeField] private List<EnumValuePair<InputModifierType, Strings>> m_controlStickSensitivity;
        [SerializeField] private List<EnumValuePair<CameraSensitivityType, Strings>> m_cameraSecondarySensitivity;
        private int m_cachedControlStickSensitivity;
        private int m_cachedCameraSecondarySensitivity;

        // 0x060034e0 (0x67BD00)
        private void Awake()
        {
            RefreshBinders();
            m_cachedControlStickSensitivity = ControlStickSensitivityIndex.Value;
            m_cachedCameraSecondarySensitivity = CameraSecondarySensitivityIndex.Value;
        }

        // 0x060034e1 (0x67BD60): changes made on the page are reported once, when it closes.
        private void OnDestroy()
        {
            if (m_cachedControlStickSensitivity != ControlStickSensitivityIndex.Value)
                AnalyticsEventCollector.SettingChangedEvent("control_stick_sensitivity",
                    m_controlStickSensitivity[m_cachedControlStickSensitivity].Value.GetString(),
                    ControlStickSensitivityString.Value.GetString());

            if (m_cachedCameraSecondarySensitivity == CameraSecondarySensitivityIndex.Value)
                return;
            AnalyticsEventCollector.SettingChangedEvent("camera_secondary_sensitivity",
                m_cameraSecondarySensitivity[m_cachedCameraSecondarySensitivity].Value.GetString(),
                CameraSecondarySensitivityString.Value.GetString());
        }

        // 0x060034e2 (0x67BF60): the two "can" flags are the constant false of the retail build.
        protected override void RefreshBinders()
        {
            SaveDataSettings saveDataSettings = m_saveManagerRef.Get().GetSaveDataSettings();
            FixedMovementStick.Value = !saveDataSettings.DynamicMovementStick;
            DynamicMovementStick.Value = saveDataSettings.DynamicMovementStick;
            AirControlContinuous.Value = saveDataSettings.AirAbilityHold;
            HomingAttackMapping.Value = saveDataSettings.HomingAttackMapping;
            AirStompAttackMapping.Value = saveDataSettings.AirStompAttackMapping;
            ControlStickSensitivityIndex.Value = m_controlStickSensitivity.FindIndex(data => data.Key == saveDataSettings.ControlStickSensitivity);
            ControlStickSensitivityString.Value = m_controlStickSensitivity[ControlStickSensitivityIndex.Value].Value;
            CameraSecondarySensitivityIndex.Value = m_cameraSecondarySensitivity.FindIndex(data => data.Key == saveDataSettings.CameraSecondarySensitivity);
            CameraSecondarySensitivityString.Value = m_cameraSecondarySensitivity[CameraSecondarySensitivityIndex.Value].Value;
            CanAdjustControls.Value = false;
            CanUseOnScreenControls.Value = false;
        }

        // 0x060034e3 (0x67C290)
        protected override void ResetToDefaults(SaveDataSettings saveDataSettings)
        {
            saveDataSettings.SetControlsDefaults();
        }

        // 0x060034e4 (0x67C2B0)
        private void SetHomingAttackMapping(ButtonMappingType homingAttackMapping)
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("homing_attack_mapping", data.HomingAttackMapping, homingAttackMapping);
                data.HomingAttackMapping = homingAttackMapping;
            });
        }

        // 0x060034e5 (0x67C3E0)
        public void Action_SetHomingAttackMappingAll()
        {
            SetHomingAttackMapping(ButtonMappingType.All);
        }

        // 0x060034e6 (0x67C3F0)
        public void Action_SetHomingAttackMappingPrimary()
        {
            SetHomingAttackMapping(ButtonMappingType.Primary);
        }

        // 0x060034e7 (0x67C400)
        public void Action_SetHomingAttackMappingSecondary()
        {
            SetHomingAttackMapping(ButtonMappingType.Secondary);
        }

        // 0x060034e8 (0x67C410)
        private void SetAirStompAttackMapping(ButtonMappingType airStompAttackMapping)
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("air_stomp_attack_mapping", data.AirStompAttackMapping, airStompAttackMapping);
                data.AirStompAttackMapping = airStompAttackMapping;
            });
        }

        // 0x060034e9 (0x67C540)
        public void Action_SetAirStompAttackMappingAll()
        {
            SetAirStompAttackMapping(ButtonMappingType.All);
        }

        // 0x060034ea (0x67C550)
        public void Action_SetAirStompAttackMappingPrimary()
        {
            SetAirStompAttackMapping(ButtonMappingType.Primary);
        }

        // 0x060034eb (0x67C560)
        public void Action_SetAirStompAttackMappingSecondary()
        {
            SetAirStompAttackMapping(ButtonMappingType.Secondary);
        }

        // 0x060034ec (0x67C570): the fixed stick is the absence of the dynamic one, so both report "dynamic_stick_movement".
        public void Action_SetFixedMovementControls()
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("dynamic_stick_movement", data.DynamicMovementStick, false);
                data.DynamicMovementStick = false;
            });
        }

        // 0x060034ed (0x67C650)
        public void Action_SetDynamicMovementControls()
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("dynamic_stick_movement", data.DynamicMovementStick, true);
                data.DynamicMovementStick = true;
            });
        }

        // 0x060034ee (0x67C730): the slider value is rounded to the nearest list index (halves to even).
        public void Action_SetControlStickSensitivity(float value)
        {
            int index = (int)Math.Round(value);
            SetSaveData(data => data.ControlStickSensitivity = m_controlStickSensitivity[index].Key);
        }

        // 0x060034ef (0x67C960)
        public void Action_SetCameraSecondarySensitivity(float value)
        {
            int index = (int)Math.Round(value);
            SetSaveData(data => data.CameraSecondarySensitivity = m_cameraSecondarySensitivity[index].Key);
        }

        // 0x060034f0 (0x67CB90)
        public void Action_SetAirAbilityHold(bool value)
        {
            SetSaveData(data =>
            {
                SendAnalyticsEventOnSettingChanged("air_control_continuous", data.AirAbilityHold, value);
                data.AirAbilityHold = value;
            });
        }

        // 0x060034f1 (0x67CCC0)
        public UIContainerSettingsControls() { }
    }
}
