using System.Collections.Generic;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenuAttribute(fileName = "EnemyDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/EnemyDefinitionGroup")]
    public class EnemyDefinitionGroup : DefinitionDataType<EnemyType, EnemyDefinition>
    {
        protected override EnemyType GetElementKey(EnemyDefinition data) => data.Type;
        protected override IEqualityComparer<EnemyType> GetKeyComparer() => HardlightEnumComparers.EnemyTypeComparer;
        public EnemyDefinitionGroup() { }
    }
}
