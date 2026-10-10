using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_BurstAttackAir", menuName = "HardlightProject/DefinitionData/AbilityDef/BurstAttackAir", order = 1)]
    public class CharacterAbilityDefinition_BurstAttackAir : CharacterAbilityDefinition_BurstAttack
    {
        public override Type ScriptType => typeof(CharacterAbility_BurstAttackAir);
        public CharacterAbilityDefinition_BurstAttackAir() { }
    }
}
