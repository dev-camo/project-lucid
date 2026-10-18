using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a77: gravity along the offset from the transform's
    // position, scaled by the signed description value (inward when negative).
    [RequireComponent(typeof(SphereCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GravitySphere : GravityTrigger<SphereCollider>
    {
        // 06003c28: no zero-length guard, so the centre itself yields NaN.
        public override Vector3 GetGravity(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            float distance = offset.magnitude;
            return offset / distance * Description.CalculateGravity(distance);
        }

        protected override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            m_collider.center = Vector3.zero;
            m_collider.radius = MaxDistance;
        }

        protected override void DrawGizmo(float distance)
        {
            Gizmos.DrawWireSphere(transform.position, distance);
        }
    }
}
