using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000811; bodies translated from the 1.10.1 x86-64 binary.
    // A surface that runs around part of a circle in the plane perpendicular to the axis.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TransformSurfaceCircleDescription : TransformSurfaceDescription
    {
        [SerializeField]
        [Tooltip("Radius of the circle to follow.")]
        private float m_radius = 10f;

        [SerializeField]
        [Tooltip("Start angle on the circle to follow.")]
        private float m_angleStart;

        [SerializeField]
        [Tooltip("End angle on the circle to follow.")]
        private float m_angleEnd = 360f;

        [SerializeField]
        [Tooltip("Direction is clockwise or anti-clockwise.")]
        private bool m_clockwise = true;

        [Tooltip("The axis of rotation.")]
        [SerializeField]
        private Vector3 m_axis = Vector3.up;

        private Vector3 m_forward;
        private Vector3 m_right;
        private Vector3 m_up;
        private float m_angle;

        public float Radius => m_radius;

        public float AngleStart => m_angleStart;

        public float AngleEnd => m_angleEnd;

        public bool Clockwise => m_clockwise;

        public Vector3 Forward => m_forward;

        public Vector3 Right => m_right;

        public Vector3 Up => m_up;

        // 06002eb0 (0x5FE320): advances along the arc. Once the time passes the end of the surface, the
        // overshoot is handed back through deltaTime and the position is held at the arc's far end.
        public override bool Update(out Vector3 position, ref float time, ref float deltaTime, int direction, bool usesTransform,
            Transform transform, Tracker2D tracker = null)
        {
            position = usesTransform ? transform.TransformPoint(LocalPosition) : PositionWorld;
            if (m_timeTotal > 0f)
            {
                time += deltaTime;
                if (time >= m_timeTotal)
                {
                    m_angle = direction >= 0 ? m_angleEnd : m_angleStart;
                    position = CalculatePosition(m_angle, usesTransform, transform);
                    deltaTime = time - m_timeTotal;
                    time = m_timeTotal;
                    return false;
                }
                deltaTime = 0f;
                m_angle = GetProgressByTime(time, direction) * (m_angleEnd - m_angleStart) + m_angleStart;
                position = CalculatePosition(m_angle, usesTransform, transform);
                return true;
            }
            return false;
        }

        // 06002eb1 (0x5FE950)
        public override Vector3 Teleport(int direction, bool usesTransform, Transform transform, Tracker2D tracker = null)
        {
            m_angle = direction <= 0 ? m_angleEnd : m_angleStart;
            return CalculatePosition(m_angle, usesTransform, transform);
        }

        // 06002eb2 (0x5FE980): the angle is taken relative to the start angle.
        public override float GetTimeByAngle(float angle)
        {
            float angleRange = m_angleEnd - m_angleStart;
            if (angle >= angleRange)
                return m_timeTotal;
            return m_timeTotal * angle / angleRange;
        }

        // 06002eb3 (0x5FE9B0): the transform's rotation turned by the arc rotation, and half a turn more
        // when orientating to the direction while travelling against the arc's sense (backwards when
        // clockwise, forwards when anticlockwise).
        public override Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null)
        {
            Quaternion rotationLocal = GetRotationLocal(transform);
            Quaternion rotation = transform.rotation * rotationLocal;
            if (orientateToDirection && ((direction < 0 && m_clockwise) || (direction > 0 && !m_clockwise)))
                rotation = rotation * m_inverseRotation;
            return rotation;
        }

        // 06002eb4 (0x5FEBF0)
        public override Quaternion GetRotationLocal(Transform transform)
        {
            return Quaternion.AngleAxis(m_clockwise ? m_angle : -m_angle, m_axis);
        }

        // 06002eb5 (0x5FEC30): the start position is rebuilt when the position moved or, if it did not,
        // while the transform's forward or right still matches the stored basis; otherwise false.
        public override bool SetPosition(Vector3 worldPosition, Transform transform)
        {
            bool forwardUnchanged = m_forward.WithinTolerance(transform.forward, ValueTolerance);
            bool rightUnchanged = m_right.WithinTolerance(transform.right, ValueTolerance);
            bool positionChanged = base.SetPosition(worldPosition, transform);
            if (!positionChanged && !(forwardUnchanged | rightUnchanged))
                return false;
            m_positionStartWorld = CalculatePosition(m_angleStart, true, transform);
            return true;
        }

        // 06002eb6 (0x5FEDE0): the given world position is not used; the end is the arc's end angle.
        public override void SetPositionEnd(Vector3 worldPosition, Transform transform)
        {
            m_positionEndWorld = CalculatePosition(m_angleEnd, true, transform);
            m_positionEndLocal = transform.InverseTransformPoint(m_positionEndWorld);
        }

        // 06002eb7 (0x5FE4D0): with a transform, first rebuilds the circle's basis from the transform's
        // rotation turned from up to the axis; the centre is the local position in world space.
        private Vector3 CalculatePosition(float angleDeg, bool usesTransform, Transform transform)
        {
            Vector3 centre;
            if (usesTransform)
            {
                Quaternion fromUp = Quaternion.FromToRotation(Vector3.up, m_axis);
                Quaternion rotation = transform.rotation * fromUp;
                m_forward = rotation * Vector3.forward;
                m_right = rotation * Vector3.right;
                m_up = rotation * Vector3.up;
                centre = transform.TransformPoint(LocalPosition);
            }
            else
            {
                centre = PositionWorld;
            }
            float radians = angleDeg * Mathf.Deg2Rad;
            if (!m_clockwise)
                radians = -radians;
            return centre + (m_right * Mathf.Sin(radians) + m_forward * Mathf.Cos(radians)) * m_radius;
        }

        // 06002eb8 (0x5FEE30)
        protected override void CalculateDistance()
        {
            m_distance = (m_angleEnd - m_angleStart) * (m_radius * (Mathf.PI * 2f)) / 360f;
        }

        // 06002eb9 (0x5FEE70)
        public override void SetExtentsMultiplier(float extentsMultiplier)
        {
            m_radius *= extentsMultiplier / m_extentsMultiplier;
            base.SetExtentsMultiplier(extentsMultiplier);
        }
    }
}
