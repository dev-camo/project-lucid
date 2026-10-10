using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class CharacterAbilityDefinition_BurstAttack : CharacterAbilityDefinition
    {
        [Tooltip("Size of AOE sphere that will damage any targets within it's radius.")]
        public float RangeRadius;
        [Tooltip("AOE sphere will be triggered at this point relative to the character's transform.")]
        public Vector3 Offset;
        [Tooltip("AOE sphere will keep triggering for this many seconds.")]
        public float DurationSeconds;
        [Tooltip("Burst attack cannot be used again until this time has expired.")]
        public float CooldownSeconds;
        [Tooltip("If set, burst attack is deactivated on brain input release.")]
        public bool DeactivateOnRelease;
        [Tooltip("Layers that AOE sphere will check.")]
        public LayerMask CollisionLayerMask;
        [Tooltip("If true, use boost stamina to trigger attack.")]
        public bool CostsBoostStamina;
        [ShowIf("CostsBoostStamina", null)]
        public CharacterStamina.Definition BoostStaminaCostDefinition;
        [Tooltip("If true, use chaos stamina to trigger attack.")]
        public bool CostsChaosStamina;
        [ShowIf("CostsChaosStamina", null)]
        public CharacterStamina.Definition ChaosStaminaCostDefinition;

        protected CharacterAbilityDefinition_BurstAttack() { }
    }
}
