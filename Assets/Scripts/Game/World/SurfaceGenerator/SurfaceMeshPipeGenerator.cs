using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa7, seven complete methods. Native-derived source candidate.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SurfaceMeshPipeGenerator : SurfaceMeshSplineGenerator
    {
        [SerializeField, Tooltip("The spline we'll step along to generate the mesh.")] private Spline m_spline;
        [Tooltip("Radius to generate mesh around spline."), SerializeField] private float m_pipeRadius = 0.2f;
        [Tooltip("Number of segments to use to make each pipe."), SerializeField, Range(3f, 32f)] private int m_pipeSegments = 8;
        [Tooltip("Number of segments to use to make each pipe."), SerializeField, Range(1f, 32f)] private float m_materialTiling = 1f;
        private readonly List<Vector3> m_splinePositions = new List<Vector3>();
        private readonly List<Quaternion> m_splineRotations = new List<Quaternion>();
        private readonly List<Vector2> m_pipeOffsets = new List<Vector2>();

        //06003d4a. Step vector is captured once; distance advances from fresh mesh-step reads.
        // Original active intervals with zero/negative step may not terminate; no invented safe-step guard.
        protected override void InitialiseSplineTrackers()
        {
            AddSpline(m_spline);
            float length = m_spline.GetSpline().Length;
            Vector3 step = Vector3.forward * m_meshStepDistance;
            Quaternion inverseRotation = Quaternion.Inverse(transform.rotation);
            Tracker2D tracker = GetTracker(0);
            tracker.MoveLocal(Vector3.back * length);
            m_splinePositions.Clear();
            m_splineRotations.Clear();
            float distance = 0f;
            while (distance <= length)
            {
                m_splinePositions.Add(transform.InverseTransformPoint(tracker.Location.m_worldPosition));
                m_splineRotations.Add(inverseRotation * tracker.Location.m_worldRotation);
                tracker.MoveLocal(step);
                if (Mathf.Approximately(length, distance)) break;
                distance = Mathf.Min(distance + m_meshStepDistance, length);
            }
            m_pipeOffsets.Clear();
            float angleStep = (Mathf.PI * 2f) / m_pipeSegments;
            for (int i = 0; i < m_pipeSegments; i++)
            {
                float angle = i * angleStep;
                m_pipeOffsets.Add(new Vector2(Mathf.Cos(angle) * m_pipeRadius, Mathf.Sin(angle) * m_pipeRadius));
            }
        }
        protected override void ApplyTrackerToMesh() //06003d4b; transform calls precede corresponding list indexing.
        {
            float length = m_spline.GetSpline().Length;
            Tracker2D tracker = GetTracker(0);
            tracker.MoveLocal(Vector3.back * length);
            float distance = 0f;
            int profileIndex = 0;
            int vertexIndex = 0;
            while (distance <= length)
            {
                Vector3 position = transform.TransformPoint(m_splinePositions[profileIndex]);
                Quaternion orientation = transform.rotation * m_splineRotations[profileIndex];
                Vector3 forward = orientation * Vector3.forward;
                Vector3 right = orientation * Vector3.right;
                Vector3 up = orientation * Vector3.up;
                Vector3 nearest = FindNearestSplineLocation(position, tracker, forward);
                Vector3 centre = nearest + up * m_meshNormalOffset;
                int i;
                for (i = 0; i < m_pipeSegments; i++)
                {
                    Vector2 offset = m_pipeOffsets[i];
                    Vector3 vertex = centre + right * offset.x + up * offset.y;
                    SetVertex(vertexIndex + i, transform.InverseTransformPoint(vertex));
                }
                vertexIndex += i;
                if (Mathf.Approximately(length, distance)) break;
                distance = Mathf.Min(distance + m_meshStepDistance, length);
                profileIndex++;
            }
            ApplyVertices();
        }
        [Conditional("UNITY_EDITOR")]
        public void SetPipeData(List<LightweightTransform> samplePoints, Material material, float radius, int segments) //06003d4c
        {
            m_pipeRadius = radius;
            m_pipeSegments = segments;
            SetMeshData(samplePoints, material);
        }
        protected override void SetMeshData(List<LightweightTransform> samplePoints, Material material) //06003d4d
        {
            base.SetMeshData(samplePoints, material);
            int samplePointIndex = 0;
            foreach (LightweightTransform point in samplePoints)
                GenerateCircleAtPoint(point, point.Forwards, 0f, ref samplePointIndex);
            for (int i = 0; i < samplePoints.Count - 1; i++) MeshTrianglesAddCircle(i, m_pipeSegments);
            GenerateMesh(false);
        }
        protected override bool UpdateSamplePoints(bool forceGenerate) //06003d4e; all updates and trim execute before change decision.
        {
            if (m_spline == null) return false;
            ISpline spline = m_spline.GetSpline();
            float length = spline.Length;
            int samplePointIndex = 0;
            bool changed = false;
            float distance = 0f;
            while (distance <= length)
            {
                LightweightTransform centre = spline.GetWorldTransformFromDistance(distance);
                changed |= GenerateCircleAtPoint(centre, centre.Orientation * Vector3.forward, distance / length, ref samplePointIndex);
                if (Mathf.Approximately(length, distance)) break;
                distance = Mathf.Min(distance + m_meshStepDistance, length);
            }
            bool clamped = ClampSamplePoints(samplePointIndex);
            if (!changed && !clamped && !forceGenerate) return false;
            MeshTrianglesClear();
            int segmentIndex = 0;
            distance = 0f;
            while (distance <= length)
            {
                MeshTrianglesAddCircle(segmentIndex, m_pipeSegments);
                distance = Mathf.Min(distance + m_meshStepDistance, length);
                if (Mathf.Approximately(length, distance)) break;
                segmentIndex++;
            }
            return true;
        }
        private bool GenerateCircleAtPoint(LightweightTransform centreLocation, Vector3 direction, float progress, ref int samplePointIndex) //06003d4f
        {
            Vector3 right = centreLocation.Right;
            Vector3 up = centreLocation.Up;
            Vector3 centre = centreLocation.Location + up * m_meshNormalOffset;
            Vector3.OrthoNormalize(ref direction, ref right, ref up);
            float angleStep = (Mathf.PI * 2f) / m_pipeSegments;
            bool changed = false;
            for (int i = 0; i < m_pipeSegments; i++)
            {
                float angle = i * angleStep;
                Vector3 vertex = centre + right * (Mathf.Cos(angle) * m_pipeRadius) + up * (Mathf.Sin(angle) * m_pipeRadius);
                Vector3 position = transform.InverseTransformPoint(vertex);
                // Preserve original mixed local-position/world-centre subtraction and separate vector transform.
                Vector3 normal = transform.InverseTransformVector((position - centre).normalized);
                Vector2 uv = new Vector2(m_materialTiling * progress, angle / (Mathf.PI * 2f));
                changed |= SetSamplePoint(samplePointIndex, position, normal, uv);
                samplePointIndex++;
            }
            return changed;
        }
        public SurfaceMeshPipeGenerator() { } //06003d50: .2/8/1 and three collections precede genuine base constructor.
    }
}
