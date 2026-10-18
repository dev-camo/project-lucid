using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a6.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(AudioSource))]
    public class HLAudioAmbientComponent : MonoBehaviour
    {
        [HashEnum(typeof(HLAudioClipIdentifier))]
        [SerializeField]
        private HLAudioClipIdentifier m_audioClipIdentifier;
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private bool m_startPlaybackAtRandomSeekTime;
        [SerializeField] private bool m_playOnAwake = true;
        private SystemRef<AudioManager> m_audioManagerRef;
        private AudioClip m_clip;

        // A zero clip identifier never acquires the AudioManager reference.
        private void Start()
        {
            if (m_audioClipIdentifier == 0) return;
            m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
            m_audioManagerRef.InvokeOnValid(OnAudioManagerReady);
        }

        // A null result is ignored, so an earlier clip is kept.
        private void OnAudioManagerReady(AudioManager audioManager)
        {
            audioManager.GetClip(m_audioClipIdentifier, loadedAudioParameters =>
            {
                if (loadedAudioParameters.Clip == null) return;
                m_clip = loadedAudioParameters.Clip;
                if (m_playOnAwake) Play(loadedAudioParameters.Clip);
            });
        }

        public void PlayAudio()
        {
            Play(m_clip);
        }

        public void StopAudio()
        {
            if (m_audioSource == null) return;
            m_audioSource.Stop();
        }

        private void Play(AudioClip clip)
        {
            if (m_audioSource == null) return;
            m_audioSource.clip = clip;
            if (m_startPlaybackAtRandomSeekTime) m_audioSource.time = Random.Range(0f, clip.length);
            m_audioSource.Play();
        }

        private void Reset()
        {
            m_audioSource = GetComponent<AudioSource>();
        }
    }
}
