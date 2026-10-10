using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CharacterAbilityDefinition_MovementGroundSpinDashCharge", menuName = "HardlightProject/DefinitionData/AbilityDef/GroundSpinDashCharge", order = 1)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterAbilityDefinition_MovementGroundSpinDashCharge : CharacterAbilityDefinition_MovementGround
    {
        [Header("Spin Dash Charge Settings")]
        [Tooltip("Defines how long before landing a spin dash can be triggered.")]
        public float QueuedTriggerTime;
        [Tooltip("Defines how long charge needs to be held before displaying associated UI.")]
        public float UIDelayTime;
        [Tooltip("Smooths the character's actual rotation to the heading calculated from curve.")]
        public AnimationCurve RotationLerpByAngleMultiplier;
        [Tooltip("Smooths the character's actual rotation to the heading calculated from curve.")]
        public AnimationCurve RotationLerpByVelocityMultiplier;
        [Tooltip("Defines how much velocity will be given to the player based on how long charge is held for.")]
        public AnimationCurve LaunchSpeed;
        [Tooltip("Period of time to lock velocity after release.")]
        public AnimationCurve VelocityLockTime;
        [Tooltip("Time category to update velocity lock time.")]
        public TimeCategory VelocityLockTimeCategory = TimeCategory.PlayerMovement;
        [Tooltip("Whether the locked velocity can be exceeded.")]
        public bool VelocityLockCanBeExceeded = true;
        [Tooltip("While charging, these air abilities will be reset.")]
        public List<ActorAbilityType> ResetAirAbilities = new List<ActorAbilityType>();

        public override Type ScriptType => typeof(CharacterAbility_MovementGroundSpinDashCharge);
        public CharacterAbilityDefinition_MovementGroundSpinDashCharge() { }
    }
}
