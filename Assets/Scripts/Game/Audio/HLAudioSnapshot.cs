using UnityEngine;
using UnityEngine.Audio;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001c0. The parameter is set and cleared on the
    // snapshot's own mixer; both results are discarded.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioSnapshot : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The audio mixer snapshot to transition to.")]
        private AudioMixerSnapshot m_snapshot;
        [Tooltip("Time interval to transition towards the snapshot.")]
        [SerializeField]
        private float m_fadeInTime;
        [SerializeField]
        [Tooltip("Parameter whose value will be modified.")]
        private string m_parameterName;

        public void TransitionTo()
        {
            m_snapshot.TransitionTo(m_fadeInTime);
        }

        public void ResetParameter()
        {
            m_snapshot.audioMixer.ClearFloat(m_parameterName);
        }

        public void SetParameter(float parameterValue)
        {
            m_snapshot.audioMixer.SetFloat(m_parameterName, parameterValue);
        }
    }
}
