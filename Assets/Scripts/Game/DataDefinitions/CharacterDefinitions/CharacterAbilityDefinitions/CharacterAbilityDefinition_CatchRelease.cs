using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020004c4; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_CatchRelease", menuName = "HardlightProject/DefinitionData/AbilityDef/CatchRelease", order = 1)]
    public class CharacterAbilityDefinition_CatchRelease : CharacterAbilityDefinition
    {
        public CatchReleaseDefinition Data;

        // 06001af4 (1.10.1 RVA 0x542B40)
        public override Type ScriptType => typeof(CharacterAbility_CatchRelease);

        // 06001af5 (1.10.1 RVA 0x542BA0). The native body inlines Data.CalculateCachedValues, of
        // which only the attach cosine has a body, so a missing Data faults after the base cache.
        public override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            Data.CalculateCachedValues();
        }

        // 06001af6 (1.10.1 RVA 0x542BE0): the base GravityMultiplier initialiser runs first.
        public CharacterAbilityDefinition_CatchRelease() { }
    }
}
