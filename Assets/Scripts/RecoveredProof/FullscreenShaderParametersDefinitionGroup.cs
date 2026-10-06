using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "FullscreenShaderParametersDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/FullscreenShaderParametersDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class FullscreenShaderParametersDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.FullscreenShaderParametersType, HardlightProject.FullscreenShaderParametersDefinition>
    {
        // Original Game.Runtime 0x06001cff, ARM 0x524058.
        protected override HardlightProject.FullscreenShaderParametersType GetElementKey(HardlightProject.FullscreenShaderParametersDefinition data) => data.FullscreenShaderParametersType;

        // Original Game.Runtime 0x06001d00, ARM 0x524060.
        protected override IEqualityComparer<HardlightProject.FullscreenShaderParametersType> GetKeyComparer() => HardlightEnumComparers.FullscreenShaderParametersTypeComparer;

        // Original Game.Runtime 0x06001d01, ARM 0x5240dc.
        // Natural original genuine DefinitionDataType constructor.
    }
}
