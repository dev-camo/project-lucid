using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "GlobalConstantDefinition_Character", menuName = "HardlightProject/DefinitionData/Definitions/GlobalConstantDefinition_Character")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GlobalConstantDefinition_Character : GlobalConstantDefinition
    {
        [Tooltip("Controls mapping across all supported input types.")]
        public CharacterControlsMappingDefinition ControlsMappingDefinition;
        [Tooltip("Default multiplier on forward input.")]
        public float InputForwardScalingMultiplier = 1f;
        [Tooltip("Default multiplier on backward input.")]
        public float InputBackwardScalingMultiplier = 1f;
        [Tooltip("To allow a velocity dead zone for idle, set minimum speed to this when input is active.")]
        public float MovementInputSpeedMin = 1f;
        [Tooltip("When slope angle is exceeded, apply speed min to stop being stationary.")]
        public float MovementStationarySlopeAngleMax = 45f;
        [Tooltip("Slope angle past which we no longer orientate character to when enforcing minimum speed.")]
        public float MovementOrientateToSlopeAngleMax = 90f;
        [Tooltip("Velocity angle to world up past which we no longer orientate character to their velocity.")]
        public float MovementOrientateToVelocityMinAngleToWorldUp = 5f;
        [Tooltip("Time to restore grip after it has been fully lost.")]
        public float GripRestoreTime = 1f;
        [Tooltip("Time to restore boost after a half pipe landing.")]
        public float BoostRestoreTimeFromHalfPipe = 1f;
        [Tooltip("Angle upwards past which controls are inverted.")]
        public float ControlsUpAngleInversionThreshold = 135f;
        [Tooltip("Angle forwards past which controls are inverted.")]
        public float ControlsForwardAngleInversionThreshold = 120f;
        [Tooltip("When input magnitude is below the dead zone, sticky controls are deactivated.")]
        public float StickyControlsInputDeadZone = 0.5f;
        [Tooltip("Angle between camera forward and character up to start the blend to screen space.")]
        public float CameraToCharacterScreenSpaceAngleMin = 25f;
        [Tooltip("Angle between camera forward and character up to be in screen space.")]
        public float CameraToCharacterScreenSpaceAngleMax = 60f;
        [Tooltip("Rail switch uses ability setting for direction timer but add requirement of pressing jump.")]
        public bool RailSwitchRequiresJump = true;
        [Min(0f)]
        [Tooltip("If detaching from a rail at an end, lock controls for this time when in the air.")]
        public float RailDetachAirControlsLockTime = 0.5f;
        [Range(-1f, 1f)]
        [Tooltip("Threshold to check if camera forward and gravity are pointing the same direction, camera up will be used as camera forward in this case.")]
        public float CameraForwardToGravityParallelDotThreshold = 0.95f;
        [Tooltip("Maximum raycast distance when trying to detect tracking type below character.")]
        [Min(0f)]
        public float DetectTrackingRaycastDistance = 10f;
        [Tooltip("How long a homing target will be tracked for before attempting to find a new target.")]
        [Min(0f)]
        public float HomingTargetGracePeriodSeconds;
        [Tooltip("Distance within which character will hit the current homing target.")]
        [Min(0f)]
        public float HomingTargetHitDistance = 1f;
        [Tooltip("Angle threshold between character up and surface forward to determine as being parallel.")]
        public float CharacterToSurfaceParallelAngleThreshold = 15f;
        [Tooltip("Period of time after a quick switch that switching character again is disabled.")]
        public float QuickSwitchCooldownTime = 0.5f;
        [Min(0f)]
        [Tooltip("When leaving a chaos dash, lock controls for this time when in the air.")]
        public float ChaosDashAirControlsLockTime = 0.5f;
        public float MovementInputSpeedMinSqr { get; private set; }
        public float MovementStationarySlopeCosineAngleMax { get; private set; }
        public float MovementOrientateToVelocityMinCosineAngleToWorldUp { get; private set; }
        public float CameraToCharacterScreenSpaceCosineAngleMin { get; private set; }
        public float CameraToCharacterScreenSpaceCosineAngleMax { get; private set; }
        public float CharacterToSurfaceParallelCosineAngleThreshold { get; private set; }
        // 06001d1e: each cache is published in native order before calculating the next.
        protected override void UpdateCachedValues()
        {
            MovementInputSpeedMinSqr = MovementInputSpeedMin * MovementInputSpeedMin;
            MovementStationarySlopeCosineAngleMax = Mathf.Cos(MovementStationarySlopeAngleMax * Mathf.Deg2Rad);
            MovementOrientateToVelocityMinCosineAngleToWorldUp = Mathf.Cos(MovementOrientateToVelocityMinAngleToWorldUp * Mathf.Deg2Rad);
            CameraToCharacterScreenSpaceCosineAngleMin = Mathf.Cos(CameraToCharacterScreenSpaceAngleMin * Mathf.Deg2Rad);
            CameraToCharacterScreenSpaceCosineAngleMax = Mathf.Cos(CameraToCharacterScreenSpaceAngleMax * Mathf.Deg2Rad);
            CharacterToSurfaceParallelCosineAngleThreshold = Mathf.Cos(CharacterToSurfaceParallelAngleThreshold * Mathf.Deg2Rad);
        }
        // 06001d1f: authored defaults above execute before the ScriptableObject base ctor.
        public GlobalConstantDefinition_Character() { }
    }
}
