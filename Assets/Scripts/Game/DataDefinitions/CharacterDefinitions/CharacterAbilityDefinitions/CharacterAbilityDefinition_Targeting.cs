using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Full original 020004ff, eight methods/eleven fields, real ability base.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CharacterAbilityDefinition_Targeting : CharacterAbilityDefinition
    {
        [Min(0f)]
        [Tooltip("Period of time, when active, between evaluating the current target.")]
        public float TargetingIntervalSeconds = 0.1f;
        [Min(0f)]
        [Tooltip("Period of time, after leaving ability, to evaluate the current target.")]
        public float CooldownTimeSeconds = 0.1f;
        [Tooltip("Whether to use game time scaling.")]
        public bool UseTimeScaling;
        [Range(0f, 180f)]
        [Tooltip("When input is being made, discard any target which is greater than this angle from the intended input direction.")]
        public float InputDiscardTargetAngle;
        [Range(0f, 90f)]
        [Tooltip("Maximum angle a target is allowed to be above/below the character.")]
        public float TargetMaxPitchAngle;
        [Min(0f)]
        [Tooltip("Radius of the inner targeting cylinder that takes priority over any targets falling outside of it.")]
        public float TargetInnerRadius;
        [Tooltip("Homing will end after this number of seconds, regardless of reaching the target.")]
        [Min(0f)]
        public float TimeoutSeconds = 3f;
        [Min(0f)]
        [Tooltip("Velocity to reach homing target.")]
        public float HomingVelocityMin;

        public float InputDiscardTargetCosine { get; private set; } // 06001b89/8a
        public float TargetMaxPitchCosine { get; private set; } // 06001b8b/8c
        public float TargetInnerRadiusSqr { get; private set; } // 06001b8d/8e

        public override void CalculateCachedValues() // 06001b8f, base first, live reads.
        {
            base.CalculateCachedValues();
            InputDiscardTargetCosine = Mathf.Cos(InputDiscardTargetAngle * Mathf.Deg2Rad);
            TargetMaxPitchCosine = Mathf.Cos((90f - TargetMaxPitchAngle) * Mathf.Deg2Rad);
            TargetInnerRadiusSqr = TargetInnerRadius * TargetInnerRadius;
        }

        protected CharacterAbilityDefinition_Targeting() { } // 06001b90; inherited GravityMultiplier=1 is genuine base initialization.
    }
}
