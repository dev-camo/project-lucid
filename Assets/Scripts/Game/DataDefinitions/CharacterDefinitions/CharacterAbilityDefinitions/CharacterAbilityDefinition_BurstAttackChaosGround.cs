using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_BurstAttackChaosGround", menuName = "HardlightProject/DefinitionData/AbilityDef/BurstAttackChaosGround", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbilityDefinition_BurstAttackChaosGround : CharacterAbilityDefinition_BurstAttackChaos
    {
        public override Type ScriptType => typeof(CharacterAbility_BurstAttackChaosGround);
        public CharacterAbilityDefinition_BurstAttackChaosGround() { }
    }
}
