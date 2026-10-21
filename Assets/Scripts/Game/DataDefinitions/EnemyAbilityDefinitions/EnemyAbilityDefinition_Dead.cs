using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000539; bodies translated from the 1.10.1 x86-64 binary.
    [CreateAssetMenu(fileName = "EnemyAbility_Dead", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_Dead", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EnemyAbilityDefinition_Dead : AbilityDefinition
    {
        [Min(0)]
        [SerializeField]
        [Tooltip("Time the enemy remains dead until revived.")]
        private float m_reviveTime = 10f;

        // 06001c73 (1.10.1 RVA 0x54BD10)
        public override Type ScriptType => typeof(EnemyAbility_Dead);

        // 06001c74 (1.10.1 RVA 0x54BD70)
        public float ReviveTime => m_reviveTime;

        // 06001c75 (1.10.1 RVA 0x54BD80)
        public EnemyAbilityDefinition_Dead() { }
    }
}
