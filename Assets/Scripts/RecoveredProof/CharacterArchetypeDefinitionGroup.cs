using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterArchetypeDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/CharacterArchetypeDefinitionGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterArchetypeDefinitionGroup : DefinitionDataType<CharacterArchetype, CharacterArchetypeDefinition>
    {
        // Game.Runtime 0x06001bd4; ARM64 0x51d5fc. Direct original key getter.
        protected override CharacterArchetype GetElementKey(CharacterArchetypeDefinition data) => data.CharacterArchetype;
        // Game.Runtime 0x06001bd5; ARM64 0x51d604. Original static registry field
        // at offset 0x100, after its genuine type initializer.
        protected override IEqualityComparer<CharacterArchetype> GetKeyComparer() => HardlightEnumComparers.CharacterArchetypeComparer;
        // Implicit public constructor 0x06001bd6 forwards to the genuine generic
        // DefinitionDataType base; the original adds no fields/default elements.
    }
}
