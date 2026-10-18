using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aab; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class SurfacePathEllipseGenerator : SurfacePathGenerator
    {
        [Min(1f)]
        [SerializeField]
        private int m_curveSegments;

        [SerializeField]
        private Vector2 m_axisLengths;

        [SerializeField]
        private Vector2 m_angleRange;

        [SerializeField]
        private bool m_clockwise;

        private float AngleStart => m_angleRange.x;

        private float AngleEnd => m_angleRange.y;

        // 06003d81 (0x6DAB80): the arc is shifted so that its start sits on the origin. The start
        // point is only added to an empty list; otherwise the last knot already stands there.
        public override void GenerateKnots(List<LightweightTransform> knots)
        {
            float angleStart = AngleStart;
            float angleEnd = AngleEnd;
            int curveSegments = m_curveSegments;
            GetOrigin(out Vector3 originPosition, out Quaternion originRotation, knots);
            float angle = angleStart * Mathf.Deg2Rad;
            Vector3 originOffset = originPosition - originRotation * GetPosition(angle);
            Vector3 up = originRotation * Vector3.up;
            for (int i = 0; i <= curveSegments; i++)
            {
                if (i > 0 || knots.Count == 0)
                {
                    Vector3 position = originRotation * GetPosition(angle) + originOffset;
                    Vector3 forward = originRotation * GetTangent(angle);
                    knots.Add(new LightweightTransform(position, Quaternion.LookRotation(forward, up)));
                }
                angle += (angleEnd - angleStart) * Mathf.Deg2Rad / curveSegments;
            }
        }

        private Vector3 GetPosition(float angleRadians)
        {
            return GetAxis(m_axisLengths.x * Mathf.Cos(angleRadians), m_axisLengths.y * Mathf.Sin(angleRadians), m_clockwise);
        }

        // 06003d83 (0x6DB550): the tangent ignores the axis lengths.
        private Vector3 GetTangent(float angleRadians)
        {
            return GetAxis(-Mathf.Sin(angleRadians), Mathf.Cos(angleRadians), m_clockwise);
        }
    }
}
