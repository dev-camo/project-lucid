using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "HalfPipeDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HalfPipeDefinition")]
    public class HalfPipeDefinition : HardlightProject.MetadataDefinition
    {
        [SerializeField]
        [Tooltip("Defines the half pipe type.")]
        private HardlightProject.HalfPipeType m_type;

        [SerializeField]
        [Tooltip("Half pipe metadata group key.")]
        private Hardlight.MetadataGroupKey m_metadataGroupKey;

        [SerializeField]
        [Tooltip("Key identifier for the trajectory metadata value.")]
        private Hardlight.MetadataKeyType m_metadataTrajectoryKey;

        [SerializeField]
        [Tooltip("Key identifier for the ignore metadata value.")]
        private Hardlight.MetadataKeyType m_metadataIgnoreKey;

        // Original Game.Runtime 0x06001f82.
        // Direct native field load at 32; no validation or substitution.
        public override Hardlight.MetadataGroupKey MetadataGroupKey => m_metadataGroupKey;

        // Original Game.Runtime 0x06001f83.
        // Direct native field load at 40; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataTrajectoryKey => m_metadataTrajectoryKey;

        // Original Game.Runtime 0x06001f84.
        // Direct native field load at 48; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataIgnoreKey => m_metadataIgnoreKey;

        // Original Game.Runtime 0x06001f85.
        // Direct native field load at 24; no validation or substitution.
        public HardlightProject.HalfPipeType Type => m_type;

        // Original Game.Runtime 0x06001f86.
        // Native performs only the empty original base-constructor chain.
        public HalfPipeDefinition() { }

    }
}
