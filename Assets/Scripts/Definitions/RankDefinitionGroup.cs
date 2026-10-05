using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RankDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/RankDefinitionGroup")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RankDefinitionGroup : DefinitionDataType<RankType, RankDefinition>
    {
        // Game.Runtime 0x06001ef3..0x06001ef5: direct key, original maintained comparer, base-only constructor.
        protected override RankType GetElementKey(RankDefinition data) => data.Type;
        protected override IEqualityComparer<RankType> GetKeyComparer() => HardlightEnumComparers.RankTypeComparer;
        public RankDefinitionGroup() { }
    }
}
