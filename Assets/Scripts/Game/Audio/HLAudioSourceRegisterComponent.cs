using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001ca; complete registration component candidate.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(AudioSource))]
    public class HLAudioSourceRegisterComponent : MonoBehaviour
    {
        [SerializeField] private HLAudioSourceIdentifier m_audioSourceIdentifier;
        [SerializeField] private bool m_isGameplayLevelSource;
        [SerializeField] private HLAudioSourceTransition m_transition;
        [SerializeField] private AudioFadeTransitionSettings m_fadeTransitionSettings;
        private SystemRef<AudioManager> m_audioManagerRef;
        private AudioSource m_audioSource;

        public HLAudioSourceIdentifier AudioSourceIdentifier => m_audioSourceIdentifier;

        // Original 0x060009a8. Acquire the source before registering the callback;
        // InvokeOnValid may invoke synchronously if the genuine system is ready.
        private void Start()
        {
            m_audioSource = GetComponent<AudioSource>();
            m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
            m_audioManagerRef.InvokeOnValid(RegisterSource);
        }

        // Original 0x060009a9. Capture all configuration before constructing the
        // wrapper, whose filter lookup executes before AddSource is invoked.
        private void RegisterSource(AudioManager audioManager)
        {
            audioManager.AddSource(new HLAudioSourceData(m_audioSourceIdentifier, m_audioSource,
                m_isGameplayLevelSource, m_transition, m_fadeTransitionSettings));
        }

        // Original 0x060009aa, base-only MonoBehaviour constructor.
        public HLAudioSourceRegisterComponent() { }
    }
}
