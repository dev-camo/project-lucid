using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001b4.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioOneShotAmbient : MonoBehaviour
    {
        [HashEnum(typeof(HLAudioClipIdentifier))]
        [SerializeField]
        private HLAudioClipIdentifier[] m_oneShotIds;
        [SerializeField]
        private HLAudioSourceIdentifier[] m_audioSourceIdentifiers =
        {
            HLAudioSourceIdentifier.BackgroundOneShot1,
            HLAudioSourceIdentifier.BackgroundOneShot2,
            HLAudioSourceIdentifier.BackgroundOneShot3,
            HLAudioSourceIdentifier.BackgroundOneShot4,
            HLAudioSourceIdentifier.BackgroundOneShot5
        };
        [SerializeField] private bool m_randomPan = true;
        [SerializeField] private bool m_randomVolume = true;
        [SerializeField] private float m_minWaitNewSound = 1f;
        [SerializeField] private float m_maxWaitNewSound = 5f;
        [SerializeField] private float m_minVolume = 0.1f;
        [SerializeField] private float m_maxVolume = 1f;
        private AudioManager m_audioManager;
        private TimeManager m_timeManager;
        private float m_timeSinceLastPlay;
        private float m_randomCooldown;
        private int m_lastPlayed;

        private void Awake()
        {
            m_audioManager = ProcessManager.GetSystem<AudioManager>();
            m_timeManager = ProcessManager.GetSystem<TimeManager>();
        }

        public void PlayOneShots()
        {
            TryPlayOneShot();
        }

        // Each call advances the timer by the PlayerMovement delta; once the
        // cooldown has elapsed a clip other than the last one (when there is a
        // choice) goes to the first free background one-shot source.
        private void TryPlayOneShot()
        {
            m_timeSinceLastPlay += m_timeManager.GetDeltaTime(TimeCategory.PlayerMovement);
            if (m_timeSinceLastPlay < m_randomCooldown) return;

            m_timeSinceLastPlay = 0f;
            m_randomCooldown = Random.Range(m_minWaitNewSound, m_maxWaitNewSound);
            float pan = m_randomPan ? Random.Range(-1f, 1f) : 0f;
            float volume = m_randomVolume ? Random.Range(m_minVolume, m_maxVolume) : 1f;
            int index;
            do
            {
                index = Random.Range(0, m_oneShotIds.Length);
            } while (index == m_lastPlayed && m_oneShotIds.Length > 1);
            m_lastPlayed = index;
            m_audioManager.PlayOneShotAtAvailableSource(m_oneShotIds[index], m_audioSourceIdentifiers, pan, volume);
        }
    }
}
