using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa1; bodies translated from the 1.10.1 x86-64 binary.
    // A section that follows an elliptical arc: y = sin(angle) * height and
    // z = (1 - cos(angle)) * width, swept over the angle range.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class SurfaceMeshCrossSectionEllipseGenerator : SurfaceMeshCrossSectionGenerator
    {
        [Tooltip("The height of the ellipse.")]
        [SerializeField]
        [Min(0.01f)]
        private float m_height;

        [Min(1f)]
        [Tooltip("Step size for each evaluated angle.")]
        [SerializeField]
        private float m_angleStep;

        [SerializeField]
        [Tooltip("Start and end angles.")]
        private Vector2 m_angleRange;

        [SerializeField]
        [Tooltip("Direction to step around ellipse.")]
        private bool m_angleClockwise;

        public override SurfaceCrossSectionType Type => SurfaceCrossSectionType.Ellipse;

        private float AngleStart => m_angleRange.x;

        private float AngleEnd => m_angleRange.y;

        // 06003d2d (0x6D3260): the knot origin pulled back along its forward by the offset
        // multiplier times the width, then back by the arc point where the sweep begins
        // (the start angle when clockwise, otherwise the end angle).
        public override Vector3 GetOrigin(KnotStepData knotStepData, float offsetMultiplier)
        {
            float angle = m_angleClockwise ? AngleStart : AngleEnd;
            Vector3 forward = knotStepData.m_rotation * Vector3.forward;
            return knotStepData.m_origin - forward * (Width * offsetMultiplier) - knotStepData.m_rotation * GetPosition(angle);
        }

        // 06003d2e (0x6D33F0): walks the arc from its first angle in steps of the angle step, with
        // one extra final vertex clamped to the far end of the range. Every vertex also becomes the
        // knot origin. Afterwards the knot rotation is turned by the tangent at the final angle.
        public override void GenerateMeshCrossSection(List<Vector3> vertices, List<int> triangles, KnotStepData knotStepData,
            int knotStepIndex, int knotStepCount, bool invertOrder, float offsetMultiplier)
        {
            int stepCount = (int)((AngleEnd - AngleStart) / m_angleStep);
            float step = m_angleClockwise ? m_angleStep : -m_angleStep;
            Vector3 origin = GetOrigin(knotStepData, offsetMultiplier);
            float angle = m_angleClockwise ? AngleStart : AngleEnd;
            for (int i = 0; i <= stepCount + 1; i++)
            {
                if (i == stepCount + 1)
                    angle = m_angleClockwise ? AngleEnd : AngleStart;
                int vertexIndex = vertices.Count;
                Vector3 vertex = knotStepData.m_rotation * (OffsetLocal + GetPosition(angle)) + origin;
                knotStepData.m_origin = vertex;
                vertices.Add(vertex);
                if (knotStepCount - 1 > knotStepIndex && i <= stepCount)
                    AddQuad(vertexIndex, stepCount + 2, invertOrder, triangles);
                angle += step;
            }
            // The shipped call passes the angle already converted to radians into GetTangent, which converts again.
            float finalAngle = m_angleClockwise ? AngleEnd : AngleStart;
            Vector3 tangent = GetTangent(finalAngle * Mathf.Deg2Rad);
            knotStepData.m_rotation = knotStepData.m_rotation * Quaternion.LookRotation(-tangent, Vector3.right);
        }

        // 06003d2f (0x6D33B0)
        private Vector3 GetPosition(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(radians) * m_height, (1f - Mathf.Cos(radians)) * Width);
        }

        // 06003d30 (0x6D3B60)
        private Vector3 GetTangent(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Cos(radians), Mathf.Sin(radians));
        }

        // 06003d31 (0x6D3B90): the angle is proportional to the distance over the total length of the
        // arc, measured by summing one-degree chords. The result is not offset by the start angle;
        // the shipped code adds a constant zero.
        protected override void CalculateLocationLocal(float distance, out Vector3 positionLocal, out Quaternion rotationLocal)
        {
            float step = AngleEnd > AngleStart ? 1f : -1f;
            float length = 0f;
            for (float angle = AngleStart; angle < AngleEnd; angle += step)
                length += Vector3.Distance(GetPosition(angle), GetPosition(angle + step));
            float arcAngle = distance / length * (AngleEnd - AngleStart) + 0f;
            positionLocal = GetPosition(arcAngle);
            rotationLocal = Quaternion.LookRotation(GetTangent(arcAngle), Vector3.up);
        }
    }
}
