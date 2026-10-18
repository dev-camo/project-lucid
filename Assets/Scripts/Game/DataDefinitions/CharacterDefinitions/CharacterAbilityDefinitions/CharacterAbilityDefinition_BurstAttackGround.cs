using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_BurstAttackGround", menuName = "HardlightProject/DefinitionData/AbilityDef/BurstAttackGround", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbilityDefinition_BurstAttackGround : CharacterAbilityDefinition_BurstAttack
    {
        public override Type ScriptType => typeof(CharacterAbility_BurstAttackGround);
        public CharacterAbilityDefinition_BurstAttackGround() { }
    }
}
