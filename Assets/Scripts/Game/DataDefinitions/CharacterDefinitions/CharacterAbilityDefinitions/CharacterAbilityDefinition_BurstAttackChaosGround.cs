using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020004c2; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_BurstAttackChaosGround", menuName = "HardlightProject/DefinitionData/AbilityDef/BurstAttackChaosGround", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbilityDefinition_BurstAttackChaosGround : CharacterAbilityDefinition_BurstAttackChaos
    {
        // 06001af0 (1.10.1 RVA 0x542A50)
        public override Type ScriptType => typeof(CharacterAbility_BurstAttackChaosGround);

        // 06001af1 (1.10.1 RVA 0x542AB0)
        public CharacterAbilityDefinition_BurstAttackChaosGround() { }
    }
}
