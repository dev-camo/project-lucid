using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001b9.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioRandomLoop : MonoBehaviour
    {
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField]
        [HashEnum(typeof(HLAudioClipIdentifier))]
        private HLAudioClipIdentifier[] m_randomAudioClipIdentifiers;
        private SystemRef<AudioManager> m_audioManagerRef;

        // The clip is picked before the AudioManager is fetched.
        private void OnEnable()
        {
            m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
            HLAudioClipIdentifier clipIdentifier = m_randomAudioClipIdentifiers[Random.Range(0, m_randomAudioClipIdentifiers.Length)];
            m_audioManagerRef.Get().GetClip(clipIdentifier, loadedAudioParameters =>
            {
                m_audioSource.loop = true;
                m_audioSource.clip = loadedAudioParameters.Clip;
                m_audioSource.Play();
            });
        }
    }
}
