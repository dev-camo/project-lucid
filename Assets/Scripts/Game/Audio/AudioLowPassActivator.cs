using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200082f.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AudioLowPassActivator : MonoBehaviour
    {
        [SerializeField] private InGameAudioListener.LowFilterType m_filter;
        private AudioManager m_audioManager;

        private void Awake()
        {
            m_audioManager = ProcessManager.GetSystemSafe<AudioManager>();
        }

        public void Action_ActivateLowPass()
        {
            m_audioManager.ActivateLowPass(m_filter);
        }

        public void Action_DeactivateLowPass()
        {
            m_audioManager.RemoveLowPassFilter(m_filter);
        }
    }
}
