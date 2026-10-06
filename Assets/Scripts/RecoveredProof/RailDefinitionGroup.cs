using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RailDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/RailDefinitionGroup")]
    public class RailDefinitionGroup : DefinitionDataType<RailType, RailDefinition>
    {
        // Game.Runtime 0x06001f95; ARM 0x532a80. Original direct typed key load.
        protected override RailType GetElementKey(RailDefinition data) => data.Type;
        // Game.Runtime 0x06001f96; ARM 0x532a88. Original shared registry field offset0x2c0.
        protected override IEqualityComparer<RailType> GetKeyComparer() => HardlightEnumComparers.RailTypeComparer;
        // Game.Runtime 0x06001f97; ARM 0x532b04. Original closed generic base-only constructor.
        public RailDefinitionGroup() { }
    }
}
