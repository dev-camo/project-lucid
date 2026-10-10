// Preserves the original shipping declarations and inferred whole managed bodies.
// Compiler-generated identities and exceptional native fault ordering require separate qualification.
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000545.
    [CreateAssetMenu(fileName = "EntityActivationDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/EntityActivationDefinitionGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationDefinitionGroup : DefinitionDataType<EntityActivationType, EntityActivationDefinition>
    {
        // Original 0x06001ca6.
        protected override EntityActivationType GetElementKey(EntityActivationDefinition data) => data.Type;
        // Original 0x06001ca7: use the genuine generated comparer registry.
        protected override IEqualityComparer<EntityActivationType> GetKeyComparer() => HardlightEnumComparers.EntityActivationTypeComparer;
        // Original 0x06001ca8.
        public EntityActivationDefinitionGroup() { }
    }
}
