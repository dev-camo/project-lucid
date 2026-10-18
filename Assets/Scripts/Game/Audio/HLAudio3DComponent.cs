using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a5.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudio3DComponent : MonoBehaviour
    {
        [SerializeField]
        [HashEnum]
        private HLAudioSourceIdentifier m_audioSourceIdentifier;
        [HashEnum]
        [SerializeField]
        private HLAudioClipIdentifier m_audioClipIdentifier;
        private SystemRef<AudioManager> m_audioManagerRef;

        private void Start()
        {
            m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        }

        // The shared source is moved to this object before the one-shot is requested.
        public void PlayOneShotAtSource()
        {
            AudioManager audioManager = m_audioManagerRef.Get();
            audioManager.MoveAudioSource(m_audioSourceIdentifier, transform.position);
            audioManager.PlayOneShotAtSource(m_audioClipIdentifier, m_audioSourceIdentifier);
        }
    }
}
