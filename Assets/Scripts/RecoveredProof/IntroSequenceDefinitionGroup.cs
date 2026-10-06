using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "IntroSequenceDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/IntroSequenceDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class IntroSequenceDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.IntroSequenceIdentifier, HardlightProject.IntroSequenceDefinition>
    {
        // Original Game.Runtime 0x06001d30, ARM 0x524740.
        protected override HardlightProject.IntroSequenceIdentifier GetElementKey(HardlightProject.IntroSequenceDefinition data) => data.IntroSequenceIdentifier;

        // Original Game.Runtime 0x06001d31, ARM 0x524748.
        protected override IEqualityComparer<HardlightProject.IntroSequenceIdentifier> GetKeyComparer() => HardlightEnumComparers.IntroSequenceIdentifierComparer;

        // Original Game.Runtime 0x06001d32, ARM 0x5247c4.
        // Natural original base-only constructor.
    }
}
