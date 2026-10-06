using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [CreateAssetMenu(fileName = "MetadataKeyType", menuName = "Hardlight/Metadata/MetadataKeyType", order = 0)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MetadataKeyType : ScriptableObjectWithGuid
    {
        [SerializeField] private MetadataValueType m_valueType;
        public MetadataValueType ValueType => m_valueType;
        public MetadataKeyType() { }
    }
}
