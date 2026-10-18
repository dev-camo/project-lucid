using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004a7; CalculateCachedValues 0x5402A0, constructor 0x540470.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class CatchReleaseDefinition_Attach
    {
        [Tooltip("Attach will only trigger if inside collision trigger volume and input has been activated.")]
        public bool InputRequired;
        [Tooltip("Whether input required is instant or continuous.")]
        public bool InputRequiredIsContinuous = true;
        [Tooltip("Game action input required to trigger catch-release toy.")]
        public GameAction InputGameAction = GameAction.CharacterHomingAttack;
        [Tooltip("Pivot can be moved which requires position offset re-calculation.")]
        public bool IsDynamic;
        [Tooltip("Whether orientation is determined from velocity or forward direction.")]
        public bool OrientateFromVelocity = true;
        [Tooltip("Whether to use the current speed magnitude or speed in the forward direction.")]
        public bool UseSpeedMagnitude;
        [Tooltip("Minimum magnitude of velocity to activate an attach.")]
        public float SpeedMin;
        [Tooltip("Direction velocity must be within tolerance of to activate an attach.")]
        public Vector3 AlignmentDirection;
        [Min(0)]
        [Tooltip("Angle velocity must be within tolerance of to activate an attach. NB: Zero means no alignment check.")]
        public float AlignmentAngleTolerance;
        [Min(0)]
        [Tooltip("Speed that attach point will travel at normalised multiplier.")]
        public float SpeedNormalised;
        [Tooltip("X axis = velocity magnitude \nY axis = speed while attached.")]
        public AnimationCurve SpeedCurve = AnimationCurve.Linear(0f, 0f, 100f, 100f);
        [Tooltip("Snap to local position of attach point.")]
        public SnapToAxis SnapToAxis;
        [Tooltip("Snap to attach point.")]
        public bool SnapToAttachPoint;
        [Tooltip("Offsets the character by its height.")]
        public bool OffsetToTopOfCharacter = true;

        public float AlignmentCosAngleTolerance { get; private set; }

        public void CalculateCachedValues()
        {
            AlignmentCosAngleTolerance = Mathf.Cos(AlignmentAngleTolerance * Mathf.Deg2Rad);
        }
    }
}
