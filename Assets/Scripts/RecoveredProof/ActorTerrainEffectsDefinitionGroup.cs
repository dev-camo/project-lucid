using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ActorTerrainEffectsDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/ActorTerrainEffectsDefinitionGroup")]
    public class ActorTerrainEffectsDefinitionGroup : DefinitionDataType<TerrainEffectType, ActorTerrainEffectsDefinition>
    {
        // Game.Runtime.dll:0x060019ad; ARM64 0x781fec. Original direct key read.
        protected override TerrainEffectType GetElementKey(ActorTerrainEffectsDefinition data) => data.TerrainEffectType;
        // Game.Runtime.dll:0x060019ae; ARM64 0x781ff4. Original generated registry,
        // static field offset0x2f8; comparer and its identity stay shared.
        protected override IEqualityComparer<TerrainEffectType> GetKeyComparer() => HardlightEnumComparers.TerrainEffectTypeComparer;
        // Game.Runtime.dll:0x060019af; ARM64 0x782070. Original closed generic base.
        public ActorTerrainEffectsDefinitionGroup() { }
    }
}
