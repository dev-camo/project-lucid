using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GravityDescription
    {
        public static readonly float GravityDefault = -Physics.gravity.magnitude;
        public const float InnerFalloffDistanceDefault = 0f;
        public const float InnerDistanceDefault = 0f;
        public const float OuterDistanceDefault = 10f;
        public const float OuterFalloffDistanceDefault = 15f;
        private const float DistanceTolerance = 0.01f;
        public static readonly Color GizmoColourFalloff = new Color(0f, 1f, 1f, 1f);
        public static readonly Color GizmoColourFullStrength = new Color(1f, 0.9215686321258545f, 0.01568627543747425f, 1f);

        [SerializeField] private float m_gravity = GravityDefault;
        [SerializeField] private float m_innerFalloffDistance;
        [SerializeField] private float m_innerDistance;
        [SerializeField] private float m_outerDistance = OuterDistanceDefault;
        [SerializeField] private float m_outerFalloffDistance = OuterFalloffDistanceDefault;
        private float m_innerFalloffFactor;
        private float m_outerFalloffFactor;

        public float InnerFalloffDistance => m_innerFalloffDistance;
        public float InnerDistance => m_innerDistance;
        public float OuterDistance => m_outerDistance;
        public float OuterFalloffDistance => m_outerFalloffDistance;
        public float MaxDistance => m_outerFalloffDistance;
        public float Distance { get; private set; }

        // Game.Runtime 06003bfc. Clamp the authored limits in their original order.
        public void Set(float gravity, float innerFalloffDistance, float innerDistance, float outerDistance, float outerFalloffDistance)
        {
            m_gravity = gravity;
            m_innerFalloffDistance = innerFalloffDistance;
            m_innerDistance = Mathf.Max(innerDistance, innerFalloffDistance);
            m_outerDistance = Mathf.Max(outerDistance, m_innerDistance);
            m_outerFalloffDistance = Mathf.Max(outerFalloffDistance, m_outerDistance);
            m_innerFalloffFactor = innerDistance > innerFalloffDistance
                ? 1f / (innerDistance - innerFalloffDistance) : 0f;
            // Preserve the shipped reversed comparison: validated ordinary limits
            // produce zero here, rather than a conventional outward falloff.
            m_outerFalloffFactor = m_outerDistance > m_outerFalloffDistance
                ? 1f / (m_outerFalloffDistance - m_outerDistance) : 0f;
        }

        // Game.Runtime 06003bfd. Record the query even when it lies outside the range.
        public float CalculateGravity(float distance)
        {
            Distance = distance;
            if (m_innerFalloffDistance - DistanceTolerance > distance) return 0f;
            if (distance > m_outerFalloffDistance + DistanceTolerance) return 0f;
            if (distance > m_outerDistance)
                return m_gravity * (1f - (distance - m_outerDistance) * m_outerFalloffFactor);
            if (m_innerDistance > distance)
                return m_gravity * (1f - (m_innerDistance - distance) * m_innerFalloffFactor);
            return m_gravity;
        }

        // Game.Runtime 06003bfe. Refresh factors from the serialized distances;
        // Set retains the original pre-clamp inner-factor inputs.
        public void Validate()
        {
            Set(m_gravity, m_innerFalloffDistance, m_innerDistance, m_outerDistance, m_outerFalloffDistance);
        }
    }
}
