using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "MetadataFlexiGroup", menuName = "Hardlight/Metadata/MetadataFlexiGroup", order = 0)]
    public class MetadataFlexiGroup : MetadataGroup
    {
        // Original constructor publishes real data before MetadataGroup/Guid construction.
        [SerializeField] private MetadataFlexiGroupData m_data = new MetadataFlexiGroupData();
        public override IReadOnlyList<Metadata> Metadata => m_data.Metadata;
        public MetadataFlexiGroup() { }
    }
}
