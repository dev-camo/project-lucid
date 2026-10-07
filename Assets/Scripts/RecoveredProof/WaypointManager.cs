using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class WaypointManager : ISystem
    {
        public Action WaypointsUpdated;
        private readonly List<WaypointTarget> m_availableTargets = new List<WaypointTarget>();
        private readonly List<WaypointTarget> m_overrideTargets = new List<WaypointTarget>();

        //06003aa8: suppress duplicate registration through the original helper,
        //but notify even when this target was already present (including null).
        public void RegisterWaypoint(WaypointTarget waypointTarget)
        {
            m_availableTargets.AddUnique(waypointTarget);
            WaypointsUpdated?.Invoke();
        }

        //06003aa9: notification is independent of Remove's result. The original
        //does not filter destroyed Unity references or remove override entries.
        public void RemoveWaypoint(WaypointTarget waypointTarget)
        {
            m_availableTargets.Remove(waypointTarget);
            WaypointsUpdated?.Invoke();
        }

        //06003aaa: clear before enumerating the supplied sequence. Preserve
        //duplicates and order; a null or failing sequence skips notification and
        //leaves whatever replacement entries were appended before the failure.
        public void SetOverrideTargets(IEnumerable<WaypointTarget> waypoints)
        {
            m_overrideTargets.Clear();
            m_overrideTargets.AddRange(waypoints);
            WaypointsUpdated?.Invoke();
        }

        //06003aab: no overrides exposes the original live available list. With
        //overrides, return a new ordered intersection retaining duplicate override
        //entries and managed list membership semantics, even for Unity objects.
        public IReadOnlyList<WaypointTarget> GetTargets()
        {
            if (m_overrideTargets.Count <= 0) return m_availableTargets;
            var targets = new List<WaypointTarget>();
            foreach (WaypointTarget target in m_overrideTargets)
                if (m_availableTargets.Contains(target)) targets.Add(target);
            return targets;
        }

        //06003aac: both list allocations precede the original Object constructor.
        public WaypointManager()
        {
        }
    }
}
