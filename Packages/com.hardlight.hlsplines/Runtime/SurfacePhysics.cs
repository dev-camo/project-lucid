using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000090; six genuine MethodDefs, no fields.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class SurfacePhysics
    {
        // 0x06000365. Count is captured once and every nonignored entry is visited.
        public static bool Raycast(Ray ray, float distance, IReadOnlyList<ISurface> surfacesList,
            out RaycastHit raycastHit, out ISurface hitTrackableSurface,
            ISurface surfaceToIgnore = null, bool ignoreInactive = true)
        {
            hitTrackableSurface = null;
            raycastHit = new RaycastHit { distance = distance + 1f };
            int count = surfacesList.Count;
            bool hit = false;
            for (int i = 0; i < count; ++i)
            {
                ISurface surface = surfacesList[i];
                if (surface == surfaceToIgnore)
                    continue;
                hit |= Raycast(ray, distance, surface, ref raycastHit,
                    ref hitTrackableSurface, ignoreInactive);
            }
            return hit;
        }

        // 0x06000366. The original route accepts IRibbon only; MeshSurface fails the cast.
        public static bool Raycast(Ray ray, float distance, ISurface surface,
            ref RaycastHit raycastHit, ref ISurface hitTrackableSurface, bool ignoreInactive = true)
        {
            RaycastHit candidate = default;
            IRibbon ribbon = surface as IRibbon;
            if (ribbon == null)
                return false;
            if (ignoreInactive && !ribbon.IsActive())
                return false;
            Bounds bounds = ribbon.GetBoundingBox(true);
            if (!bounds.IntersectRay(ray))
                return false;
            if (!ribbon.TryGetRaycastHit(ray, distance, out candidate))
                return false;
            if (!(candidate.distance < raycastHit.distance))
                return false;
            raycastHit = candidate;
            hitTrackableSurface = surface;
            return true;
        }

        // 0x06000367. The output is cleared before enumeration and nearest-world callbacks.
        public static bool Raycast(Ray ray, float distance, IReadOnlyList<ISurface> surfacesList,
            out SurfaceLocation surfaceLocation, bool ignoreInactive = true)
        {
            RaycastHit raycastHit = default;
            surfaceLocation = default;
            ISurface hitSurface;
            bool hit = Raycast(ray, distance, surfacesList, out raycastHit,
                out hitSurface, null, ignoreInactive);
            if (hit)
            {
                ISurface capturedSurface = hitSurface;
                Vector3 point = raycastHit.point;
                surfaceLocation = capturedSurface.FindNearestSurfaceLocationFromWorld(point);
            }
            return hit;
        }

        // 0x06000368. Unlike the list wrapper, failed calls clear output after the call.
        public static bool Raycast(Ray ray, float distance, ISurface surface,
            ref RaycastHit raycastHit, out SurfaceLocation surfaceLocation, bool ignoreInactive = true)
        {
            ISurface hitSurface = null;
            bool hit = Raycast(ray, distance, surface, ref raycastHit,
                ref hitSurface, ignoreInactive);
            if (hit)
            {
                ISurface capturedSurface = hitSurface;
                Vector3 point = raycastHit.point;
                surfaceLocation = capturedSurface.FindNearestSurfaceLocationFromWorld(point);
            }
            else
                surfaceLocation = default;
            return hit;
        }

        // 0x06000369. Both limits are strict ordered comparisons; a tie is untouched.
        public static bool TryGetCloserHitFromTriangle(Ray ray, Vector3 firstPoint,
            Vector3 secondPoint, Vector3 thirdPoint, bool bidirectional,
            float rayDistance, ref RaycastHit raycastHit)
        {
            RaycastHit candidate;
            if (!IntersectRayTriangle(ray, firstPoint, secondPoint, thirdPoint,
                bidirectional, out candidate))
                return false;
            float candidateDistance = candidate.distance;
            if (!(candidateDistance < rayDistance))
                return false;
            if (!(candidateDistance < raycastHit.distance))
                return false;
            raycastHit = candidate;
            return true;
        }

        // 0x0600036a. Bidirectional permits a negative ray parameter, but keeps winding.
        public static bool IntersectRayTriangle(Ray ray, Vector3 v0, Vector3 v1,
            Vector3 v2, bool bidirectional, out RaycastHit raycastHit)
        {
            raycastHit = default;
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 normal = Vector3.Cross(edge1, edge2);
            Vector3 direction = ray.direction;
            float determinant = -direction.y * normal.y - normal.x * direction.x
                - normal.z * direction.z;
            if (determinant <= 0f)
                return false;
            Vector3 origin = ray.origin;
            Vector3 fromVertex = origin - v0;
            float parameter = Vector3.Dot(normal, fromVertex);
            if (parameter < 0f && !bidirectional)
                return false;
            Vector3 cross = Vector3.Cross(fromVertex, direction);
            float secondWeight = Vector3.Dot(edge2, cross);
            if (secondWeight < 0f || secondWeight > determinant)
                return false;
            float negativeThirdWeight = Vector3.Dot(edge1, cross);
            if (negativeThirdWeight > 0f || secondWeight - negativeThirdWeight > determinant)
                return false;
            float inverseDeterminant = 1f / determinant;
            float distance = inverseDeterminant * parameter;
            float second = inverseDeterminant * secondWeight;
            float third = -(negativeThirdWeight * inverseDeterminant);
            float first = 1f - second - third;
            RaycastHit candidate = default;
            candidate.point = origin + direction * distance;
            candidate.distance = distance;
            candidate.barycentricCoordinate = new Vector3(first, second, third);
            candidate.normal = Vector3.Normalize(normal);
            raycastHit = candidate;
            return true;
        }
    }
}
