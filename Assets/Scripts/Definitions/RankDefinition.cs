using System;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RankDefinition", menuName = "HardlightProject/DefinitionData/Definitions/RankDefinition")]
    public sealed class RankDefinition : ScriptableObject
    {
        [SerializeField] private RankType m_type;
        [SerializeField] private AssetReferenceAtlasedSprite m_baseIcon;
        [SerializeField] private AssetReferenceAtlasedSprite m_backgroundIcon;
        [SerializeField] private AssetReferenceAtlasedSprite m_foregroundIcon;
        [HashEnum(null)] [SerializeField] private HLAudioClipIdentifier m_audioClipIdentifier;
        [NonSerialized] private ManagedAddressableAsset<Sprite> m_baseAddressable;
        [NonSerialized] private ManagedAddressableAsset<Sprite> m_backgroundAddressable;
        [NonSerialized] private ManagedAddressableAsset<Sprite> m_foregroundAddressable;

        // Original Game.Runtime 0x06001eec..0x06001ef0, ARM64 0x52d720..0x52d748.
        public RankType Type => m_type;
        public ManagedAddressableAsset<Sprite> BaseAddressable => m_baseAddressable;
        public ManagedAddressableAsset<Sprite> BackgroundAddressable => m_backgroundAddressable;
        public ManagedAddressableAsset<Sprite> ForegroundAddressable => m_foregroundAddressable;
        public HLAudioClipIdentifier AudioClipIdentifier => m_audioClipIdentifier;

        // Original 0x06001ef1, ARM64 0x52d748..0x52d830. Each enable replaces all
        // three wrappers, in base/background/foreground order, without loading.
        private void OnEnable()
        {
            m_baseAddressable = new ManagedAddressableAsset<Sprite>(m_baseIcon);
            m_backgroundAddressable = new ManagedAddressableAsset<Sprite>(m_backgroundIcon);
            m_foregroundAddressable = new ManagedAddressableAsset<Sprite>(m_foregroundIcon);
        }

        // Implicit original 0x06001ef2, ARM64 0x52d830..0x52d838: base only.
        // Both enum fields remain native zero; no None initializer is inferred.
    }
}
