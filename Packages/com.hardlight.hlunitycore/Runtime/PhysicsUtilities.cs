using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000194: complete two-field/three-method
    // type. Field initialization generates original 0x06000b60 while retaining
    // BeforeFieldInit; an explicit static constructor would change that flag.
    public static class PhysicsUtilities
    {
        private const int MaxNumRaycastHits = 16;
        private static RaycastHit[] m_raycastHits = new RaycastHit[MaxNumRaycastHits];

        // 0x06000b5e: retain the shared limited buffer and native enumeration order.
        // Choose by collider-transform distance from the camera, not hit distance.
        // Only null disables the tag comparison; empty tags reach the engine.
        public static GameObject ProjectScreenPointToGameObject(Camera camera, Vector2 position, string tag = null)
        {
            Ray ray = camera.ScreenPointToRay(position);
            int count = Physics.RaycastNonAlloc(ray.origin, ray.direction, m_raycastHits);
            if (count < 1) return null;

            GameObject nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider collider = m_raycastHits[i].collider;
                if (collider == null) continue;
                if (tag != null && !collider.gameObject.CompareTag(tag)) continue;
                float distance = (collider.transform.position - camera.transform.position).sqrMagnitude;
                // Equal distances keep the first hit. The native reject-if->=
                // branch also admits unordered values; do not invert it to <.
                if (distance >= nearestDistance) continue;
                nearest = collider.gameObject;
                nearestDistance = distance;
            }
            return nearest;
        }

        // 0x06000b5f: sweep from the supplied position minus displacement. The
        // original ignores collider.center/direction and uses transform.up with
        // the full scaled half-height, without subtracting the sphere radius.
        public static int CapsuleCastNonAlloc(CapsuleCollider collider, Vector3 position, Vector3 offset, int layerMask, RaycastHit[] results)
        {
            Transform transform = collider.transform;
            Vector3 up = transform.up;
            Vector3 scale = transform.lossyScale;
            float radius = collider.radius;
            float height = collider.height;
            float distance = offset.magnitude;
            Vector3 direction = offset.normalized;
            distance = Mathf.Max(distance, float.Epsilon);

            Vector3 halfHeight = up * (height * Mathf.Abs(scale.y) * 0.5f);
            Vector3 upper = position + halfHeight - offset;
            Vector3 lower = position - halfHeight - offset;
            radius *= Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return Physics.CapsuleCastNonAlloc(upper, lower, radius, direction, results, distance, layerMask);
        }
    }
}
