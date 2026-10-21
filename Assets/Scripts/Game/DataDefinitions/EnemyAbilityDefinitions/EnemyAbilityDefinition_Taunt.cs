using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000540; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "EnemyAbilityDefinition_Taunt", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_Taunt", order = 1)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EnemyAbilityDefinition_Taunt : AbilityDefinition
    {
        [SerializeField]
        [Min(0)]
        private float m_tauntingAnimationLength = 2f;

        // 06001c8f (1.10.1 RVA 0x54C170)
        public override Type ScriptType => typeof(EnemyAbility_Taunt);

        // 06001c90 (1.10.1 RVA 0x54C1D0)
        public float TauntTime => m_tauntingAnimationLength;

        // 06001c91 (1.10.1 RVA 0x54C1E0)
        public EnemyAbilityDefinition_Taunt() { }
    }
}
