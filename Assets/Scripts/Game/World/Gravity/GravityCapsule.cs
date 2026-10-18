using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a6d: an axis volume sized by a capsule collider.
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(CapsuleCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GravityCapsule : GravityAxis<CapsuleCollider>
    {
        // CapsuleCollider.direction indices.
        private enum AxisDirection
        {
            AxisX,
            AxisY,
            AxisZ
        }

        // 06003beb: the capsule runs along the transform's forward axis.
        protected override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            m_collider.center = Vector3.zero;
            m_collider.direction = (int)AxisDirection.AxisZ;
            m_collider.height = m_height;
            m_collider.radius = MaxDistance;
        }

        // 06003bec: the retail body is empty; any editor-only
        // drawing is absent from the player and is not reconstructed.
        protected override void DrawGizmo(float distance) { }
    }
}
