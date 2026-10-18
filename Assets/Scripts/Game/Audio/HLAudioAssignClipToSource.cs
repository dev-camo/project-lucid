using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a7.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioAssignClipToSource : MonoBehaviour
    {
        [HashEnum(typeof(HLAudioClipIdentifier))]
        [SerializeField]
        private HLAudioClipIdentifier m_identifier;
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private bool m_playOnLoad;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();

        private void Awake()
        {
            m_audioManagerRef.InvokeOnValid(OnAudioManagerValid);
        }

        // Unlike HLAudioAmbientComponent, a null loaded clip is still assigned.
        private void OnAudioManagerValid(AudioManager audioManager)
        {
            audioManager.GetClip(m_identifier, loadedAudioParameters =>
            {
                m_audioSource.clip = loadedAudioParameters.Clip;
                if (m_playOnLoad) m_audioSource.Play();
            });
        }

        private void Reset()
        {
            m_audioSource = GetComponent<AudioSource>();
        }
    }
}
