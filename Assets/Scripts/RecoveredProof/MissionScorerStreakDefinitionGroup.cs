using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "MissionScorerStreakDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/MissionScorerStreakDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class MissionScorerStreakDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.MissionScorerStreakIdentifier, HardlightProject.MissionScorerStreakDefinition>
    {
        // Original Game.Runtime 0x06001e5f, ARM 0x52b530.
        protected override HardlightProject.MissionScorerStreakIdentifier GetElementKey(HardlightProject.MissionScorerStreakDefinition data) => data.Identifier;

        // Original Game.Runtime 0x06001e60, ARM 0x52b538.
        protected override IEqualityComparer<HardlightProject.MissionScorerStreakIdentifier> GetKeyComparer() => HardlightEnumComparers.MissionScorerStreakIdentifierComparer;

        // Original Game.Runtime 0x06001e61, ARM 0x52b5b4.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
