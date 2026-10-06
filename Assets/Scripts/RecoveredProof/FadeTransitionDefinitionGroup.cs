using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "FadeTransitionDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/FadeTransitionDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class FadeTransitionDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.FadeTransitionType, HardlightProject.FadeTransitionDefinition>
    {
        // Original Game.Runtime 0x06001ccd, ARM 0x523aec.
        protected override HardlightProject.FadeTransitionType GetElementKey(HardlightProject.FadeTransitionDefinition data) => data.Type;

        // Original Game.Runtime 0x06001cce, ARM 0x523af4.
        protected override IEqualityComparer<HardlightProject.FadeTransitionType> GetKeyComparer() => HardlightEnumComparers.FadeTransitionTypeComparer;

        // Original Game.Runtime 0x06001ccf, ARM 0x523b70.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
