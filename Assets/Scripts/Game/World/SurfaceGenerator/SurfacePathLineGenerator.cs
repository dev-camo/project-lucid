using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aaf; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class SurfacePathLineGenerator : SurfacePathGenerator
    {
        [Min(1f)]
        [SerializeField]
        private float m_length;

        [SerializeField]
        [Min(1f)]
        private int m_curveSegments;

        [SerializeField]
        private Vector3 m_orientation;

        // 06003d8a (0x6DB6B0): every knot shares the origin rotation turned by m_orientation;
        // the start point is only added to an empty list.
        public override void GenerateKnots(List<LightweightTransform> knots)
        {
            GetOrigin(out Vector3 originPosition, out Quaternion originRotation, knots);
            Quaternion rotation = originRotation * Quaternion.Euler(m_orientation);
            Vector3 forward = rotation * Vector3.forward;
            float distance = 0f;
            for (int i = 0; i <= m_curveSegments; i++)
            {
                if (i > 0 || knots.Count == 0)
                    knots.Add(new LightweightTransform(originPosition + forward * distance, rotation));
                distance += m_length / m_curveSegments;
            }
        }
    }
}
