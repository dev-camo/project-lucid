using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001b8. Plays the identifiers in a shuffled
    // order, reshuffling once every one has been requested, while the current
    // character is within range.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioOneShotShuffledLoop : MonoBehaviour
    {
        [HashEnum]
        [SerializeField]
        private HLAudioClipIdentifier[] m_oneShotIds;
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private float m_minWaitNewSound = 1f;
        [SerializeField] private float m_maxWaitNewSound = 5f;
        [SerializeField] private float m_scriptActivationRangeMeters = 50f;
        [Tooltip("If ticked, this audio will not be triggered during the level intro sequence.")]
        [SerializeField]
        private bool m_disableDuringIntroSequence = true;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private float m_timeSinceLastPlay;
        private float m_randomCooldown;
        private int m_lastPlayedIndex;
        private float m_scriptActivationRangeSq;
        private bool m_levelIntroSequenceHasPlayed;

        private void Awake()
        {
            m_lastPlayedIndex = 0;
            m_oneShotIds.Shuffle();
            UpdateCachedValues();
            if (!m_disableDuringIntroSequence) return;
            LevelManager levelManager = m_levelManagerRef.Get();
            levelManager.OnIntroSequenceBegin += OnLevelIntroSequenceBegin;
            levelManager.OnIntroSequenceComplete += OnLevelIntroSequenceComplete;
        }

        private void OnValidate()
        {
            UpdateCachedValues();
        }

        private void UpdateCachedValues()
        {
            m_scriptActivationRangeSq = m_scriptActivationRangeMeters * m_scriptActivationRangeMeters;
        }

        private void OnDestroy()
        {
            if (!m_disableDuringIntroSequence || !m_levelManagerRef.IsValid()) return;
            LevelManager levelManager = m_levelManagerRef.Get();
            levelManager.OnIntroSequenceBegin -= OnLevelIntroSequenceBegin;
            levelManager.OnIntroSequenceComplete -= OnLevelIntroSequenceComplete;
        }

        private void OnLevelIntroSequenceBegin()
        {
            m_levelIntroSequenceHasPlayed = false;
        }

        private void OnLevelIntroSequenceComplete()
        {
            m_levelIntroSequenceHasPlayed = true;
        }

        public void Update()
        {
            if (m_disableDuringIntroSequence && !m_levelIntroSequenceHasPlayed) return;
            if (CharacterInRange()) TryPlayOneShot();
        }

        private bool CharacterInRange()
        {
            return m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character) &&
                (character.WorldPosition - transform.position).sqrMagnitude <= m_scriptActivationRangeSq;
        }

        private void TryPlayOneShot()
        {
            m_timeSinceLastPlay += m_timeManagerRef.Get().GetDeltaTime(TimeCategory.PlayerMovement);
            if (m_timeSinceLastPlay < m_randomCooldown) return;

            if (m_lastPlayedIndex == m_oneShotIds.Length)
            {
                m_lastPlayedIndex = 0;
                m_oneShotIds.Shuffle();
            }
            m_timeSinceLastPlay = 0f;
            m_randomCooldown = Random.Range(m_minWaitNewSound, m_maxWaitNewSound);
            m_audioManagerRef.Get().GetClip(m_oneShotIds[m_lastPlayedIndex], OnGetClip);
            m_lastPlayedIndex++;
        }

        private void OnGetClip(HLAudioClipHandler.LoadedAudioParameters loadedAudioParameters)
        {
            m_audioSource.PlayOneShot(loadedAudioParameters.Clip);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = CharacterInRange() ? Color.green : Color.grey;
            Gizmos.DrawSphere(transform.position, m_scriptActivationRangeMeters);
        }
    }
}
