using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000538; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "EnemyAbility_AirMovement", menuName = "HardlightProject/DefinitionData/AbilityDef/EnemyAbility_AirMovement", order = 1)]
    public class EnemyAbilityDefinition_AirMovement : EnemyAbilityDefinition_Movement
    {
        [Header("Height restrictions")]
        [Tooltip("If true, clamp the height of the enemy above the ground.")]
        public bool RestrictHeight;
        [Tooltip("Distance to raycast to find ground. Clamping will not be applied at all if ground is not found.")]
        [Min(0)]
        public float RaycastLength;
        [Tooltip("Layers to raycast onto for ground detection.")]
        public LayerMask RaycastTarget;
        [Tooltip("Lower bound to clamp height to.")]
        [Min(0)]
        public float MinHeight;
        [Min(0)]
        [Tooltip("Upper bound to clamp height to.")]
        public float MaxHeight;

        // 06001c70 (1.10.1 RVA 0x54BC60)
        public override Type ScriptType => typeof(EnemyAbility_AirMovement);

        // 06001c71 (1.10.1 RVA 0x54BCC0)
        protected override void OnValidate() => base.OnValidate();

        // 06001c72 (1.10.1 RVA 0x54BCD0)
        public EnemyAbilityDefinition_AirMovement() { }
    }
}
