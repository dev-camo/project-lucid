using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000944; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerSoundLevelsOptions : UIContainer
    {
        public readonly Bindable<float> MasterVolume = new Bindable<float>();
        public readonly Bindable<float> MusicVolume = new Bindable<float>();
        public readonly Bindable<float> SfxVolume = new Bindable<float>();
        public readonly Bindable<float> VoiceVolume = new Bindable<float>();
        private Action m_onSaveClick;
        private Action<float> m_onMasterVolumeToggle;
        private Action<float> m_onMusicVolumeToggle;
        private Action<float> m_onSfxVolumeToggle;
        private Action<float> m_onVoiceVolumeToggle;

        // 0x06003537 (0x67FD10): the native body stores the save, music and sfx callbacks before it sets the bindables
        // and all four toggle callbacks after it, so a bindable observer sees the new music and sfx callbacks but the
        // previous master and voice ones.
        public override void Setup(IUIContainerParameters parameters)
        {
            UIContainerSoundLevelsOptionsParameters options = parameters.GetAs<UIContainerSoundLevelsOptionsParameters>();
            m_onSaveClick = options.OnSaveClick;
            m_onMusicVolumeToggle = options.OnMusicVolumeToggle;
            m_onSfxVolumeToggle = options.OnSfxVolumeToggle;
            MasterVolume.Value = options.MasterVolume;
            MusicVolume.Value = options.MusicVolume;
            SfxVolume.Value = options.SfxVolume;
            VoiceVolume.Value = options.VoiceVolume;
            m_onMasterVolumeToggle = options.OnMasterVolumeToggle;
            m_onMusicVolumeToggle = options.OnMusicVolumeToggle;
            m_onSfxVolumeToggle = options.OnSfxVolumeToggle;
            m_onVoiceVolumeToggle = options.OnVoiceVolumeToggle;
        }

        // 0x06003538 (0x67F920)
        public SoundLevelsOptionsValues GetValues()
        {
            return new SoundLevelsOptionsValues(MasterVolume.Value, MusicVolume.Value, SfxVolume.Value, VoiceVolume.Value);
        }

        // 0x06003539 (0x67FEB0): no null check, unlike the toggles.
        public void Action_OnSaveClick()
        {
            m_onSaveClick();
        }

        // 0x0600353a (0x67FED0): the bindable takes the value before the callback runs.
        public void Action_OnMasterVolumeToggle(float value)
        {
            MasterVolume.Value = value;
            m_onMasterVolumeToggle?.Invoke(value);
        }

        // 0x0600353b (0x67FF20)
        public void Action_OnMusicVolumeToggle(float value)
        {
            MusicVolume.Value = value;
            m_onMusicVolumeToggle?.Invoke(value);
        }

        // 0x0600353c (0x67FF70)
        public void Action_OnSfxVolumeToggle(float value)
        {
            SfxVolume.Value = value;
            m_onSfxVolumeToggle?.Invoke(value);
        }

        // 0x0600353d (0x67FFC0)
        public void Action_OnVoiceVolumeToggle(float value)
        {
            VoiceVolume.Value = value;
            m_onVoiceVolumeToggle?.Invoke(value);
        }

        // 0x0600353e (0x680010)
        public UIContainerSoundLevelsOptions()
        {
        }
    }
}
