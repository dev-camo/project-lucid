using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [UnityEngine.CreateAssetMenu(fileName = "HazardDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HazardDefinition")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Option.NullChecks, false)]
    public class HazardDefinition : ScriptableObject
    {
        [Hardlight.Utils.HashEnum(typeof(HardlightProject.HazardType))]
        public HardlightProject.HazardType Type;
        public HardlightProject.CollectableChangeData CollectableCost;
        [UnityEngine.Tooltip("Target actor will be instantly killed on collision.")]
        public bool InstantDeath;
        [UnityEngine.Header("Stumble and knockback")]
        [UnityEngine.Tooltip("If ticked, hazard will provide a knockback when impact velocity is under StumbleSpeedThreshold")]
        public bool AllowKnockback = true;
        [UnityEngine.Tooltip("If hit target speed magnitude is over this value, then stumble instead of knockback.")]
        [Hardlight.ShowIf("AllowKnockback", null)]
        [UnityEngine.Min(0f)]
        public float StumbleSpeedThreshold;
        [UnityEngine.Tooltip("Character will not be knocked back if any of these abilities are active, only stumble.")]
        [Hardlight.ShowIf("AllowKnockback", null)]
        public HardlightProject.ActorAbilityType[] DoNotKnockbackAbilities;
        [UnityEngine.Range(0f, 1f)]
        [UnityEngine.Tooltip("Multiplied by world velocity on hit.")]
        public float StumbleSpeedReductionFraction;
        [UnityEngine.Min(0f)]
        [UnityEngine.Header("Invulnerability")]
        public float InvulnerableDurationSeconds;
        public HardlightProject.CharacterAbilityTypeGroup InvulnerableAbilities;
        public HardlightProject.ActorFormType CharacterInvulnerableFormType = ActorFormType.None;
        [UnityEngine.Tooltip("Period of time boost cannot be activated after hit.")]
        [UnityEngine.Min(0f)]
        public float BoostCooldownDurationSeconds = 0.5f;
        [UnityEngine.Range(0f, 1f)]
        [UnityEngine.Tooltip("The starting velocity that will be given to any cascade objects (e.g. rings).Initial velocity refers to velocity character hit the hazard with.Resulting velocity is the velocity calculated after hazard hit.")]
        [UnityEngine.Header("Cascade")]
        public float InitialToResultingVelocityScale;
        [UnityEngine.Tooltip("Value used to slerp between zero velocity and the resulting velocity (after initial to resulting velocity scale has been applied).")]
        [UnityEngine.Range(0f, 1f)]
        public float ZeroToFullVelocityScale = 1f;
        [UnityEngine.Header("Characters Affecting Hazards")]
        public HardlightProject.CharacterAbilityTypeGroup CharacterDestroyHazardAbilities;
        public HardlightProject.ActorFormType CharacterDestroyHazardFormType = ActorFormType.None;
        // Original06001d29 initializes knockback, two None form hashes,
        // boost cooldown0.5 and full velocity1 before the engine base.
        public HazardDefinition() { }
    }
}
