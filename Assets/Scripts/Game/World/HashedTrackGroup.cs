using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HashedTrackGroup : HashedGroup
    {
        [SerializeField] private SerializableDictionaryKvpList<ISurface, Object, ISurface, Object> m_trackableSurfaces =
            new SerializableDictionaryKvpList<ISurface, Object, ISurface, Object>();
        [SerializeField] private SerializableDictionary<ISurface, Object, List<SurfaceSplineSection>> m_railNeighbours =
            new SerializableDictionary<ISurface, Object, List<SurfaceSplineSection>>();
        [SerializeField] private List<ObjectSerializable<ISurface>> m_trackableSurfacesListSerialized = new List<ObjectSerializable<ISurface>>();
        [SerializeField] private List<ObjectSerializable<ISurface>> m_trackableRailsList = new List<ObjectSerializable<ISurface>>();
        [SerializeField] private List<ObjectSerializable<ISurface>> m_trackableTransportersList = new List<ObjectSerializable<ISurface>>();
        [SerializeField] private List<ObjectSerializable<ISurface>> m_trackableLightspeedDashSplinesList = new List<ObjectSerializable<ISurface>>();
        [SerializeField] private List<Collider> m_trackableCollidersList = new List<Collider>();
        [SerializeField] private SerializableDictionary<Collider, CharacterCollisionData> m_colliderToCharacterCollisionData =
            new SerializableDictionary<Collider, CharacterCollisionData>();
        [SerializeField] private SerializableDictionary<Collider, GravityTrigger> m_colliderToGravityTrigger =
            new SerializableDictionary<Collider, GravityTrigger>();
        [SerializeField] private SerializableDictionary<Collider, Collider, ITriggerCollider, Object> m_colliderToTrigger =
            new SerializableDictionary<Collider, Collider, ITriggerCollider, Object>();
        [SerializeField] private int m_surfacesHashCode;
        [SerializeField] private int m_collidersHashCode;
        [SerializeField] private Bounds m_bounds;

        public Bounds Bounds => m_bounds;

        public void DeserializeData(BoundsOctree<ISurface> m_railsOctree, BoundsOctree<ISurface> m_transportersOctree,
            BoundsOctree<ISurface> m_lightspeedDashOctree)
        {
            foreach (ObjectSerializable<ISurface> rail in m_trackableRailsList)
            {
                Bounds bounds = rail.Value.GetBoundingBox(true);
                m_railsOctree.Add(rail.Value, bounds);
            }
            foreach (ObjectSerializable<ISurface> transporter in m_trackableTransportersList)
            {
                Bounds bounds = transporter.Value.GetBoundingBox(true);
                m_transportersOctree.Add(transporter.Value, bounds);
            }
            foreach (ObjectSerializable<ISurface> lightspeedDashSpline in m_trackableLightspeedDashSplinesList)
            {
                Bounds bounds = lightspeedDashSpline.Value.GetBoundingBox(true);
                m_lightspeedDashOctree.Add(lightspeedDashSpline.Value, bounds);
            }
        }

        public void SetColliderModifiableContacts(bool modifiable)
        {
            foreach (Collider collider in m_trackableCollidersList)
            {
                collider.hasModifiableContacts = modifiable;
                if (collider.TryGetComponent(out CharacterCollisionData collisionData))
                    collisionData.HasModifiableContacts = modifiable;
            }
        }

        public void AddTrackableSurfaces(List<ISurface> trackableSurfacesList,
            Dictionary<ISurface, ISurface[]> trackableSurfacesWorld)
        {
            foreach (ObjectSerializable<ISurface> surface in m_trackableSurfacesListSerialized)
                trackableSurfacesList.Add(surface.Value);
            foreach (var pair in m_trackableSurfaces)
                trackableSurfacesWorld.Add(pair.Key, pair.Value.ToArray());
        }

        public bool TryGetColliderToCharacterCollisionData(Collider collider, out CharacterCollisionData collisionData)
        {
            return m_colliderToCharacterCollisionData.TryGetValue(collider, out collisionData);
        }

        public bool TryGetGravityTriggerForCollider(Collider collider, out GravityTrigger gravityTrigger)
        {
            return m_colliderToGravityTrigger.TryGetValue(collider, out gravityTrigger);
        }

        public bool TryGetTrigger(Collider collider, out ITriggerCollider triggerCollider)
        {
            return m_colliderToTrigger.TryGetValue(collider, out triggerCollider);
        }

        public bool TrackableRailExists(ISurface surface)
        {
            // Original natural callback dereferences the wrapper before reference comparison.
            return m_trackableRailsList.Exists(rail => rail.Value == surface);
        }

        public bool TrackableTransporterExists(ISurface surface)
        {
            return m_trackableTransportersList.Exists(transporter => transporter.Value == surface);
        }

        public bool TryGetRailNeighbour(ISurface surface, out List<SurfaceSplineSection> surfaceSections)
        {
            return m_railNeighbours.TryGetValue(surface, out surfaceSections);
        }

        public void ForEachTrackableRail(Action<ISurface> callback)
        {
            foreach (ObjectSerializable<ISurface> rail in m_trackableRailsList)
                callback(rail.Value);
        }

        public void ForEachRailNeighbour(Action<List<SurfaceSplineSection>> callback)
        {
            foreach (var pair in m_railNeighbours)
                callback(pair.Value);
        }

        public void ForEachTrackableTransporter(Action<ISurface> callback)
        {
            foreach (ObjectSerializable<ISurface> transporter in m_trackableTransportersList)
                callback(transporter.Value);
        }

        public void GetInstancePrefabPoolsInfo(StringBuilder stringInfoBuilder)
        {
            InstancePrefabGroup instancePrefabGroup = GetComponentInChildren<InstancePrefabGroup>();
            if (instancePrefabGroup == null)
                return;
            instancePrefabGroup.GetInstancePrefabPoolsInfo(stringInfoBuilder);
        }

        public HashedTrackGroup() { }
    }
}
