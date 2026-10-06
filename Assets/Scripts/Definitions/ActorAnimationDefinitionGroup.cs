using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute(Option.NullChecks, false)]
    public class ActorAnimationDefinitionGroup : DefinitionDataType<string, ActorAnimationDefinition>
    {
        protected override string GetElementKey(ActorAnimationDefinition data) => data.name;
        // Original 060019ed returns null: the base chooses Dictionary's default comparer.
        protected override IEqualityComparer<string> GetKeyComparer() => null;
        public ActorAnimationDefinitionGroup() { }
    }
}
