using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "AchievementDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/AchievementDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class AchievementDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.AchievementIdentifier, HardlightProject.AchievementDefinition>
    {
        // Original Game.Runtime 0x06001960, ARM 0x77fe20.
        protected override HardlightProject.AchievementIdentifier GetElementKey(HardlightProject.AchievementDefinition data) => data.AchievementId;

        // Original Game.Runtime 0x06001961, ARM 0x77fe28.
        protected override IEqualityComparer<HardlightProject.AchievementIdentifier> GetKeyComparer() => HardlightEnumComparers.AchievementIdentifierComparer;

        // Original Game.Runtime 0x06001962, ARM 0x77fea4.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
