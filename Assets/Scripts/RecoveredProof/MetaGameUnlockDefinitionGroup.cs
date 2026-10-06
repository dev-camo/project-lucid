using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "MetaGameUnlockDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/MetaGameUnlockDefinitionGroup")]
    public class MetaGameUnlockDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.PlayerProgressionTypes, HardlightProject.MetaGameUnlockDefinition>
    {
        // Original Game.Runtime 0x06001d4a, ARM 0x5249d8.
        protected override HardlightProject.PlayerProgressionTypes GetElementKey(HardlightProject.MetaGameUnlockDefinition data) => data.Type;

        // Original Game.Runtime 0x06001d4b, ARM 0x5249e0.
        protected override IEqualityComparer<HardlightProject.PlayerProgressionTypes> GetKeyComparer() => HardlightEnumComparers.PlayerProgressionTypesComparer;

        // Original Game.Runtime 0x06001d4c, ARM 0x524a5c.
        // Natural original base-only constructor.
    }
}
