// Original HLUnityUI.Runtime Hardlight.UISoundEffect 02000066 (MethodDefs
// 0x060002f8..fa), translated from the x86-64 bodies. A directly assigned clip
// plays through UISoundEffectManager; otherwise a non-zero audio type plays
// through HLUIEffectManager. Both singletons are read without a null check.
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UISoundEffect : MonoBehaviour
    {
        [SerializeField]
        private AudioClip m_audioClip;

        [SerializeField]
        [HashEnum(typeof(HLAudioTypes))]
        private HLAudioTypes m_audioType;

        [SerializeField]
        private bool m_playOnAwake;

        [SerializeField]
        private bool m_playOnlyWhenActive;

        [SerializeField]
        private bool m_randomiseSemitonePitch;

        [SerializeField]
        private RangePropertyInt m_semitoneOffsetRange = new RangePropertyInt(-12, 2);

        private void OnEnable()
        {
            if (m_playOnAwake)
            {
                Play();
            }
        }

        public void Play()
        {
            if (m_playOnlyWhenActive && !isActiveAndEnabled)
            {
                return;
            }

            if (m_audioClip != null)
            {
                if (m_randomiseSemitonePitch)
                {
                    UISoundEffectManager.Instance.PlayAudioClipWithSemitoneOffset(m_audioClip, m_semitoneOffsetRange.NextValue);
                }
                else
                {
                    UISoundEffectManager.Instance.PlayAudioClip(m_audioClip);
                }
            }
            // The fallback tests the raw value against zero (0x1B4FCF9), not
            // HLAudioTypes.None, which is a non-zero hash.
            else if (m_audioType != 0)
            {
                if (m_randomiseSemitonePitch)
                {
                    HLUIEffectManager.Instance.PlayAudioClipWithSemitoneOffset(m_audioType, m_semitoneOffsetRange.NextValue);
                }
                else
                {
                    HLUIEffectManager.Instance.PlayAudioClip(m_audioType);
                }
            }
        }
    }
}
