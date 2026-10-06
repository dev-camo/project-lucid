using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RailDefinition", menuName = "HardlightProject/DefinitionData/Definitions/RailDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class RailDefinition : HardlightProject.TrackerMetadataDefinition
    {
        [Tooltip("Defines the rail type.")]
        [SerializeField]
        private HardlightProject.RailType m_type;

        [Tooltip("Key identifier for the auto rail metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataAutoRailKey;

        [Tooltip("Key identifier for the controls metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataControlsKey;

        [SerializeField]
        [Tooltip("Key identifier for the gravity metadata value.")]
        private Hardlight.MetadataKeyType m_metadataGravityKey;

        [Tooltip("Key identifier for the targetable metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataTargetableKey;

        // Original Game.Runtime 0x06001f8f.
        // Direct native field load at 88; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataAutoRailKey => m_metadataAutoRailKey;

        // Original Game.Runtime 0x06001f90.
        // Direct native field load at 96; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataControlsKey => m_metadataControlsKey;

        // Original Game.Runtime 0x06001f91.
        // Direct native field load at 104; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataGravityKey => m_metadataGravityKey;

        // Original Game.Runtime 0x06001f92.
        // Direct native field load at 112; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataTargetableKey => m_metadataTargetableKey;

        // Original Game.Runtime 0x06001f93.
        // Direct native field load at 80; no validation or substitution.
        public HardlightProject.RailType Type => m_type;

        // Original Game.Runtime 0x06001f94.
        // Native performs only the empty original base-constructor chain.
        public RailDefinition() { }

    }
}
