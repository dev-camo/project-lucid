using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "MetadataGroupKey", menuName = "Hardlight/Metadata/MetadataGroupKey", order = 0)]
    public class MetadataGroupKey : ScriptableObjectWithGuid
    {
        public MetadataGroupKey() { }
    }
}
