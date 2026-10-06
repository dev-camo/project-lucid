using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TerrainMetadataDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TerrainMetadataDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TerrainMetadataDefinition : ScriptableObject
    {
        [Tooltip("Metadata group key")]
        [SerializeField]
        private MetadataGroupKey m_metadataGroupKey;

        [SerializeField]
        [FormerlySerializedAs("metadataType")]
        [Tooltip("Defines the terrain type.")]
        private TerrainMetadataType m_metadataType;

        [Tooltip("Key for movement metadata.")]
        [SerializeField]
        private MetadataKeyType m_metadataMovementKey;

        // Original Game.Runtime 06001f65..67: direct field loads, no validation,
        // wrapping, enum normalization or key cloning. Native offsets 24/32/40.
        public MetadataGroupKey MetadataGroupKey => m_metadataGroupKey;
        public TerrainMetadataType MetadataType => m_metadataType;
        public MetadataKeyType MetadataMovementKey => m_metadataMovementKey;

        // Original 06001f68: tail call to ScriptableObject constructor only.
        public TerrainMetadataDefinition() { }
    }
}
