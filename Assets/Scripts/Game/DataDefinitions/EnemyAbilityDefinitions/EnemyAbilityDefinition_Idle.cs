using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200053e; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "EnemyAbility_Idle", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_Idle", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyAbilityDefinition_Idle : AbilityDefinition
    {
        // 06001c8c (1.10.1 RVA 0x54C100)
        public override Type ScriptType => typeof(EnemyAbility_Idle);

        // 06001c8d (1.10.1 RVA 0x54C160)
        public EnemyAbilityDefinition_Idle() { }
    }
}
