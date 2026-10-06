using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "HardlightProject/CharacterSkinDefinition", fileName = "CharacterSkinDefinition")]
    public class CharacterSkinDefinition : ScriptableObjectWithGuid
    {
        [SerializeField] private AssetReferenceT<GameObject> m_prefab;
        [SerializeField] private AssetReferenceT<GameObject> m_meshPrefab;
        [SerializeField] private AssetReferenceAtlasedSprite m_render;
        [SerializeField] private AssetReferenceAtlasedSprite m_renderCompanion;
        [SerializeField] private AssetReferenceAtlasedSprite m_cardImage;
        [HashEnum(null)] [SerializeField] private Strings m_name;
        [SerializeField]
        [Tooltip("Lookups are applied in order - items in this list will override existing items on the CharacterDefinition with the same effect type.")]
        private ActorParticleEffectLookup[] m_pfxOverrides = Array.Empty<ActorParticleEffectLookup>();
        [Tooltip("Lookups are applied in order - items in this list will override existing items on the CharacterDefinition with the same audio type.")]
        [SerializeField] private ActorAudioDefinition[] m_audioOverrides = Array.Empty<ActorAudioDefinition>();
        [SerializeField] private bool m_unlockedByDefault;
        public ManagedAddressableAsset<Sprite> Thumbnail;

        public AssetReferenceT<GameObject> Prefab => m_prefab;
        public AssetReferenceT<GameObject> MeshPrefab => m_meshPrefab;
        public AssetReferenceAtlasedSprite Render => m_render;
        public AssetReferenceAtlasedSprite RenderCompanion => m_renderCompanion;
        public Strings Name => m_name;
        public ActorParticleEffectLookup[] PfxOverrides => m_pfxOverrides;
        public ActorAudioDefinition[] AudioOverrides => m_audioOverrides;
        public bool UnlockedByDefault => m_unlockedByDefault;
        public AssetReferenceAtlasedSprite CardImage => m_cardImage;

        private void OnEnable()
        {
            // Original 06001c08 uses the render reference, not the card image.
            // Replace the wrapper without inventing loading/releasing work.
            Thumbnail = new ManagedAddressableAsset<Sprite>(m_render);
        }

        // Original 06001c09 initializes both shared empty override arrays before
        // the real GUID base constructor; unlockedByDefault remains false.
        public CharacterSkinDefinition() { }
    }
}
