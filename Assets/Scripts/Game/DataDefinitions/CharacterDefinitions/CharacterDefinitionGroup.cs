using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "CharacterDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/CharacterDefinitionGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterDefinitionGroup : DefinitionDataType<CharacterId, CharacterDefinition>
    {
        protected override CharacterId GetElementKey(CharacterDefinition data) => data.Id;
        protected override IEqualityComparer<CharacterId> GetKeyComparer() => HardlightEnumComparers.CharacterIdComparer;
        public CharacterDefinitionGroup() { }
    }
}
