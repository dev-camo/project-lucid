using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a06; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FlightStaminaGizmoDisplay : MonoBehaviour
    {
        [SerializeField]
        private SphereCollider m_collider;

        [SerializeField]
        private Vector3 m_flightRulerSize;

        [SerializeField]
        private Vector3 m_flightRulerOffset;

        [SerializeField]
        private Color m_gizmoColour = Color.white;

        // 06003984 (1.10.1 RVA 0x6B0B40)
        private void Reset()
        {
            m_collider = GetComponent<SphereCollider>();
        }

        // 06003985 (1.10.1 RVA 0x6B0B90): the ruler cube is drawn in the object's local space,
        // then a sphere of the collider's radius at the local origin.
        private void OnDrawGizmos()
        {
            if (m_collider == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = m_gizmoColour;
            Gizmos.DrawCube(m_flightRulerOffset, m_flightRulerSize);
            Gizmos.DrawSphere(Vector3.zero, m_collider.radius);
        }

        // 06003986 (1.10.1 RVA 0x6B0CD0): the colour initialiser (opaque white) precedes the base call.
        public FlightStaminaGizmoDisplay() { }
    }
}
