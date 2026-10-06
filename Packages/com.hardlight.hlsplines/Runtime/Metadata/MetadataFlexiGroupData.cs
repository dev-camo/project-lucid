using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MetadataFlexiGroupData
    {
        // This field initializer precedes Object construction, unlike MetadataGroups' list.
        [SerializeField] private List<MetadataFlexi> m_metadata = new List<MetadataFlexi>();
        public IReadOnlyList<Metadata> Metadata => m_metadata;
        public MetadataFlexiGroupData() { }
    }
}
