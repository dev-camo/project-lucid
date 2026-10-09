using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Dead Zone")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierDeadZone : InputModifier
    {
        [SerializeField] protected float m_openGateValue = 0.5f;
        [SerializeField] protected float m_closeGateValue = 0.4f;
        [SerializeField] protected bool m_invertDeadZone;
        protected bool m_gateOpen;

        public override float Modify(float value, float deltaTime) // 0600014c
        {
            float magnitude = Mathf.Abs(value);
            if (!m_gateOpen && (m_invertDeadZone ? magnitude <= m_openGateValue : magnitude >= m_openGateValue))
            {
                m_gateOpen = true;
                return value;
            }
            if (!m_gateOpen || (m_invertDeadZone ? magnitude >= m_closeGateValue : magnitude <= m_closeGateValue))
            {
                m_gateOpen = false;
                return 0f;
            }
            return value;
        }

        public override Vector3 Modify(Vector3 originalValue, float deltaTime) // 0600014d
        {
            float magnitude = Mathf.Abs(originalValue.magnitude);
            if (!m_gateOpen && (m_invertDeadZone ? magnitude <= m_openGateValue : magnitude >= m_openGateValue))
            {
                m_gateOpen = true;
                return originalValue;
            }
            if (!m_gateOpen || (m_invertDeadZone ? magnitude >= m_closeGateValue : magnitude <= m_closeGateValue))
            {
                m_gateOpen = false;
                return Vector3.zero;
            }
            return originalValue;
        }

        public override void Reset() { m_gateOpen = false; } // 0600014e
        public ModifierDeadZone() { } // 0600014f: original0.5/0.4 initializers, no invented state.
    }
}
