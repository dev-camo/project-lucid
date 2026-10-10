using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class SurfaceTrackingUtilities
    {
        private static GameObject s_debugLocationParent;

        public delegate void ForEachSurface(Action<ISurface> callback);

        // Original06003a60: local origin; the interface implementation supplies
        // the returned surface identity and world-space values.
        public static SurfaceLocation StartLocation(this ISurface surface) =>
            surface.FindNearestSurfaceLocationFromLocal(Vector3.zero);

        // Original06003a61: read Length before the local forward vector.
        public static SurfaceLocation EndLocation(this ISurface surface) =>
            surface.FindNearestSurfaceLocationFromLocal(surface.Length * Vector3.forward);

        // Original06003a62 and its actual captured callback06003a6b.
        // A nonpositive step leaves the caller's ref value untouched. Each surface
        // keeps searching until the first hit; after that, its first rejected
        // projection ends that surface's search. Equal distances replace a hit.
        public static bool TryGetProjectedConeSurfaceHit(ForEachSurface forEachSurface,
            ConeVolume.ConeDescription cone, Actor actor, ref SurfaceLocation closestLocation,
            List<ISurface> surfacesToIgnore = null, Func<ISurface, bool> isSurfaceValid = null)
        {
            if (cone.Step <= 0f) return false;
            Vector3 planeForward = cone.Rotation * Vector3.forward;
            // The shipped method still evaluates this unused rotation.
            _ = cone.Rotation * Vector3.up;
            Quaternion worldToLocal = Quaternion.Inverse(cone.Rotation);
            Bounds projectedBounds = ConeVolume.CalculateConeBounds(cone);
            float closestDistanceToRailSqr = float.MaxValue;
            SurfaceLocation closestProjectedLocation = default;
            bool hasSurfacesToIgnore = surfacesToIgnore != null;
            bool hasSurfaceValidCallback = isSurfaceValid != null;
            forEachSurface(surface =>
            {
                if (!surface.IsActive()) return;
                if (hasSurfaceValidCallback && !isSurfaceValid(surface)) return;
                if (hasSurfacesToIgnore && surfacesToIgnore.Contains(surface)) return;
                Bounds surfaceBounds = surface.GetBoundingBox(true);
                if (!surfaceBounds.Intersects(projectedBounds)) return;
                Vector3 position = cone.Origin;
                float distance = 0f;
                bool hasHit = false;
                while (distance < cone.Distance)
                {
                    StepProjection(cone.Step, ref distance, ref position, planeForward, cone.Distance);
                    SurfaceLocation projectedLocation = surface.FindNearestSurfaceLocationFromWorld(position);
                    Vector3 worldPosition = projectedLocation.m_worldPosition;
                    Vector3 pointOnLine = MathUtilities.GetClosestPointOnLine(cone.Origin, planeForward, worldPosition);
                    float coneDistance = Vector3.Dot(pointOnLine - cone.Origin, planeForward);
                    if (coneDistance < 0f || coneDistance > cone.Distance)
                    {
                        if (hasHit) break;
                        continue;
                    }
                    float distanceToRailSqr = (worldPosition - pointOnLine).sqrMagnitude;
                    if (distanceToRailSqr > closestDistanceToRailSqr)
                    {
                        if (hasHit) break;
                        continue;
                    }
                    if (!ConeVolume.IsPositionInsideCone(worldPosition, cone.Origin, cone.Tangents, coneDistance, worldToLocal))
                    {
                        if (hasHit) break;
                        continue;
                    }
                    closestDistanceToRailSqr = distanceToRailSqr;
                    closestProjectedLocation = projectedLocation;
                    hasHit = true;
                }
            });
            closestLocation = closestProjectedLocation;
            return closestLocation.m_surface != null;
        }

        // Original06003a63: shorten only an overshooting step. Position changes
        // before distance is re-read, preserving their possible ref aliasing.
        private static void StepProjection(float step, ref float distance,
            ref Vector3 position, Vector3 forward, float distanceTotal)
        {
            float nextDistance = distance + step;
            float overflow = nextDistance > distanceTotal ? nextDistance - distanceTotal : 0f;
            step -= overflow;
            position += forward * step;
            distance += step;
        }

        // Original06003a64: retained development-only boundary; the shipping
        // projection routine contains no call to this conditional method.
        [Conditional("BUILD_DEVELOPMENT")]
        private static void BeginDebugLocation(Actor actor)
        {
            if (actor == null) return;
            s_debugLocationParent = actor.DebugGetLocationParent();
        }

        // Original06003a65: shipping code evaluates Unity equality only; it
        // does not clear the static parent or invent absent drawing behavior.
        [Conditional("BUILD_DEVELOPMENT")]
        private static void EndDebugLocation(Actor actor)
        {
            _ = actor == null;
        }
    }
}
