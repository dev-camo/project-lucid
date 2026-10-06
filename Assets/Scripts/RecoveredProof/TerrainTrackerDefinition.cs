using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "TerrainTrackerDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TerrainTrackerDefinition")]
    public class TerrainTrackerDefinition : UnityEngine.ScriptableObject
    {
        [SerializeField]
        [Tooltip("Metadata group key")]
        private Hardlight.MetadataGroupKey m_metadataGroupKey;

        [SerializeField]
        [Tooltip("Defines the terrain type.")]
        private HardlightProject.TerrainTrackerType m_metadataType;

        [Tooltip("The maximum turn angle away from the currently tracked spline as a function of velocity.")]
        public UnityEngine.AnimationCurve SplineTurnAngleMax;

        // Original Game.Runtime 0x06001f6a.
        // Direct native field load at 24; no validation or substitution.
        public Hardlight.MetadataGroupKey MetadataGroupKey => m_metadataGroupKey;

        // Original Game.Runtime 0x06001f6b.
        // Direct native field load at 32; no validation or substitution.
        public HardlightProject.TerrainTrackerType MetadataType => m_metadataType;

        // Original Game.Runtime 0x06001f6c.
        // Native performs only the empty original base-constructor chain.
        public TerrainTrackerDefinition() { }

    }
}
