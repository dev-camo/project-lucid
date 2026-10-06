using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000079: all nine fields and nine own MethodDefs.
    public class CollisionResolver2D : ICollisionResolver2D
    {
        private const float MaxStepSize = 0.2f;
        private const float CurrentSurfaceDistanceThreshold = 0.001f;
        private readonly List<SurfaceLocation> m_closestPoints = new List<SurfaceLocation>(1);
        // Original06000321 initializes the six real markers in this order. Release bodies
        // contain no Begin/End calls. Field initializers retain original BeforeFieldInit.
        private static ProfilerMarker m_profilerMarkerGetNearestLocationFromWorld = new ProfilerMarker("GetNearestLocationFromWorld");
        private static ProfilerMarker m_profilerMarkerGetNearestLocationHitFromWorld = new ProfilerMarker("GetNearestLocationHitFromWorld");
        private static ProfilerMarker m_profilerMarkerMoveTowardsWorldLocation = new ProfilerMarker("MoveTowardsWorldLocation");
        private static ProfilerMarker m_profilerMarkerInternalMoveTowardsWorldLocation = new ProfilerMarker("InternalMoveTowardsWorldLocation");
        private static ProfilerMarker m_profilerMarkerMoveTowardsLocalLocation = new ProfilerMarker("MoveTowardsLocalLocation");
        private static ProfilerMarker m_profilerMarkerFindClosestPointOnNeighbours = new ProfilerMarker("FindClosestPointOnNeighbours");

        //06000319. Empty worlds retain the full incoming location; ties/NaN do not replace it.
        public virtual Collision2DOutput GetNearestLocationFromWorld(Collision2DInput input, Vector3 worldPosition)
        {
            SurfaceLocation nearest = input.m_currentLocation;
            float closestDistance = float.MaxValue;
            foreach (ISurface surface in input.m_world.Keys)
            {
                SurfaceLocation location = surface.FindNearestSurfaceLocationFromWorld(worldPosition);
                float distance = (worldPosition - location.m_worldPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    nearest = location;
                    closestDistance = distance;
                }
            }
            return new Collision2DOutput { m_newLocation = nearest };
        }

        //0600031a. Raycasts progressively tighten the hit distance, while the selected
        // surface location is the closest projection to the ray's requested endpoint.
        public virtual RayHit2DOutput GetNearestLocationHitFromWorld(RayHit2DInput input)
        {
            List<ISurface> surfaces = input.m_surfaces;
            Vector3 target = input.m_ray.GetPoint(input.m_distance);
            RaycastHit raycastHit = new RaycastHit { distance = input.m_distance + 1f };
            SurfaceLocation nearest = default;
            float closestDistance = float.MaxValue;
            bool hit = false;
            foreach (ISurface surface in surfaces)
            {
                SurfaceLocation rayLocation;
                if (!SurfacePhysics.Raycast(input.m_ray, input.m_distance, surface,
                    ref raycastHit, out rayLocation, true))
                    continue;
                SurfaceLocation location = surface.FindNearestSurfaceLocationFromWorld(target);
                float distance = (target - location.m_worldPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    nearest = location;
                    closestDistance = distance;
                    hit = true;
                }
            }
            return new RayHit2DOutput { m_hit = hit, m_location = nearest };
        }

        //0600031b. Every iteration subtracts the fixed step, including the final short step.
        public virtual Collision2DOutput MoveTowardsWorldLocation(Collision2DInput input, Vector3 worldOffset)
        {
            ISurface currentSurface = input.m_currentLocation.m_surface;
            SurfaceLocation currentLocation = input.m_currentLocation;
            if (currentSurface == null)
                return GetNearestLocationFromWorld(input, input.m_currentLocation.m_worldPosition + worldOffset);
            float remaining = worldOffset.magnitude;
            Vector3 direction = worldOffset.normalized;
            Collision2DOutput output = new Collision2DOutput { m_newLocation = input.m_currentLocation };
            while (remaining > 0f)
            {
                float step = remaining > MaxStepSize ? MaxStepSize : remaining;
                output = InternalMoveTowardsWorldLocation(input.m_world, currentSurface,
                    currentLocation, direction * step);
                currentSurface = output.m_newLocation.m_surface;
                currentLocation = output.m_newLocation;
                remaining -= MaxStepSize;
            }
            return output;
        }

        //0600031c. The original current projection establishes a fixed baseline; an equal
        // neighbour may replace it, and the nearest qualifying neighbour wins strictly.
        protected Collision2DOutput InternalMoveTowardsWorldLocation(Dictionary<ISurface, ISurface[]> world,
            ISurface currentSurface, SurfaceLocation currentLocation, Vector3 worldOffset)
        {
            Vector3 target = currentLocation.m_worldPosition + worldOffset;
            SurfaceLocation nearest = currentSurface.FindNearestSurfaceLocationFromWorld(target);
            ISurface[] neighbours = world[currentSurface];
            if (neighbours.Length != 0)
            {
                float currentDistance = (target - nearest.m_worldPosition).sqrMagnitude;
                float closestNeighbourDistance = float.MaxValue;
                for (int i = 0; i < neighbours.Length; ++i)
                {
                    SurfaceLocation location = neighbours[i].FindNearestSurfaceLocationFromWorld(target);
                    float distance = (target - location.m_worldPosition).sqrMagnitude;
                    if (distance <= currentDistance && distance < closestNeighbourDistance)
                    {
                        nearest = location;
                        closestNeighbourDistance = distance;
                    }
                }
            }
            return new Collision2DOutput { m_newLocation = nearest };
        }

        //0600031d. Y movement is discarded, local/world offsets are intentionally used
        // in their original spaces, and unordered direction scores replace the best row.
        public virtual Collision2DOutput MoveTowardsLocalLocation(Collision2DInput input, Vector3 localOffset)
        {
            ISurface currentSurface = input.m_currentLocation.m_surface;
            if (currentSurface == null)
                return GetNearestLocationFromWorld(input, input.m_currentLocation.m_worldPosition);
            Vector3 currentLocalPosition = input.m_currentLocation.m_localPosition;
            Quaternion currentRotation = input.m_currentLocation.m_worldRotation;
            Vector3 offset = new Vector3(localOffset.x, 0f, localOffset.z);
            Vector3 target = currentLocalPosition + offset;
            SurfaceLocation nearest = currentSurface.FindNearestSurfaceLocationFromLocal(target);
            float travelled = (currentLocalPosition - nearest.m_localPosition).sqrMagnitude;
            if (!(travelled < offset.sqrMagnitude - CurrentSurfaceDistanceThreshold))
                return CreateCollision(nearest, false);
            ISurface[] neighbours = input.m_world[currentSurface];
            Vector3 intendedDirection = (currentRotation * offset).normalized;
            m_closestPoints.Clear();
            FindClosestPointOnNeighbours(neighbours, m_closestPoints, nearest.m_worldPosition,
                target - nearest.m_localPosition, intendedDirection);
            Vector3 localForward = localOffset.z >= 0f ? Vector3.forward : Vector3.back;
            float bestScore = float.MinValue;
            bool atEndEdge = true;
            foreach (SurfaceLocation location in m_closestPoints)
            {
                Vector3 direction = (location.m_worldRotation * localForward).normalized;
                float score = Vector3.Dot(intendedDirection, direction);
                if (score <= bestScore)
                    continue;
                nearest = location;
                bestScore = score;
                atEndEdge = false;
            }
            return CreateCollision(nearest, atEndEdge);
        }

        //0600031e. All bound distances/positions survive changing only AtEndEdge.
        private Collision2DOutput CreateCollision(SurfaceLocation location, bool atEndEdge)
        {
            PositionBoundsInfo bounds = location.m_positionBoundsInfo;
            if (bounds.AtEndEdge != atEndEdge)
                location.m_positionBoundsInfo = new PositionBoundsInfo(bounds.LeftBoundSqrDistance,
                    bounds.RightBoundSqrDistance, atEndEdge, bounds.LeftPosition, bounds.RightPosition);
            return new Collision2DOutput { m_newLocation = location };
        }

        //0600031f. This appends without clearing; NaN bound distances pass the >= gate.
        private void FindClosestPointOnNeighbours(ISurface[] neighbourSurfaces, List<SurfaceLocation> closestPoints,
            Vector3 targetLocation, Vector3 remainingOffset, Vector3 intendedDirection)
        {
            if (neighbourSurfaces.Length == 0)
                return;
            float maxDistance = remainingOffset.sqrMagnitude + CurrentSurfaceDistanceThreshold;
            for (int i = 0; i < neighbourSurfaces.Length; ++i)
            {
                ISurface surface = neighbourSurfaces[i];
                Bounds bounds = surface.GetBoundingBox(true);
                if (bounds.SqrDistance(targetLocation) >= maxDistance)
                    continue;
                SurfaceLocation nearest = surface.FindNearestSurfaceLocationFromWorld(targetLocation);
                if ((nearest.m_worldPosition - targetLocation).sqrMagnitude >= maxDistance)
                    continue;
                SurfaceLocation moved = surface.FindNearestSurfaceLocationFromLocal(nearest.m_localPosition + remainingOffset);
                Vector3 direction = (moved.m_worldPosition - targetLocation).normalized;
                if (Vector3.Dot(intendedDirection, direction) < 0f)
                    continue;
                closestPoints.Add(moved);
            }
        }

        //06000320: the capacity-one list initializer executes before System.Object's ctor.
        public CollisionResolver2D() { }
    }
}
