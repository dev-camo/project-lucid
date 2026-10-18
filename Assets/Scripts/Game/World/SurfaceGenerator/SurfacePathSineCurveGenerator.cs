using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ab1; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class SurfacePathSineCurveGenerator : SurfacePathGenerator
    {
        [Min(1f)]
        [SerializeField]
        private int m_curveSegments;

        [SerializeField]
        private float m_curveHeight;

        [Min(0.001f)]
        [SerializeField]
        private float m_curveAmplitude;

        [SerializeField]
        [Min(1f)]
        private float m_curveLength;

        // 06003d8d (0x6DBB50): the sine angle in degrees advances by m_curveAmplitude and the
        // distance by m_curveLength over the segments. Every knot keeps the origin rotation and
        // the start point is only added to an empty list.
        public override void GenerateKnots(List<LightweightTransform> knots)
        {
            GetOrigin(out Vector3 originPosition, out Quaternion originRotation, knots);
            float angle = 0f;
            float distance = 0f;
            for (int i = 0; i <= m_curveSegments; i++)
            {
                if (i > 0 || knots.Count == 0)
                    knots.Add(new LightweightTransform(originRotation * GetPosition(angle, distance) + originPosition, originRotation));
                angle += m_curveAmplitude / m_curveSegments;
                distance += m_curveLength / m_curveSegments;
            }
        }

        private Vector3 GetPosition(float angle, float distance)
        {
            return GetAxis(distance, Mathf.Sin(angle * Mathf.Deg2Rad) * m_curveHeight, false);
        }
    }
}
