using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class WaypointTarget : MonoBehaviour
    {
        [SerializeField] private WaypointType m_type;
        private readonly SystemRef<WaypointManager> m_waypointManagerRef = ProcessManager.GetSystemRef<WaypointManager>();

        //06003aad: the authored enum is returned without validation or fallback.
        public WaypointType Type => m_type;

        //06003aae/06003ab1: retain InvokeOnValid's immediate-or-deferred callback.
        //The original does not cancel a pending callback when this object disables.
        private void OnEnable()
        {
            m_waypointManagerRef.InvokeOnValid(manager => manager.RegisterWaypoint(this));
        }

        //06003aaf: the actual IsNull test differs from checking process validity.
        //Remove only from an existing reference; callback failures propagate.
        private void OnDisable()
        {
            if (m_waypointManagerRef.IsNull()) return;
            m_waypointManagerRef.Get().RemoveWaypoint(this);
        }

        //06003ab0: resolve the original system reference before MonoBehaviour's
        //constructor, retaining the default authored enum value.
        public WaypointTarget()
        {
        }
    }
}
