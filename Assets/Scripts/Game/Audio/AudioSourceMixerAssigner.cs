using Hardlight;
using UnityEngine;
using UnityEngine.Audio;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a4.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AudioSourceMixerAssigner : MonoBehaviour
    {
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private HLAudioMixerIdentifier m_mixerIdentifier = HLAudioMixerIdentifier.Main;
        [SerializeField] private HLAudioMixerGroupIdentifier m_groupIdentifier;

        private void Reset()
        {
            m_audioSource = GetComponent<AudioSource>();
        }

        // A missing mixer or group leaves the source's output group as it was.
        private void Awake()
        {
            DataManager dataManager = ProcessManager.GetSystem<DataManager>();
            if (dataManager.AudioMixers.TryGetValue(m_mixerIdentifier, out HLAudioMixerDefinition mixer) &&
                mixer.MixerGroups.TryGetValue(m_groupIdentifier, out AudioMixerGroup group))
                m_audioSource.outputAudioMixerGroup = group;
        }
    }
}
