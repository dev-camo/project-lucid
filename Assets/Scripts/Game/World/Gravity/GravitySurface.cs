using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class GravitySurface : GravitySource
    {
        protected const float SurfaceTolerance = -0.25f;
        [SerializeField] private UnityEvent m_onEnterEvent;
        [SerializeField] private UnityEvent m_onStayEvent;
        [SerializeField] private UnityEvent m_onExitEvent;
        private GravityProvider m_gravityProvider;
        private Tracker2D m_tracker;
        private ISurface m_surface;
        private GravitySurfaceDefinition m_definition;
        protected SurfaceLocation Location { get; private set; }

        // Original 06003c2e: virtual cached-value calculation runs before allocating
        // the real description. Do not validate/publish a substitute first.
        protected override void Awake()
        {
            base.Awake();
            Description = new GravityDescription();
        }

        // 06003c2f: publish provider/tracker/definition, clear surface, register with
        // the provider, then clear Active even though Register sets it true.
        public void SetTracker(GravityProvider gravityProvider, Tracker2D tracker, GravitySurfaceDefinition definition)
        {
            m_gravityProvider = gravityProvider;
            m_tracker = tracker;
            m_definition = definition;
            m_surface = null;
            if (m_gravityProvider != null)
                Register(m_gravityProvider);
            Active = false;
        }

        public void ClearTracker()
        {
            if (m_tracker == null)
                return;
            Unregister(m_gravityProvider);
            SetTracker(null, null, null);
        }

        private void Update()
        {
            if (m_tracker == null)
                return;
            bool wasActive = Active;
            Active = ValidateTracker();
            if (Active)
            {
                if (wasActive)
                    OnGravityStay(m_gravityProvider);
                else
                    OnGravityEnter(m_gravityProvider);
            }
            else if (wasActive)
                OnGravityExit(m_gravityProvider);
        }

        protected override void OnGravityEnter(GravityProvider gravityProvider)
        {
            m_onEnterEvent.Invoke();
        }

        protected override void OnGravityStay(GravityProvider gravityProvider)
        {
            m_onStayEvent.Invoke();
        }

        protected override void OnGravityExit(GravityProvider gravityProvider)
        {
            base.OnGravityExit(gravityProvider);
            m_onExitEvent.Invoke();
        }

        public override void ManualTriggerExit(GravityProvider gravityProvider)
        {
            OnGravityExit(gravityProvider);
        }

        // 06003c36: Location publishes before validation. Missing required gravity
        // leaves the prior surface/description; distance parameters refresh only when
        // the actual surface reference changes, before derived location validation.
        private bool ValidateTracker()
        {
            Location = m_tracker.Location;
            if (m_definition == null)
                return false;
            if (!TryGetMetadataValue(Location, m_definition.m_metadataGravityKey,
                    out float gravity, GravityDescription.GravityDefault))
                return false;
            if (m_surface != Location.m_surface)
            {
                m_surface = Location.m_surface;
                TryGetMetadataValue(Location, m_definition.m_metadataInnerFalloffDistanceKey,
                    out float innerFalloff, GravityDescription.InnerFalloffDistanceDefault);
                TryGetMetadataValue(Location, m_definition.m_metadataInnerDistanceKey,
                    out float inner, GravityDescription.InnerDistanceDefault);
                TryGetMetadataValue(Location, m_definition.m_metadataOuterDistanceKey,
                    out float outer, GravityDescription.OuterDistanceDefault);
                TryGetMetadataValue(Location, m_definition.m_metadataOuterFalloffDistanceKey,
                    out float outerFalloff, GravityDescription.OuterFalloffDistanceDefault);
                Description.Set(gravity, innerFalloff, inner, outer, outerFalloff);
            }
            return ValidateTrackerLocation();
        }

        // 06003c37: look up the group before checking key nullness. Publish the out
        // scalar only after metadata conversion; an earlier fault leaves it untouched.
        private bool TryGetMetadataValue(SurfaceLocation location, MetadataKeyType key,
            out float value, float valueDefault)
        {
            MetadataGroups groups = location.m_metadata.Surface;
            MetadataGroup group = groups != null ? groups.GetGroup(m_definition.MetadataGroupKey) : null;
            if (key == null || group == null)
            {
                value = valueDefault;
                return false;
            }
            if (!group.TryGetMetadata(key, out Metadata metadata))
            {
                value = valueDefault;
                return false;
            }
            value = metadata.AsFloat();
            return true;
        }

        protected abstract bool ValidateTrackerLocation();
    }
}
