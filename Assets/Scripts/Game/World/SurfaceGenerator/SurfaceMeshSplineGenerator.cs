using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000aa9; complete abstract owner, 31 concrete methods and three abstract contracts.
    // Native-derived source candidate. Exceptional numerical/native pointer behavior is not universal CLR parity.
    [RequireComponent(typeof(MeshFilter))]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SurfaceMeshSplineGenerator : TimeScaledComponent_SDT
    {
        [Tooltip("Mesh filter to assign generated mesh to."), SerializeField] protected MeshFilter m_meshFilter;
        [Tooltip("Determines whether the mesh can be generated at runtime."), SerializeField] private bool m_updateAtRuntime;
        [Tooltip("The distance to offset the mesh along each knot's normal."), SerializeField] protected float m_meshNormalOffset;
        [SerializeField, Tooltip("The distance to step along each spline when finding closest mesh points.")] private float m_splineStepDistance = 0.1f;
        [Tooltip("The distance to step along each axis for mesh sample points."), SerializeField] protected float m_meshStepDistance = 1f;
        [SerializeField, Tooltip("Material to assign to the mesh.")] private Material m_material;
        [SerializeField, Tooltip("Duplicate all triangles with inverted normals so the mesh can be seen both from the outside and the inside.")] private bool m_makeDoubleSided;
        private readonly List<ISpline> m_splines = new List<ISpline>();
        private readonly List<Tracker2D> m_trackers = new List<Tracker2D>();
        private bool m_isDirty;
        private bool m_wasDirty;
        private readonly List<Vector3> m_meshVertices = new List<Vector3>();
        private readonly List<Vector3> m_meshNormals = new List<Vector3>();
        private readonly List<Vector2> m_meshUVs = new List<Vector2>();
        private readonly List<int> m_meshTriangles = new List<int>();
        private int m_meshInstanceId;
        private Mesh m_sharedMesh;
        private readonly SystemRef<InstancedObjectPoolManager> m_poolManagerRef = ProcessManager.GetSystemRef<InstancedObjectPoolManager>(null, true);

        // 06003d5b..5e. SharedMesh reads the filter; the retained m_sharedMesh is the prior pooled mesh.
        public Mesh SharedMesh => m_meshFilter.sharedMesh;
        protected abstract void InitialiseSplineTrackers();
        protected abstract void ApplyTrackerToMesh();
        protected abstract bool UpdateSamplePoints(bool forceGenerate);
        protected override TimeCategory GetDefaultTimeCategoryEnum() => TimeCategory.GameplayUnscaled; //06003d5f, 90ae2b46
        protected Tracker2D GetTracker(int i) => m_trackers[i]; //06003d60

        //06003d61. Vertex read precedes pool subscriptions and the original virtual Reset call.
        protected override void Awake()
        {
            base.Awake();
            if (m_meshFilter == null) return;
            m_meshFilter.sharedMesh.GetVertices(m_meshVertices);
            m_poolManagerRef.InvokeOnValid(RegisterMesh);
            m_poolManagerRef.OnSystemStartup += RegisterMesh;
            m_poolManagerRef.OnSystemShutdown += UnregisterMesh;
            Reset();
        }
        //06003d62. Original teardown removes startup before shutdown, after release and identity reset.
        public override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseMesh();
            m_meshInstanceId = 0;
            m_poolManagerRef.OnSystemStartup -= RegisterMesh;
            m_poolManagerRef.OnSystemShutdown -= UnregisterMesh;
        }
        private void RegisterMesh(InstancedObjectPoolManager poolManager) { m_meshInstanceId = poolManager.RegisterMesh(m_meshFilter.sharedMesh); } //06003d63
        private void UnregisterMesh(InstancedObjectPoolManager poolManager) { ReleaseMesh(); m_meshInstanceId = 0; } //06003d64
        private void AcquireMesh() //06003d65
        {
            if (m_sharedMesh != null) return;
            if (m_poolManagerRef.Get().GetMesh(m_meshInstanceId, out Mesh mesh))
            {
                m_sharedMesh = m_meshFilter.sharedMesh;
                m_meshFilter.sharedMesh = mesh;
            }
        }
        private void ReleaseMesh() //06003d66
        {
            if (m_sharedMesh == null) return;
            m_poolManagerRef.Get().ReleaseMesh(m_meshInstanceId, m_meshFilter.sharedMesh);
            m_meshFilter.sharedMesh = m_sharedMesh;
            m_sharedMesh = null;
        }
        protected void AddSpline(Spline splineAxis) //06003d67; constructor and collection/fault order retained.
        {
            Tracker2D tracker = new Tracker2D(new CollisionResolver2D());
            ISpline spline = splineAxis.GetSpline();
            List<ISurface> surfaces = new List<ISurface> { spline };
            Dictionary<ISurface, ISurface[]> world = new Dictionary<ISurface, ISurface[]> { { spline, new ISurface[0] } };
            tracker.SetWorldInformation(surfaces, world);
            tracker.MoveLocal(Vector3.back * spline.Length);
            m_trackers.Add(tracker);
            m_splines.Add(spline);
        }
        protected override void OnEnable() //06003d68
        {
            base.OnEnable();
            if (!m_updateAtRuntime) return;
            foreach (ISpline spline in m_splines) spline.OnSplineKnotsUpdated += OnKnotsUpdated;
        }
        protected override void OnDisable() //06003d69
        {
            base.OnDisable();
            if (!m_updateAtRuntime) return;
            foreach (ISpline spline in m_splines) spline.OnSplineKnotsUpdated -= OnKnotsUpdated;
        }
        protected override void OnValidate() //06003d6a; no runtime-update flag check in this original resubscription.
        {
            base.OnValidate();
            if (!gameObject.CanValidate(false)) return;
            foreach (ISpline spline in m_splines)
            {
                spline.OnSplineKnotsUpdated -= OnKnotsUpdated;
                spline.OnSplineKnotsUpdated += OnKnotsUpdated;
            }
        }
        protected override void InternalUpdate(float deltaTime) //06003d6b; one clean frame delays release.
        {
            if (m_isDirty)
            {
                AcquireMesh();
                ApplyTrackerToMesh();
                m_isDirty = false;
                m_wasDirty = true;
                return;
            }
            if (!m_wasDirty && m_sharedMesh != null) ReleaseMesh();
            m_wasDirty = false;
        }
        private void OnKnotsUpdated() { m_isDirty = true; } //06003d6c
        protected Vector3 GetVertex(int index) => m_meshVertices[index]; //06003d6d
        protected void SetVertex(int index, Vector3 value) { m_meshVertices[index] = value; } //06003d6e
        protected void ApplyVertices() { m_meshFilter.sharedMesh.SetVertices(m_meshVertices); } //06003d6f

        //06003d70. Keep the original compulsory first step, accepted-position capture, edge transition and backstep.
        // Zero/negative step, non-finite inputs and native unchecked pointers remain exceptional behavior holds.
        protected Vector3 FindNearestSplineLocation(Vector3 vertex, Tracker2D trackerAxis, Vector3 alignmentAxis)
        {
            float threshold = m_splineStepDistance * 2f;
            Vector3 nearest = trackerAxis.Location.m_worldPosition;
            float distance = Mathf.Abs(Vector3.Dot(nearest - vertex, alignmentAxis));
            bool close = distance < threshold;
            bool atEnd = trackerAxis.Location.m_positionBoundsInfo.AtEndEdge;
            while (true)
            {
                trackerAxis.MoveLocal(Vector3.forward * m_splineStepDistance);
                Vector3 next = trackerAxis.Location.m_worldPosition;
                float nextDistance = Mathf.Abs(Vector3.Dot(next - vertex, alignmentAxis));
                if (!(nextDistance < distance) && close) break;
                close = (nextDistance < distance && close) || nextDistance < threshold;
                bool nextAtEnd = trackerAxis.Location.m_positionBoundsInfo.AtEndEdge;
                nearest = next;
                distance = nextDistance;
                if (!atEnd && nextAtEnd) break;
                atEnd = nextAtEnd;
            }
            trackerAxis.MoveLocal(Vector3.forward * -m_splineStepDistance);
            return nearest;
        }
        protected override void Reset() //06003d71; reads a captured mesh and preserves existing nonempty data.
        {
            base.Reset();
            TryGetComponent(out m_meshFilter);
            if (m_meshFilter == null) return;
            Mesh mesh = m_meshFilter.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0) { GenerateMesh(false); return; }
            mesh.GetVertices(m_meshVertices);
            mesh.GetNormals(m_meshNormals);
            mesh.GetTriangles(m_meshTriangles, 0);
            mesh.GetUVs(0, m_meshUVs);
        }
        public void GenerateMesh(bool forceGenerate = false) //06003d72
        {
            if (!UpdateSamplePoints(forceGenerate) && m_meshVertices.Count == 0) return;
            Mesh mesh = m_meshFilter.sharedMesh;
            if (mesh == null || forceGenerate)
            {
                mesh = new Mesh();
                mesh.name = name;
                m_meshFilter.sharedMesh = mesh;
            }
            mesh.Clear();
            mesh.SetVertices(m_meshVertices);
            mesh.SetNormals(m_meshNormals);
            mesh.SetUVs(0, m_meshUVs);
            mesh.SetTriangles(m_meshTriangles, 0);
            if (m_makeDoubleSided) MakeMeshDoubleSided();
            gameObject.GetOrAddComponent<MeshRenderer>().materials = new Material[] { m_material };
        }
        protected virtual void SetMeshData(List<LightweightTransform> samplePoints, Material material) //06003d73; samplePoints unused.
        {
            m_meshFilter = gameObject.GetOrAddComponent<MeshFilter>();
            m_material = material;
        }
        protected bool ClampSamplePoints(int sampleCount) //06003d74; each list is checked and trimmed independently.
        {
            bool changed = false;
            if (m_meshVertices.Count > sampleCount) { m_meshVertices.RemoveRange(sampleCount, m_meshVertices.Count - sampleCount); changed = true; }
            if (m_meshNormals.Count > sampleCount) { m_meshNormals.RemoveRange(sampleCount, m_meshNormals.Count - sampleCount); changed = true; }
            if (m_meshUVs.Count > sampleCount) { m_meshUVs.RemoveRange(sampleCount, m_meshUVs.Count - sampleCount); changed = true; }
            return changed;
        }
        private static bool SetSampleValue<T>(int index, List<T> m_list, T value) where T : IEquatable<T> //06003d75; all three physical AOT contexts retained.
        {
            if (index < m_list.Count)
            {
                if (m_list[index].Equals(value)) return false;
                m_list[index] = value;
            }
            else m_list.Add(value); // Original appends one element even when index skips beyond Count.
            return true;
        }
        protected bool SetSamplePoint(int samplePointIndex, Vector3 position, Vector3 normal, Vector2 uvCoord) //06003d76; all three calls execute.
        {
            bool positionChanged = SetSampleValue(samplePointIndex, m_meshVertices, position);
            bool normalChanged = SetSampleValue(samplePointIndex, m_meshNormals, normal);
            bool uvChanged = SetSampleValue(samplePointIndex, m_meshUVs, uvCoord);
            return positionChanged | normalChanged | uvChanged;
        }
        protected void MeshTrianglesClear() { m_meshTriangles.Clear(); } //06003d77
        protected void MeshTrianglesAddQuad(int i0, int i1, int i2, int i3) //06003d78
        { m_meshTriangles.Add(i0); m_meshTriangles.Add(i1); m_meshTriangles.Add(i2); m_meshTriangles.Add(i3); m_meshTriangles.Add(i2); m_meshTriangles.Add(i1); }
        protected void MeshTrianglesAddQuad(int index, int stride) { MeshTrianglesAddQuad(index, index + 1, index + stride, index + 1 + stride); } //06003d79
        protected void MeshTrianglesAddCircle(int segmentIndex, int segmentCount) //06003d7a; original six-index order differs from AddQuad.
        {
            for (int i = 0; i < segmentCount; i++)
            {
                int next = i == segmentCount - 1 ? 0 : i + 1;
                int currentBase = segmentIndex * segmentCount;
                int nextBase = (segmentIndex + 1) * segmentCount;
                m_meshTriangles.Add(currentBase + next);
                m_meshTriangles.Add(nextBase + i);
                m_meshTriangles.Add(currentBase + i);
                m_meshTriangles.Add(currentBase + next);
                m_meshTriangles.Add(nextBase + next);
                m_meshTriangles.Add(nextBase + i);
            }
        }
        private void MakeMeshDoubleSided() //06003d7b; retain repeated triangle property reads and only reverse winding.
        {
            Mesh mesh = m_meshFilter.sharedMesh;
            int count = mesh.triangles.Length;
            List<int> triangles = new List<int>(mesh.triangles);
            for (int i = 0; i < count; i += 3)
            {
                int i0 = mesh.triangles[i];
                int i1 = mesh.triangles[i + 1];
                int i2 = mesh.triangles[i + 2];
                triangles.Add(i2); triangles.Add(i1); triangles.Add(i0);
            }
            m_meshFilter.sharedMesh.SetTriangles(triangles, 0);
        }
        protected SurfaceMeshSplineGenerator() { } //06003d7c; .1/1 defaults, six collections and original SystemRef precede base.
    }
}
