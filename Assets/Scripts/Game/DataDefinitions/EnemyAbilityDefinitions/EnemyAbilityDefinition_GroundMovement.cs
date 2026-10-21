using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200053c; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "EnemyAbility_GroundMovement", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_GroundMovement", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbilityDefinition_GroundMovement : EnemyAbilityDefinition_Movement
    {
        // 06001c88 (1.10.1 RVA 0x54C000)
        public override Type ScriptType => typeof(EnemyAbility_GroundMovement);

        // 06001c89 (1.10.1 RVA 0x54C060)
        public EnemyAbilityDefinition_GroundMovement() { }
    }
}
