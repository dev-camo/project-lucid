using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a6c: gravity towards the nearest face of a box
    // that rises MaxDistance above the transform.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(BoxCollider))]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GravityBox : GravityTrigger<BoxCollider>
    {
        [SerializeField]
        [Tooltip("Length of the box defining the gravity volume.")]
        private float m_length = 1f;
        [Tooltip("Planes at either end of the box are also used for gravity calculation.")]
        [SerializeField]
        private bool m_applyAtEnds;

        // 06003be5: faces are tried in this order and a later face wins ties. The
        // first face is always taken, so a NaN distance still selects a face.
        public override Vector3 GetGravity(Vector3 position)
        {
            Transform boxTransform = transform;
            Vector3 up = boxTransform.up;
            Vector3 right = boxTransform.right;
            float bestDistance = float.PositiveInfinity;
            Vector3 bestNormal = Vector3.zero;
            SetPlaneIfCloser(position, up, MaxDistance, ref bestDistance, ref bestNormal);
            SetPlaneIfCloser(position, -up, MaxDistance, ref bestDistance, ref bestNormal);
            SetPlaneIfCloser(position, right, MaxDistance, ref bestDistance, ref bestNormal);
            SetPlaneIfCloser(position, -right, MaxDistance, ref bestDistance, ref bestNormal);
            if (m_applyAtEnds)
            {
                Vector3 forward = boxTransform.forward;
                SetPlaneIfCloser(position, forward, m_length, ref bestDistance, ref bestNormal);
                SetPlaneIfCloser(position, -forward, m_length, ref bestDistance, ref bestNormal);
            }
            return bestNormal * Description.CalculateGravity(bestDistance);
        }

        // 06003be6: equal and unordered distances replace the current best.
        private void SetPlaneIfCloser(Vector3 position, Vector3 normal, float maxDistance,
            ref float bestDistance, ref Vector3 bestNormal)
        {
            float distance = CalculateDistance(position, normal, maxDistance);
            if (distance > bestDistance)
                return;
            bestDistance = distance;
            bestNormal = normal;
        }

        // 06003be7: signed distance above the face half the extent away from the
        // collider centre, measured along the face normal.
        private float CalculateDistance(Vector3 position, Vector3 planeNormal, float maxDistance)
        {
            Vector3 center = transform.TransformPoint(m_collider.center);
            Vector3 planePosition = center - planeNormal * (maxDistance * 0.5f);
            return Vector3.Dot(position - planePosition, planeNormal);
        }

        protected override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            m_collider.center = Vector3.up * (MaxDistance * 0.5f);
            float maxDistance = MaxDistance;
            m_collider.size = new Vector3(maxDistance, maxDistance, m_length);
        }

        protected override void DrawGizmo(float distance)
        {
            Transform boxTransform = transform;
            Gizmos.matrix = Matrix4x4.TRS(boxTransform.position + boxTransform.up * (0.5f * distance),
                boxTransform.rotation, boxTransform.localScale);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(MaxDistance, distance, m_length));
        }
    }
}
