using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001be.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioSfxRandomisedComponent : HLAudioSfxBase
    {
        [SerializeField]
        [HashEnum(typeof(HLAudioClipIdentifier))]
        private HLAudioClipIdentifier[] m_randomAudioClipIdentifiers;

        public void PlayOneShotAtSource()
        {
            PlayOneShotAtSource(m_randomAudioClipIdentifiers[Random.Range(0, m_randomAudioClipIdentifiers.Length)]);
        }
    }
}
