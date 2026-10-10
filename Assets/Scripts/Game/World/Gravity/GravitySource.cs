using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class GravitySource : MonoBehaviour
    {
        [Tooltip("Only registered gravity sources of the highest priority will be evaluated.")]
        [SerializeField] private int m_priority;
        private readonly List<GravityProvider> m_registeredGravities = new List<GravityProvider>();
        private StackableDataHandle m_cameraOverrideHandle;
        protected GravityDescription Description;

        public int Priority => m_priority;
        public float Distance
        {
            get
            {
                GravityDescription description = Description;
                return description != null ? description.Distance : 0f;
            }
        }
        public float MaxDistance
        {
            get
            {
                GravityDescription description = Description;
                return description != null ? description.MaxDistance : 0f;
            }
        }
        public bool Active { get; protected set; }

        public abstract Vector3 GetGravity(Vector3 position);
        public abstract void ManualTriggerExit(GravityProvider gravityProvider);

        // Original 06003c18: do not replace the virtual gravity query.
        public virtual Vector3 GetGravityPrimary(Vector3 position)
        {
            return GetGravity(position);
        }

        protected virtual void Awake()
        {
            CalculateCachedValues();
        }

        // 06003c1a: callbacks run against the live list, before the second unregister
        // pass. A callback that mutates the list can invalidate its enumerator.
        private void OnDisable()
        {
            foreach (GravityProvider gravityProvider in m_registeredGravities)
                ManualTriggerExit(gravityProvider);
            UnregisterAllGravities();
        }

        // 06003c1b includes the genuine discarded GameObject property access.
        private void OnValidate()
        {
            _ = gameObject;
            CalculateCachedValues();
        }

        protected abstract void CalculateCachedValues();

        // 06003c1d: publish the description before validating its original limits.
        protected void SetDescription(GravityDescription description)
        {
            Description = description;
            if (Description != null)
                Description.Validate();
        }

        // 06003c1e: Active becomes true before the provider callback. List publication
        // follows that callback; a failure retains the original partial state.
        protected void Register(GravityProvider gravityProvider)
        {
            if (m_registeredGravities.Contains(gravityProvider))
                return;
            Active = true;
            gravityProvider.Register(this);
            m_registeredGravities.Add(gravityProvider);
        }

        // 06003c1f: provider removal runs before local removal/count publication.
        protected void Unregister(GravityProvider gravityProvider)
        {
            if (!m_registeredGravities.Contains(gravityProvider))
                return;
            gravityProvider.Unregister(this);
            m_registeredGravities.Remove(gravityProvider);
            Active = m_registeredGravities.Count > 0;
        }

        private void UnregisterAllGravities()
        {
            foreach (GravityProvider gravityProvider in m_registeredGravities)
                gravityProvider.Unregister(this);
            m_registeredGravities.Clear();
            Active = false;
        }

        // 06003c21/22 are genuine empty virtual callbacks in the supplied player.
        protected virtual void OnGravityEnter(GravityProvider gravityProvider) { }
        protected virtual void OnGravityStay(GravityProvider gravityProvider) { }

        // 06003c23 depends on the real Character and camera managers. No substitute
        // provider is permitted while their full source graph remains open.
        protected virtual void OnGravityExit(GravityProvider gravityProvider)
        {
            if (m_cameraOverrideHandle == null)
                return;
            CharacterManager characterManager = ProcessManager.GetSystem<CharacterManager>(null, true);
            if (!characterManager.TryGetCurrentCharacter(out Character character))
                return;
            if (gravityProvider.gameObject != character.gameObject)
                return;
            CameraYAxisProxyManager cameraManager = ProcessManager.GetSystemSafe<CameraYAxisProxyManager>(null, true);
            cameraManager.OnRemoveCameraYUpOverride(m_cameraOverrideHandle);
            m_cameraOverrideHandle = null;
        }

        // 06003c24 is the shipped player path. Conditional editor drawing calls
        // have no native calls here; do not invent their absent invocation graph.
        [Conditional("UNITY_EDITOR")]
        private void OnDrawGizmos()
        {
            GravityDescription description = Description;
            if (description.InnerFalloffDistance > 0f &&
                description.InnerFalloffDistance < description.InnerDistance)
                Gizmos.color = GravityDescription.GizmoColourFalloff;
            Gizmos.color = GravityDescription.GizmoColourFullStrength;
            description = Description;
            if (description.OuterFalloffDistance > description.OuterDistance)
                Gizmos.color = GravityDescription.GizmoColourFalloff;
        }

        // 06003c25 is a genuine empty original virtual method.
        public virtual void DrawTargetGizmo(Transform target) { }

        [Conditional("UNITY_EDITOR")]
        protected abstract void DrawGizmo(float distance);
    }
}
