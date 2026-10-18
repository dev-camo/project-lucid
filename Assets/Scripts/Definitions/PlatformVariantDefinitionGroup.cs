using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "PlatformVariantDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/PlatformVariantDefinitionGroup")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class PlatformVariantDefinitionGroup : DefinitionDataType<PlatformAssetTypes, PlatformVariantDefinition>
    {
        // Game.Runtime 0x06001e9d..0x06001e9f: direct key, original maintained comparer, base-only constructor.
        protected override PlatformAssetTypes GetElementKey(PlatformVariantDefinition data) => data.AssetType;
        protected override IEqualityComparer<PlatformAssetTypes> GetKeyComparer() => HardlightEnumComparers.PlatformAssetTypesComparer;
        public PlatformVariantDefinitionGroup() { }
    }
}
