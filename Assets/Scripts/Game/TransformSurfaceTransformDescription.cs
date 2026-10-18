using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000818; bodies translated from the 1.10.1 x86-64 binary.
    // A point surface that sits wherever another transform is.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class TransformSurfaceTransformDescription : TransformSurfaceDescription
    {
        [Tooltip("Transform to follow.")]
        [SerializeField]
        private Transform m_transform;

        // 06002eff (0x600E80): the base validation, then the follow mode is forced to Time with no speed. The
        // retail body stores nothing for the follow time, so the stored time is passed back unchanged.
        public override void OnValidate()
        {
            base.OnValidate();
            SetFollowMode(FollowModeType.Time, 0f, FollowTime);
        }

        // 06002f00 (0x600F90): waits at the point; once the total time has passed the overshoot is handed back through deltaTime.
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

        // 06002f01 (0x601030): the followed transform's rotation, or the given transform's without one.
        public override Quaternion GetRotation(Transform transform, int direction, bool orientateToDirection, Tracker2D tracker = null)
        {
            if (m_transform != null)
                transform = m_transform;
            return transform.rotation;
        }

        // 06002f02 (0x6010B0): the followed transform's rotation relative to the given transform, or identity without one.
        public override Quaternion GetRotationLocal(Transform transform)
        {
            if (m_transform != null)
                return Quaternion.Inverse(transform.rotation) * m_transform.rotation;
            return Quaternion.identity;
        }

        // 06002f03 (0x601240): the followed transform's position replaces the given one. The start is stored
        // even when the position did not move.
        public override bool SetPosition(Vector3 worldPosition, Transform transform)
        {
            if (m_transform != null)
                worldPosition = m_transform.position;
            m_positionStartWorld = worldPosition;
            return base.SetPosition(worldPosition, transform);
        }

        // 06002f04 (0x601330): the end is the start; the given position is not used.
        public override void SetPositionEnd(Vector3 worldPosition, Transform transform)
        {
            m_positionEndWorld = m_positionStartWorld;
            m_positionEndLocal = transform.InverseTransformPoint(m_positionEndWorld);
        }

        // 06002f05 (0x601370)
        protected override void CalculateDistance()
        {
            m_distance = 0f;
        }
    }
}
