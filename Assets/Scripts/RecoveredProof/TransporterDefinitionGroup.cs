using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "TransporterDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/TransporterDefinitionGroup")]
    public class TransporterDefinitionGroup : DefinitionDataType<TransporterType, TransporterDefinition>
    {
        // Game.Runtime 0x06001fb1; ARM 0x532d10. Original direct typed key load.
        protected override TransporterType GetElementKey(TransporterDefinition data) => data.Type;
        // Game.Runtime 0x06001fb2; ARM 0x532d18. Original shared registry field offset0x318.
        protected override IEqualityComparer<TransporterType> GetKeyComparer() => HardlightEnumComparers.TransporterTypeComparer;
        // Game.Runtime 0x06001fb3; ARM 0x532d94. Original closed generic base-only constructor.
        public TransporterDefinitionGroup() { }
    }
}
