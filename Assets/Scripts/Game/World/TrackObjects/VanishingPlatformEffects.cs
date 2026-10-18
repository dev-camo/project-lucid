using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ad4: audio actions for a vanishing platform.
    // None of the components is null-checked.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class VanishingPlatformEffects : MonoBehaviour
    {
        [SerializeField]
        private HLAudioOneShotOnSource m_platformStartAnimationAudio;
        [SerializeField]
        private HLAudioAmbientComponent m_platformLoopingAudio;
        [SerializeField]
        private HLAudioOneShotOnSource m_platformAudioWarning;

        public void Action_PlayPlatformAudio()
        {
            m_platformStartAnimationAudio.PlayOneShots();
            m_platformLoopingAudio.PlayAudio();
        }

        public void Action_PlayPlatformWarning()
        {
            m_platformAudioWarning.PlayOneShots();
        }

        public void Action_StopPlatformAudio()
        {
            m_platformLoopingAudio.StopAudio();
        }
    }
}
