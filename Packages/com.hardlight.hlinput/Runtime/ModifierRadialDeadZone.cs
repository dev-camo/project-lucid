using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Dead Zone")]
    public class ModifierRadialDeadZone : ModifierDeadZone
    {
        [NonSerialized] private bool m_cachedValues;
        [NonSerialized] private float m_radiusOpenGateSquared;
        [NonSerialized] private float m_radiusCloseGateSquared;

        public override Vector3 Modify(Vector3 originalValue, float deltaTime) // 0600015f
        {
            if (!m_cachedValues)
            {
                m_radiusOpenGateSquared = m_openGateValue * m_openGateValue;
                m_radiusCloseGateSquared = m_closeGateValue * m_closeGateValue;
                m_cachedValues = true;
            }
            float magnitudeSquared = Mathf.Abs(originalValue.x * originalValue.x + originalValue.y * originalValue.y);
            if (!m_gateOpen && (m_invertDeadZone ? magnitudeSquared <= m_radiusOpenGateSquared : magnitudeSquared >= m_radiusOpenGateSquared))
            {
                m_gateOpen = true;
                return originalValue;
            }
            if (!m_gateOpen || (m_invertDeadZone ? magnitudeSquared >= m_radiusCloseGateSquared : magnitudeSquared <= m_radiusCloseGateSquared))
            {
                m_gateOpen = false;
                return Vector3.zero;
            }
            return originalValue;
        }

        public override void Reset() { m_gateOpen = false; m_cachedValues = false; } // 06000160: squared caches not cleared.
        public ModifierRadialDeadZone() { } // 06000161: native inlines original base defaults0.5/0.4.
    }
}
