using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MetadataFlexi : Metadata
    {
        public MetadataFlexi(MetadataKeyType key, string value) : base(key, value) { }
    }
}
