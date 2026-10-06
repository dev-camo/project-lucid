using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "ChallengeZoneMaterialDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/ChallengeZoneMaterialDefinitionGroup")]
    public class ChallengeZoneMaterialDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.ChallengeZoneIdentifier, HardlightProject.ChallengeZoneMaterialDefinition>
    {
        // Original Game.Runtime 0x06001aa9, ARM 0x5174c4.
        protected override HardlightProject.ChallengeZoneIdentifier GetElementKey(HardlightProject.ChallengeZoneMaterialDefinition data) => data.Identifier;

        // Original Game.Runtime 0x06001aaa, ARM 0x5174cc.
        protected override IEqualityComparer<HardlightProject.ChallengeZoneIdentifier> GetKeyComparer() => HardlightEnumComparers.ChallengeZoneIdentifierComparer;

        // Original Game.Runtime 0x06001aab, ARM 0x517548.
        // Natural original constructor; field initializers precede the genuine base.
    }
}
