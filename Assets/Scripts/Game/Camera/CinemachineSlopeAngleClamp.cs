using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000293. While a grounded free-tracking character runs along a
    // slope inside the configured angle band, the free-look camera's Y axis is pulled up to a clamp
    // value; leaving the band eases it back to where the player left it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineSlopeAngleClamp : CinemachineExtension
    {
        [SerializeField]
        private CinemachineCore.Stage m_applyAfterStage;
        [SerializeField]
        private TimeCategory m_timeCategory = TimeCategory.Camera;
        [Tooltip("Angle at which upward slopes trigger axis clamping (value <= 180)")]
        [SerializeField]
        private float m_upwardSlopeAngle;
        [Tooltip("Angle at which downward slopes trigger axis clamping (value < 360)")]
        [SerializeField]
        private float m_downwardSlopeAngle;
        [SerializeField]
        private float m_clampedYAxisValue;
        [SerializeField]
        [Tooltip("Do not apply clamp if current camera pitch is below this threshold")]
        private float m_lowerThreshold;
        [Tooltip("Speed the clamp is applied / removed, based on the angle correction required")]
        [SerializeField]
        private AnimationCurve m_angleCorrectionCurve;
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private readonly SystemRef<TimeManager> m_timeManagerRef =
            ProcessManager.GetSystemRef<TimeManager>(null, true);
        private float m_unclampedYAxisValue;
        private int m_lastFrameNumber;
        private bool m_clampApplied;

        // 0x06000e45; x86_64 0x724890. The slope band test is ordered: a NaN angle takes the remove path.
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != m_applyAfterStage) return;
            if (vcam.Follow == null) return;
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            if (character.ActiveTracking.Type != CharacterTrackingType.ColliderFree) return;
            if (!character.ActiveTracking.IsOnGround()) return;
            if (!TryGetFreeLookCam(vcam, out CinemachineFreeLook freeLookCam)) return;

            float slopeAngle = ActorMovementUtilities.GetSlopeAngle(character);
            if (slopeAngle >= m_upwardSlopeAngle && m_downwardSlopeAngle >= slopeAngle)
                freeLookCam.m_YAxis = ApplyCameraAxisClamp(freeLookCam.m_YAxis);
            else
                freeLookCam.m_YAxis = RemoveCameraAxisClamp(freeLookCam.m_YAxis);
        }

        // 0x06000e46; x86_64 0x724F20. Starting a clamp remembers the unclamped value; a pitch below
        // the lower threshold only forgets it. The axis then rises toward the clamp, never above it.
        private AxisState ApplyCameraAxisClamp(AxisState axisState)
        {
            if (m_lowerThreshold > axisState.Value)
            {
                m_unclampedYAxisValue = 0f;
                return axisState;
            }
            if (!m_clampApplied)
            {
                if (m_clampedYAxisValue > axisState.Value) m_unclampedYAxisValue = axisState.Value;
                m_clampApplied = true;
            }
            if (m_clampedYAxisValue > axisState.Value)
            {
                float correction = m_angleCorrectionCurve.Evaluate(m_clampedYAxisValue - axisState.Value);
                float deltaTime = m_timeManagerRef.Get().GetDeltaTime(m_timeCategory);
                axisState.Value = Mathf.Min(m_clampedYAxisValue, deltaTime * correction + axisState.Value);
            }
            return axisState;
        }

        // 0x06000e47; x86_64 0x725040. The axis eases back down toward the remembered unclamped value;
        // the clamp is released once it gets there, or as soon as the player has pushed past it. The negated
        // comparisons keep the native ordered form so a NaN axis or remembered value takes the original branches.
        private AxisState RemoveCameraAxisClamp(AxisState axisState)
        {
            if (!m_clampApplied) return axisState;
            if (!(0f >= m_unclampedYAxisValue || axisState.Value > m_clampedYAxisValue))
            {
                float correction = m_angleCorrectionCurve.Evaluate(axisState.Value - m_unclampedYAxisValue);
                float deltaTime = m_timeManagerRef.Get().GetDeltaTime(m_timeCategory);
                axisState.Value = axisState.Value - deltaTime * correction;
                if (!(m_unclampedYAxisValue >= axisState.Value)) return axisState;
                axisState.Value = m_unclampedYAxisValue;
            }
            m_unclampedYAxisValue = 0f;
            m_clampApplied = false;
            return axisState;
        }

        // 0x06000e48; x86_64 0x725150. Forgets the clamp without touching the axis.
        private AxisState ResetClampingValues(AxisState axisState)
        {
            m_unclampedYAxisValue = 0f;
            m_clampApplied = false;
            return axisState;
        }

        // 0x06000e49; x86_64 0x724D50. A child camera is resolved through its parent camera.
        private static bool TryGetFreeLookCam(ICinemachineCamera cameraBase, out CinemachineFreeLook freeLookCam)
        {
            ICinemachineCamera camera = cameraBase.ParentCamera != null ? cameraBase.ParentCamera : cameraBase;
            freeLookCam = camera as CinemachineFreeLook;
            return freeLookCam != null;
        }

        // 0x06000e4a; the field initializers run before the base constructor.
        public CinemachineSlopeAngleClamp() { }
    }
}
