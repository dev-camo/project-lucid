using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime 0x020002aa, methods 0x0600114b..0x06001153.
    [Serializable]
    public struct AndroidManifestMetadata : IValidatable
    {
        [SerializeField]
        private string m_name;
        [SerializeField]
        private string m_value;
        [SerializeField]
        private MetadataValueType m_valueType;
        public const string DefaultValueTypeString = "value";
        private static readonly Dictionary<MetadataValueType, string> s_metadataValueTypeDictionary = new Dictionary<MetadataValueType, string>
        {
            { MetadataValueType.Value, DefaultValueTypeString },
            { MetadataValueType.Resource, "resource" }
        };
        private const string FormatString = "{0}<meta-data android:name=\"{1}\" android:{2}=\"{3}\" />";

        public string Name => m_name;
        public string Value => m_value;
        public MetadataValueType ValueType => m_valueType;

        // 0x0600114e: a plain value row. Both shipped constructors (x86-64
        // 0x1b30860, 0x1b30900) end with a class-init check on this type after
        // the three stores, so building a row runs the .cctor (the dictionary
        // fill). The type is beforefieldinit, so that implies a static field
        // read the stores do not explain; it could not be identified and is
        // not reproduced here, so the dictionary may be built later.
        public AndroidManifestMetadata(string name, string value) : this(name, value, MetadataValueType.Value) { }

        // 0x0600114f.
        public AndroidManifestMetadata(string name, string value, MetadataValueType valueType)
        {
            m_name = name;
            m_value = value;
            m_valueType = valueType;
        }

        // 0x06001150: an unmapped type falls back to "value".
        public string GetValueTypeString()
        {
            return s_metadataValueTypeDictionary.TryGetValue(m_valueType, out string valueTypeString) ? valueTypeString : DefaultValueTypeString;
        }

        // 0x06001151: the type string is resolved before the argument array is
        // built; values are written verbatim, without XML escaping.
        public string FormatMetadataString(string indent)
        {
            string valueTypeString = GetValueTypeString();
            return string.Format(FormatString, indent, m_name, valueTypeString, m_value);
        }

        // 0x06001152: the retail body is a single RET.
        public void Validate() { }
    }
}
