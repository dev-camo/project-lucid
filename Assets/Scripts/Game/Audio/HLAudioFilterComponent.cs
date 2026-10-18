using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001b2. The reference is static, so its type
    // initializer acquires (and if absent, registers) the AudioManager row.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioFilterComponent : MonoBehaviour
    {
        [Tooltip("Filter to apply to currently playing music.")]
        [SerializeField]
        private InGameAudioListener.LowFilterType m_filter = InGameAudioListener.LowFilterType.None;
        private static readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();

        public void ActivateFilter(bool activate)
        {
            if (m_filter == InGameAudioListener.LowFilterType.None) return;
            if (activate) m_audioManagerRef.Get().ActivateLowPass(m_filter);
            else m_audioManagerRef.Get().RemoveLowPassFilter(m_filter);
        }
    }
}
