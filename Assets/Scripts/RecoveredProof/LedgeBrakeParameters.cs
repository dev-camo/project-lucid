using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020004eb; complete 12 own fields/10 own methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class LedgeBrakeParameters
    {
        // 0x04001194; original instance offset 0x10.
        [Tooltip("The maximum angle from the world flat plane to be treated as a ledge.")]
        [SerializeField]
        private float m_maximumAngleToPlane = 60f;
        // 0x04001195; original instance offset 0x14.
        [Tooltip("Velocity threshold for intent angle to brake.")]
        [SerializeField]
        private float m_maximumVelocityThreshold = 10f;
        // 0x04001196; original instance offset 0x18.
        [Tooltip("Intent angle for braking when below velocity threshold.")]
        [SerializeField]
        private float m_intentAngleBelowVelocity = 90f;
        // 0x04001197; original instance offset 0x1c.
        [Tooltip("Intent angle for braking when above velocity threshold.")]
        [SerializeField]
        private float m_intentAngleAboveVelocity = 45f;
        // 0x04001198; original instance offset 0x20.
        [Tooltip("Minimum angle to determine a slope as an edge.")]
        [SerializeField]
        private float m_ledgeMinAngleLimit = 45f;
        // 0x04001199; original instance offset 0x24.
        [Tooltip("Distance ahead to stop character if ledge reached.")]
        [SerializeField]
        private float m_raycastStopDistance = 1f;
        // 0x0400119a; original instance offset 0x28.
        [SerializeField]
        [Tooltip("Distance ahead to apply braking if ledge detected.")]
        private float m_raycastDetectDistance = 2f;
        // 0x0400119b; original instance offset 0x2c.
        [SerializeField]
        [Tooltip("Distance to project downwards to detect a ledge.")]
        private float m_raycastDownDistance = 2f;
        // 0x0400119c; original instance offset 0x30.
        private float m_maximumCosAngleToPlane;
        // 0x0400119d; original instance offset 0x34.
        private float m_ledgeCosAngleLimit;
        // 0x0400119e; original instance offset 0x38.
        private float m_intentCosAngleBelowVelocity;
        // 0x0400119f; original instance offset 0x3c.
        private float m_intentCosAngleAboveVelocity;

        // 0x06001b4a: original direct field getter.
        public float MaximumCosAngleToPlane => m_maximumCosAngleToPlane;
        // 0x06001b4b: original direct field getter.
        public float MaximumVelocityThreshold => m_maximumVelocityThreshold;
        // 0x06001b4c: original direct field getter.
        public float IntentCosAngleBelowVelocity => m_intentCosAngleBelowVelocity;
        // 0x06001b4d: original direct field getter.
        public float IntentCosAngleAboveVelocity => m_intentCosAngleAboveVelocity;
        // 0x06001b4e: original direct field getter.
        public float LedgeCosAngleLimit => m_ledgeCosAngleLimit;
        // 0x06001b4f: original direct field getter.
        public float RaycastStopDistance => m_raycastStopDistance;
        // 0x06001b50: original direct field getter.
        public float RaycastDetectDistance => m_raycastDetectDistance;
        // 0x06001b51: original direct field getter.
        public float RaycastDownDistance => m_raycastDownDistance;

        // 0x06001b52: four original degree-to-radian products and cosines.
        // ARM publishes in this order; x86 computes all four, then publishes one vector.
        // No cross-architecture concurrent observation or libc rounding parity is claimed.
        public void CalculateCachedValues()
        {
            m_maximumCosAngleToPlane = Mathf.Cos(m_maximumAngleToPlane * Mathf.Deg2Rad);
            m_ledgeCosAngleLimit = Mathf.Cos(m_ledgeMinAngleLimit * Mathf.Deg2Rad);
            m_intentCosAngleBelowVelocity = Mathf.Cos(m_intentAngleBelowVelocity * Mathf.Deg2Rad);
            m_intentCosAngleAboveVelocity = Mathf.Cos(m_intentAngleAboveVelocity * Mathf.Deg2Rad);
        }

        // 0x06001b53: eight original scalar defaults precede Object construction.
        // Cache fields remain zero until CalculateCachedValues is called.
        public LedgeBrakeParameters() { }
    }
}
