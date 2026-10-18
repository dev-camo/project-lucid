using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa8; bodies translated from the 1.10.1 x86-64 binary.
    // A grid mesh spanned by two splines, with an optional skirt hanging from its four edges.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SurfaceMeshPlaneGenerator : SurfaceMeshSplineGenerator
    {
        [Tooltip("The spline we'll step along to generate the mesh on the x-axis.")]
        [SerializeField]
        private Spline m_splineAxisX;

        [SerializeField]
        [Tooltip("The spline we'll step along to generate the mesh on the z-axis.")]
        private Spline m_splineAxisZ;

        [Min(0f)]
        [SerializeField]
        [Tooltip("If set, adds a skirt around the edge of the plane.")]
        private float m_skirtHeight;

        protected override void InitialiseSplineTrackers()
        {
            AddSpline(m_splineAxisX);
            AddSpline(m_splineAxisZ);
        }

        // 06003d52 (0x6D75C0): each grid vertex keeps its local x and z and takes the mean height,
        // along this transform's up, of the nearest points on both splines. The x tracker rewinds
        // once and the z tracker once per row. Skirt vertices then copy the four edge rows,
        // lowered by the skirt height in local space.
        protected override void ApplyTrackerToMesh()
        {
            float lengthAxisX = m_splineAxisX.GetSpline().Length;
            float lengthAxisZ = m_splineAxisZ.GetSpline().Length;
            Tracker2D trackerAxisX = GetTracker(0);
            Tracker2D trackerAxisZ = GetTracker(1);
            Vector3 position = transform.position;
            Vector3 up = transform.up;
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            trackerAxisX.MoveLocal(Vector3.back * lengthAxisX);
            int strideAxisX = Mathf.CeilToInt(lengthAxisX / m_meshStepDistance);
            int strideAxisZ = Mathf.CeilToInt(lengthAxisZ / m_meshStepDistance);
            int vertexIndex = 0;
            for (int x = 0; x <= strideAxisX; x++)
            {
                trackerAxisZ.MoveLocal(Vector3.back * lengthAxisZ);
                for (int z = 0; z <= strideAxisZ; z++)
                {
                    Vector3 vertex = GetVertex(vertexIndex);
                    Vector3 vertexWorld = transform.TransformPoint(vertex);
                    Vector3 nearestAxisX = FindNearestSplineLocation(vertexWorld, trackerAxisX, right);
                    Vector3 nearestAxisZ = FindNearestSplineLocation(vertexWorld, trackerAxisZ, forward);
                    float heightAxisX = Vector3.Dot(nearestAxisX - position, up);
                    float heightAxisZ = Vector3.Dot(nearestAxisZ - position, up);
                    SetVertex(vertexIndex, new Vector3(vertex.x, (heightAxisX + heightAxisZ) * 0.5f + m_meshNormalOffset, vertex.z));
                    vertexIndex++;
                }
            }
            if (m_skirtHeight > 0f)
            {
                Vector3 skirtOffset = Vector3.up * m_skirtHeight;
                int strideSkirtX = strideAxisX + 1;
                for (int z = 0; z <= strideAxisZ; z++)
                    SetVertex(vertexIndex++, GetVertex(z) - skirtOffset);
                for (int z = 0; z <= strideAxisZ; z++)
                    SetVertex(vertexIndex++, GetVertex(strideSkirtX * strideAxisZ + z) - skirtOffset);
                for (int x = 0; x <= strideAxisX; x++)
                    SetVertex(vertexIndex++, GetVertex(x * strideSkirtX) - skirtOffset);
                for (int x = 0; x <= strideAxisX; x++)
                    SetVertex(vertexIndex++, GetVertex(strideAxisX + x * strideSkirtX) - skirtOffset);
            }
            ApplyVertices();
        }

        // 06003d53 (0x6D82A0): the grid starts where both splines start, projected onto this
        // transform's right and forward axes, and takes the x spline's start rotation. Every
        // step runs; the result is the trim, the force flag or any changed sample point.
        protected override bool UpdateSamplePoints(bool forceGenerate)
        {
            if (m_splineAxisX == null || m_splineAxisZ == null)
                return false;
            ISpline splineAxisX = m_splineAxisX.GetSpline();
            ISpline splineAxisZ = m_splineAxisZ.GetSpline();
            float lengthAxisX = splineAxisX.Length;
            float lengthAxisZ = splineAxisZ.Length;
            Vector3 startAxisX = splineAxisX.GetWorldTransformFromDistance(0f).Location;
            Vector3 startAxisZ = splineAxisZ.GetWorldTransformFromDistance(0f).Location;
            Vector3 position = transform.position;
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            float offsetAxisX = Vector3.Dot(startAxisX - position, right);
            float offsetAxisZ = Vector3.Dot(startAxisZ - position, forward);
            Quaternion rotation = splineAxisX.GetWorldTransformFromDistance(0f).Orientation;
            Vector3 origin = transform.TransformPoint(offsetAxisX, m_meshNormalOffset, offsetAxisZ);
            int strideAxisX = Mathf.CeilToInt(lengthAxisX / m_meshStepDistance);
            int strideAxisZ = Mathf.CeilToInt(lengthAxisZ / m_meshStepDistance);
            MeshTrianglesClear();
            int samplePointIndex = 0;
            bool changed = GenerateSamplePoints(ref samplePointIndex, new LightweightTransform(origin, rotation), strideAxisX, strideAxisZ);
            if (m_skirtHeight > 0f)
            {
                LightweightTransform skirtLocation = new LightweightTransform(origin - transform.up * m_skirtHeight, rotation);
                changed |= GenerateSkirtPoints(ref samplePointIndex, skirtLocation, strideAxisX, strideAxisZ);
            }
            return ClampSamplePoints(samplePointIndex) | forceGenerate | changed;
        }

        // 06003d54 (0x6D8920): points are generated row by row along x, but each quad starts at
        // x + z * (strideAxisZ + 1); the last row and column add no quads.
        private bool GenerateSamplePoints(ref int samplePointIndex, LightweightTransform location, int strideAxisX, int strideAxisZ)
        {
            float lengthAxisX = m_splineAxisX.GetSpline().Length;
            float lengthAxisZ = m_splineAxisZ.GetSpline().Length;
            bool changed = false;
            float distanceAxisX = 0f;
            for (int x = 0; x <= strideAxisX; x++)
            {
                float distanceAxisZ = 0f;
                for (int z = 0; z <= strideAxisZ; z++)
                {
                    changed |= GenerateSamplePoint(ref samplePointIndex, location, distanceAxisX, distanceAxisZ);
                    if (x < strideAxisX && z < strideAxisZ)
                        MeshTrianglesAddQuad(x + z * (strideAxisZ + 1), strideAxisZ + 1);
                    distanceAxisZ = Mathf.Min(distanceAxisZ + m_meshStepDistance, lengthAxisZ);
                }
                distanceAxisX = Mathf.Min(distanceAxisX + m_meshStepDistance, lengthAxisX);
            }
            return changed;
        }

        private bool GenerateSamplePoint(ref int samplePointIndex, LightweightTransform location, float distanceAxisX, float distanceAxisZ)
        {
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            float lengthAxisX = m_splineAxisX.GetSpline().Length;
            float lengthAxisZ = m_splineAxisZ.GetSpline().Length;
            location.Location += right * distanceAxisX + forward * distanceAxisZ;
            Vector3 position = transform.InverseTransformPoint(location.Location);
            Vector3 normal = transform.InverseTransformVector(location.Up);
            return SetSamplePoint(samplePointIndex++, position, normal, new Vector2(distanceAxisX / lengthAxisX, distanceAxisZ / lengthAxisZ));
        }

        // 06003d56 (0x6D8C20): the two z edges, then the two x edges; the far edges move the
        // location by the full spline length. The index arguments are the shipped arithmetic.
        private bool GenerateSkirtPoints(ref int samplePointIndex, LightweightTransform location, int strideAxisX, int strideAxisZ)
        {
            float lengthAxisX = m_splineAxisX.GetSpline().Length;
            float lengthAxisZ = m_splineAxisZ.GetSpline().Length;
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            int strideSkirtX = strideAxisX + 1;
            int strideSkirtZ = strideAxisZ + 1;
            bool changedStartAxisZ = GenerateSkirtAxisZPoints(ref samplePointIndex, location, strideAxisZ, 0, strideSkirtZ * strideSkirtX);
            location.Location += right * lengthAxisX;
            bool changedEndAxisZ = GenerateSkirtAxisZPoints(ref samplePointIndex, location, strideAxisZ, strideSkirtX * strideAxisZ, strideSkirtZ * 2);
            location.Location -= right * lengthAxisX;
            bool changedStartAxisX = GenerateSkirtAxisXPoints(ref samplePointIndex, location, strideAxisX, strideSkirtX,
                strideSkirtZ * 3 + strideAxisZ * strideAxisX, 0);
            location.Location += forward * lengthAxisZ;
            bool changedEndAxisX = GenerateSkirtAxisXPoints(ref samplePointIndex, location, strideAxisX, strideSkirtX,
                strideAxisZ * strideAxisX + strideSkirtZ * 4, strideAxisX);
            return changedStartAxisZ | changedEndAxisZ | changedStartAxisX | changedEndAxisX;
        }

        // 06003d57 (0x6D9480): the uv for each point is the one left by the previous step.
        private bool GenerateSkirtAxisXPoints(ref int samplePointIndex, LightweightTransform location, int strideAxisX,
            int strideSkirtX, int strideSkirtZ, int startSkirtX)
        {
            float lengthAxisX = m_splineAxisX.GetSpline().Length;
            bool changed = false;
            float distanceAxisX = 0f;
            Vector2 uvCoord = Vector2.zero;
            if (startSkirtX > 0)
                uvCoord.y = 1f;
            for (int x = 0; x <= strideAxisX; x++)
            {
                changed |= GenerateSkirtPoint(ref samplePointIndex, location, distanceAxisX, 0f, uvCoord);
                if (x < strideAxisX)
                {
                    int skirtIndex = strideSkirtZ + strideSkirtX + x;
                    MeshTrianglesAddQuad(startSkirtX + x * strideSkirtX, startSkirtX + (x + 1) * strideSkirtX, skirtIndex - 1, skirtIndex);
                }
                distanceAxisX = Mathf.Min(distanceAxisX + m_meshStepDistance, lengthAxisX);
                uvCoord.x = distanceAxisX / lengthAxisX;
            }
            return changed;
        }

        // 06003d58 (0x6D9270): the uv for each point is the one left by the previous step.
        private bool GenerateSkirtAxisZPoints(ref int samplePointIndex, LightweightTransform location, int strideAxisZ,
            int strideSkirtX, int strideSkirtZ)
        {
            float lengthAxisZ = m_splineAxisZ.GetSpline().Length;
            bool changed = false;
            float distanceAxisZ = 0f;
            Vector2 uvCoord = Vector2.zero;
            if (strideSkirtX > 0)
                uvCoord.x = 1f;
            for (int z = 0; z <= strideAxisZ; z++)
            {
                changed |= GenerateSkirtPoint(ref samplePointIndex, location, 0f, distanceAxisZ, uvCoord);
                if (z < strideAxisZ)
                    MeshTrianglesAddQuad(strideSkirtX + z, strideSkirtZ);
                distanceAxisZ = Mathf.Min(distanceAxisZ + m_meshStepDistance, lengthAxisZ);
                uvCoord.y = distanceAxisZ / lengthAxisZ;
            }
            return changed;
        }

        private bool GenerateSkirtPoint(ref int samplePointIndex, LightweightTransform location, float distanceAxisX,
            float distanceAxisZ, Vector2 uvCoord)
        {
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            location.Location += right * distanceAxisX + forward * distanceAxisZ;
            Vector3 position = transform.InverseTransformPoint(location.Location);
            Vector3 normal = transform.InverseTransformVector(location.Up);
            return SetSamplePoint(samplePointIndex++, position, normal, uvCoord);
        }
    }
}
