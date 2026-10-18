using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a79: gravity along the tracked surface location's
    // up axis, valid while the height stays inside the description.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GravitySurfacePlane : GravitySurface
    {
        private float m_distanceAlongNormal;

        // 06003c3a: the height is recorded before the Active gate, so inactive
        // queries still refresh what ValidateTrackerLocation reads.
        public override Vector3 GetGravity(Vector3 position)
        {
            Vector3 offset = position - Location.m_worldPosition;
            Vector3 normal = Location.m_worldRotation * Vector3.up;
            m_distanceAlongNormal = Vector3.Dot(offset, normal);
            if (!Active)
                return Vector3.zero;
            return normal * Description.CalculateGravity(m_distanceAlongNormal);
        }

        // 06003c3b: the -0.25 surface tolerance is the lower height limit and also
        // the squared lateral-bounds threshold. A NaN height passes both limits.
        protected override bool ValidateTrackerLocation()
        {
            if (Location.m_positionBoundsInfo.AtEndEdge)
                return false;
            if (m_distanceAlongNormal < SurfaceTolerance || m_distanceAlongNormal > Description.MaxDistance)
                return false;
            return !Location.m_positionBoundsInfo.OnLateralBounds(SurfaceTolerance);
        }

        // 06003c3c: the retail body is empty.
        protected override void CalculateCachedValues() { }

        protected override void DrawGizmo(float distance)
        {
            if (Location.m_surface == null)
                return;
            Quaternion rotation = Location.m_worldRotation;
            Gizmos.matrix = Matrix4x4.TRS(Location.m_worldPosition + rotation * Vector3.up * (0.5f * distance),
                rotation, transform.localScale);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(10f, distance, 10f));
        }
    }
}
