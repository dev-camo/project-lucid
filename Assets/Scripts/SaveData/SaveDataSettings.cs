using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataSettings : SaveDataResolvableItem<SaveDataSettings>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private float m_masterVolume;
        [SerializeField]
        private float m_musicVolume;
        [SerializeField]
        private float m_sfxVolume;
        [SerializeField]
        private float m_voiceVolume;
        [SerializeField]
        private CameraRecenterHeadingType m_cameraRecenterHeadingType;
        [SerializeField]
        private bool m_cameraInvertedControls;
        [SerializeField]
        private CameraConfigurationType m_cameraConfiguration;
        [SerializeField]
        private CameraSensitivityType m_cameraSensitivity;
        [SerializeField]
        private bool m_cameraSnapBehindCharacterButtonVisible;
        [SerializeField]
        private bool m_dynamicMovementStick;
        [SerializeField]
        private InputModifierType m_controlStickSensitivity = InputModifierType.Medium;
        [SerializeField]
        private CameraSensitivityType m_cameraSecondarySensitivity = CameraSensitivityType.Medium;
        [SerializeField]
        private bool m_airAbilityHold;
        [SerializeField]
        private List<SaveDataSettingsEditableUIComponent> m_editableUIComponents = new List<SaveDataSettingsEditableUIComponent>();
        [SerializeField]
        private bool m_onScreenControlsFlipped;
        [SerializeField]
        private ButtonMappingType m_homingAttackMapping;
        [SerializeField]
        private ButtonMappingType m_airStompAttackMapping;
        [SerializeField]
        private float m_gameSpeed;
        [SerializeField]
        private AccessibilityFailState m_failState;
        [SerializeField]
        private bool m_gameSpeedToggleEnabled;
        [SerializeField]
        private bool m_subtitlesEnabled;
        [SerializeField]
        private SaveDataDebug m_debug;
        [SerializeField]
        private string m_performanceProfileName;
        [SerializeField]
        private float m_renderScale = 1f;
        [SerializeField]
        private string m_lastWhatsNewVersion = string.Empty;
        [SerializeField]
        private bool m_skipFTUE;
        private Action<CameraConfigurationType> m_onCameraConfigurationChanged;
        private Action<CameraSensitivityType> m_onCameraSensitivityChanged;
        private Action<CameraSensitivityType> m_onCameraSecondarySensitivityChanged;
        private const StringComparison EditableUIComponentsIDComparison = StringComparison.InvariantCultureIgnoreCase;
        private static readonly Dictionary<string, SaveDataSettingsEditableUIComponent> s_editableUIComponentsTempDictionary;
        // Game.Runtime.dll 0x06002cb4.
        public float MusicVolume
        {
            get
            {
                return m_musicVolume;
            }

            // 0x06002cb5
            set
            {
                m_musicVolume = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cb6.
        public float MasterVolume
        {
            get
            {
                return m_masterVolume;
            }

            // 0x06002cb7
            set
            {
                m_masterVolume = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cb8.
        public float VoiceVolume
        {
            get
            {
                return m_voiceVolume;
            }

            // 0x06002cb9
            set
            {
                m_voiceVolume = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cba.
        public float SfxVolume
        {
            get
            {
                return m_sfxVolume;
            }

            // 0x06002cbb
            set
            {
                m_sfxVolume = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cbc.
        public ButtonMappingType HomingAttackMapping
        {
            get
            {
                return m_homingAttackMapping;
            }

            // 0x06002cbd
            set
            {
                m_homingAttackMapping = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cbe.
        public ButtonMappingType AirStompAttackMapping
        {
            get
            {
                return m_airStompAttackMapping;
            }

            // 0x06002cbf
            set
            {
                m_airStompAttackMapping = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cc0.
        public CameraRecenterHeadingType CameraRecenterHeadingType
        {
            get
            {
                return m_cameraRecenterHeadingType;
            }

            // 0x06002cc1
            set
            {
                m_cameraRecenterHeadingType = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cc2.
        public bool CameraInvertedControls
        {
            get
            {
                return m_cameraInvertedControls;
            }

            // 0x06002cc3
            set
            {
                m_cameraInvertedControls = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cc4.
        public CameraConfigurationType CameraConfiguration
        {
            get
            {
                return m_cameraConfiguration;
            }

            // 0x06002cc5
            set
            {
                m_cameraConfiguration = value;
                m_onCameraConfigurationChanged?.Invoke(value);
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cc6.
        public AccessibilityFailState FailState
        {
            get
            {
                return m_failState;
            }

            // 0x06002cc7
            set
            {
                m_failState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cc8.
        public bool GameSpeedToggleEnabled
        {
            get
            {
                return m_gameSpeedToggleEnabled;
            }

            // 0x06002cc9
            set
            {
                m_gameSpeedToggleEnabled = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cca.
        public bool SubtitlesEnabled
        {
            get
            {
                return m_subtitlesEnabled;
            }

            // 0x06002ccb
            set
            {
                m_subtitlesEnabled = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ccc.
        public string PerformanceProfileName
        {
            get
            {
                return m_performanceProfileName;
            }

            // 0x06002ccd
            set
            {
                m_performanceProfileName = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cce.
        public float RenderScale
        {
            get
            {
                return m_renderScale;
            }

            // 0x06002ccf
            set
            {
                m_renderScale = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cd0.
        public string LastWhatsNewVersion
        {
            get
            {
                return m_lastWhatsNewVersion;
            }

            // 0x06002cd1
            set
            {
                m_lastWhatsNewVersion = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cd2.
        public bool SkipFTUE
        {
            get
            {
                return m_skipFTUE;
            }

            // 0x06002cd3
            set
            {
                m_skipFTUE = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cd6.
        public CameraSensitivityType CameraSensitivity
        {
            get
            {
                return m_cameraSensitivity;
            }

            // 0x06002cd7
            set
            {
                m_cameraSensitivity = value;
                m_onCameraSensitivityChanged?.Invoke(value);
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cd8.
        public CameraSensitivityType CameraSecondarySensitivity
        {
            get
            {
                return m_cameraSecondarySensitivity;
            }

            // 0x06002cd9
            set
            {
                m_cameraSecondarySensitivity = value;
                m_onCameraSecondarySensitivityChanged?.Invoke(value);
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cde.
        public bool CameraSnapBehindCharacterButtonVisible
        {
            get
            {
                return m_cameraSnapBehindCharacterButtonVisible;
            }

            // 0x06002cdf
            set
            {
                m_cameraSnapBehindCharacterButtonVisible = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ce0.
        public bool DynamicMovementStick
        {
            get
            {
                return m_dynamicMovementStick;
            }

            // 0x06002ce1
            set
            {
                m_dynamicMovementStick = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ce2.
        public bool AirAbilityHold
        {
            get
            {
                return m_airAbilityHold;
            }

            // 0x06002ce3
            set
            {
                m_airAbilityHold = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ce4.
        public InputModifierType ControlStickSensitivity
        {
            get
            {
                return m_controlStickSensitivity;
            }

            // 0x06002ce5
            set
            {
                m_controlStickSensitivity = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ce6.
        public bool OnScreenControlsFlipped
        {
            get
            {
                return m_onScreenControlsFlipped;
            }

            // 0x06002ce7
            set
            {
                m_onScreenControlsFlipped = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ce8.
        public float GameSpeed
        {
            get
            {
                return m_gameSpeed;
            }

            // 0x06002ce9
            set
            {
                m_gameSpeed = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cea.
        public SaveDataDebug Debug
        {
            get
            {
                if (m_debug == null)
                {
                    m_debug = new SaveDataDebug();
                    MarkDirty();
                }

                return m_debug;
            }

            // 0x06002ceb
            set
            {
                m_debug = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002cec.
        public List<SaveDataSettingsEditableUIComponent> EditableUIComponents
        {
            get
            {
                return m_editableUIComponents;
            }
        }

        // Game.Runtime.dll 0x06002cd4.
        public void InvokeOnConfigurationChanged(Action<CameraConfigurationType> action)
        {
            m_onCameraConfigurationChanged += action;
            action(CameraConfiguration);
        }

        // Game.Runtime.dll 0x06002cd5.
        public void RemoveConfigurationChangedAction(Action<CameraConfigurationType> action)
        {
            m_onCameraConfigurationChanged -= action;
        }

        // Game.Runtime.dll 0x06002cda.
        public void InvokeOnCameraSensitivityChanged(Action<CameraSensitivityType> action)
        {
            m_onCameraSensitivityChanged += action;
            action(CameraSensitivity);
        }

        // Game.Runtime.dll 0x06002cdb.
        public void RemoveCameraSensitivityChangedAction(Action<CameraSensitivityType> action)
        {
            m_onCameraSensitivityChanged -= action;
        }

        // Game.Runtime.dll 0x06002cdc.
        public void InvokeOnCameraSecondarySensitivityChanged(Action<CameraSensitivityType> action)
        {
            m_onCameraSecondarySensitivityChanged += action;
            action(CameraSecondarySensitivity);
        }

        // Game.Runtime.dll 0x06002cdd.
        public void RemoveCameraSecondarySensitivityChangedAction(Action<CameraSensitivityType> action)
        {
            m_onCameraSecondarySensitivityChanged -= action;
        }

        // Game.Runtime.dll 0x06002ced.
        public SaveDataSettings()
        {
            SetAudioDefaults(false);
            SetCameraDefaults(false);
            SetControlsDefaults(false);
            SetAccessibilityDefaults(false);
            SetQualityDefaults(false);
        }

        // Game.Runtime.dll 0x06002cee.
        public void SetAudioDefaults(bool setDirty = true)
        {
            // 0x06002cee always marks dirty, including when setDirty is false.
            m_masterVolume = m_musicVolume = m_sfxVolume = m_voiceVolume = 1f;
            MarkDirty();
        }

        // Game.Runtime.dll 0x06002cef.
        public void SetCameraDefaults(bool setDirty = true)
        {
            m_cameraRecenterHeadingType = CameraRecenterHeadingType.Medium;
            m_cameraInvertedControls = false;
            m_cameraSnapBehindCharacterButtonVisible = false;
            m_cameraConfiguration = CameraConfigurationType.Default;
            if (setDirty)
            {
                m_onCameraConfigurationChanged?.Invoke(CameraConfigurationType.Default);
                MarkDirty();
                CameraSensitivity = CameraSensitivityType.Medium;
            }
            else
                m_cameraSensitivity = CameraSensitivityType.Medium;
        }

        // Game.Runtime.dll 0x06002cf0.
        public void SetControlsDefaults(bool setDirty = true)
        {
            m_dynamicMovementStick = false;
            m_onScreenControlsFlipped = false;
            m_airAbilityHold = true;
            m_homingAttackMapping = m_airStompAttackMapping = ButtonMappingType.Secondary;
            m_controlStickSensitivity = InputModifierType.Medium;
            m_cameraSecondarySensitivity = CameraSensitivityType.Medium;
            if (setDirty)
                MarkDirty();
        }

        // Game.Runtime.dll 0x06002cf1.
        public void SetAccessibilityDefaults(bool setDirty = true)
        {
            m_gameSpeed = 1f;
            m_failState = AccessibilityFailState.Default;
            m_gameSpeedToggleEnabled = false;
            m_subtitlesEnabled = true;
            if (setDirty)
                MarkDirty();
        }

        // Game.Runtime.dll 0x06002cf2.
        public void SetQualityDefaults(bool setDirty = true)
        {
            m_performanceProfileName = "";
            m_renderScale = -1f;
            if (setDirty)
                MarkDirty();
        }

        // Game.Runtime.dll 0x06002cf3.
        public override void Initialise()
        {
            if (m_editableUIComponents == null)
                m_editableUIComponents = new List<SaveDataSettingsEditableUIComponent>();
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002cf4.
        public override void ResolveNewData(SaveDataSettings newSaveDataSettings)
        {
            // 0x06002cf4 preserves operand order for NaN and keeps local version/FTUE fields.
            // Direct field assignment preserves the original callback and dirty behavior.
            m_musicVolume = m_musicVolume < newSaveDataSettings.m_musicVolume ? m_musicVolume : newSaveDataSettings.m_musicVolume;
            m_masterVolume = m_masterVolume < newSaveDataSettings.m_masterVolume ? m_masterVolume : newSaveDataSettings.m_masterVolume;
            m_sfxVolume = m_sfxVolume < newSaveDataSettings.m_sfxVolume ? m_sfxVolume : newSaveDataSettings.m_sfxVolume;
            m_voiceVolume = m_voiceVolume < newSaveDataSettings.m_voiceVolume ? m_voiceVolume : newSaveDataSettings.m_voiceVolume;
            m_dynamicMovementStick |= newSaveDataSettings.m_dynamicMovementStick;
            m_debug = new SaveDataDebug();
            m_cameraRecenterHeadingType = newSaveDataSettings.m_cameraRecenterHeadingType;
            m_cameraInvertedControls = newSaveDataSettings.m_cameraInvertedControls;
            m_cameraConfiguration = newSaveDataSettings.m_cameraConfiguration;
            m_cameraSensitivity = newSaveDataSettings.m_cameraSensitivity;
            m_cameraSnapBehindCharacterButtonVisible = newSaveDataSettings.m_cameraSnapBehindCharacterButtonVisible;
            m_controlStickSensitivity = newSaveDataSettings.m_controlStickSensitivity;
            m_cameraSecondarySensitivity = newSaveDataSettings.m_cameraSecondarySensitivity;
            m_airAbilityHold = newSaveDataSettings.m_airAbilityHold;
            m_onScreenControlsFlipped = newSaveDataSettings.m_onScreenControlsFlipped;
            m_homingAttackMapping = newSaveDataSettings.m_homingAttackMapping;
            m_airStompAttackMapping = newSaveDataSettings.m_airStompAttackMapping;
            m_gameSpeed = newSaveDataSettings.m_gameSpeed;
            m_failState = newSaveDataSettings.m_failState;
            m_gameSpeedToggleEnabled = newSaveDataSettings.m_gameSpeedToggleEnabled;
            m_subtitlesEnabled = newSaveDataSettings.m_subtitlesEnabled;
            m_performanceProfileName = newSaveDataSettings.m_performanceProfileName;
            m_renderScale = newSaveDataSettings.m_renderScale;
            ResolveNewDataEditableUIComponents(newSaveDataSettings.m_editableUIComponents);
        }

        // Game.Runtime.dll 0x06002cf5.
        private void ResolveNewDataEditableUIComponents(IReadOnlyList<SaveDataSettingsEditableUIComponent> newEditableUIComponents)
        {
            foreach (SaveDataSettingsEditableUIComponent incoming in newEditableUIComponents)
            {
                SaveDataSettingsEditableUIComponent current = null;
                for (int i = 0; i < m_editableUIComponents.Count; ++i)
                {
                    if (string.Equals(m_editableUIComponents[i].ID, incoming.ID, EditableUIComponentsIDComparison))
                    {
                        current = m_editableUIComponents[i];
                        break;
                    }
                }

                if (current == null)
                    m_editableUIComponents.Add(incoming);
                else if (incoming.UserSet)
                    current.CopyFrom(incoming);
            }
        }

        // Game.Runtime.dll 0x06002cf6.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (SaveDataSettingsEditableUIComponent component in m_editableUIComponents)
                action(component);
        }

        // Game.Runtime.dll 0x06002cf7.
        public void OnBeforeSerialize()
        {
        // 0x06002cf7 has no additional native work.
        }

        // Game.Runtime.dll 0x06002cf8.
        public void OnAfterDeserialize()
        {
            EnsureUniqueEditableComponentData();
        }

        // Game.Runtime.dll 0x06002cf9.
        private void EnsureUniqueEditableComponentData()
        {
            s_editableUIComponentsTempDictionary.Clear();
            foreach (SaveDataSettingsEditableUIComponent component in m_editableUIComponents)
            {
                if (component == null || string.IsNullOrWhiteSpace(component.ID))
                    continue;
                if (!s_editableUIComponentsTempDictionary.ContainsKey(component.ID) || component.UserSet)
                    s_editableUIComponentsTempDictionary[component.ID] = component;
            }

            m_editableUIComponents.Clear();
            m_editableUIComponents.AddRange(s_editableUIComponentsTempDictionary.Values);
            s_editableUIComponentsTempDictionary.Clear();
        }

        // Game.Runtime.dll 0x06002cfa.
        public void CopyFrom(SaveDataSettings other)
        {
            // 0x06002cfa omits secondary sensitivity, stomp mapping, Debug and SkipFTUE.
            if (ReferenceEquals(this, other))
                return;
            m_masterVolume = other.m_masterVolume;
            m_musicVolume = other.m_musicVolume;
            m_sfxVolume = other.m_sfxVolume;
            m_voiceVolume = other.m_voiceVolume;
            m_dynamicMovementStick = other.m_dynamicMovementStick;
            m_cameraRecenterHeadingType = other.m_cameraRecenterHeadingType;
            m_cameraInvertedControls = other.m_cameraInvertedControls;
            MarkDirty();
            CameraConfiguration = other.m_cameraConfiguration;
            CameraSensitivity = other.m_cameraSensitivity;
            m_cameraSnapBehindCharacterButtonVisible = other.m_cameraSnapBehindCharacterButtonVisible;
            m_controlStickSensitivity = other.m_controlStickSensitivity;
            m_airAbilityHold = other.m_airAbilityHold;
            m_onScreenControlsFlipped = other.m_onScreenControlsFlipped;
            m_homingAttackMapping = other.m_homingAttackMapping;
            m_gameSpeed = other.m_gameSpeed;
            m_failState = other.m_failState;
            m_gameSpeedToggleEnabled = other.m_gameSpeedToggleEnabled;
            m_subtitlesEnabled = other.m_subtitlesEnabled;
            MarkDirty();
            m_performanceProfileName = other.m_performanceProfileName;
            m_renderScale = other.m_renderScale;
            m_lastWhatsNewVersion = other.m_lastWhatsNewVersion;
            MarkDirty();
            m_editableUIComponents = new List<SaveDataSettingsEditableUIComponent>(other.m_editableUIComponents.Count);
            foreach (SaveDataSettingsEditableUIComponent component in other.m_editableUIComponents)
                m_editableUIComponents.Add(component.Clone());
        }

        // Game.Runtime.dll 0x06002cfb.
        public SaveDataSettingsEditableUIComponent GetOrCreateEditableUIComponentData(string id)
        {
            foreach (SaveDataSettingsEditableUIComponent component in m_editableUIComponents)
            {
                if (component.ID.Equals(id, EditableUIComponentsIDComparison))
                    return component;
            }

            SaveDataSettingsEditableUIComponent created = new SaveDataSettingsEditableUIComponent(id);
            m_editableUIComponents.Add(created);
            MarkDirty();
            return created;
        }

        // Game.Runtime.dll 0x06002cfc.
        static SaveDataSettings()
        {
            s_editableUIComponentsTempDictionary = new Dictionary<string, SaveDataSettingsEditableUIComponent>(StringComparer.FromComparison(EditableUIComponentsIDComparison));
        }
    }
}
