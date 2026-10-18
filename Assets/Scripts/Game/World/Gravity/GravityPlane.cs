using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a73: gravity along the transform's up axis,
    // scaled by the height above the transform.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(BoxCollider))]
    public class GravityPlane : GravityTrigger<BoxCollider>
    {
        [SerializeField] private Vector2 m_planeSize = new Vector2(10f, 10f);
        [Tooltip("If true, does not automatically adjust the box collider size to match the gravity distance.")]
        [SerializeField]
        private bool m_decoupleColliderSize;

        // 06003c04: the plane is unbounded here; only the description limits it.
        public override Vector3 GetGravity(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            Vector3 up = transform.up;
            float distance = Vector3.Dot(offset, up);
            return up * Description.CalculateGravity(distance);
        }

        // 06003c05: a decoupled collider keeps its authored layout but is still
        // made a trigger by the base.
        protected override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            if (m_decoupleColliderSize)
                return;
            m_collider.center = Vector3.up * (MaxDistance * 0.5f);
            m_collider.size = new Vector3(m_planeSize.x, MaxDistance, m_planeSize.y);
        }

        protected override void DrawGizmo(float distance)
        {
            Transform planeTransform = transform;
            Gizmos.matrix = Matrix4x4.TRS(planeTransform.position + planeTransform.up * (0.5f * distance),
                planeTransform.rotation, planeTransform.localScale);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(m_planeSize.x, distance, m_planeSize.y));
        }
    }
}
