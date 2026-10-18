using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ab3; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class SurfacePathSpiralGenerator : SurfacePathGenerator
    {
        [SerializeField]
        private float m_length;

        [SerializeField]
        [Min(1f)]
        private int m_curveSegments;

        [SerializeField]
        private Vector2 m_axisLengths;

        [SerializeField]
        private Vector2 m_angleRange;

        [SerializeField]
        private bool m_clockwise;

        private float AngleStart => m_angleRange.x;

        private float AngleEnd => m_angleRange.y;

        // 06003d93 (0x6DC090): an ellipse in the aligned plane rises by m_length along the
        // remaining axis and each knot turns about that axis. The arc is shifted so that its
        // start sits on the origin, and the start point is only added to an empty list.
        public override void GenerateKnots(List<LightweightTransform> knots)
        {
            LightweightTransform knot = new LightweightTransform(Vector3.zero, Quaternion.identity);
            float angleStart = AngleStart;
            float angleEnd = AngleEnd;
            int curveSegments = m_curveSegments;
            float angle = angleStart * Mathf.Deg2Rad;
            GetOrigin(out Vector3 originPosition, out Quaternion originRotation, knots);
            Vector3 originOffset = originPosition - originRotation * GetPosition(angle);
            Vector3 heightAxis = Vector3.one - GetAxis(1f, 1f, false);
            float distance = 0f;
            for (int i = 0; i <= curveSegments; i++)
            {
                if (i > 0 || knots.Count == 0)
                {
                    knot.Location = originRotation * (GetPosition(angle) + heightAxis * distance) + originOffset;
                    knot.Orientation = originRotation * Quaternion.AngleAxis((m_clockwise ? -angle : angle) * Mathf.Rad2Deg, heightAxis);
                    knots.Add(knot);
                }
                angle += (angleEnd - angleStart) * Mathf.Deg2Rad / curveSegments;
                distance += m_length / m_curveSegments;
            }
        }

        private Vector3 GetPosition(float angleRadians)
        {
            return GetAxis(m_axisLengths.x * Mathf.Cos(angleRadians), m_axisLengths.y * Mathf.Sin(angleRadians), m_clockwise);
        }
    }
}
