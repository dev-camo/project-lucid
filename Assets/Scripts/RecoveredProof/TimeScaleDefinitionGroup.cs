using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "TimeScaleDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/TimeScaleDefinitionGroup")]
    public class TimeScaleDefinitionGroup : HardlightProject.DefinitionDataType<System.String, HardlightProject.TimeScaleDefinition>
    {
        // Original Game.Runtime 0x06001f73, ARM 0x532354.
        protected override System.String GetElementKey(HardlightProject.TimeScaleDefinition data) => data.name;

        // Original Game.Runtime 0x06001f74, ARM 0x532360.
        protected override IEqualityComparer<System.String> GetKeyComparer() => null;

        // Original Game.Runtime 0x06001f75, ARM 0x532368.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
