using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ac9: turns this transform towards a target, or
    // back towards the default target's Awake position once the target clears.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LookAtTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Whether to change the vertical coordinate of the target.")]
        private bool m_retainVerticalAxis;
        [Tooltip("Time it will take to reach the target. 0 will snap to target.")]
        [SerializeField]
        private float m_smoothTime;
        [Tooltip("Optionally clamp the max smooth speed.  0 will leave unclamped.")]
        [SerializeField]
        private float m_maxSpeed;
        [Tooltip("Target to default to if look at target is cleared.")]
        [SerializeField]
        private Transform m_defaultTarget;
        [Tooltip("Clears the target transform when component is disabled.")]
        [SerializeField]
        private bool m_clearTargetOnDisable = true;
        [SerializeField]
        [Tooltip("If set, uses the constraint angles to limit look at rotation on each axis.")]
        private bool m_useConstraintAngles;
        [SerializeField]
        [ShowIf("m_useConstraintAngles", null)]
        [Min(0)]
        private Vector2 m_horizontalConstraintAngles;
        [SerializeField]
        [ShowIf("m_useConstraintAngles", null)]
        [Min(0)]
        private Vector2 m_verticalConstraintAngles;
        [Min(0)]
        [SerializeField]
        [ShowIf("m_useConstraintAngles", null)]
        private float m_gizmoConstraintDistance;
        private Vector3 m_worldUp;
        private Transform m_target;
        private bool m_lookAtDefault;
        private Vector3 m_defaultTargetPosition;
        private Vector3 m_velocity;

        // 06003e42: the up axis and the default target position are both fixed here.
        private void Awake()
        {
            m_worldUp = transform.up;
            if (m_defaultTarget == null)
                return;
            m_defaultTargetPosition = m_defaultTarget.position;
        }

        public void SetLookAt(Collider target)
        {
            SetLookAt(target.transform);
        }

        public void SetLookAt(Transform target)
        {
            m_target = target;
            m_lookAtDefault = m_target == null && m_defaultTarget != null;
        }

        // 06003e45: the return to the default ends once the smoothing velocity has
        // settled. Without smoothing the velocity is never updated, so the outcome
        // depends on its last value (zero unless smoothing ran before).
        private void Update()
        {
            Vector3 lookAtPosition;
            if (m_target != null)
                lookAtPosition = m_target.position;
            else if (m_lookAtDefault)
                lookAtPosition = m_defaultTargetPosition;
            else
                return;
            RetainVerticalAxis(ref lookAtPosition);
            SmoothToTarget(ref lookAtPosition);
            UpdateConstraints(ref lookAtPosition);
            transform.LookAt(lookAtPosition, m_worldUp);
            if (m_lookAtDefault)
                m_lookAtDefault = m_velocity.sqrMagnitude > 0.0001f;
        }

        // 06003e46: removes the target's height above this transform along the Awake up axis.
        private void RetainVerticalAxis(ref Vector3 lookAtPosition)
        {
            if (!m_retainVerticalAxis)
                return;
            float height = Vector3.Dot(lookAtPosition - transform.position, m_worldUp);
            lookAtPosition -= m_worldUp * height;
        }

        // 06003e47: smooths the look direction from the current forward and keeps
        // the target's distance. The offset is divided without a zero guard.
        private void SmoothToTarget(ref Vector3 lookAtPosition)
        {
            if (m_smoothTime == 0f)
                return;
            Vector3 offset = lookAtPosition - transform.position;
            float distance = offset.magnitude;
            Vector3 direction = offset / distance;
            Vector3 smoothedDirection = Vector3.SmoothDamp(transform.forward, direction, ref m_velocity, m_smoothTime,
                m_maxSpeed > 0f ? m_maxSpeed : Mathf.Infinity, Time.deltaTime);
            lookAtPosition = transform.position + smoothedDirection * distance;
        }

        // 06003e48: the four limits are planes through the parent's position, each
        // turned by its angle from the parent's right or up axis.
        private void UpdateConstraints(ref Vector3 lookAtPosition)
        {
            if (!m_useConstraintAngles)
                return;
            Transform parent = transform.parent;
            Vector3 origin = parent.position;
            Vector3 up = parent.up;
            Vector3 right = parent.right;
            UpdateTargetConstraint(ref lookAtPosition, origin, up, right, m_horizontalConstraintAngles.x);
            UpdateTargetConstraint(ref lookAtPosition, origin, -up, -right, m_horizontalConstraintAngles.y);
            UpdateTargetConstraint(ref lookAtPosition, origin, right, -up, m_verticalConstraintAngles.x);
            UpdateTargetConstraint(ref lookAtPosition, origin, -right, up, m_verticalConstraintAngles.y);
        }

        // 06003e49: a zero angle disables the limit. A target beyond the plane, or
        // an unordered distance, is moved back onto it.
        private static void UpdateTargetConstraint(ref Vector3 target, Vector3 origin, Vector3 axis, Vector3 normal,
            float constraintAngle)
        {
            if (constraintAngle == 0f)
                return;
            Vector3 planeNormal = Quaternion.AngleAxis(constraintAngle, axis) * normal;
            float distance = Vector3.Dot(target - origin, planeNormal);
            if (distance <= 0f)
                return;
            target -= planeNormal * distance;
        }

        public void EndLookAt()
        {
            SetLookAt((Transform)null);
        }

        private void OnDisable()
        {
            if (m_clearTargetOnDisable)
                EndLookAt();
        }
    }
}
