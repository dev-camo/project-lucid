using System;
using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game02000ab7: all 36 owner declarations and two natural closure
    // declarations are represented. Genuine providers remain explicit dependencies.
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(HashedTrackGroup))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TrackManager : MonoBehaviour, ISystem
    {
        public Action OnColliderContactIgnored;
        public readonly Dictionary<int, Func<bool>> BodyCollisionCanModify = new Dictionary<int, Func<bool>>();
        public readonly Dictionary<int, Func<bool>> ColliderCollisionCanModify = new Dictionary<int, Func<bool>>();
        [SerializeField] private HashedTrackGroup m_trackData;
        [SerializeField] private ShaderPrewarmer m_shaderPrewarmer;
        private BoundsOctree<ISurface> m_railsOctree;
        private BoundsOctree<ISurface> m_transportersOctree;
        private BoundsOctree<ISurface> m_lightspeedDashOctree;
        private readonly List<ISurface> m_surfacesCollidingWithBounds = new List<ISurface>();
        private readonly List<HashedTrackGroup> m_trackDataList = new List<HashedTrackGroup>();
        private Tracker2D m_tracker;

        public Bounds Bounds { get; private set; }
        private const string DebugMenuPath = "Track Info";
        private const string DebugMenuButton = "Show / Hide Instance Pools";
        private const string DebugMenuContainerId = "uicontaineridentifier_track_debug_info";
        private const int DebugMenuPriority = 70;
        public ShaderPrewarmer ShaderPrewarmer => m_shaderPrewarmer;

        //06003da0 and owner callbacks 3dbf/3dc0: registration precedes physics
        //subscription and tetrahedralization. Shutdown removes the delegate first.
        public void SubscribeSystemActions()
        {
            this.SubscribeToAction(SystemAction.Initialise, _ =>
            {
                m_trackDataList.Add(m_trackData);
                EnableColliderModifiableContacts(true);
                LightProbes.Tetrahedralize();
            });
            this.SubscribeToAction(SystemAction.Shutdown, _ =>
            {
                EnableColliderModifiableContacts(false);
                m_trackDataList.Clear();
            });
        }

        public void Register(HashedTrackGroup trackData) { m_trackDataList.Add(trackData); }
        public void Unregister(HashedTrackGroup trackData) { m_trackDataList.Remove(trackData); }
        public void SetSurfaceTracker(Tracker2D tracker) { m_tracker = tracker; DeserializeData(); }

        //06003da4: the original does not expand Bounds using the registered groups.
        private void DeserializeData()
        {
            Bounds = new Bounds(transform.position, Vector3.one);
            m_railsOctree = new BoundsOctree<ISurface>(100f, Vector3.zero, 10f, 2f);
            m_transportersOctree = new BoundsOctree<ISurface>(10f, Vector3.zero, 10f, 2f);
            m_lightspeedDashOctree = new BoundsOctree<ISurface>(10f, Vector3.zero, 10f, 2f);
            var surfaces = new List<ISurface>();
            var neighbours = new Dictionary<ISurface, ISurface[]>();
            foreach (HashedTrackGroup trackData in m_trackDataList)
            {
                trackData.DeserializeData(m_railsOctree, m_transportersOctree, m_lightspeedDashOctree);
                trackData.AddTrackableSurfaces(surfaces, neighbours);
            }
            m_tracker.SetWorldInformation(surfaces, neighbours);
        }

        private void EnableColliderModifiableContacts(bool enable)
        {
            if (enable) Physics.ContactModifyEvent += OnColliderContactModify;
            else Physics.ContactModifyEvent -= OnColliderContactModify;
        }

        public void SetColliderModifiableContacts(bool modifiable)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList)
                trackData.SetColliderModifiableContacts(modifiable);
        }

        //06003da7 ARM6b99b0..6b9cd4: collider lookup owns the branch even when
        //its predicate is false. Body predicates run for each contact. Only the
        //body route emits OnColliderContactIgnored; negative separation skips it.
        private void OnColliderContactModify(PhysicsScene physicsScene, NativeArray<ModifiableContactPair> pairs)
        {
            foreach (ModifiableContactPair pairValue in pairs)
            {
                ModifiableContactPair pair = pairValue;
                for (int i = 0; i < pair.contactCount; ++i)
                {
                    if (ColliderCollisionCanModify.TryGetValue(pair.otherColliderInstanceID, out Func<bool> colliderCanModify))
                    {
                        if (colliderCanModify()) pair.IgnoreContact(i);
                    }
                    else if (BodyCollisionCanModify.TryGetValue(pair.bodyInstanceID, out Func<bool> bodyCanModify) && bodyCanModify())
                    {
                        if (pair.GetSeparation(i) < 0f) continue;
                        OnColliderContactIgnored?.Invoke();
                        pair.IgnoreContact(i);
                    }
                }
            }
        }

        public bool TrackableRailExists(ISurface surface)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList)
                if (trackData.TrackableRailExists(surface)) return true;
            return false;
        }

        public bool TrackableTransporterExists(ISurface surface)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList)
                if (trackData.TrackableTransporterExists(surface)) return true;
            return false;
        }

        //06003daa: the spatial query includes inactive surfaces, then validates
        //only the chosen nearest result. It does not search for a second result.
        public bool TryGetClosestLightspeedDashSpline(Vector3 position, float proximityDistance,
            ref SurfaceLocation closestLocation, bool includeInactive)
        {
            bool found = TryGetSurfaceHit(m_lightspeedDashOctree, position,
                new Bounds(position, Vector3.one * (proximityDistance + proximityDistance)),
                proximityDistance, ref closestLocation, true);
            if (!found || includeInactive) return found;
            MonoBehaviour surfaceBehaviour = closestLocation.m_surface as MonoBehaviour;
            if ((object)surfaceBehaviour != null)
            {
                CharacterAbilityEvent abilityEvent = surfaceBehaviour.transform.GetComponentInParent<CharacterAbilityEvent>();
                if (abilityEvent != null && abilityEvent.AbilityType == ActorAbilityType.Character_LightspeedDash &&
                    abilityEvent.isActiveAndEnabled) return true;
            }
            closestLocation.m_surface = null;
            return false;
        }

        private bool TryGetSurfaceHit(BoundsOctree<ISurface> surfacesOctree, Vector3 position,
            float proximityDistance, ref SurfaceLocation closestLocation, bool includeInactive)
        {
            return TryGetSurfaceHit(surfacesOctree, position,
                new Bounds(position, Vector3.one * (proximityDistance + proximityDistance)),
                proximityDistance, ref closestLocation, includeInactive);
        }

        //06003dac ARM6ba240..6ba70c and x866df6c0..6dfc60: strict greater-than
        //rejections preserve inclusive boundaries, later equal-distance choices,
        //and unordered floating-point behavior. Only m_surface is cleared first.
        private bool TryGetSurfaceHit(BoundsOctree<ISurface> surfacesOctree, Vector3 position,
            Bounds projectBounds, float proximityDistance, ref SurfaceLocation closestLocation, bool includeInactive)
        {
            closestLocation.m_surface = null;
            m_surfacesCollidingWithBounds.Clear();
            surfacesOctree.GetColliding(m_surfacesCollidingWithBounds, projectBounds);
            float proximityDistanceSquared = proximityDistance * proximityDistance;
            float closestDistanceSquared = float.MaxValue;
            foreach (ISurface surface in m_surfacesCollidingWithBounds)
            {
                if (!includeInactive && !surface.IsActive()) continue;
                SurfaceLocation location = surface.FindNearestSurfaceLocationFromWorld(position);
                Vector3 normal = location.m_worldRotation * Vector3.up;
                Vector3 displacement = location.m_worldPosition - position;
                float distanceAlongNormal = Vector3.Dot(displacement, normal);
                if (Mathf.Abs(distanceAlongNormal) > proximityDistance) continue;
                Vector3 projectedDisplacement = displacement - normal * distanceAlongNormal;
                if (projectedDisplacement.sqrMagnitude > proximityDistanceSquared) continue;
                float distanceSquared = displacement.sqrMagnitude;
                if (distanceSquared > closestDistanceSquared) continue;
                closestLocation = location;
                closestDistanceSquared = distanceSquared;
            }
            return closestLocation.m_surface != null;
        }

        public bool TryGetRailHit(Vector3 position, float proximityDistance, ref SurfaceLocation closestLocation)
        {
            return TryGetSurfaceHit(m_railsOctree, position, proximityDistance, ref closestLocation, false);
        }
        public bool TryGetRailHit(Vector3 position, Bounds bounds, float proximityDistance, ref SurfaceLocation closestLocation)
        {
            return TryGetSurfaceHit(m_railsOctree, position, bounds, proximityDistance, ref closestLocation, false);
        }
        public bool TryGetTransporterHit(Vector3 position, float proximityDistance, ref SurfaceLocation closestLocation)
        {
            return TryGetSurfaceHit(m_transportersOctree, position, proximityDistance, ref closestLocation, false);
        }

        public bool TryGetProjectedRailHit(ConeVolume.ConeDescription cone, Actor actor,
            ref SurfaceLocation closestLocation, List<ISurface> surfacesToIgnore = null, Func<ISurface, bool> isSurfaceValid = null)
        {
            return SurfaceTrackingUtilities.TryGetProjectedConeSurfaceHit(ForEachTrackableRail,
                cone, actor, ref closestLocation, surfacesToIgnore, isSurfaceValid);
        }
        public bool TryGetProjectedTransporterHit(ConeVolume.ConeDescription cone, Actor actor,
            ref SurfaceLocation closestLocation, List<ISurface> surfacesToIgnore = null, Func<ISurface, bool> isSurfaceValid = null)
        {
            return SurfaceTrackingUtilities.TryGetProjectedConeSurfaceHit(ForEachTrackableTransporter,
                cone, actor, ref closestLocation, surfacesToIgnore, isSurfaceValid);
        }

        //06003db2: a missing neighbour list returns false without clearing the
        //caller's list. An existing list clears it before filtering. Strict outside
        //comparisons preserve inclusive endpoints and unordered distance values.
        public bool TryGetRailNeighbours(ISurface surface, float distance, SurfaceSplineSection.Side side,
            ref List<SurfaceSplineSection> surfaceTargetSections)
        {
            List<SurfaceSplineSection> neighbours = null;
            foreach (HashedTrackGroup trackData in m_trackDataList)
                if (trackData.TryGetRailNeighbour(surface, out neighbours)) break;
            if (neighbours == null) return false;
            surfaceTargetSections.Clear();
            foreach (SurfaceSplineSection neighbour in neighbours)
            {
                if (!neighbour.TargetSpline.Value.IsActive()) continue;
                if (side != SurfaceSplineSection.Side.Any && neighbour.TargetSide != side) continue;
                if (neighbour.SourceDistanceStart > distance) continue;
                if (neighbour.SourceDistanceEnd < distance) continue;
                surfaceTargetSections.Add(neighbour);
            }
            return surfaceTargetSections.Count > 0;
        }

        public void ForEachTrackableRail(Action<ISurface> callback)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList) trackData.ForEachTrackableRail(callback);
        }
        public void ForEachRailNeighbour(Action<List<SurfaceSplineSection>> callback)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList) trackData.ForEachRailNeighbour(callback);
        }
        public void ForEachTrackableTransporter(Action<ISurface> callback)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList) trackData.ForEachTrackableTransporter(callback);
        }

        public bool TryGetCharacterCollisionData(Collider collider, out CharacterCollisionData collisionData)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList)
                if (trackData.TryGetColliderToCharacterCollisionData(collider, out collisionData)) return true;
            collisionData = null;
            return false;
        }
        public bool TryGetGravityTriggerForCollider(Collider collider, out GravityTrigger gravityTrigger)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList)
                if (trackData.TryGetGravityTriggerForCollider(collider, out gravityTrigger)) return true;
            gravityTrigger = null;
            return false;
        }
        public bool TryGetTrigger(Collider collider, out ITriggerCollider triggerCollider)
        {
            foreach (HashedTrackGroup trackData in m_trackDataList)
                if (trackData.TryGetTrigger(collider, out triggerCollider)) return true;
            triggerCollider = null;
            return false;
        }

        private void OnDestroy()
        {
            LevelManager levelManager = ProcessManager.GetSystemSafe<LevelManager>();
            if (levelManager != null) levelManager.ManagedSystems.RemoveManagedSystem(this);
        }

        private void DebugGetInstancePoolsMenuData(out UIManager uiManager, out UIContainerIdentifier containerId)
        {
            uiManager = ProcessManager.GetSystem<UIManager>();
            ProcessManager.GetSystem<DataManager>().Containers.TryGetValue(DebugMenuContainerId, out containerId);
        }
        public bool DebugInstancePoolsUIIsOpen()
        {
            DebugGetInstancePoolsMenuData(out UIManager uiManager, out UIContainerIdentifier containerId);
            return uiManager.IsOpen(containerId);
        }
        public void DebugToggleInstancePoolsInfo()
        {
            DebugGetInstancePoolsMenuData(out UIManager uiManager, out UIContainerIdentifier containerId);
            if (uiManager.IsOpen(containerId)) uiManager.Close(containerId);
            else uiManager.GetOrCreate(containerId);
        }
        public virtual void DebugGetInstancePrefabPoolsInfo(StringBuilder stringInfoBuilder)
        {
            m_trackDataList.ForEach(trackData => trackData.GetInstancePrefabPoolsInfo(stringInfoBuilder));
        }

        //06003dbe ARM6bbb0c..6bbbb8: field initializers construct the two
        //dictionaries and then the two lists before the MonoBehaviour base call.
        public TrackManager() { }
    }
}
