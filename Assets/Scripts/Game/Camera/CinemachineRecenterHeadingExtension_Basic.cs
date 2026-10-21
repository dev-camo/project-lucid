using Cinemachine;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000289.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineRecenterHeadingExtension_Basic : CinemachineRecenterHeadingExtension
    {
        [SerializeField]
        private CinemachineCore.Stage m_executeInCameraStage;
        [SerializeField]
        private CameraRecenterHeadingDefinition_Basic m_settingsDefinition;

        // 0x06000e08: while snapping, orbits the camera about the follow target's up axis towards the
        // spot behind it, and ends the snap once the remaining angle is within the exit threshold.
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase virtualCamera,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != m_executeInCameraStage || !m_snapToCharacter) return;
            Transform follow = virtualCamera.Follow;
            if (follow == null) return;

            Vector3 localPosition = follow.InverseTransformPoint(state.RawPosition);
            Vector3 flatDirection = new Vector3(localPosition.x, 0f, localPosition.z).normalized;
            float angle = Vector3.SignedAngle(flatDirection, Vector3.back, Vector3.up);
            float absoluteAngle = Mathf.Abs(angle);
            if (absoluteAngle > m_settingsDefinition.ForwardAngleExitSnapThreshold)
            {
                float speedMultiplier = m_settingsDefinition.AngleToSpeedMultiplierCurve.Evaluate(absoluteAngle / 180f);
                float rotationAmount = speedMultiplier * deltaTime * m_settingsDefinition.RotationSpeed;
                rotationAmount = Mathf.Clamp(rotationAmount, 0f, absoluteAngle);
                RotateAroundPoint(ref state, follow.position, follow.up, Mathf.Sign(angle) * rotationAmount);
            }
            else if (m_snapToCharacter)
            {
                ClearSnapToCharacter();
            }
        }

        // 0x06000e09
        private void RotateAroundPoint(ref CameraState state, Vector3 point, Vector3 axis, float angle)
        {
            Quaternion rotation = Quaternion.AngleAxis(angle, axis);
            state.RawPosition = rotation * (state.RawPosition - point) + point;
            state.RawOrientation = rotation * state.RawOrientation;
        }

        // 0x06000e0a
        protected override CameraRecenterHeadingDefinition GetSettingsDefinition() => m_settingsDefinition;

        // 0x06000e0b
        public CinemachineRecenterHeadingExtension_Basic() { }
    }
}
