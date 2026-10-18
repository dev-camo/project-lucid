using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001ba.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioSfxBase : MonoBehaviour
    {
        [SerializeField] private HLAudioSourceIdentifier m_audioSourceIdentifier;
        [SerializeField] private bool m_useCooldown;
        [ShowIf("m_useCooldown", (string)null)]
        [SerializeField]
        private float m_cooldownSeconds;
        private float m_lastTriggeredTimeSeconds;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();

        // With the cooldown on, a request is dropped while no more than
        // m_cooldownSeconds of unscaled time has passed since the last one; the
        // trigger time is taken again after the request has been made.
        public void PlayOneShotAtSource(HLAudioClipIdentifier clip)
        {
            if (m_useCooldown && Time.unscaledTime - m_lastTriggeredTimeSeconds <= m_cooldownSeconds) return;
            m_audioManagerRef.Get().PlayOneShotAtSource(clip, m_audioSourceIdentifier);
            if (m_useCooldown) m_lastTriggeredTimeSeconds = Time.unscaledTime;
        }

        public void StopAudioSource()
        {
            m_audioManagerRef.Get().StopAudioSource(m_audioSourceIdentifier);
        }

        public bool IsPlaying()
        {
            return m_audioManagerRef.Get().TryGetAudioSourceData(m_audioSourceIdentifier, out HLAudioSourceData audioSourceData) &&
                audioSourceData.IsPlaying();
        }
    }
}
