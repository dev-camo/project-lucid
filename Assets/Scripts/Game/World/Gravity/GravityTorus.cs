using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a7a: gravity along the normal of an elliptical torus
    // tube in the local XZ plane, described by the box size and tube radius and
    // scaled by the signed description value (inward when negative).
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(BoxCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GravityTorus : GravityTrigger<BoxCollider>
    {
        [Tooltip("Length of the box defining the gravity volume.")]
        [SerializeField]
        private Vector3 m_size = Vector3.one;
        [SerializeField] private float m_tubeRadius = 1f;

        // 06003c3f: the distance is measured to the closest point, but the direction
        // is the separate normal from ClosestPointOnTorus.
        public override Vector3 GetGravity(Vector3 position)
        {
            Vector3 segmentRadius = new Vector3(m_size.x * 0.5f - m_tubeRadius, m_size.y * 0.5f,
                m_size.z * 0.5f - m_tubeRadius);
            Vector3 localPosition = transform.InverseTransformPoint(position);
            Vector3 closestPoint = ClosestPointOnTorus(localPosition, m_tubeRadius, segmentRadius, out Vector3 normal);
            closestPoint = transform.TransformPoint(closestPoint);
            normal = transform.TransformDirection(normal);
            float distance = (position - closestPoint).magnitude;
            return normal * Description.CalculateGravity(distance);
        }

        // 06003c40: the collider grows by MaxDistance in every axis and by half of
        // it in the tube radius, then spans the outer ring's X and Z extents.
        protected override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            Vector3 size = m_size + Vector3.one * MaxDistance;
            float tubeRadius = m_tubeRadius + MaxDistance * 0.5f;
            Vector3 segmentRadius = new Vector3(size.x * 0.5f - tubeRadius, size.y * 0.5f, size.z * 0.5f - tubeRadius);
            float radiusX = MathUtilities.CalculateTorusPosition(90f, 0f, tubeRadius, segmentRadius).x;
            float radiusZ = MathUtilities.CalculateTorusPosition(0f, 0f, tubeRadius, segmentRadius).z;
            m_collider.center = Vector3.zero;
            m_collider.size = new Vector3(radiusX * 2f, size.y, radiusZ * 2f);
        }

        // 06003c41: the tube angle comes from the height against the segment's Y
        // radius, mirrored for points inside the core ellipse. The normal points
        // away from the core ellipse point, not from the returned point, and the
        // height is clamped to the tube's top rather than projected onto it.
        private static Vector3 ClosestPointOnTorus(Vector3 position, float tubeRadius, Vector3 segmentRadius,
            out Vector3 normal)
        {
            Vector3 top = MathUtilities.CalculateTorusPosition(0f, 90f, tubeRadius, segmentRadius);
            Vector2 corePoint = MathUtilities.ClosestPointOnEllipse(position.xz(), segmentRadius.x, segmentRadius.z);
            float tubeAngle = Mathf.Asin(Mathf.Clamp(position.y / segmentRadius.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (position.xz().sqrMagnitude < corePoint.sqrMagnitude)
                tubeAngle = 180f - tubeAngle;
            float radiusX = MathUtilities.CalculateTorusPosition(90f, tubeAngle, tubeRadius, segmentRadius).x;
            float radiusZ = MathUtilities.CalculateTorusPosition(0f, tubeAngle, tubeRadius, segmentRadius).z;
            Vector3 closestPoint = MathUtilities.ClosestPointOnEllipse(position.xz(), radiusX, radiusZ).ToXZ();
            normal = (position - corePoint.ToXZ()).normalized;
            closestPoint.y = Mathf.Clamp(position.y, -top.y, top.y);
            return closestPoint;
        }

        // 06003c42: the retail body is empty; any editor-only
        // drawing is absent from the player and is not reconstructed.
        protected override void DrawGizmo(float distance) { }
    }
}
