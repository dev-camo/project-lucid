using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "GravitySurfaceDefinition", menuName = "HardlightProject/DefinitionData/Definitions/GravitySurfaceDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GravitySurfaceDefinition : ScriptableObject
    {
        [Tooltip("Key identifier for the gravity metadata group")]
        public MetadataGroupKey MetadataGroupKey;
        [SerializeField]
        [Tooltip("Defines the gravity surface type.")]
        public GravitySurfaceType Type;
        [Tooltip("Key identifier for the gravity metadata value.")]
        public MetadataKeyType m_metadataGravityKey;
        [Tooltip("Key identifier for the gravity inner falloff distance metadata value.")]
        public MetadataKeyType m_metadataInnerFalloffDistanceKey;
        [Tooltip("Key identifier for the gravity inner distance metadata value.")]
        public MetadataKeyType m_metadataInnerDistanceKey;
        [Tooltip("Key identifier for the gravity outer distance metadata value.")]
        public MetadataKeyType m_metadataOuterDistanceKey;
        [Tooltip("Key identifier for the gravity outer falloff distance metadata value.")]
        public MetadataKeyType m_metadataOuterFalloffDistanceKey;
    }
}
