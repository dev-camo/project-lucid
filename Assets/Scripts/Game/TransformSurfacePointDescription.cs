using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000816; bodies translated from the 1.10.1 x86-64 binary.
    // A surface that is a single point: it has no length, so the target waits there for the follow time.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TransformSurfacePointDescription : TransformSurfaceDescription
    {
        private Quaternion m_rotationLocal;

        public override bool Orientate => false;

        // 06002eec (0x5FFF30): the base set-up, then the rotation that looks from the previous point at this one.
        public override void Initialise(TransformSurfaceFollower follower, Vector3 localPositionEnd, Vector3 localPositionPrev,
            Quaternion localRotationEnd)
        {
            base.Initialise(follower, localPositionEnd, localPositionPrev, localRotationEnd);
            m_rotationLocal = Quaternion.LookRotation(LocalPosition - localPositionPrev, Vector3.up);
        }

        // 06002eed (0x600050): the base validation, then the follow mode is forced to Time with no speed. The
        // retail body stores nothing for the follow time, so the stored time is passed back unchanged.
        public override void OnValidate()
        {
            base.OnValidate();
            SetFollowMode(FollowModeType.Time, 0f, FollowTime);
        }

        // 06002eee (0x600160): waits at the point; once the total time has passed the overshoot is handed back through deltaTime.
        public override bool Update(out Vector3 position, ref float time, ref float deltaTime, int direction, bool usesTransform,
            Transform transform, Tracker2D tracker = null)
        {
            position = usesTransform ? transform.TransformPoint(LocalPosition) : PositionWorld;
            if (m_timeTotal > 0f)
            {
                time += deltaTime;
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

        // 06002eef (0x600200): the transform's rotation turned by the look rotation, and half a turn more
        // when orientating to the direction while travelling backwards.
        public override Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null)
        {
            Quaternion rotation = transform.rotation * m_rotationLocal;
            if (direction < 0 && orientateToDirection)
                rotation = rotation * m_inverseRotation;
            return rotation;
        }

        // 06002ef0 (0x600410)
        public override Quaternion GetRotationLocal(Transform transform)
        {
            return m_rotationLocal;
        }

        // 06002ef1 (0x600430): when the position moved, the start is the same point.
        public override bool SetPosition(Vector3 worldPosition, Transform transform)
        {
            bool moved = base.SetPosition(worldPosition, transform);
            if (moved)
                m_positionStartWorld = worldPosition;
            return moved;
        }

        // 06002ef2 (0x6004C0): the end is the start; the given position is not used.
        public override void SetPositionEnd(Vector3 worldPosition, Transform transform)
        {
            m_positionEndWorld = m_positionStartWorld;
            m_positionEndLocal = transform.InverseTransformPoint(m_positionEndWorld);
        }

        // 06002ef3 (0x600500)
        protected override void CalculateDistance()
        {
            m_distance = 0f;
        }
    }
}
