using System.Collections.Generic;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CustomGravity : GravityProvider
    {
        [Tooltip("Metadata keys to retrieve gravity values along a surface.")]
        [SerializeField] private GravitySurfaceDefinition m_surfaceDefinition;
        private static readonly Vector3 s_gravityDefault = Physics.gravity;
        private bool m_maintainLastGravity;
        private Vector3 m_lastGravity;
        private readonly LinkedList<GravitySource> m_sources = new LinkedList<GravitySource>();
        private Tracker2D m_tracker;

        // Original 06003bd4: descending priority, newest first among equal priorities.
        public override void Register(GravitySource source)
        {
            if (m_sources.Contains(source))
                return;
            LinkedListNode<GravitySource> node = m_sources.First;
            while (node != null && node.Value.Priority > source.Priority)
                node = node.Next;
            if (node != null)
                m_sources.AddBefore(node, source);
            else
                m_sources.AddLast(source);
        }

        public override void Unregister(GravitySource source)
        {
            m_sources.Remove(source);
        }

        // 06003bd6: read Previous after the exit callback. Removing the current node
        // during that callback can end this traversal before the remaining list clears.
        protected override void UnregisterAll()
        {
            LinkedListNode<GravitySource> node = m_sources.Last;
            while (node != null)
            {
                node.Value.ManualTriggerExit(this);
                node = node.Previous;
            }
            m_sources.Clear();
        }

        public float GetMaxDistance()
        {
            LinkedListNode<GravitySource> first = m_sources.First;
            int priority = first != null ? first.Value.Priority : 0;
            float distance = 0f;
            foreach (GravitySource source in m_sources)
            {
                if (source.Priority < priority)
                    break;
                float sourceDistance = source.MaxDistance;
                if (sourceDistance > distance)
                    distance = sourceDistance;
            }
            return distance;
        }

        // 06003bd8: lower priorities contribute only after the preceding group has
        // failed the ordered positive magnitude test. Cancellation and NaN therefore
        // have the shipped fallback behavior; Active is not filtered in this method.
        public override Vector3 GetGravity(Vector3 position)
        {
            if (m_maintainLastGravity)
                return m_lastGravity;
            if (m_sources.Count == 0)
                return s_gravityDefault;
            Vector3 gravity = Vector3.zero;
            int priority = m_sources.First.Value.Priority;
            foreach (GravitySource source in m_sources)
            {
                int sourcePriority = source.Priority;
                if (sourcePriority < priority)
                {
                    priority = sourcePriority;
                    if (gravity.sqrMagnitude > 0.0001f)
                        break;
                }
                gravity += source.GetGravity(position);
            }
            return gravity.sqrMagnitude > 0.0001f ? gravity : s_gravityDefault;
        }

        // 06003bd9: only the highest registered priority is considered. A tie or
        // unordered magnitude replaces the prior vector; retain the ordered-less skip.
        public Vector3 GetGravityPrimary(Vector3 position)
        {
            if (m_maintainLastGravity)
                return m_lastGravity;
            if (m_sources.Count == 0)
                return s_gravityDefault;
            Vector3 gravity = Vector3.zero;
            float maximum = 0f;
            int priority = m_sources.First.Value.Priority;
            foreach (GravitySource source in m_sources)
            {
                if (source.Priority < priority)
                    break;
                Vector3 sourceGravity = source.GetGravityPrimary(position);
                float magnitude = sourceGravity.sqrMagnitude;
                if (magnitude < maximum)
                    continue;
                gravity = sourceGravity;
                maximum = magnitude;
            }
            return gravity;
        }

        // 06003bda: the four original format strings were recovered from encoded
        // metadata bytes. Query each active source before printing its name/cache.
        public void GetDebugInfo(Vector3 position, float mass, StringBuilder stringInfoBuilder)
        {
            stringInfoBuilder.AppendLine(string.Format("Gravity Sources: (mass = {0})", mass));
            int index = 0;
            foreach (GravitySource source in m_sources)
            {
                if (!source.Active)
                    continue;
                Vector3 gravity = source.GetGravity(position);
                stringInfoBuilder.AppendLine(string.Format("  {0} {1}", index, source.name));
                stringInfoBuilder.AppendLine(string.Format("    {0}, D = {1:F2}, P = {2}",
                    gravity.ToString("F2"), source.Distance, source.Priority));
                index++;
            }
        }

        // 06003bdb: a non-null reassignment publishes/attaches directly. The shipped
        // method does not detach an older tracker first or deduplicate its delegate.
        public void SetTracker(Tracker2D tracker)
        {
            if (tracker != null)
            {
                m_tracker = tracker;
                GravitySurface surface = GetGravitySurface(m_tracker.Location);
                if (surface != null)
                    surface.SetTracker(this, m_tracker, m_surfaceDefinition);
                m_tracker.OnTrackableSurfaceChange += OnTrackableSurfaceChange;
            }
            else if (m_tracker != null)
            {
                GravitySurface surface = GetGravitySurface(m_tracker.Location);
                if (surface != null)
                    surface.ClearTracker();
                m_tracker.OnTrackableSurfaceChange -= OnTrackableSurfaceChange;
                m_tracker = null;
            }
        }

        public void EnableMaintainLastGravity(Vector3 position)
        {
            m_lastGravity = GetGravity(position);
            m_maintainLastGravity = true;
        }

        public void DisableMaintainLastGravity()
        {
            m_maintainLastGravity = false;
            m_lastGravity = Vector3.zero;
        }

        private void OnTrackableSurfaceChange(SurfaceLocation fromLocation, SurfaceLocation toLocation)
        {
            GravitySurface surface = GetGravitySurface(fromLocation);
            if (surface != null)
                surface.ClearTracker();
            surface = GetGravitySurface(toLocation);
            if (surface != null)
                surface.SetTracker(this, m_tracker, m_surfaceDefinition);
        }

        // 06003bdf: the original cast is specifically MonoBehaviour. Its immediate
        // parent must exist; no parent/null fallback or parent-chain search is added.
        private static GravitySurface GetGravitySurface(SurfaceLocation location)
        {
            MonoBehaviour behaviour = location.m_surface as MonoBehaviour;
            if (!behaviour)
                return null;
            return behaviour.transform.parent.GetComponent<GravitySurface>();
        }

        public IReadOnlyCollection<GravitySource> GetSources()
        {
            return m_sources;
        }
    }
}
