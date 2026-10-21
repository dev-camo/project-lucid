using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200053b; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "EnemyAbility_GroundAttack", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_GroundAttack", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbilityDefinition_GroundAttack : AbilityDefinition
    {
        public float WindUpAttackTime = 0.35f;

        // 06001c86 (1.10.1 RVA 0x54BF80)
        public override Type ScriptType => typeof(EnemyAbility_GroundAttack);

        // 06001c87 (1.10.1 RVA 0x54BFE0)
        public EnemyAbilityDefinition_GroundAttack() { }
    }
}
