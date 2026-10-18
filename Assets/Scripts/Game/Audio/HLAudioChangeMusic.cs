using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a8. Action_PlayClip and PlayClip have
    // identical bodies in the original.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioChangeMusic : MonoBehaviour
    {
        [SerializeField]
        [HashEnum(typeof(HLAudioClipIdentifier))]
        private HLAudioClipIdentifier m_clipID;
        [HashEnum(typeof(HLAudioSourceIdentifier))]
        [SerializeField]
        private HLAudioSourceIdentifier m_sourceID;
        [SerializeField] private bool m_useCustomTransition;
        [SerializeField]
        [ShowIf("m_useCustomTransition", (string)null)]
        private HLAudioSourceTransition m_audioSourceTransition;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private AudioManager m_audioManager;

        protected virtual void Awake()
        {
            m_audioManagerRef.InvokeOnValid(OnAudioManagerValid);
        }

        private void OnAudioManagerValid(AudioManager audioManager)
        {
            m_audioManager = audioManager;
        }

        public void Action_PlayClip()
        {
            m_audioManager.PlayClipAtSource(m_clipID, m_sourceID, false,
                m_useCustomTransition ? m_audioSourceTransition : (HLAudioSourceTransition?)null);
        }

        protected void PlayClip()
        {
            m_audioManager.PlayClipAtSource(m_clipID, m_sourceID, false,
                m_useCustomTransition ? m_audioSourceTransition : (HLAudioSourceTransition?)null);
        }
    }
}
