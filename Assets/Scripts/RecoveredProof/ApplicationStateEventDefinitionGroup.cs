using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "ApplicationStateEventDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/ApplicationStateEventDefinitionGroup")]
    public class ApplicationStateEventDefinitionGroup : HardlightProject.DefinitionDataType<System.String, HardlightProject.ApplicationStateEvent>
    {
        // Original Game.Runtime 0x060019ef, ARM 0x514020.
        protected override System.String GetElementKey(HardlightProject.ApplicationStateEvent data) => data.name;

        // Original Game.Runtime 0x060019f0, ARM 0x51402c.
        protected override IEqualityComparer<System.String> GetKeyComparer() => null;

        // Original Game.Runtime 0x060019f1, ARM 0x514034.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
