using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "LightspeedDashDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/LightspeedDashDefinitionGroup")]
    public class LightspeedDashDefinitionGroup : DefinitionDataType<LightspeedDashType, LightspeedDashDefinition>
    {
        // Game.Runtime 0x06001f8c; ARM 0x532978. Original direct typed key load.
        protected override LightspeedDashType GetElementKey(LightspeedDashDefinition data) => data.Type;
        // Game.Runtime 0x06001f8d; ARM 0x532980. Original shared registry field offset0x220.
        protected override IEqualityComparer<LightspeedDashType> GetKeyComparer() => HardlightEnumComparers.LightspeedDashTypeComparer;
        // Game.Runtime 0x06001f8e; ARM 0x5329fc. Original closed generic base-only constructor.
        public LightspeedDashDefinitionGroup() { }
    }
}
