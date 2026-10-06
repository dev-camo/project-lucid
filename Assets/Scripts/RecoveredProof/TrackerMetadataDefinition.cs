using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class TrackerMetadataDefinition : HardlightProject.MetadataDefinition
    {
        [Tooltip("Tracker metadata group key.")]
        [SerializeField]
        private Hardlight.MetadataGroupKey m_metadataGroupKey;

        [Tooltip("Key identifier for the motion metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataMotionKey;

        [Tooltip("Key identifier for the enter metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataEnterKey;

        [Tooltip("Key identifier for the exit metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataExitKey;

        [Tooltip("Key identifier for the forward metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataForwardKey;

        [SerializeField]
        [Tooltip("Key identifier for the backward metadata value.")]
        private Hardlight.MetadataKeyType m_metadataBackwardKey;

        [Tooltip("Key identifier for the camera metadata value.")]
        [SerializeField]
        private Hardlight.MetadataKeyType m_metadataCameraKey;

        // Original Game.Runtime 0x06001fa5.
        // Direct native field load at 24; no validation or substitution.
        public override Hardlight.MetadataGroupKey MetadataGroupKey => m_metadataGroupKey;

        // Original Game.Runtime 0x06001fa6.
        // Direct native field load at 32; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataMotionKey => m_metadataMotionKey;

        // Original Game.Runtime 0x06001fa7.
        // Direct native field load at 40; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataEnterKey => m_metadataEnterKey;

        // Original Game.Runtime 0x06001fa8.
        // Direct native field load at 48; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataExitKey => m_metadataExitKey;

        // Original Game.Runtime 0x06001fa9.
        // Direct native field load at 56; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataForwardKey => m_metadataForwardKey;

        // Original Game.Runtime 0x06001faa.
        // Direct native field load at 64; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataBackwardKey => m_metadataBackwardKey;

        // Original Game.Runtime 0x06001fab.
        // Direct native field load at 72; no validation or substitution.
        public Hardlight.MetadataKeyType MetadataCameraKey => m_metadataCameraKey;

        // Original Game.Runtime 0x06001fac.
        // Native performs only the empty original base-constructor chain.
        protected TrackerMetadataDefinition() { }

    }
}
