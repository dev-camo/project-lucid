// Complete original type candidate; source/runtime/serialization acceptance pending.
using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [CreateAssetMenu(fileName = "CameraRecenterHeadingOverrides", menuName = "HardlightProject/DefinitionData/Definitions/CameraRecenterHeadingOverrides")]
    public class CameraRecenterHeadingOverrides : ScriptableObject
    {
        [Tooltip("Lookup by type to camera recenter heading definitions.")]
        [SerializeField]
        private SerializableDictionary<CameraRecenterHeadingType,CameraRecenterHeadingDefinition> m_overrides = new SerializableDictionary<CameraRecenterHeadingType, CameraRecenterHeadingDefinition>(HardlightEnumComparers.CameraRecenterHeadingTypeComparer);
        public bool TryGetValue<T>(CameraRecenterHeadingType key, out T valueT)
            where T : CameraRecenterHeadingDefinition
        {
            valueT = null;
            if (!m_overrides.TryGetValue(key, out CameraRecenterHeadingDefinition value)) return false;
            valueT = value as T;
            return valueT != null;
        }
        public CameraRecenterHeadingOverrides() { }
    }
}
