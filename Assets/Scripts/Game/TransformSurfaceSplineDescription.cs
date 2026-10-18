using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000817; bodies translated from the 1.10.1 x86-64 binary.
    // A surface that follows a spline. The target travels along a tracker that is moved along the spline
    // surface by the distance covered each frame.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class TransformSurfaceSplineDescription : TransformSurfaceDescription
    {
        [SerializeField]
        private Spline m_spline;

        // 06002ef5 (0x600560)
        public TransformSurfaceSplineDescription(Spline spline = null)
        {
            m_spline = spline;
        }

        // 06002ef6 (0x6005D0): the start (the end when travelling backwards). With a tracker, the tracker is
        // placed there and then moved a whole length against the direction; its location is the answer.
        public override Vector3 Teleport(int direction, bool usesTransform, Transform transform, Tracker2D tracker = null)
        {
            Vector3 position;
            if (usesTransform)
                position = transform.TransformPoint(direction > 0 ? LocalPosition : m_positionEndLocal);
            else
                position = direction > 0 ? m_positionStartWorld : m_positionEndWorld;
            if (tracker == null)
                return position;
            tracker.Teleport(position);
            tracker.MoveLocal(Vector3.forward * (m_distance * -direction));
            return tracker.Location.m_worldPosition;
        }

        // 06002ef7 (0x6006E0): with a tracker, moves it along the spline by the share of the length covered in
        // this step; the position is then the tracker's. Once the time passes the end of the surface, the
        // overshoot is handed back through deltaTime.
        public override bool Update(out Vector3 position, ref float time, ref float deltaTime, int direction, bool usesTransform,
            Transform transform, Tracker2D tracker)
        {
            position = usesTransform ? transform.TransformPoint(LocalPosition) : PositionWorld;
            if (m_timeTotal > 0f)
            {
                time += deltaTime;
                if (tracker != null)
                {
                    float step = direction * m_distance * deltaTime / m_timeTotal;
                    tracker.MoveLocal(Vector3.forward * step);
                    position = tracker.Location.m_worldPosition;
                }
                // An unordered compare (NaN) waits, as the retail jae does.
                if (time >= m_timeTotal)
                {
                    deltaTime = time - m_timeTotal;
                    time = m_timeTotal;
                    return false;
                }
                deltaTime = 0f;
                return true;
            }
            return false;
        }

        // 06002ef8 (0x600890): the tracker's rotation, or the transform's without one, and half a turn more
        // when orientating to the direction while travelling backwards.
        public override Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null)
        {
            Quaternion rotation = tracker != null ? tracker.Location.m_worldRotation : transform.rotation;
            if (direction < 0 && orientateToDirection)
                rotation = rotation * m_inverseRotation;
            return rotation;
        }

        // 06002ef9 (0x600A00): the spline's rotation relative to the given transform, or identity without a spline.
        public override Quaternion GetRotationLocal(Transform transform)
        {
            if (m_spline != null)
                return Quaternion.Inverse(transform.rotation) * m_spline.transform.rotation;
            return Quaternion.identity;
        }

        // 06002efa (0x600BA0): when the position moved, the start is the same point.
        public override bool SetPosition(Vector3 worldPosition, Transform transform)
        {
            bool moved = base.SetPosition(worldPosition, transform);
            if (moved)
                m_positionStartWorld = worldPosition;
            return moved;
        }

        // 06002efb (0x600C30)
        public override void SetPositionEnd(Vector3 worldPosition, Transform transform)
        {
            m_positionEndWorld = worldPosition;
            m_positionEndLocal = transform.InverseTransformPoint(worldPosition);
        }

        // 06002efc (0x600C60): the spline's length, kept as it was without a spline.
        protected override void CalculateDistance()
        {
            if (m_spline == null)
                return;
            ISpline spline = m_spline.GetSpline();
            if (spline != null)
                m_distance = spline.Length;
        }

        // 06002efd (0x600D60): measures the end of the spline on the tracker: from the start, a whole length
        // along it. The tracker is left at the start.
        public override void OnValidateTracker(Tracker2D tracker)
        {
            if (tracker == null)
                return;
            tracker.Teleport(m_positionStartWorld);
            tracker.MoveLocal(Vector3.forward * m_distance);
            m_positionEndWorld = tracker.Location.m_worldPosition;
            tracker.Teleport(m_positionStartWorld);
        }

        // 06002efe (0x600E70)
        public override Spline GetSpline()
        {
            return m_spline;
        }
    }
}
