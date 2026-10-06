// Complete original type candidate; source/runtime/serialization acceptance pending.
using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Il2CppSetOption((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [CreateAssetMenu(fileName = "CameraRecenterHeadingDefinition_FreeLook", menuName = "HardlightProject/DefinitionData/Definitions/CameraRecenterHeadingDefinition_FreeLook")]
    public class CameraRecenterHeadingDefinition_FreeLook : CameraRecenterHeadingDefinition
    {
        public enum RecenteringMode
        {
            Default = 0,
            Always = 1,
            Never = 2,
        }
        public enum HeadingDefinition
        {
            TargetForward = 0,
            Velocity = 1,
            PositionDelta = 2,
        }
        [SerializeField]
        private CameraRecenterHeadingDefinition_FreeLook.RecenteringMode m_recenterMode;
        [ShowIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)0)]
        [SerializeField]
        [Tooltip("When Recenter to heading is disabled, it will be re-enabled when angle between camera forward and character forward is less than or equal to this.")]
        private float m_recenterToHeadingReEnableAngle = 90f;
        [SerializeField]
        [Tooltip("When Recenter to heading is enabled, it will be disabled when angle between camera forward and character forward is greater than or equal to this.")]
        [ShowIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)0)]
        private float m_recenterToHeadingDisableAngle = 100f;
        [SerializeField]
        [Tooltip("Recenter to heading is always disabled when less than or equal to this velocity magnitude")]
        [ShowIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)0)]
        private float m_minimumRequiredVelocityForRecenter = 1f;
        [ShowIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)0)]
        [SerializeField]
        [Tooltip("Value multiplied by delta time to move recentering time towards target value. Set to zero to disable.")]
        private float m_recenterTimeSmoothingValue;
        [HideIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)2)]
        [Tooltip("X Axis = Velocity magnitude\nY Axis = Recentering time")]
        [SerializeField]
        private AnimationCurve m_velocityToRecenteringTimeCurve;
        [Tooltip("X Axis = Angle from camera forward to input intent forward\nY Axis = Recentering time multiplier (smaller value will mean faster recentering)")]
        [HideIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)2)]
        [SerializeField]
        private AnimationCurve m_angleRecenteringTimeMultiplierCurve;
        [HideIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)2)]
        [Tooltip("X Axis = Velocity magnitude\nY Axis = Maximum angle recentering value")]
        [SerializeField]
        private AnimationCurve m_angleMultiplierLimitByVelocityCurve;
        [SerializeField]
        [Tooltip("Recentering time set when player triggers snap to character button.")]
        private float m_snapRecenterTimeSeconds = 0.1f;
        [HideIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)2)]
        [Tooltip("Defines what the recentering logic will use as it's forward direction.")]
        [SerializeField]
        private CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition m_headingDefinitionType;
        [HideIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)2)]
        [Tooltip("Time before recentering when there is no input.")]
        [SerializeField]
        private float m_stoppedInputDisableRecenterSeconds;
        [SerializeField]
        [Tooltip("Angle tolerance for recentering when there is no input.")]
        [HideIf("m_recenterMode", (CameraRecenterHeadingDefinition_FreeLook.RecenteringMode)2)]
        private float m_stoppedInputDisableRecenterAngleThreshold = 45f;
        [SerializeField]
        [ShowIf("m_headingDefinitionType", (CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition)1)]
        [Tooltip("Character forward will be used when velocity is within this angle of the target up direction.")]
        private float m_angleFromVelocityToTargetUp = 5f;
        public RecenteringMode RecenterMode => m_recenterMode;
        public float RecenterToHeadingReEnableAngle => m_recenterToHeadingReEnableAngle;
        public float RecenterToHeadingDisableAngle => m_recenterToHeadingDisableAngle;
        public float MinimumRequiredVelocityForRecenter => m_minimumRequiredVelocityForRecenter;
        public AnimationCurve VelocityToRecenteringTimeCurve => m_velocityToRecenteringTimeCurve;
        public AnimationCurve AngleRecenteringTimeMultiplierCurve => m_angleRecenteringTimeMultiplierCurve;
        public AnimationCurve AngleMultiplierLimitByVelocityCurve => m_angleMultiplierLimitByVelocityCurve;
        public float SnapRecenterTimeSeconds => m_snapRecenterTimeSeconds;
        public float RecenterTimeSmoothingValue => m_recenterTimeSmoothingValue;
        public HeadingDefinition HeadingDefinitionType => m_headingDefinitionType;
        public float StoppedInputDisableRecenterSeconds => m_stoppedInputDisableRecenterSeconds;
        public float StoppedInputDisableRecenterCosineAngleThreshold { get; private set; }
        public float VelocityToTargetUpCosine { get; private set; }

        private void Awake()
        {
            // The native ARM FCMP/B.LT and x86 UCOMISS/JB skip the warning
            // for unordered angles; the source comparison remains ordered.
            if (m_recenterMode == RecenteringMode.Default &&
                m_recenterToHeadingReEnableAngle >= m_recenterToHeadingDisableAngle)
                HLOutput.LogError("Re-enable angle must be less than disable angle.", this);
            StoppedInputDisableRecenterCosineAngleThreshold =
                Mathf.Cos(m_stoppedInputDisableRecenterAngleThreshold * Mathf.Deg2Rad);
            VelocityToTargetUpCosine = Mathf.Cos(m_angleFromVelocityToTargetUp * Mathf.Deg2Rad);
        }

        private void OnValidate()
        {
            if (m_recenterMode == RecenteringMode.Default &&
                m_recenterToHeadingReEnableAngle >= m_recenterToHeadingDisableAngle)
                HLOutput.LogError("Re-enable angle must be less than disable angle.", this);
            StoppedInputDisableRecenterCosineAngleThreshold =
                Mathf.Cos(m_stoppedInputDisableRecenterAngleThreshold * Mathf.Deg2Rad);
            VelocityToTargetUpCosine = Mathf.Cos(m_angleFromVelocityToTargetUp * Mathf.Deg2Rad);
        }
        public CameraRecenterHeadingDefinition_FreeLook() { }
    }
}
