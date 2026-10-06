using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Audio;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "HLAudioMixerDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HLAudioMixerDefinition")]
    public class HLAudioMixerDefinition : ScriptableObject
    {
        [SerializeField] private HLAudioMixerIdentifier m_identifier;
        [SerializeField] private AudioMixer m_audioMixer;
        [SerializeField] private SerializableDictionary<HLAudioMixerGroupIdentifier, AudioMixerGroup> m_mixerGroups =
            new SerializableDictionary<HLAudioMixerGroupIdentifier, AudioMixerGroup>(HardlightEnumComparers.HLAudioMixerGroupIdentifierComparer);
        public HLAudioMixerIdentifier Identifier => m_identifier;
        public SerializableDictionary<HLAudioMixerGroupIdentifier, AudioMixerGroup> MixerGroups => m_mixerGroups;

        public void ResetVolume(SaveDataSettings gameSettings)
        {
            SetMasterVolume(gameSettings.MasterVolume);
            SetMusicVolume(gameSettings.MusicVolume);
            SetSfxVolume(gameSettings.SfxVolume);
            SetVoiceVolume(gameSettings.VoiceVolume);
            ClearAmbientVolume();
        }
        public void SetMute(bool mute) => m_audioMixer.SetFloat("MasterVolume", mute ? -80f : 0f);
        public void SetMasterVolume(float volume) => SetFloat("MasterVolume", volume);
        public void SetMusicVolume(float volume) => SetFloat("MusicVolume", volume);
        public void SetSfxVolume(float volume) => SetFloat("SfxVolume", volume);
        public void SetVoiceVolume(float volume) => SetFloat("VO", volume);
        public void SetAmbientVolume(float volume) => SetFloat("AmbientVolume", volume);
        public void ClearAmbientVolume() => m_audioMixer.ClearFloat("AmbientVolume");
        public float GetMusicVolume()
        {
            // The original ignores GetFloat's success result; a missing exposed
            // parameter keeps the initialized zero decibels and returns one.
            float decibels = 0f;
            m_audioMixer.GetFloat("MusicVolume", out decibels);
            return ConvertDecibelsToLinear(decibels);
        }
        private void SetFloat(string parameter, float volume)
        {
            float decibels = ConvertToDecibels(Mathf.Clamp(volume, 0.0001f, 1f));
            m_audioMixer.SetFloat(parameter, decibels);
        }
        private static float ConvertToDecibels(float value) => Mathf.Log10(value) * 20f;
        private static float ConvertDecibelsToLinear(float decibels) => Mathf.Pow(10f, decibels / 20f);
        public HLAudioMixerDefinition() { }
    }
}
