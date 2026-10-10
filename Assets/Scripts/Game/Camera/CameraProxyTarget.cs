using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200025d. The complete original owner has
    // sixteen methods and sixteen fields, plus the private TargetType enum.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CameraProxyTarget : TimeScaledComponent_SDT
    {
        [SerializeField] protected SmoothedTransformProxy m_smoothedTransformProxy;
        [SerializeField] private bool m_raycastToGround;
        [Tooltip("Y axis = smooth dampening time\nX axis = distance from proxy Y to character Y")]
        [SerializeField] private AnimationCurve m_yPositionSmoothTimeCurve;
        [SerializeField] private Vector3 m_relativeOffset;
        private const float GizmoSphereRadius = 0.5f;
        private const float GizmoBoundLineLength = 3f;
        protected Vector3 m_currentTargetPosition;
        protected CameraProxyTargetSettings m_settings;
        private Transform m_target;
        private TargetType m_targetType;
        private Transform m_transform;
        private readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private float m_currentDampeningVelocity;
        private float m_currentYOffset;
        private Vector3 m_yDampVelocity;
        private float m_gravityRelativeDifference;

        // Original nested 0x0200025e, three constants and no methods.
        private enum TargetType { None = 0, Generic = 1, Character = 2 }

        // 0x06000ce8; ARM64 0x6ef558, x86_64 0x713530.
        protected override void Awake()
        {
            base.Awake();
            m_transform = transform;
        }

        // 0x06000ce9; ARM64 0x6ef594, x86_64 0x713570. Store first, then
        // apply the original Unity object predicate to the stored reference.
        public void SetTarget(Transform target)
        {
            m_target = target;
            m_targetType = m_target != null ? TargetType.Generic : TargetType.None;
        }

        // 0x06000cea; ARM64 0x6ef620, x86_64 0x7135f0.
        public void SetCharacterAsTarget() { m_targetType = TargetType.Character; }

        // 0x06000ceb; ARM64 0x6ef62c, x86_64 0x713600.
        public bool TargetIsCharacter() { return m_targetType == TargetType.Character; }

        // 0x06000cec; ARM64 0x6ef63c, x86_64 0x713610. Teleport does not
        // apply the relative offset or introduce a current-character guard.
        public void TeleportToTarget()
        {
            if (m_targetType == TargetType.Character && m_characterManagerRef.IsValid())
            {
                Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
                m_smoothedTransformProxy.SnapToLocation(character.WorldPosition, character.WorldRotation);
            }
            else if (m_targetType == TargetType.Generic)
            {
                m_smoothedTransformProxy.SnapToLocation(m_target.position, m_target.rotation);
            }
        }

        // 0x06000ced; ARM64 0x6ef780, x86_64 0x713720.
        public void SetSettings(CameraProxyTargetSettings settings) { m_settings = settings; }

        // 0x06000cee; ARM64 0x6ef79c, x86_64 0x713760. Original zeroed
        // value-type assignment, without calling a settings constructor.
        public void ResetToDefaultSettings() { m_settings = default(CameraProxyTargetSettings); }

        // 0x06000cef; ARM64 0x6ef7ac, x86_64 0x713790.
        protected override void InternalUpdate(float deltaTime)
        {
            switch (m_targetType)
            {
                case TargetType.None:
                    return;
                case TargetType.Generic:
                    UpdateSmoothedTransformProxy(m_target.TransformPoint(GetRelativeOffset()),
                        m_target.rotation, deltaTime);
                    return;
                case TargetType.Character:
                    UpdateToCharacter(deltaTime);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 0x06000cf0; ARM64 0x6efaac, x86_64 0x713a50.
        protected virtual void UpdateSmoothedTransformProxy(Vector3 targetPosition,
            Quaternion targetRotation, float deltaTime)
        {
            m_smoothedTransformProxy.UpdatePositionAndRotation(targetPosition, targetRotation, deltaTime);
        }

        // 0x06000cf1; ARM64 0x6ef930, x86_64 0x7138d0. Zero local Y in
        // proxy coordinates before evaluating the selected smoothing curve.
        private void UpdateToCharacter(float deltaTime)
        {
            if (m_characterManagerRef.IsNull()) return;
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            Vector3 targetPosition = AdjustPositionForYBounds(character, deltaTime);
            Vector3 localPosition = m_transform.InverseTransformPoint(targetPosition);
            localPosition.y = 0f;
            m_currentTargetPosition = m_transform.TransformPoint(localPosition);
            AnimationCurve curve = m_settings.OverrideYSmoothTimeCurve
                ? m_settings.YSmoothTimeCurveOverride : m_yPositionSmoothTimeCurve;
            float smoothTime = curve.Evaluate(Mathf.Abs(m_gravityRelativeDifference));
            if (smoothTime > 0f)
            {
                targetPosition = Vector3.SmoothDamp(m_currentTargetPosition, targetPosition,
                    ref m_yDampVelocity, smoothTime, float.PositiveInfinity, deltaTime);
            }
            m_currentTargetPosition = targetPosition;
            UpdateSmoothedTransformProxy(character, deltaTime);
        }

        // 0x06000cf2; ARM64 0x6efe10, x86_64 0x713e80. Resolve rotation
        // before capturing the smoothed-proxy receiver and current position.
        protected virtual void UpdateSmoothedTransformProxy(Character character, float deltaTime)
        {
            Quaternion targetRotation = m_settings.GetWorldRotation(character);
            m_smoothedTransformProxy.UpdatePositionAndRotation(m_currentTargetPosition, targetRotation, deltaTime);
        }

        // 0x06000cf3; ARM64 0x6efab8, x86_64 0x713a60. The ordered
        // nonzero test below encodes both shipped native branches: zero and
        // NaN skip the ground raycast. Ordinary CLR != would include NaN.
        private Vector3 AdjustPositionForYBounds(Character character, float deltaTime)
        {
            RaycastHit hit = default(RaycastHit);
            Vector3 targetPosition = character.WorldPosition + m_transform.rotation * GetRelativeOffset();
            if (!m_settings.BoundsEnabled)
            {
                m_gravityRelativeDifference = 0f;
                m_currentYOffset = 0f;
                return targetPosition;
            }

            m_gravityRelativeDifference = Vector3.Dot(targetPosition - m_transform.position, character.WorldUp);
            float targetYOffset;
            bool inBounds = false;
            if (m_gravityRelativeDifference > m_settings.UpperYBound)
                targetYOffset = m_settings.UpperYBound;
            else if (m_gravityRelativeDifference < m_settings.LowerYBound)
                targetYOffset = m_settings.LowerYBound;
            else
            {
                m_currentYOffset = m_gravityRelativeDifference;
                targetYOffset = m_gravityRelativeDifference;
                inBounds = true;
            }

            bool groundHit = false;
            float currentYOffset = m_currentYOffset;
            if ((currentYOffset < 0f || currentYOffset > 0f) && m_raycastToGround)
            {
                Ray ray = new Ray(character.WorldPosition,
                    Mathf.Sign(currentYOffset) * character.GravityNormalised);
                if (Physics.Raycast(ray, out hit, Mathf.Abs(currentYOffset),
                    character.CollisionMask, QueryTriggerInteraction.Ignore))
                {
                    // Original hit distance is stored without restoring the
                    // prior offset sign, and bypasses out-of-bounds damping.
                    m_currentYOffset = hit.distance;
                    groundHit = true;
                }
            }
            if (!groundHit && !inBounds)
            {
                m_currentYOffset = Mathf.SmoothDamp(m_currentYOffset, targetYOffset,
                    ref m_currentDampeningVelocity, m_settings.OutOfBoundsDampening,
                    float.PositiveInfinity, deltaTime);
            }
            targetPosition += character.GravityNormalised * m_currentYOffset;
            return targetPosition;
        }

        // 0x06000cf4; ARM64 0x6eff54, x86_64 0x713f70. Bounds-enabled
        // is not checked and the original last gizmo color remains blue.
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            Gizmos.DrawWireSphere(m_transform.position, GizmoSphereRadius);
            if (m_targetType == TargetType.Character && m_characterManagerRef.IsValid())
            {
                Character character = m_characterManagerRef.Get().GetCurrentCharacterUnsafe();
                Vector3 upper = m_transform.position + character.WorldUp * m_settings.UpperYBound;
                Vector3 line = m_transform.forward * GizmoBoundLineLength;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(upper + line, upper - line);
                Vector3 lower = m_transform.position + character.WorldUp * m_settings.LowerYBound;
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(line + lower, lower - line);
            }
        }

        // 0x06000cf5; ARM64 0x6f0180, x86_64 0x7141c0. The genuine
        // inherited toggle is inlined as its field store in the original.
        public void DisableRotation(bool disableRotation)
        {
            m_smoothedTransformProxy.ToggleDisableRotationUpdate(disableRotation);
        }

        // 0x06000cf6; ARM64 0x6ef8f4, x86_64 0x713890.
        private Vector3 GetRelativeOffset()
        {
            return m_settings.OverrideRelativeOffset ? m_settings.RelativeOffset : m_relativeOffset;
        }

        // 0x06000cf7; ARM64 0x6f018c, x86_64 0x7141d0. The genuine
        // readonly reference initializer above runs before the base ctor.
        public CameraProxyTarget() { }
    }
}
