using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "CollectableDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/CollectableDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class CollectableDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.CollectableType, HardlightProject.CollectableDefinition>
    {
        // Original Game.Runtime 0x06001c1e, ARM 0x51ed10.
        protected override HardlightProject.CollectableType GetElementKey(HardlightProject.CollectableDefinition data) => data.Type;

        // Original Game.Runtime 0x06001c1f, ARM 0x51ed18.
        protected override IEqualityComparer<HardlightProject.CollectableType> GetKeyComparer() => HardlightEnumComparers.CollectableTypeComparer;

        // Original Game.Runtime 0x06001c20, ARM 0x51ed94.
        // Natural original constructor; genuine field initializers precede base.
    }
}
