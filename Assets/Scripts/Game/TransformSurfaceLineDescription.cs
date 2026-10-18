using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000815; bodies translated from the 1.10.1 x86-64 binary.
    // A straight line from this point to the next one.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class TransformSurfaceLineDescription : TransformSurfaceDescription
    {
        [Tooltip("Lerps the transform orientation linearly to the target over its length.")]
        [SerializeField]
        private bool m_lerpOrientationToTarget;

        private Quaternion m_rotationLocal;
        private Quaternion m_rotationLocalStart;
        private Quaternion m_rotationLocalEnd;

        // 06002ee3 (0x5FF7B0): the base set-up, then, for a line with a length, the rotation that looks along it
        // and the rotation the next surface starts with.
        public override void Initialise(TransformSurfaceFollower follower, Vector3 localPositionEnd, Vector3 localPositionPrev,
            Quaternion localRotationEnd)
        {
            base.Initialise(follower, localPositionEnd, localPositionPrev, localRotationEnd);
            if (m_distance != 0f)
            {
                m_rotationLocalStart = Quaternion.LookRotation(m_positionEndLocal - LocalPosition, Vector3.up);
                m_rotationLocalEnd = localRotationEnd;
            }
        }

        // 06002ee4 (0x5FF8F0): moves along the line. Once the time passes the end of the surface, the overshoot is
        // handed back through deltaTime and the position is held at the end it ran to.
        public override bool Update(out Vector3 position, ref float time, ref float deltaTime, int direction, bool usesTransform,
            Transform transform, Tracker2D tracker = null)
        {
            position = usesTransform ? transform.TransformPoint(LocalPosition) : PositionWorld;
            if (m_timeTotal > 0f)
            {
                time += deltaTime;
                Vector3 start;
                Vector3 end;
                if (usesTransform)
                {
                    start = transform.TransformPoint(LocalPosition);
                    end = transform.TransformPoint(m_positionEndLocal);
                }
                else
                {
                    start = m_positionStartWorld;
                    end = m_positionEndWorld;
                }
                if (time >= m_timeTotal)
                {
                    position = direction < 0 ? start : end;
                    deltaTime = time - m_timeTotal;
                    time = m_timeTotal;
                    return false;
                }
                deltaTime = 0f;
                float progress = GetProgressByTime(time, direction);
                position = Vector3.Lerp(start, end, progress);
                m_rotationLocal = m_lerpOrientationToTarget
                    ? Quaternion.Lerp(m_rotationLocalStart, m_rotationLocalEnd, progress)
                    : m_rotationLocalStart;
                return true;
            }
            return false;
        }

        // 06002ee5 (0x5FFAE0): the transform's rotation turned by the line's rotation, and half a turn more
        // when orientating to the direction while travelling backwards.
        public override Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null)
        {
            Quaternion rotation = transform.rotation * m_rotationLocal;
            if (direction < 0 && orientateToDirection)
                rotation = rotation * m_inverseRotation;
            return rotation;
        }

        // 06002ee6 (0x5FFCF0)
        public override Quaternion GetRotationLocal(Transform transform)
        {
            return m_rotationLocal;
        }

        // 06002ee7 (0x5FFD10): when the position moved, the start is the same point.
        public override bool SetPosition(Vector3 worldPosition, Transform transform)
        {
            bool moved = base.SetPosition(worldPosition, transform);
            if (moved)
                m_positionStartWorld = worldPosition;
            return moved;
        }

        // 06002ee8 (0x5FFDA0)
        public override void SetPositionEnd(Vector3 worldPosition, Transform transform)
        {
            m_positionEndWorld = worldPosition;
            m_positionEndLocal = transform.InverseTransformPoint(worldPosition);
        }

        // 06002ee9 (0x5FFDD0)
        protected override void CalculateDistance()
        {
            m_distance = Vector3.Distance(m_positionEndWorld, m_positionStartWorld);
        }
    }
}
