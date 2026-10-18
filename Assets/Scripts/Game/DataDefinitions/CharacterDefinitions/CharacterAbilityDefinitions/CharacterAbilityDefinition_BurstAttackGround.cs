using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020004c3; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_BurstAttackGround", menuName = "HardlightProject/DefinitionData/AbilityDef/BurstAttackGround", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbilityDefinition_BurstAttackGround : CharacterAbilityDefinition_BurstAttack
    {
        // 06001af2 (1.10.1 RVA 0x542AC0)
        public override Type ScriptType => typeof(CharacterAbility_BurstAttackGround);

        // 06001af3 (1.10.1 RVA 0x542B20)
        public CharacterAbilityDefinition_BurstAttackGround() { }
    }
}
