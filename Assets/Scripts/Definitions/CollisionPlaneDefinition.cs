using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000529 and nested 0x0200052a. Serialized names,
    // field order, attributes and zero-initialized constructor state are retained.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CollisionPlaneDefinition",
        menuName = "HardlightProject/DefinitionData/Definitions/CollisionPlaneDefinition")]
    public class CollisionPlaneDefinition : ScriptableObject
    {
        [Tooltip("Vector that describes the plane's normal.")]
        public Vector3 Normal;
        [Range(0f, 90f)]
        [Tooltip("The threshold angle that validates the plane. 90 defines the entire plane.")]
        public float Angle;
        [Tooltip("Logic for motion towards the plane.")]
        public CollisionMotion[] CollisionOverrides;
        [HideInInspector]
        [SerializeField]
        private float m_angleCosine;

        public float AngleCosine { get { return m_angleCosine; } }

        // 0x06001c22: cache the signed plane cosine before visiting authored
        // overrides in order. Null arrays or entries are not skipped by the original.
        private void OnValidate()
        {
            m_angleCosine = Mathf.Cos(Angle * Mathf.Deg2Rad);
            foreach (CollisionMotion motion in CollisionOverrides) motion.RefreshCache();
        }

        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class CollisionMotion
        {
            [Tooltip("Axis to evaluate angle around.")]
            public Vector3 Axis;
            [Tooltip("Minimum angle of range from 0 inclusive to 90 exclusive. Angle is reflected so 90 encompasses the entire plane.")]
            [Range(0f, 90f)]
            public float AngleMin;
            [Range(0f, 90f)]
            [Tooltip("Maximum angle of range from 0 inclusive to 90 exclusive. Angle is reflected so 90 encompasses the entire plane.")]
            public float AngleMax;
            [Tooltip("Allows character to orientate their forward motion to the plane.")]
            public bool OrientateHeadingToPlane;
            [Tooltip("Character's rigid body feet will orientate to the plane if it is within the angle of the current gravity.")]
            [Range(0f, 90f)]
            public float OrientateBodyToGravityThresholdAngle;
            [HideInInspector]
            [SerializeField]
            private float m_angleMinCosine;
            [HideInInspector]
            [SerializeField]
            private float m_angleMaxCosine;
            [HideInInspector]
            [SerializeField]
            private float m_orientateBodyToGravityThresholdAngleCosine;

            public float AngleMinCosine { get { return m_angleMinCosine; } }
            public float AngleMaxCosine { get { return m_angleMaxCosine; } }
            public float OrientateBodyToGravityThresholdAngleCosine
            {
                get { return m_orientateBodyToGravityThresholdAngleCosine; }
            }

            // 0x06001c27: these cosines are absolute, unlike the outer plane.
            // Cache both range products before evaluating either cosine, then
            // evaluate the maximum first, preserving the supplied native order.
            // RangeAttribute is an inspector hint; runtime values are not clamped.
            public void RefreshCache()
            {
                float minimumRadians = AngleMin * Mathf.Deg2Rad;
                float maximumRadians = AngleMax * Mathf.Deg2Rad;
                float maximumCosine = Mathf.Cos(maximumRadians);
                float minimumCosine = Mathf.Cos(minimumRadians);
                m_angleMinCosine = Mathf.Abs(minimumCosine);
                m_angleMaxCosine = Mathf.Abs(maximumCosine);
                m_orientateBodyToGravityThresholdAngleCosine = Mathf.Abs(
                    Mathf.Cos(OrientateBodyToGravityThresholdAngle * Mathf.Deg2Rad));
            }
        }
    }
}
