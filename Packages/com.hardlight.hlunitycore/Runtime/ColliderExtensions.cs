using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Complete original Core0200005e, used directly by original collision projection.
    public static class ColliderExtensions
    {
        private const int MaximumLayers = 32;

        //06000257: the collider layer is sampled once before all32 engine queries.
        public static int GetLayerMask(this Collider collider)
        {
            int colliderLayer = collider.gameObject.layer;
            int layerMask = 0;
            for (int layer = 0; layer < MaximumLayers; ++layer)
                if (!Physics.GetIgnoreLayerCollision(colliderLayer, layer))
                    layerMask |= 1 << layer;
            return layerMask;
        }

        //06000258: only the original sphere and capsule cases are supported.
        public static int PhysicsCastFromCollider(this Collider collider, Vector3 movementOffset, LayerMask layerMask, RaycastHit[] raycastHits, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (collider is SphereCollider sphere)
                return PhysicsCastFromCollider(sphere, movementOffset, layerMask, raycastHits, queryTriggerInteraction);
            if (collider is CapsuleCollider capsule)
                return PhysicsCastFromCollider(capsule, movementOffset, layerMask, raycastHits, false, queryTriggerInteraction);
            return 0;
        }

        //06000259: a zero normalized offset still casts forward by Single.Epsilon.
        // Debug rays occur after the actual cast, using the original full offset.
        public static int PhysicsCastFromCollider(this CapsuleCollider capsule, Vector3 movementOffset, LayerMask layerMask, RaycastHit[] raycastHits, bool debugDrawRays = false, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (capsule == null)
                return 0;
            Vector3 topSphereCentre;
            Vector3 bottomSphereCentre;
            float radius;
            capsule.ToWorldSpace(out topSphereCentre, out bottomSphereCentre, out radius);
            float distance = movementOffset.magnitude;
            Vector3 direction = movementOffset.normalized;
            if (movementOffset.normalized == Vector3.zero)
                direction = Vector3.forward;
            distance = Mathf.Max(distance, float.Epsilon);
            int hits = Physics.CapsuleCastNonAlloc(topSphereCentre, bottomSphereCentre, radius, direction, raycastHits, distance, layerMask, queryTriggerInteraction);
            if (debugDrawRays)
            {
                Debug.DrawRay(topSphereCentre, movementOffset, Color.white);
                Debug.DrawRay(bottomSphereCentre, movementOffset, Color.white);
            }
            return hits;
        }

        //0600025a: absolute scale selects radius perpendicular to the capsule axis.
        // Radius publishes before later engine access; the inverse height comparison
        // preserves the observed original unordered branch before endpoint writes.
        private static void ToWorldSpace(this CapsuleCollider capsule, out Vector3 topSphereCentre, out Vector3 bottomSphereCentre, out float radius)
        {
            Transform transform = capsule.transform;
            radius = capsule.radius;
            float height = capsule.height;
            Vector3 scale = transform.lossyScale.Abs();
            Vector3 axis = Vector3.zero;
            switch (capsule.direction)
            {
                case 0:
                    radius *= Mathf.Max(scale.y, scale.z);
                    axis = Vector3.right;
                    height *= scale.x;
                    break;
                case 1:
                    radius *= Mathf.Max(scale.x, scale.z);
                    axis = Vector3.up;
                    height *= scale.y;
                    break;
                case 2:
                    radius *= Mathf.Max(scale.x, scale.y);
                    axis = Vector3.forward;
                    height *= scale.z;
                    break;
            }
            Vector3 centre = transform.TransformPoint(capsule.center);
            Vector3 top = centre;
            Vector3 bottom = centre;
            if (!(height <= radius * 2f))
            {
                Vector3 extent = transform.TransformDirection(axis) * (height * 0.5f - radius);
                top = centre + extent;
                bottom = centre - extent;
            }
            topSphereCentre = top;
            bottomSphereCentre = bottom;
        }

        //0600025b: the sphere retains its authored radius; the source uses the
        // real collider bounds center and does not add an unobserved scale factor.
        public static int PhysicsCastFromCollider(this SphereCollider collider, Vector3 movementOffset, LayerMask layerMask, RaycastHit[] raycastHits, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            if (collider == null)
                return 0;
            Vector3 centre = collider.bounds.center;
            float distance = movementOffset.magnitude;
            Vector3 direction = movementOffset.normalized;
            if (movementOffset.normalized == Vector3.zero)
                direction = Vector3.forward;
            distance = Mathf.Max(distance, float.Epsilon);
            return Physics.SphereCastNonAlloc(centre, collider.radius, direction, raycastHits, distance, layerMask, queryTriggerInteraction);
        }

        //0600025c/25d: ref outputs survive a zero-hit query. Both use the original
        // Vector3 overload without an explicit QueryTriggerInteraction parameter.
        public static bool ProjectRaycastLocation(Vector3 position, Vector3 offset, LayerMask layerMask, RaycastHit[] raycastHits, ref Vector3 collisionPosition, ref Vector3 collisionNormal, List<GameObject> includeList = null)
        {
            int hits = Physics.RaycastNonAlloc(position, offset.normalized, raycastHits, offset.magnitude, layerMask);
            return CollateHits(hits, raycastHits, ref collisionPosition, ref collisionNormal, includeList);
        }

        public static bool ProjectRaycastToLocationClosest(Vector3 position, Vector3 offset, LayerMask layerMask, RaycastHit[] raycastHits, ref Vector3 collisionPosition, ref Vector3 collisionNormal, List<GameObject> includeList = null)
        {
            int hits = Physics.RaycastNonAlloc(position, offset.normalized, raycastHits, offset.magnitude, layerMask);
            return ClosestHit(position, hits, raycastHits, ref collisionPosition, ref collisionNormal, includeList);
        }

        //0600025e: average normals remain unnormalized. Nonzero invalid counts
        // clear both refs first, and faults retain any already accumulated values.
        private static bool CollateHits(int numberOfHits, RaycastHit[] raycastHits, ref Vector3 collisionPosition, ref Vector3 collisionNormal, List<GameObject> includeList)
        {
            if (numberOfHits == 0)
                return false;
            collisionPosition = Vector3.zero;
            collisionNormal = Vector3.zero;
            if (numberOfHits < 0)
                return false;
            int includedHits = numberOfHits;
            if (includeList != null)
            {
                includedHits = 0;
                for (int i = 0; i < numberOfHits; ++i)
                {
                    RaycastHit hit = raycastHits[i];
                    if (!includeList.Contains(hit.transform.gameObject))
                        continue;
                    collisionPosition += hit.point;
                    collisionNormal += hit.normal;
                    ++includedHits;
                }
            }
            else
            {
                for (int i = 0; i < numberOfHits; ++i)
                {
                    RaycastHit hit = raycastHits[i];
                    collisionPosition += hit.point;
                    collisionNormal += hit.normal;
                }
            }
            if (includedHits == 0)
                return false;
            collisionPosition /= includedHits;
            collisionNormal /= includedHits;
            return true;
        }

        //0600025f: equal-distance hits replace earlier hits. The inverse ordered
        // guard also publishes a NaN hit before the final ordered success test.
        private static bool ClosestHit(Vector3 position, int numberOfHits, RaycastHit[] raycastHits, ref Vector3 collisionPosition, ref Vector3 collisionNormal, List<GameObject> includeList)
        {
            if (numberOfHits == 0)
                return false;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < numberOfHits; ++i)
            {
                RaycastHit hit = raycastHits[i];
                if (includeList != null && !includeList.Contains(hit.transform.gameObject))
                    continue;
                float distance = (position - hit.point).sqrMagnitude;
                if (distance > closestDistance)
                    continue;
                collisionPosition = hit.point;
                collisionNormal = hit.normal.ClampToZero(0.0001f);
                closestDistance = distance;
            }
            return closestDistance < float.MaxValue;
        }

        //06000260: test authored local size/center after inverse transformation.
        public static bool Contains(this BoxCollider boxCollider, Vector3 point)
        {
            Vector3 halfSize = boxCollider.size * 0.5f;
            Vector3 localPoint = boxCollider.transform.InverseTransformPoint(point);
            localPoint = (localPoint - boxCollider.center).Abs();
            return localPoint.x <= halfSize.x && localPoint.y <= halfSize.y && localPoint.z <= halfSize.z;
        }
    }
}
