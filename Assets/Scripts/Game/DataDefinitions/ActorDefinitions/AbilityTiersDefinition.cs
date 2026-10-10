using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AbilityTiersDefinition
    {
        [Tooltip("Ability definitions by tier.")]
        [SerializeField]
        private List<AbilityDefinition> m_abilityTiers;

        public ActorAbilityType Type => GetBase().AbilityType;
        public List<AbilityDefinition> AbilityTiers => m_abilityTiers;

        // Original 06001975: return the first definition surviving Unity's
        // Object equality check. The original constructor leaves the list null.
        public AbilityDefinition GetBase()
        {
            foreach (AbilityDefinition definition in m_abilityTiers)
                if (definition != null)
                    return definition;
            return null;
        }

        public bool HasTier(AbilityDefinition abilityDefinition) =>
            m_abilityTiers.Find(definition => definition == abilityDefinition);

        // Original callback 0600197c directly reads AbilityType; it has no
        // fake-null guard. List.Find's first result is tested as Unity.Object.
        public bool HasAbility(ActorAbilityType type) =>
            m_abilityTiers.Find(definition => definition.AbilityType == type);

        public AbilityTiersDefinition() { }
    }
}
