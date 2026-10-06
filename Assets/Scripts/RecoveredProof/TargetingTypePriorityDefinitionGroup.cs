using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "TargetingTypePriorityDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/TargetingTypePriorityDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class TargetingTypePriorityDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.HomingTargetType, HardlightProject.TargetingTypePriorityDefinition>
    {
        // Original Game.Runtime 0x06001f5d, ARM 0x531fcc.
        protected override HardlightProject.HomingTargetType GetElementKey(HardlightProject.TargetingTypePriorityDefinition data) => data.TargetType;

        // Original Game.Runtime 0x06001f5e, ARM 0x531fd4.
        protected override IEqualityComparer<HardlightProject.HomingTargetType> GetKeyComparer() => HardlightEnumComparers.HomingTargetTypeComparer;

        // Original Game.Runtime 0x06001f5f, ARM 0x532050.
        // Natural original constructor; field initializers precede the genuine base.
    }
}
