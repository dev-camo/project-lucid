using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MetadataValueType : ScriptableObjectWithGuid
    {
        [SerializeField] protected string m_label = "Value";
        [SerializeField] protected string m_helpText = "";
        public abstract void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue);
        public virtual string GetDefaultAsString() => "";
        protected MetadataValueType() { }
    }
}
