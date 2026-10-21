using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0200053d; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "EnemyAbility_GroundMovement_WithTarget", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_GroundMovement_WithTarget", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbilityDefinition_GroundMovement_WithTarget : EnemyAbilityDefinition_GroundMovement
    {
        // 06001c8a (1.10.1 RVA 0x54C080)
        public override Type ScriptType => typeof(EnemyAbility_GroundMovement_WithTarget);

        // 06001c8b (1.10.1 RVA 0x54C0E0)
        public EnemyAbilityDefinition_GroundMovement_WithTarget() { }
    }
}
