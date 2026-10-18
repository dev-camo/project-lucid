using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a6b: gravity towards the transform's forward axis.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class GravityAxis<T> : GravityTrigger<T> where T : Collider
    {
        [Tooltip("Sets the gravity collider height away from the central axis.")]
        [SerializeField] protected float m_height = 1f;

        // 06003be3 (shared 0x1391250): the offset is divided by its own length with
        // no zero guard, so a position on the axis yields a NaN direction.
        public override Vector3 GetGravity(Vector3 position)
        {
            Vector3 closestPoint = MathUtilities.GetClosestPointOnLine(transform.position, transform.forward, position);
            Vector3 offset = position - closestPoint;
            float distance = offset.magnitude;
            Vector3 direction = offset / distance;
            return direction * Description.CalculateGravity(distance);
        }

        protected GravityAxis() { }
    }
}
