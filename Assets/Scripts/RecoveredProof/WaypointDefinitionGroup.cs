using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "WaypointDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/WaypointDefinitionGroup")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class WaypointDefinitionGroup : HardlightProject.DefinitionDataType<HardlightProject.WaypointType, HardlightProject.WaypointDefinition>
    {
        // Original Game.Runtime 0x06001fc3, ARM 0x533020.
        protected override HardlightProject.WaypointType GetElementKey(HardlightProject.WaypointDefinition data) => data.Type;

        // Original Game.Runtime 0x06001fc4, ARM 0x533028.
        protected override IEqualityComparer<HardlightProject.WaypointType> GetKeyComparer() => HardlightEnumComparers.WaypointTypeComparer;

        // Original Game.Runtime 0x06001fc5, ARM 0x5330a4.
        // Natural original constructor; documented field initializers precede the genuine base call.
    }
}
