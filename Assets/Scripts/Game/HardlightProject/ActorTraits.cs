using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original 0x0200045c, two fields and one protected constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public abstract class ActorTraits
    {
        // 0x04000efb; original instance offset 0x10.
        [Tooltip("All ability tiers associated to this actor.")]
        public System.Collections.Generic.List<HardlightProject.AbilityTiersDefinition> AbilityTierDefinitions;
        // 0x04000efc; original instance offset 0x18.
        [Tooltip("All forms associated to this actor.")]
        public System.Collections.Generic.List<HardlightProject.FormTraits> Forms = new System.Collections.Generic.List<FormTraits>();

        // 0x06001992: only Forms is allocated; AbilityTierDefinitions remains null.
        protected ActorTraits() { }
    }
}
