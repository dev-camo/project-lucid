using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Tracker2D
    {
        private SurfaceLocation m_location;
        private ICollisionResolver2D m_collisionResolver;
        private Dictionary<ISurface, ISurface[]> m_world;
        private List<ISurface> m_surfaces;
        private Dictionary<ISurface, ISurface[]> m_temporaryAllocationWorld =
            new Dictionary<ISurface, ISurface[]>(1);
        public Action<SurfaceLocation, SurfaceLocation> OnTrackableSurfaceChange;

        // HLSplines.Runtime 06000326/327: direct original field getters.
        public SurfaceLocation Location => m_location;
        public IReadOnlyList<ISurface> Surfaces => m_surfaces;

        // 06000328: temporary dictionary allocation/publication precedes Object's ctor.
        public Tracker2D(ICollisionResolver2D collisionResolver)
        {
            m_collisionResolver = collisionResolver;
        }

        // 06000329: capture the resolver before constructing the by-value input.
        public void Teleport(Vector3 worldPosition)
        {
            Collision2DOutput output = m_collisionResolver.GetNearestLocationFromWorld(
                new Collision2DInput { m_world = m_world, m_currentLocation = m_location },
                worldPosition);
            SetSurfaceLocation(output.m_newLocation);
        }

        // 0600032a: retain the native metadata filter and ordered-greater rejection.
        public void Teleport(Vector3 worldPosition, float boundsSearchRadius, MetadataGroupKey key = null)
        {
            m_temporaryAllocationWorld.Clear();
            float sqrRadius = boundsSearchRadius * boundsSearchRadius;
            foreach (KeyValuePair<ISurface, ISurface[]> surfaceAndNeighbours in m_world)
            {
                if (key != null)
                {
                    MetadataGroups metadata = surfaceAndNeighbours.Key.GetSurfaceMetadata();
                    if (!metadata.HasData || metadata.GetGroup(key) == null)
                        continue;
                }

                Bounds bounds = surfaceAndNeighbours.Key.GetBoundingBox(true);
                if (bounds.SqrDistance(worldPosition) > sqrRadius)
                    continue;
                m_temporaryAllocationWorld.Add(surfaceAndNeighbours.Key, surfaceAndNeighbours.Value);
            }

            Collision2DOutput output = m_collisionResolver.GetNearestLocationFromWorld(
                new Collision2DInput
                {
                    m_world = m_temporaryAllocationWorld,
                    m_currentLocation = m_location
                }, worldPosition);
            SetSurfaceLocation(output.m_newLocation);
        }

        // 0600032b: m_currentLocation remains the default struct in the ray input.
        public bool TeleportIfHit(Ray ray, float distance)
        {
            RayHit2DOutput output = m_collisionResolver.GetNearestLocationHitFromWorld(
                new RayHit2DInput { m_surfaces = m_surfaces, m_ray = ray, m_distance = distance });
            if (output.m_hit)
                SetSurfaceLocation(output.m_location);
            return output.m_hit;
        }

        // 0600032c/32d: resolver result passes through the same pre-publication callback.
        public void MoveWorld(Vector3 worldOffset)
        {
            Collision2DOutput output = m_collisionResolver.MoveTowardsWorldLocation(
                new Collision2DInput { m_world = m_world, m_currentLocation = m_location },
                worldOffset);
            SetSurfaceLocation(output.m_newLocation);
        }

        public void MoveLocal(Vector3 localOffset)
        {
            Collision2DOutput output = m_collisionResolver.MoveTowardsLocalLocation(
                new Collision2DInput { m_world = m_world, m_currentLocation = m_location },
                localOffset);
            SetSurfaceLocation(output.m_newLocation);
        }

        // 0600032e: list is stored before the dictionary; neither is copied/validated.
        public void SetWorldInformation(List<ISurface> surfaces, Dictionary<ISurface, ISurface[]> world)
        {
            m_surfaces = surfaces;
            m_world = world;
        }

        // 0600032f: null surface returns false before touching m_world.
        public bool CheckIfPointIsOnNeighbourBounds(Vector3 point, Vector3 expandBounds)
        {
            if (m_location.m_surface == null)
                return false;
            ISurface[] neighbours = m_world[m_location.m_surface];
            for (int i = 0; i < neighbours.Length; i++)
            {
                Bounds bounds = neighbours[i].GetBoundingBox(true);
                bounds.extents += expandBounds * 0.5f;
                if (bounds.Contains(point))
                    return true;
            }
            return false;
        }

        // 06000330: reference change only. Throwing callback preserves the old location;
        // reentrant updates are overwritten by this call's incoming by-value location.
        private void SetSurfaceLocation(SurfaceLocation surfaceLocation)
        {
            if (m_location.m_surface != surfaceLocation.m_surface)
                OnTrackableSurfaceChange?.Invoke(m_location, surfaceLocation);
            m_location = surfaceLocation;
        }
    }
}
