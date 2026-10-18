using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a70: an axis volume whose box collider is sized from its distance and height.
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(BoxCollider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GravityCylinder : GravityAxis<BoxCollider>
    {
        // 06003bf2: the box encloses the cylinder of radius MaxDistance.
        protected override void CalculateCachedValues()
        {
            base.CalculateCachedValues();
            m_collider.center = Vector3.zero;
            float diameter = MaxDistance * 2f;
            m_collider.size = new Vector3(diameter, diameter, m_height);
        }

        // 06003bf3: the retail body is empty; any editor-only
        // drawing is absent from the player and is not reconstructed.
        protected override void DrawGizmo(float distance) { }
    }
}
