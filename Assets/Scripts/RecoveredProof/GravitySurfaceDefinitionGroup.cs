using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "GravitySurfaceDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/GravitySurfaceDefinitionGroup")]
    public class GravitySurfaceDefinitionGroup : DefinitionDataType<GravitySurfaceType, GravitySurfaceDefinition>
    {
        // Game.Runtime 06001d22: preserve the authored enum bits without filtering.
        protected override GravitySurfaceType GetElementKey(GravitySurfaceDefinition data) => data.Type;

        // Game.Runtime 06001d23: return the original generated comparer singleton.
        protected override IEqualityComparer<GravitySurfaceType> GetKeyComparer() => HardlightEnumComparers.GravitySurfaceTypeComparer;
    }
}
