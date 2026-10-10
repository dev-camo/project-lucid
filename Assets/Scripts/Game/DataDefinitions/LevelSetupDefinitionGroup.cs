using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "LevelSetupDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/LevelSetupDefinitionGroup")]
    public class LevelSetupDefinitionGroup : DefinitionDataType<LevelSetupTypes, LevelSetupDefinition>
    {
        protected override LevelSetupTypes GetElementKey(LevelSetupDefinition data)
        {
            return data.LevelSetupType;
        }

        protected override IEqualityComparer<LevelSetupTypes> GetKeyComparer()
        {
            return HardlightEnumComparers.LevelSetupTypesComparer;
        }

        public LevelSetupDefinitionGroup() { }
    }
}
