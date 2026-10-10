using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_BurstAttackChaosAir", menuName = "HardlightProject/DefinitionData/AbilityDef/BurstAttackChaosAir", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityDefinition_BurstAttackChaosAir : CharacterAbilityDefinition_BurstAttackChaos
    {
        public override Type ScriptType => typeof(CharacterAbility_BurstAttackChaosAir);
        public CharacterAbilityDefinition_BurstAttackChaosAir() { }
    }
}
