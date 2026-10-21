using Cinemachine;
using Cinemachine.Utility;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200028a.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CinemachineRecenterHeadingExtension_FreeLookBase : CinemachineRecenterHeadingExtension
    {
        [SerializeField]
        private CameraRecenterHeadingDefinition_FreeLook m_defaultSettingsDefinition;
        [SerializeField]
        [Range(0f, 1f)]
        private float m_defaultYAxisValue = 0.5f;
        [SerializeField]
        private float m_defaultYAxisRevertSpeed = 0.5f;
        [SerializeField]
        [Tooltip("Time before the heading override is re-enabled after this virtual camera has been disabled.")]
        private UnityEvent m_onYAxisHeadingOverrideStart;
        [SerializeField]
        private UnityEvent m_onYAxisHeadingOverrideEnd;
        private CameraRecenterHeadingDefinition_FreeLook m_activeSettings;
        private float m_targetRecenteringTime;
        private float m_stopIdleRecenterTimeStamp;
        private bool m_isOverridingYAxis;
        private float m_overrideResetAfterInputTimer;
        private bool m_overrideWaitAfterInput;
        private bool m_headingOverrideActive;
        private Vector3 m_previousTargetPosition;
        private bool m_keepCameraHeadingWhileIdle;

        // 0x06000e0c
        protected CinemachineFreeLook m_freeLookCamera => VirtualCamera as CinemachineFreeLook;

        // 0x06000e0d
        protected override void Awake()
        {
            base.Awake();
            m_activeSettings = m_defaultSettingsDefinition;
        }

        // 0x06000e0e: the character's free-look heading override, when present, replaces the default settings.
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (m_characterManagerRef.IsNull()) return;
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            if (!character.TryGetFreeLookHeadingOverride(out m_activeSettings))
                m_activeSettings = m_defaultSettingsDefinition;
            OnUpdate(character, Time.fixedDeltaTime);
            m_previousTargetPosition = character.WorldPosition;
        }

        // 0x06000e0f
        protected abstract void OnUpdate(Character character, float deltaTime);

        // 0x06000e10: decides whether the axis recentres this frame. Comparisons that the native code
        // makes with ordered predicates keep NaN on the same branch as the disassembly.
        protected void EvaluateRecentering(Character character, ref AxisState.Recentering recentering,
            float deltaTime, float forwardAngle)
        {
            CameraHeadingOverride headingOverride = m_cameraManager.GetCameraSettingOverride<CameraHeadingOverride>(CameraSettingType.HeadingOverride);
            if (m_snapToCharacter)
            {
                if (forwardAngle >= m_activeSettings.ForwardAngleExitSnapThreshold) return;
                ClearSnapToCharacter();
                return;
            }

            if (headingOverride != null)
            {
                if (character.Storage.TryGetValue(ActorFSMKeys.CameraHeadingOverrideDisabled, out bool headingOverrideDisabled)
                    && headingOverrideDisabled)
                {
                    m_headingOverrideActive = false;
                }
                else
                {
                    m_headingOverrideActive = UpdateWithHeadingOverride(m_cameraManager, character, ref recentering, deltaTime, headingOverride);
                    if (m_headingOverrideActive && headingOverride.Settings.UseTargetAsXAxisOverride) return;
                }
            }
            else
            {
                m_overrideWaitAfterInput = false;
                m_overrideResetAfterInputTimer = 0f;
                m_headingOverrideActive = false;
            }

            if (m_isOverridingYAxis && (headingOverride == null || m_overrideWaitAfterInput))
            {
                if (!Mathf.Approximately(m_freeLookCamera.m_YAxis.Value, m_defaultYAxisValue)
                    && 0.0001f > m_cameraManager.ControllerMovement.y)
                    MoveYAxisToTarget(m_defaultYAxisValue, m_defaultYAxisRevertSpeed, deltaTime);
                else
                    SetOverrideYHeadingAxis(false);
            }

            if (m_activeSettings.RecenterMode == CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Never)
            {
                recentering.m_enabled = false;
                return;
            }

            bool isIdle = m_activeSettings.MinimumRequiredVelocityForRecenter == 0f
                && character.Storage.GetValue<bool>(ActorFSMKeys.IsIdle);
            if (m_cameraManager.InputBrain.MaintainHeading || m_cameraManager.ControllerMovement.sqrMagnitude > 0.0001f)
            {
                m_stopIdleRecenterTimeStamp = Time.fixedTime;
                recentering.m_enabled = false;
                if (isIdle) m_keepCameraHeadingWhileIdle = true;
                return;
            }

            if (isIdle && m_activeSettings.StoppedInputDisableRecenterSeconds > 0f)
            {
                if (GetMaintainHeading(character, recentering.m_enabled))
                {
                    recentering.m_enabled = false;
                    return;
                }
            }
            else
            {
                m_keepCameraHeadingWhileIdle = false;
                m_stopIdleRecenterTimeStamp = Time.fixedTime;
            }

            CalculateRecenteringTime(character, ref recentering, deltaTime);
            if (isIdle || m_activeSettings.RecenterMode == CameraRecenterHeadingDefinition_FreeLook.RecenteringMode.Always)
            {
                recentering.m_enabled = true;
                return;
            }
            if (!m_cameraManager.GetCameraProxyTargetIsCharacter())
            {
                recentering.m_enabled = false;
                return;
            }
            if (m_activeSettings.MinimumRequiredVelocityForRecenter > character.WorldVelocityMagnitude)
            {
                recentering.m_enabled = false;
                return;
            }
            // Hysteresis: an active recentre releases at the disable angle, an idle one resumes at the re-enable angle.
            float angleThreshold = recentering.m_enabled
                ? m_activeSettings.RecenterToHeadingDisableAngle
                : m_activeSettings.RecenterToHeadingReEnableAngle;
            recentering.m_enabled = angleThreshold >= forwardAngle;
        }

        // 0x06000e11
        private bool GetMaintainHeading(Character character, bool recenteringEnabled)
        {
            if (m_keepCameraHeadingWhileIdle) return true;
            if (m_activeSettings.StoppedInputDisableRecenterSeconds >= Time.fixedTime - m_stopIdleRecenterTimeStamp) return true;
            if (recenteringEnabled) return false;
            return Vector3.Dot(character.CameraForwardOnCharacterPlane, character.ForwardDirection)
                > m_activeSettings.StoppedInputDisableRecenterCosineAngleThreshold;
        }

        // 0x06000e12: returns whether the heading override handled the frame.
        private bool UpdateWithHeadingOverride(CinemachineCameraManager cameraManager, Character character,
            ref AxisState.Recentering recentering, float deltaTime, CameraHeadingOverride headingOverride)
        {
            CameraHeadingOverride.CameraHeadingOverrideSettings settings = headingOverride.Settings;
            float resetAfterInputSeconds = settings.ResetAfterCameraInputTimeSeconds;
            if (resetAfterInputSeconds > 0f && cameraManager.ControllerMovement.sqrMagnitude > 0.0001f)
            {
                m_overrideWaitAfterInput = true;
                m_overrideResetAfterInputTimer = 0f;
                if (settings.RecenterToCameraTargetOnCameraInput) return false;
                recentering.m_enabled = false;
                return true;
            }
            if (m_overrideWaitAfterInput && resetAfterInputSeconds > m_overrideResetAfterInputTimer)
            {
                m_overrideResetAfterInputTimer += deltaTime;
                return !settings.RecenterToCameraTargetOnCameraInput;
            }

            m_overrideWaitAfterInput = false;
            m_overrideResetAfterInputTimer = 0f;
            if (settings.UseTargetAsXAxisOverride)
            {
                recentering.m_enabled = true;
                if (settings.OverrideXAxisRecenteringTime)
                    recentering.m_RecenteringTime = settings.XAxisRecenteringTime;
                else
                    CalculateRecenteringTime(character, ref recentering, deltaTime);
            }
            if (settings.OverrideYAxisValue && 0.0001f > cameraManager.ControllerMovement.y)
            {
                MoveYAxisToTarget(settings.YAxisValue, settings.YAxisSpeed, deltaTime);
                SetOverrideYHeadingAxis(true);
            }
            else
            {
                SetOverrideYHeadingAxis(false);
            }
            return true;
        }

        // 0x06000e13
        private void MoveYAxisToTarget(float targetValue, float moveSpeed, float deltaTime)
        {
            CinemachineFreeLook freeLook = m_freeLookCamera;
            freeLook.m_YAxis.Value = Mathf.MoveTowards(freeLook.m_YAxis.Value, targetValue, moveSpeed * deltaTime);
        }

        // 0x06000e14
        protected void RecenterNow(ref AxisState.Recentering recentering)
        {
            recentering.m_enabled = true;
            recentering.RecenterNow();
            recentering.m_RecenteringTime = m_activeSettings.SnapRecenterTimeSeconds;
        }

        // 0x06000e15
        protected override CameraRecenterHeadingDefinition GetSettingsDefinition() => m_activeSettings;

        // 0x06000e16: the recentering time eases towards the target at the smoothing rate, or jumps when it is zero.
        private void CalculateRecenteringTime(Character character, ref AxisState.Recentering recentering, float deltaTime)
        {
            float angleMultiplier = m_activeSettings.AngleRecenteringTimeMultiplierCurve.Evaluate(character.GetIntendedToCameraForwardAngle());
            float limitMultiplier = m_activeSettings.AngleMultiplierLimitByVelocityCurve.Evaluate(character.WorldVelocityMagnitude);
            float velocityTime = m_activeSettings.VelocityToRecenteringTimeCurve.Evaluate(character.WorldVelocityMagnitude);
            m_targetRecenteringTime = velocityTime * Mathf.Max(angleMultiplier, limitMultiplier);
            float smoothingValue = m_activeSettings.RecenterTimeSmoothingValue;
            recentering.m_RecenteringTime = smoothingValue != 0f
                ? Mathf.MoveTowards(recentering.m_RecenteringTime, m_targetRecenteringTime, smoothingValue * deltaTime)
                : m_targetRecenteringTime;
        }

        // 0x06000e17: a heading override target wins; otherwise the camera proxy's up and the configured heading.
        protected void GetTargetDirections(Character character, out Vector3 forward, out Vector3 up)
        {
            if (m_headingOverrideActive)
            {
                CameraHeadingOverride headingOverride = m_cameraManager.GetCameraSettingOverride<CameraHeadingOverride>(CameraSettingType.HeadingOverride);
                if (headingOverride != null && headingOverride.Settings.UseTargetAsXAxisOverride)
                {
                    forward = headingOverride.Target.forward;
                    up = headingOverride.Target.up;
                    return;
                }
            }

            CameraProxyTargetSettings proxySettings = m_cameraManager.GetCameraProxySettings();
            up = proxySettings.GetWorldUp(character);
            switch (m_activeSettings.HeadingDefinitionType)
            {
                case CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition.TargetForward:
                    forward = proxySettings.GetWorldForward(character);
                    break;
                case CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition.Velocity:
                    Vector3 velocityDirection = character.WorldVelocityNormalised;
                    if (Mathf.Abs(Vector3.Dot(up, velocityDirection)) >= m_activeSettings.VelocityToTargetUpCosine)
                        forward = character.ForwardDirection;
                    else
                        forward = velocityDirection.ProjectOntoPlane(up);
                    break;
                case CameraRecenterHeadingDefinition_FreeLook.HeadingDefinition.PositionDelta:
                    forward = (character.WorldPosition - m_previousTargetPosition).ProjectOntoPlane(up);
                    if (forward.AlmostZero()) forward = character.ForwardDirection;
                    break;
                default:
                    forward = character.ForwardDirection;
                    up = character.UpDirection;
                    break;
            }
        }

        // 0x06000e18: the angle between the target heading and the camera's forward, re-aimed to the target's up.
        protected float GetCameraForwardFromTargetAngle(Vector3 targetForward, Vector3 targetUp, bool signedAngle = false)
        {
            Transform cameraTransform = m_cameraManager.CinemachineBrain.transform;
            Quaternion alignToTargetUp = Quaternion.FromToRotation(cameraTransform.up, targetUp);
            Vector3 cameraForward = (alignToTargetUp * cameraTransform.forward).normalized;
            if (signedAngle) return Vector3.SignedAngle(targetForward, cameraForward, targetUp);
            return Vector3.Angle(targetForward, cameraForward);
        }

        // 0x06000e19
        private void SetOverrideYHeadingAxis(bool value)
        {
            m_isOverridingYAxis = value;
            if (value)
            {
                if (m_onYAxisHeadingOverrideStart != null) m_onYAxisHeadingOverrideStart.Invoke();
            }
            else if (m_onYAxisHeadingOverrideEnd != null)
            {
                m_onYAxisHeadingOverrideEnd.Invoke();
            }
        }

        // 0x06000e1a
        protected CinemachineRecenterHeadingExtension_FreeLookBase() { }
    }
}
