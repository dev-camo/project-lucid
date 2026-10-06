using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Metadata
    {
        [SerializeField] private MetadataKeyType m_key;
        [SerializeField] private string m_value;
        [SerializeField] private UnityEngine.Object m_valueObject;

        public MetadataKeyType Key => m_key;
        public string Value => m_value;
        public UnityEngine.Object ValueObject => m_valueObject;
        public string AsString() => m_value;
        public int AsInt() => MetadataUtilities.ConvertToInt(m_value);
        public float AsFloat() => MetadataUtilities.ConvertToFloat(m_value);
        public bool AsBool() => MetadataUtilities.ConvertToBool(m_value);
        public TEnum AsEnum<TEnum>() where TEnum : struct, Enum => MetadataUtilities.ConvertToEnum<TEnum>(m_value);
        public TObject AsObject<TObject>() where TObject : UnityEngine.Object => m_valueObject as TObject;

        // The inactive value field remains null; Object construction precedes both assignments.
        public Metadata(MetadataKeyType key, string value)
        {
            m_key = key;
            m_value = value;
        }
        public Metadata(MetadataKeyType key, UnityEngine.Object valueObject)
        {
            m_key = key;
            m_valueObject = valueObject;
        }
    }
}
