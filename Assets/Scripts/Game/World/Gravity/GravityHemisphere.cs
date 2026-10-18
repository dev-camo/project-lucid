using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a72: the sphere's gravity on or above the plane
    // through the transform perpendicular to its up axis.
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(SphereCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GravityHemisphere : GravitySphere
    {
        // 06003c01: the plane itself and unordered heights keep the sphere's gravity.
        public override Vector3 GetGravity(Vector3 position)
        {
            if (Vector3.Dot(position - transform.position, transform.up) < 0f)
                return Vector3.zero;
            return base.GetGravity(position);
        }

        // 06003c02: the retail body is empty; any editor-only
        // drawing is absent from the player and is not reconstructed.
        protected override void DrawGizmo(float distance) { }
    }
}
