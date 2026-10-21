using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000943; bodies translated from the 1.10.1 x86-64 binary.
    // The sound levels page: volume edits go to the live mixer and reach the settings only through the copy stored for the save event on exit; reset-to-defaults changes the live settings directly.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerSoundLevels : UIContainer
    {
        [SerializeField] private UIContainerSoundLevelsOptions m_soundLevelsOptionsContainer;
        [SerializeField] private UIModernEvent m_saveEvent;
        private SaveDataSettings m_existingSaveData;
        private AudioManager m_audioManager;
        private SaveManager m_saveManager;
        private float m_masterVolume;
        private float m_musicVolume;
        private float m_sfxVolume;
        private float m_voiceVolume;

        // 0x0600352c (0x67F2E0)
        private void Start()
        {
            m_audioManager = ProcessManager.GetSystem<AudioManager>(null, true);
            m_saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            m_existingSaveData = m_saveManager.GetSaveDataSettings();
            Refresh();
        }

        // 0x0600352d (0x67F3D0): remembers the stored volumes and hands them, with the page's callbacks, to the options.
        private void Refresh()
        {
            m_masterVolume = m_existingSaveData.MasterVolume;
            m_musicVolume = m_existingSaveData.MusicVolume;
            m_sfxVolume = m_existingSaveData.SfxVolume;
            m_voiceVolume = m_existingSaveData.VoiceVolume;
            m_soundLevelsOptionsContainer.Setup(new UIContainerSoundLevelsOptionsParameters(
                m_existingSaveData.MasterVolume,
                m_existingSaveData.MusicVolume,
                m_existingSaveData.SfxVolume,
                m_existingSaveData.VoiceVolume,
                Action_OnExit,
                OnMasterVolumeToggle,
                OnMusicVolumeToggle,
                OnSfxVolumeToggle,
                OnVoiceVolumeToggle));
        }

        // 0x0600352e (0x67F690): applies the edited volumes to a copy of the stored settings, puts it in the app storage
        // for the save state and sends the save event.
        public void Action_OnExit()
        {
            App app = ProcessManager.GetSystem<App>(null, true);
            SoundLevelsOptionsValues values = m_soundLevelsOptionsContainer.GetValues();
            var settings = new SaveDataSettings();
            settings.CopyFrom(m_existingSaveData);
            values.ApplyToSettings(settings);
            app.Storage.SetValue<SaveDataSettings>(AppFSMKeys.GameSettings, settings);
            var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var message = new UIModernMessage(UIModernEventType.OnClick);
            exchange.PublishMessage(in message, in m_saveEvent, MessageBroadcastType.Both);
        }

        // 0x0600352f (0x67FA10)
        public void Action_ResetToDefaults()
        {
            m_existingSaveData.SetAudioDefaults(true);
            Refresh();
        }

        // 0x06003530 (0x67FA40): the callbacks keep the remembered volumes current and move the live mixer.
        private void OnMasterVolumeToggle(float masterVolume)
        {
            m_masterVolume = masterVolume;
            UpdateAudioSettings(m_masterVolume, m_musicVolume, m_sfxVolume, m_voiceVolume);
        }

        // 0x06003531 (0x67FB50)
        private void OnMusicVolumeToggle(float musicVolume)
        {
            m_musicVolume = musicVolume;
            UpdateAudioSettings(m_masterVolume, m_musicVolume, m_sfxVolume, m_voiceVolume);
        }

        // 0x06003532 (0x67FBE0)
        private void OnSfxVolumeToggle(float sfxVolume)
        {
            m_sfxVolume = sfxVolume;
            UpdateAudioSettings(m_masterVolume, m_musicVolume, m_sfxVolume, m_voiceVolume);
        }

        // 0x06003533 (0x67FC70)
        private void OnVoiceVolumeToggle(float voiceVolume)
        {
            m_voiceVolume = voiceVolume;
            UpdateAudioSettings(m_masterVolume, m_musicVolume, m_sfxVolume, m_voiceVolume);
        }

        // 0x06003534 (0x67FAD0): the native toggles carry this body inlined.
        private void UpdateAudioSettings(float masterVolume, float musicVolume, float sfxVolume, float voiceVolume)
        {
            HLAudioMixerDefinition mixer = m_audioManager.GetMixer(HLAudioMixerIdentifier.Main);
            mixer.SetMasterVolume(masterVolume);
            mixer.SetMusicVolume(musicVolume);
            mixer.SetSfxVolume(sfxVolume);
            mixer.SetVoiceVolume(voiceVolume);
        }

        // 0x06003535 (0x67F3B0)
        private SaveDataSettings LoadSettingsSaveData()
        {
            return m_saveManager.GetSaveDataSettings();
        }

        // 0x06003536 (0x67FD00)
        public UIContainerSoundLevels()
        {
        }
    }
}
