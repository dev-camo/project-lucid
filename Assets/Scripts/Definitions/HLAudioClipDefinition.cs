using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class HLAudioClipDefinition : ScriptableObject
    {
        [HashEnum(typeof(HLAudioClipIdentifier))]
        [SerializeField] private HLAudioClipIdentifier m_identifier;
        [SerializeField] private AssetReferenceT<AudioClip> m_audioClipReference;
        public HLAudioClipIdentifier Identifier => m_identifier;
        public AssetReferenceT<AudioClip> AudioClipReference => m_audioClipReference;
        protected HLAudioClipDefinition() { }
    }
}
