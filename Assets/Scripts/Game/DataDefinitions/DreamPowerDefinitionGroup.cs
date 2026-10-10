using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "DreamPowerDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/DreamPowerDefinitionGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class DreamPowerDefinitionGroup : DefinitionDataType<string, DreamPowerDefinition>
    {
        protected override string GetElementKey(DreamPowerDefinition data) => data.GetGUID();
        protected override IEqualityComparer<string> GetKeyComparer() => null;
        public DreamPowerDefinitionGroup() { }
    }
}
