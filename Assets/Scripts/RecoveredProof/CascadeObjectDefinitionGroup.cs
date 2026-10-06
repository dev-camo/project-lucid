using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "CascadeObjectDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/CascadeObjectDefinitionGroup")]
    public class CascadeObjectDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.CascadeObjectType, HardlightProject.CascadeObjectDefinition>
    {
        // Original Game.Runtime 0x06001a8e, ARM 0x516ae4.
        protected override HardlightProject.CascadeObjectType GetElementKey(HardlightProject.CascadeObjectDefinition data) => data.Type;

        // Original Game.Runtime 0x06001a8f, ARM 0x516aec.
        protected override IEqualityComparer<HardlightProject.CascadeObjectType> GetKeyComparer() => HardlightEnumComparers.CascadeObjectTypeComparer;

        // Original Game.Runtime 0x06001a90, ARM 0x516b68.
        // Natural original constructor; field initializers precede the genuine base.
    }
}
