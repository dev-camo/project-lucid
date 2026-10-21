namespace HardlightProject
{
    // Original Game.Runtime 0x02000946; bodies translated from the 1.10.1 x86-64 binary.
    public struct SoundLevelsOptionsValues
    {
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public float VoiceVolume { get; }

        // 0x0600354d (0x67FE90)
        public SoundLevelsOptionsValues(float masterVolume, float musicVolume, float sfxVolume, float voiceVolume)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            VoiceVolume = voiceVolume;
        }

        // 0x0600354e (0x67F9B0): the setters run in master, music, sfx, voice order.
        public void ApplyToSettings(SaveDataSettings settings)
        {
            settings.MasterVolume = MasterVolume;
            settings.MusicVolume = MusicVolume;
            settings.SfxVolume = SfxVolume;
            settings.VoiceVolume = VoiceVolume;
        }
    }
}
