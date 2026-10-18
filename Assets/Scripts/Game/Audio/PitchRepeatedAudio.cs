using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000839. Each Play steps through the semitone
    // offsets in m_intervals (by default major-scale steps from 0 up to 19),
    // wrapping around.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PitchRepeatedAudio : MonoBehaviour
    {
        [SerializeField] private List<float> m_intervals = new List<float> { 0f, 2f, 4f, 5f, 7f, 9f, 11f, 12f, 14f, 16f, 17f, 19f };
        [SerializeField] private AudioSource m_audioSource;
        [SerializeField] private AudioClip m_audioClip;
        private int m_counter;
        private const int SemitonesInOctave = 12;

        public void Play()
        {
            float semitones = m_intervals[m_counter % m_intervals.Count];
            m_audioSource.pitch = Mathf.Pow(2f, semitones / SemitonesInOctave);
            m_audioSource.PlayOneShot(m_audioClip, 1f);
            m_counter++;
        }

        public void ResetCounter()
        {
            m_counter = 0;
        }
    }
}
