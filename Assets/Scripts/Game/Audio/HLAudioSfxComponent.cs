using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001bb.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioSfxComponent : HLAudioSfxBase
    {
        [SerializeField]
        [HashEnum(typeof(HLAudioClipIdentifier))]
        private HLAudioClipIdentifier m_audioClipIdentifier;

        public void PlayOneShotAtSource()
        {
            PlayOneShotAtSource(m_audioClipIdentifier);
        }

        public void SetClip(HLAudioClipIdentifier clipIdentifier)
        {
            m_audioClipIdentifier = clipIdentifier;
        }
    }
}
