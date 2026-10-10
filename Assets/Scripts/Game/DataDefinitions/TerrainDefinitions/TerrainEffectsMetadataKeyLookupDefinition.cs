using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TerrainEffectsMetadataKeyLookupDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TerrainEffectsMetadataKeyLookupDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TerrainEffectsMetadataKeyLookupDefinition : ScriptableObject
    {
        [Tooltip("Key identifier for looking up terrain type within metadata.")]
        public MetadataKeyType TerrainEffectTypeMetadataKey;
        public TerrainEffectsMetadataKeyLookupDefinition() { } // Game.Runtime 06001f63.
    }
}
