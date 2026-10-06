using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "OutroSequenceDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/OutroSequenceDefinitionGroup")]
    public class OutroSequenceDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.OutroSequenceIdentifier, HardlightProject.OutroSequenceDefinition>
    {
        // Original Game.Runtime 0x06001e81, ARM 0x52ba10.
        protected override HardlightProject.OutroSequenceIdentifier GetElementKey(HardlightProject.OutroSequenceDefinition data) => data.Identifier;

        // Original Game.Runtime 0x06001e82, ARM 0x52ba18.
        protected override IEqualityComparer<HardlightProject.OutroSequenceIdentifier> GetKeyComparer() => HardlightEnumComparers.OutroSequenceIdentifierComparer;

        // Original Game.Runtime 0x06001e83, ARM 0x52ba94.
        // Natural original base-only constructor.
    }
}
