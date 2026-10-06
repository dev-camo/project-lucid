using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "HalfPipeDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/HalfPipeDefinitionGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HalfPipeDefinitionGroup : DefinitionDataType<HalfPipeType, HalfPipeDefinition>
    {
        // Game.Runtime 0x06001f87; ARM 0x532888. Original direct typed key load.
        protected override HalfPipeType GetElementKey(HalfPipeDefinition data) => data.Type;
        // Game.Runtime 0x06001f88; ARM 0x532890. Original shared registry field offset0x1b0.
        protected override IEqualityComparer<HalfPipeType> GetKeyComparer() => HardlightEnumComparers.HalfPipeTypeComparer;
        // Game.Runtime 0x06001f89; ARM 0x53290c. Original closed generic base-only constructor.
        public HalfPipeDefinitionGroup() { }
    }
}
