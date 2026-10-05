using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "OrnamentDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/OrnamentDefinitionGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OrnamentDefinitionGroup : DefinitionDataType<OrnamentIdentifier, OrnamentDefinition>
    {
        // Game.Runtime 0x06001e7b..0x06001e7d: direct key, original maintained comparer, base-only constructor.
        protected override OrnamentIdentifier GetElementKey(OrnamentDefinition data) => data.OrnamentIdentifier;
        protected override IEqualityComparer<OrnamentIdentifier> GetKeyComparer() => HardlightEnumComparers.OrnamentIdentifierComparer;
        public OrnamentDefinitionGroup() { }
    }
}
