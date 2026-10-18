using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa4; bodies translated from the 1.10.1 x86-64 binary.
    // A flat strip: the section runs from the origin along a direction tilted about Vector3.left.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class SurfaceMeshCrossSectionPlaneGenerator : SurfaceMeshCrossSectionGenerator
    {
        private const int VertexStride = 2;

        [SerializeField]
        protected float m_angle;

        public override SurfaceCrossSectionType Type => SurfaceCrossSectionType.Plane;

        // 06003d3e (0x6D4790): the knot origin and local offset, pulled back along the tilted
        // direction by the offset multiplier times the section width.
        public override Vector3 GetOrigin(KnotStepData knotStepData, float offsetMultiplier)
        {
            Vector3 direction = knotStepData.m_rotation * (Quaternion.AngleAxis(m_angle, Vector3.left) * Vector3.forward);
            return knotStepData.m_origin + OffsetLocal - direction * (offsetMultiplier * Width);
        }

        // 06003d3f (0x6D4900): adds the origin and the point one width along the direction, then
        // moves the knot origin to that far point, so the next section starts from it. A quad
        // joins this pair to the next section unless this is the last knot.
        public override void GenerateMeshCrossSection(List<Vector3> vertices, List<int> triangles, KnotStepData knotStepData,
            int knotStepIndex, int knotStepCount, bool invertOrder, float offsetMultiplier = 0f)
        {
            Vector3 direction = knotStepData.m_rotation * (Quaternion.AngleAxis(m_angle, Vector3.left) * Vector3.forward);
            Vector3 origin = GetOrigin(knotStepData, offsetMultiplier);
            int vertexIndex = vertices.Count;
            vertices.Add(origin);
            Vector3 far = origin + direction * Width;
            vertices.Add(far);
            knotStepData.m_origin = far;
            if (knotStepCount - 1 > knotStepIndex)
                AddQuad(vertexIndex, VertexStride, invertOrder, triangles);
        }

        // 06003d40 (0x6D4B80)
        protected override void CalculateLocationLocal(float distance, out Vector3 positionLocal, out Quaternion rotationLocal)
        {
            rotationLocal = Quaternion.AngleAxis(m_angle, Vector3.left);
            positionLocal = rotationLocal * (Vector3.right * distance);
        }

        // 06003d41 (0x6D48A0): not called by any shipped method.
        private Quaternion CalculateRotationLocal()
        {
            return Quaternion.AngleAxis(m_angle, Vector3.left);
        }
    }
}
