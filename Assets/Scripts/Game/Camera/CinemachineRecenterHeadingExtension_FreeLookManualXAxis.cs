using Cinemachine;
using Cinemachine.Utility;
using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200028c.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineRecenterHeadingExtension_FreeLookManualXAxis : CinemachineRecenterHeadingExtension_FreeLookBase
    {
        [FormerlySerializedAs("m_xAxisState")]
        [SerializeField]
        private AxisState m_xAxisStateDefault;
        [SerializeField]
        [Tooltip("Settings for overriding the camera x-axis responsiveness state.")]
        private CameraAxisStateOverrides m_xAxisStateOverrides;
        [SerializeField]
        private CinemachineControllerMovementProvider m_movementProvider;
        private const float DefaultMaxXAxisAngle = 180f;
        private readonly SystemRef<CameraYAxisProxyManager> m_cameraYAxisProxyManagerRef = ProcessManager.GetSystemRef<CameraYAxisProxyManager>();
        private Vector3 m_lastValidWorldForward = Vector3.forward;
        private float m_currentMaxXAxisAngle;
        private AxisState m_xAxisState;

        // 0x06000e1e
        public AxisState XAxisState => m_xAxisState;

        // 0x06000e1f: takes over the free-look camera's own X axis, which this extension drives instead.
        protected override void OnCameraManagerReady(CinemachineCameraManager cameraManager)
        {
            base.OnCameraManagerReady(cameraManager);
            m_freeLookCamera.m_XAxis = default;
            m_xAxisState = m_xAxisStateDefault;
            m_xAxisState.SetInputAxisProvider(0, m_movementProvider);
            if (m_xAxisStateOverrides == null) return;
            cameraManager.InvokeOnSensitivityChanged(OnCameraSensitivityChanged);
        }

        // 0x06000e20
        private void OnCameraSensitivityChanged(CameraSensitivityType cameraSensitivity)
        {
            m_xAxisStateOverrides.SetOverride(cameraSensitivity, ref m_xAxisState);
        }

        // 0x06000e21
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_cameraManagerRef.IsValid())
                m_cameraManagerRef.Get().RemoveSensitivityChangedAction(OnCameraSensitivityChanged);
        }

        // 0x06000e22
        protected override void OnUpdate(Character character, float deltaTime)
        {
            GetTargetDirections(character, out Vector3 targetForward, out Vector3 targetUp);
            CameraProxyTargetSettings proxySettings = m_cameraManager.GetCameraProxySettings();
            bool limitXAxisAngle = proxySettings.LimitXAxisAngle;
            float forwardAngle = limitXAxisAngle
                ? Mathf.Abs(GetCameraForwardFromTargetAngle(targetForward, targetUp, true))
                : GetCameraForwardFromTargetAngle(targetForward, targetUp, false);

            bool wasRecentering = m_xAxisState.m_Recentering.m_enabled;
            EvaluateRecentering(character, ref m_xAxisState.m_Recentering, deltaTime, forwardAngle);
            if (m_xAxisState.Update(deltaTime) || (wasRecentering && !m_xAxisState.m_Recentering.m_enabled))
                m_xAxisState.m_Recentering.CancelRecentering();

            CameraYAxisProxyManager cameraYAxisProxyManager = m_cameraYAxisProxyManagerRef.Get();
            float recenteringHeading = GetRecenteringHeading(targetForward, targetUp, cameraYAxisProxyManager);
            m_xAxisState.Value -= cameraYAxisProxyManager.SignedAngleDifferenceFromLastForward;
            if (limitXAxisAngle)
            {
                EvaluateXAxisLimit(recenteringHeading, deltaTime, proxySettings.MaxXAxisAngle, proxySettings.XAxisAngleLimitSmoothingRate);
            }
            else
            {
                m_xAxisState.m_MinValue = m_xAxisStateDefault.m_MinValue;
                m_xAxisState.m_MaxValue = m_xAxisStateDefault.m_MaxValue;
                m_xAxisState.m_Wrap = m_xAxisStateDefault.m_Wrap;
                m_currentMaxXAxisAngle = DefaultMaxXAxisAngle;
            }
            m_xAxisState.m_Recentering.DoRecentering(ref m_xAxisState, deltaTime, recenteringHeading);
        }

        // 0x06000e23
        public override void SnapToCharacter()
        {
            base.SnapToCharacter();
            m_xAxisState.m_Recentering.CancelRecentering();
            RecenterNow(ref m_xAxisState.m_Recentering);
        }

        // 0x06000e24
        public override void RecenterImmediately()
        {
            base.RecenterImmediately();
            m_xAxisState.m_Recentering.CancelRecentering();
            Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
            GetTargetDirections(character, out Vector3 targetForward, out Vector3 targetUp);
            m_xAxisState.Value = GetRecenteringHeading(targetForward, targetUp, m_cameraYAxisProxyManagerRef.Get());
        }

        // 0x06000e25: the character heading measured from the Y-axis proxy's forward, flattened onto the character plane.
        private float GetRecenteringHeading(Vector3 characterForward, Vector3 characterUp, CameraYAxisProxyManager cameraYAxisProxyManager)
        {
            Vector3 worldForward = cameraYAxisProxyManager.TargetForward.ProjectOntoPlane(characterUp);
            if (worldForward.AlmostZero()) worldForward = m_lastValidWorldForward;
            else m_lastValidWorldForward = worldForward;
            return UnityVectorExtensions.SignedAngle(worldForward, characterForward, characterUp);
        }

        // 0x06000e26: the allowed X range closes in on maxAngle around the recentering heading.
        private void EvaluateXAxisLimit(float recenteringHeadingAngle, float deltaTime, float maxAngle, float smoothingRate)
        {
            m_currentMaxXAxisAngle = Mathf.MoveTowards(m_currentMaxXAxisAngle, maxAngle, deltaTime * smoothingRate);
            m_xAxisState.m_Wrap = false;
            m_xAxisState.m_MinValue = recenteringHeadingAngle - m_currentMaxXAxisAngle;
            m_xAxisState.m_MaxValue = m_currentMaxXAxisAngle + recenteringHeadingAngle;
            if (!Mathf.Approximately(m_currentMaxXAxisAngle, maxAngle))
            {
                m_xAxisState.Value = CameraUtilities.ClampAxisStateValue(recenteringHeadingAngle - maxAngle,
                    recenteringHeadingAngle + maxAngle, m_xAxisState.Value, true);
            }
            m_xAxisState.Value = MathUtilities.ShortestAngle(m_xAxisState.Value - recenteringHeadingAngle) + recenteringHeadingAngle;
        }

        // 0x06000e27
        public CinemachineRecenterHeadingExtension_FreeLookManualXAxis() { }
    }
}
