using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "CharacterAbilityUIDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/CharacterAbilityUIDefinitionGroup")]
    public class CharacterAbilityUIDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.CharacterAbilityUIType, HardlightProject.CharacterAbilityUIDefinition>
    {
        // Original Game.Runtime 0x06001bc0, ARM 0x51d2a0.
        protected override HardlightProject.CharacterAbilityUIType GetElementKey(HardlightProject.CharacterAbilityUIDefinition data) => data.Type;

        // Original Game.Runtime 0x06001bc1, ARM 0x51d2a8.
        protected override IEqualityComparer<HardlightProject.CharacterAbilityUIType> GetKeyComparer() => HardlightEnumComparers.CharacterAbilityUITypeComparer;

        // Original Game.Runtime 0x06001bc2, ARM 0x51d324.
        // Natural original constructor; genuine field initializers precede base.
    }
}
