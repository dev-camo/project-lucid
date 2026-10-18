using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.SurfaceGenerator (TypeDef 0x02000aa0), SurfaceMeshCrossSectionGenerator
    // (0x02000aa2), SurfaceMeshPrefabSpawner (0x02000aa5) and SurfaceMeshPlaneGenerator
    // (0x02000aa8), x86-64 0x6D31B0..0x6D9A60. Plane cases run on owned, inactive objects whose
    // axis splines are genuine InertSplines with an authored length; the base generator's field
    // initializer takes the typed InstancedObjectPoolManager reference, so those cases lease that
    // row, and an owned empty TimeCategoryLookup stands in the TimeScaledComponent_SDT cache read
    // by the Editor's Reset/OnValidate while the component is added.
    // Not covered: SurfaceMeshPlaneGenerator.UpdateSamplePoints past its null-spline returns and
    // ApplyTrackerToMesh. Both read GetWorldTransformFromDistance or Tracker2D lookups on the axis
    // splines, which need baked knot and LUT data whose results these cases cannot derive from
    // the native evidence alone.
    public sealed class OriginalSurfaceMeshGeneratorTests
    {
        // Records each distance query and answers with a location on itself, so that
        // RibbonLocation.GetLocalTransform has a ribbon to call back into.
        private sealed class DistanceRibbon : IRibbon
        {
            public readonly List<(float distance, RibbonAlignment alignment)> Queries = new List<(float, RibbonAlignment)>();
            public int LocalTransforms;

            public RibbonLocation GetRibbonLocationFromDistance(float distance, RibbonAlignment alignment)
            {
                Queries.Add((distance, alignment));
                return new RibbonLocation { m_ribbon = this, m_alignment = alignment };
            }

            public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment)
            {
                LocalTransforms++;
                return new LightweightTransform(Vector3.zero, Quaternion.identity);
            }

            public DateTime GetTimestamp() => throw new NotSupportedException();
            public MetadataGroups GetSurfaceMetadata() => throw new NotSupportedException();
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw new NotSupportedException();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw new NotSupportedException();
            public float Length => throw new NotSupportedException();
            public float LengthInverse => throw new NotSupportedException();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw new NotSupportedException();
            public Vector3 TransformPoint(Vector3 point) => throw new NotSupportedException();
            public Vector3 InverseTransformPoint(Vector3 point) => throw new NotSupportedException();
            public bool IsActive() => throw new NotSupportedException();
            public int KnotCount => throw new NotSupportedException();
            public IRibbonKnot GetKnot(int knotIndex, RibbonAlignment alignment) => throw new NotSupportedException();
            public LightweightTransform Transform => throw new NotSupportedException();
            public IRibbonKnot GetBackKnot(RibbonAlignment alignment) => throw new NotSupportedException();
            public IRibbonKnot GetHeadKnot(RibbonAlignment alignment) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => throw new NotSupportedException();
            public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => throw new NotSupportedException();
            public RibbonLocation FindNearestRibbonLocation(Vector3 worldPosition, RibbonAlignment alignment) => throw new NotSupportedException();
            public RibbonLocation FindAdjacentRibbonLocation(Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment) => throw new NotSupportedException();
            public LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public Vector3 GetLocalPosFromKnotT(KnotT knotT, RibbonAlignment alignment) => throw new NotSupportedException();
            public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment) => throw new NotSupportedException();
            public LightweightTransform GetWorldTransformFromDistance(float distance) => throw new NotSupportedException();
            public LightweightTransform GetWorldTransformFromRibbonT(float ribbonT) => throw new NotSupportedException();
            public void KnotsUpdated() => throw new NotSupportedException();
            public void UpdateRibbonType(IRibbon newRibbon) => throw new NotSupportedException();
            public bool TryGetRaycastHit(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public event Action OnRibbonKnotsUpdated { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
            public event Action<IRibbon> OnRibbonTypeChanged { add => throw new NotSupportedException(); remove => throw new NotSupportedException(); }
        }

        private sealed class CrossSectionProbe : SurfaceMeshCrossSectionGenerator
        {
            public readonly List<float> Distances = new List<float>();
            public override SurfaceCrossSectionType Type => default;
            public override Vector3 GetOrigin(KnotStepData knotStepData, float offsetMultiplier) => throw new NotSupportedException();
            public override void GenerateMeshCrossSection(List<Vector3> vertices, List<int> triangles, KnotStepData knotStepData,
                int knotStepIndex, int knotStepCount, bool invertOrder, float offsetMultiplier) => throw new NotSupportedException();

            protected override void CalculateLocationLocal(float distance, out Vector3 positionLocal, out Quaternion rotationLocal)
            {
                Distances.Add(distance);
                positionLocal = Vector3.zero;
                rotationLocal = Quaternion.identity;
            }

            public List<int> Quad(int v0, int vStride, bool invertOrder)
            {
                var triangles = new List<int>();
                AddQuad(v0, vStride, invertOrder, triangles);
                return triangles;
            }
        }

        private sealed class PlaneFixture : IDisposable
        {
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-surface-plane");
            private readonly VerificationFixtureOwnership.LifecycleRow m_poolRow;
            private readonly object m_priorConfig;
            public readonly SurfaceMeshPlaneGenerator Plane;

            public PlaneFixture(float lengthAxisX, float lengthAxisZ, float meshStepDistance)
            {
                m_poolRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<InstancedObjectPoolManager>());
                m_priorConfig = GetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal");
                try
                {
                    SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", Objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                    GameObject host = Objects.Create("plane");
                    host.SetActive(false);
                    Plane = host.AddComponent<SurfaceMeshPlaneGenerator>();
                    Set(Plane, "m_splineAxisX", AxisSpline("x", lengthAxisX));
                    Set(Plane, "m_splineAxisZ", AxisSpline("z", lengthAxisZ));
                    Set(Plane, "m_meshStepDistance", meshStepDistance);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private Spline AxisSpline(string name, float length)
            {
                GameObject splineObject = Objects.Create(name);
                splineObject.SetActive(false);
                var inert = splineObject.AddComponent<InertSpline>();
                Set(inert, "m_length", length);
                var facade = splineObject.AddComponent<Spline>();
                Set(facade, "m_inertSpline", inert);
                Set(facade, "m_activeSplineType", SplineVariantType.Inert);
                return facade;
            }

            public List<Vector3> Vertices => (List<Vector3>)Get(Plane, "m_meshVertices");
            public List<Vector3> Normals => (List<Vector3>)Get(Plane, "m_meshNormals");
            public List<Vector2> UVs => (List<Vector2>)Get(Plane, "m_meshUVs");
            public List<int> Triangles => (List<int>)Get(Plane, "m_meshTriangles");

            // Runs one of the private generation steps from index 0 and returns its result.
            public bool Generate(string method, params object[] arguments)
            {
                var args = new object[arguments.Length + 2];
                args[0] = 0;
                args[1] = new LightweightTransform(Vector3.zero, Quaternion.identity);
                Array.Copy(arguments, 0, args, 2, arguments.Length);
                bool changed = (bool)Call(Plane, method, args);
                LastIndex = (int)args[0];
                return changed;
            }

            public int LastIndex { get; private set; }

            public void Dispose()
            {
                try { Objects.Dispose(); }
                finally
                {
                    try { SetStatic(typeof(TimeScaledComponent_SDT), "s_config_Internal", m_priorConfig); }
                    finally { m_poolRow.Dispose(); }
                }
            }
        }

        private static void AreClose(IEnumerable<Vector3> expected, IEnumerable<Vector3> actual, string label)
        {
            Vector3[] want = expected.ToArray(), have = actual.ToArray();
            Assert.That(have.Length, Is.EqualTo(want.Length), label + " count");
            for (int i = 0; i < want.Length; i++) WorldMemberAccess.AreClose(want[i], have[i], label + " " + i);
        }

        private static void AreClose(IEnumerable<Vector2> expected, IEnumerable<Vector2> actual, string label) =>
            AreClose(expected.Select(v => (Vector3)v), actual.Select(v => (Vector3)v), label);

        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(SurfaceGenerator).Attributes, Is.EqualTo(0x00100081));
            Assert.That(Fields(typeof(SurfaceGenerator)), Is.EqualTo(new[]
                { "m_ribbon:Ribbon:0004", "m_spline:Spline:0004", "m_surfacePaths:List`1:0001" }));
            Assert.That(Methods(typeof(SurfaceGenerator)), Is.EqualTo(new[] { ".ctor:1884" }));
            Assert.That(AttributeRow(typeof(SurfaceGenerator).GetField("m_surfacePaths", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("SerializeReference() FormerlySerializedAsAttribute(m_ribbonPaths) TooltipAttribute(Paths to generate knots from.)"));

            Assert.That((int)typeof(SurfaceMeshCrossSectionGenerator).Attributes, Is.EqualTo(0x00102081));
            Assert.That((int)typeof(SurfaceMeshCrossSectionGenerator.KnotStepData).Attributes, Is.EqualTo(0x00100002));
            Assert.That(Fields(typeof(SurfaceMeshCrossSectionGenerator.KnotStepData)), Is.EqualTo(new[] { "m_origin:Vector3:0006", "m_rotation:Quaternion:0006" }));
            Assert.That(Fields(typeof(SurfaceMeshCrossSectionGenerator)), Is.EqualTo(new[]
            {
                "m_width:Single:0001", "m_offsetLocal:Vector3:0001", "m_prefabSpawnGenerate:Boolean:0001",
                "m_prefabSpawnRoot:Transform:0001", "m_prefabSpawners:List`1:0001"
            }));
            Assert.That(Methods(typeof(SurfaceMeshCrossSectionGenerator)), Is.EqualTo(new[]
            {
                "get_Width:0884", "get_OffsetLocal:0884", "get_Type:0dc6", "GetOrigin:05c6", "GenerateMeshCrossSection:05c6",
                "CalculateLocationLocal:05c4", "AddQuad:0084", "SpawnPrefabs:0086", ".ctor:1884"
            }));
            ParameterInfo offsetMultiplier = typeof(SurfaceMeshCrossSectionGenerator).GetMethod("GenerateMeshCrossSection").GetParameters()[6];
            Assert.That(offsetMultiplier.HasDefaultValue && (float)offsetMultiplier.DefaultValue == 0f, Is.True);
            // The shipped override rows carry no Optional/HasDefault for offsetMultiplier (06003d2e, 06003d3f).
            Assert.That(typeof(SurfaceMeshCrossSectionPlaneGenerator).GetMethod("GenerateMeshCrossSection").GetParameters()[6].HasDefaultValue, Is.False);
            Assert.That(typeof(SurfaceMeshCrossSectionEllipseGenerator).GetMethod("GenerateMeshCrossSection").GetParameters()[6].HasDefaultValue, Is.False);
            ParameterInfo knotOrigin = typeof(SurfaceMeshCrossSectionGenerator).GetMethod("SpawnPrefabs").GetParameters()[1];
            Assert.That(knotOrigin.HasDefaultValue && knotOrigin.DefaultValue == null, Is.True);

            Assert.That((int)typeof(SurfaceMeshPrefabSpawner).Attributes, Is.EqualTo(0x00102001));
            Assert.That(Fields(typeof(SurfaceMeshPrefabSpawner)), Is.EqualTo(new[]
            {
                "m_prefab:GameObject:0001", "m_spacing:Single:0001", "m_distance:Single:0001", "m_offsetLocal:Vector3:0001", "m_scale:Single:0001"
            }));
            Assert.That(Methods(typeof(SurfaceMeshPrefabSpawner)), Is.EqualTo(new[]
                { "get_Spacing:0886", "get_Distance:0886", "get_OffsetLocal:0886", "Spawn:0086", "InstantiatePrefab:0096", ".ctor:1886" }));

            Assert.That((int)typeof(SurfaceMeshPlaneGenerator).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(SurfaceMeshPlaneGenerator).BaseType, Is.EqualTo(typeof(SurfaceMeshSplineGenerator)));
            Assert.That(Fields(typeof(SurfaceMeshPlaneGenerator)), Is.EqualTo(new[]
                { "m_splineAxisX:Spline:0001", "m_splineAxisZ:Spline:0001", "m_skirtHeight:Single:0001" }));
            Assert.That(Methods(typeof(SurfaceMeshPlaneGenerator)), Is.EqualTo(new[]
            {
                "InitialiseSplineTrackers:00c4", "ApplyTrackerToMesh:00c4", "UpdateSamplePoints:00c4", "GenerateSamplePoints:0081",
                "GenerateSamplePoint:0081", "GenerateSkirtPoints:0081", "GenerateSkirtAxisXPoints:0081", "GenerateSkirtAxisZPoints:0081",
                "GenerateSkirtPoint:0081", ".ctor:1886"
            }));
        }

        // SurfaceGenerator..ctor 0x6D31B0 stores a new List<SurfacePathGenerator> before the
        // MonoBehaviour constructor.
        [Test]
        public void SurfaceGeneratorStartsWithAnEmptyPathList()
        {
            using (var objects = new OwnedWorldObjects("lucid-surface-generator"))
            {
                GameObject host = objects.Create("generator");
                host.SetActive(false);
                var generator = host.AddComponent<SurfaceGeneratorProbe>();
                Assert.That(Get(generator, "m_surfacePaths"), Is.InstanceOf<List<SurfacePathGenerator>>().And.Empty);
            }
        }

        // AddQuad 0x6D37A0 with v1 = v0 + 1, v2 = v0 + vStride, v3 = v2 + 1: inverted adds
        // v2, v1, v0, v1, v2, v3 (0x6D381D onwards), otherwise v0, v1, v2, v3, v2, v1.
        [Test]
        public void AddQuadKeepsBothNativeTriangleOrders()
        {
            var probe = new CrossSectionProbe();
            Assert.That(probe.Quad(4, 10, false), Is.EqualTo(new[] { 4, 5, 14, 15, 14, 5 }));
            Assert.That(probe.Quad(4, 10, true), Is.EqualTo(new[] { 14, 5, 4, 5, 14, 15 }));
        }

        // SpawnPrefabs 0x6D3F80: nothing without the request flag (0x6D405F) or a spawn root
        // (op_Equality 0x6D408D); otherwise the root's children are destroyed from the last
        // (0x6D40D3), each spawner with Spacing == 0 (ucomiss/jnp at 0x6D41CB) is skipped, the loop
        // runs while distance <= Distance + OffsetLocal.z from OffsetLocal.z (jb at 0x6D41F3) and the
        // flag is cleared only after every spawner (0x6D45DE).
        [Test]
        public void SpawnPrefabsClearsTheRootAndSkipsOnlyZeroSpacingOrAnEmptyRange()
        {
            using (var objects = new OwnedWorldObjects("lucid-surface-spawn"))
            {
                var probe = new CrossSectionProbe();
                Transform root = objects.Create("root").transform;
                for (int i = 0; i < 3; i++) objects.Create("child " + i, Vector3.zero, Quaternion.identity, root);

                probe.SpawnPrefabs(null);
                Assert.That(root.childCount, Is.EqualTo(3), "no request, no work");

                Set(probe, "m_prefabSpawnGenerate", true);
                probe.SpawnPrefabs(null);
                Assert.That(Get(probe, "m_prefabSpawnGenerate"), Is.True, "no root keeps the request");

                Set(probe, "m_prefabSpawnRoot", root);
                var zeroSpacing = new SurfaceMeshPrefabSpawner();
                Set(zeroSpacing, "m_spacing", 0f);
                var emptyRange = new SurfaceMeshPrefabSpawner();
                Set(emptyRange, "m_distance", -1f);
                var unordered = new SurfaceMeshPrefabSpawner();
                Set(unordered, "m_offsetLocal", new Vector3(0f, 0f, float.NaN));
                Set(probe, "m_prefabSpawners", new List<SurfaceMeshPrefabSpawner> { zeroSpacing, emptyRange, unordered });
                probe.SpawnPrefabs(null);
                Assert.That(root.childCount, Is.Zero, "every child is destroyed");
                Assert.That(probe.Distances, Is.Empty, "no spawner entered its range");
                Assert.That(Get(probe, "m_prefabSpawnGenerate"), Is.False);

                // A negative spacing is not skipped: the range [0, 0] is entered and the null ribbon
                // faults before CalculateLocationLocal, so the request is kept.
                var negativeSpacing = new SurfaceMeshPrefabSpawner();
                Set(negativeSpacing, "m_spacing", -1f);
                Set(probe, "m_prefabSpawners", new List<SurfaceMeshPrefabSpawner> { negativeSpacing });
                Set(probe, "m_prefabSpawnGenerate", true);
                Assert.Throws<NullReferenceException>(() => probe.SpawnPrefabs(null));
                Assert.That(probe.Distances, Is.Empty);
                Assert.That(Get(probe, "m_prefabSpawnGenerate"), Is.True);
            }
        }

        // SpawnPrefabs 0x6D3F80 inside the range: the ribbon is queried at the loop distance, which
        // starts at OffsetLocal.z ([rbp-0x2c]), with RibbonAlignment.Center (edx = 0 at 0x6D428A);
        // RibbonLocation.GetLocalTransform follows (0x6D4302); CalculateLocationLocal (vcall at
        // 0x6D4360) gets OffsetLocal.x (xmm0 = [rbp-0xb0] at 0x6D434E), not the distance. Spawn's
        // inlined null InstantiatePrefab then faults, so the request flag is kept.
        [Test]
        public void SpawnPrefabsQueriesTheRibbonAtTheDistanceAndTheCrossSectionAtOffsetX()
        {
            using (var objects = new OwnedWorldObjects("lucid-surface-spawn-ribbon"))
            {
                var probe = new CrossSectionProbe();
                var ribbon = new DistanceRibbon();
                var spawner = new SurfaceMeshPrefabSpawner();
                Set(spawner, "m_offsetLocal", new Vector3(0.25f, 0f, 0.5f));
                Set(probe, "m_prefabSpawnRoot", objects.Create("root").transform);
                Set(probe, "m_prefabSpawners", new List<SurfaceMeshPrefabSpawner> { spawner });
                Set(probe, "m_prefabSpawnGenerate", true);
                Assert.Throws<NullReferenceException>(() => probe.SpawnPrefabs(ribbon));
                Assert.That(ribbon.Queries, Is.EqualTo(new[] { (0.5f, RibbonAlignment.Center) }));
                Assert.That(ribbon.LocalTransforms, Is.EqualTo(1));
                Assert.That(probe.Distances, Is.EqualTo(new[] { 0.25f }));
                Assert.That(Get(probe, "m_prefabSpawnGenerate"), Is.True);
            }
        }

        // SurfaceMeshPrefabSpawner..ctor 0x6D4D20 stores 1.0 in m_spacing and m_scale only.
        // InstantiatePrefab 0x6D4D10 is `xor eax, eax; ret`, and Spawn 0x6D4660 inlines it, so the
        // first name access (get_name on null at 0x6D46A2) fails before any transform is set.
        // This pins the shipped player body only; it does not claim the original editor build
        // behaved this way, since an editor-only branch would not survive in the player binary.
        [Test]
        public void PrefabSpawnerInstantiatesNothingInThePlayerBody()
        {
            using (var objects = new OwnedWorldObjects("lucid-surface-prefab"))
            {
                var spawner = new SurfaceMeshPrefabSpawner();
                Assert.That(spawner.Spacing, Is.EqualTo(1f));
                Assert.That(Get(spawner, "m_scale"), Is.EqualTo(1f));
                Assert.That(spawner.Distance, Is.Zero);
                Assert.That(spawner.OffsetLocal, Is.EqualTo(Vector3.zero));
                Set(spawner, "m_offsetLocal", new Vector3(1f, 2f, 3f));
                Assert.That(spawner.OffsetLocal, Is.EqualTo(new Vector3(1f, 2f, 3f)));

                GameObject prefab = objects.Create("prefab");
                Transform root = objects.Create("root").transform;
                Assert.That(SurfaceMeshPrefabSpawner.InstantiatePrefab(prefab, root), Is.Null);
                Set(spawner, "m_prefab", prefab);
                Assert.Throws<NullReferenceException>(() => spawner.Spawn(Vector3.one, Quaternion.identity, "label", root));
                Assert.That(root.childCount, Is.Zero);
                Assert.That(prefab.name, Is.EqualTo("lucid-surface-prefab-prefab"));
            }
        }

        // GenerateSamplePoints 0x6D8920: rows along x, columns along z, each distance advancing by
        // Min(distance + m_meshStepDistance, length) (minss 0x6D8AAC/0x6D8B29). GenerateSamplePoint
        // 0x6D8F60 moves the location by right * dX + forward * dZ of this transform, stores it in
        // local space with the location's up as normal and uv (dX / lengthX, dZ / lengthZ). Quads
        // are added except on the last row and column, at x + z * (strideAxisZ + 1) with stride
        // strideAxisZ + 1 (0x6D8BC4), through the six-index MeshTrianglesAddQuad 0x6D9840.
        [Test]
        public void SamplePointsCoverTheGridAndQuadsUseTheNativeIndex()
        {
            using (var fixture = new PlaneFixture(1f, 0.75f, 0.75f))
            {
                Assert.That(fixture.Generate("GenerateSamplePoints", 2, 1), Is.True);
                Assert.That(fixture.LastIndex, Is.EqualTo(6));
                AreClose(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.75f), new Vector3(0.75f, 0f, 0f),
                    new Vector3(0.75f, 0f, 0.75f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 0.75f)
                }, fixture.Vertices, "vertex");
                AreClose(Enumerable.Repeat(Vector3.up, 6), fixture.Normals, "normal");
                AreClose(new[]
                {
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.75f, 0f),
                    new Vector2(0.75f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f)
                }, fixture.UVs, "uv");
                Assert.That(fixture.Triangles, Is.EqualTo(new[] { 0, 1, 2, 3, 2, 1, 1, 2, 3, 4, 3, 2 }));

                Assert.That(fixture.Generate("GenerateSamplePoints", 2, 1), Is.False, "unchanged points report no change");
                Assert.That(fixture.Triangles, Has.Count.EqualTo(24), "quads are appended again without a clear");

                // The grid follows this transform's own right and forward, so turning it leaves the
                // local points in place (to rounding); fixed world axes would rotate them.
                fixture.Plane.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                fixture.Generate("GenerateSamplePoints", 2, 1);
                AreClose(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.75f), new Vector3(0.75f, 0f, 0f),
                    new Vector3(0.75f, 0f, 0.75f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 0.75f)
                }, fixture.Vertices, "turned vertex");
                Assert.That(fixture.Generate("GenerateSamplePoints", -1, 1), Is.False);
                Assert.That(fixture.LastIndex, Is.Zero, "a negative stride generates nothing");
            }
        }

        // GenerateSkirtAxisZPoints 0x6D9270: uv.x = 1 only for strideSkirtX > 0 (0x6D9362); every
        // point takes the uv left by the previous step (uv.y = distance / length after the Min at
        // 0x6D9397); quads (strideSkirtX + z, stride strideSkirtZ) skip the last point (0x6D9409).
        [TestCase(0, 0f)]
        [TestCase(3, 1f)]
        public void SkirtAlongZUsesThePreviousStepUv(int strideSkirtX, float uvX)
        {
            using (var fixture = new PlaneFixture(1f, 1.5f, 0.75f))
            {
                Assert.That(fixture.Generate("GenerateSkirtAxisZPoints", 2, strideSkirtX, 6), Is.True);
                Assert.That(fixture.LastIndex, Is.EqualTo(3));
                AreClose(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.75f), new Vector3(0f, 0f, 1.5f) }, fixture.Vertices, "vertex");
                AreClose(new[] { new Vector2(uvX, 0f), new Vector2(uvX, 0.5f), new Vector2(uvX, 1f) }, fixture.UVs, "uv");
                int s = strideSkirtX;
                Assert.That(fixture.Triangles, Is.EqualTo(new[] { s, s + 1, s + 6, s + 7, s + 6, s + 1, s + 1, s + 2, s + 7, s + 8, s + 7, s + 2 }));
            }
        }

        // GenerateSkirtAxisXPoints 0x6D9480: uv.y = 1 only for startSkirtX > 0 (0x6D956F); quads use
        // (startSkirtX + x * strideSkirtX, the next column, strideSkirtZ + strideSkirtX + x - 1, + 1)
        // (0x6D9581..0x6D963C) and skip the last point.
        [TestCase(0, 0f)]
        [TestCase(2, 1f)]
        public void SkirtAlongXUsesThePreviousStepUv(int startSkirtX, float uvY)
        {
            using (var fixture = new PlaneFixture(1f, 1f, 0.75f))
            {
                Assert.That(fixture.Generate("GenerateSkirtAxisXPoints", 2, 3, 10, startSkirtX), Is.True);
                AreClose(new[] { new Vector3(0f, 0f, 0f), new Vector3(0.75f, 0f, 0f), new Vector3(1f, 0f, 0f) }, fixture.Vertices, "vertex");
                AreClose(new[] { new Vector2(0f, uvY), new Vector2(0.75f, uvY), new Vector2(1f, uvY) }, fixture.UVs, "uv");
                int s = startSkirtX;
                Assert.That(fixture.Triangles, Is.EqualTo(new[] { s, s + 3, 12, 13, 12, s + 3, s + 3, s + 6, 13, 14, 13, s + 6 }));
            }
        }

        // GenerateSkirtPoints 0x6D8C20 runs the z edge, the z edge moved by right * lengthX, the x
        // edge after moving back, and the x edge moved by forward * lengthZ, with the shipped index
        // arguments (0, A * B), (A * strideAxisZ, 2B), (A, 3B + sz * sx, 0), (A, sz * sx + 4B, sx)
        // where A = strideAxisX + 1 and B = strideAxisZ + 1.
        [Test]
        public void SkirtPointsWalkTheFourEdgesWithTheShippedIndices()
        {
            using (var fixture = new PlaneFixture(1f, 1f, 1f))
            {
                Assert.That(fixture.Generate("GenerateSkirtPoints", 1, 1), Is.True);
                Assert.That(fixture.LastIndex, Is.EqualTo(8));
                AreClose(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f),
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f)
                }, fixture.Vertices, "vertex");
                AreClose(new[]
                {
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f)
                }, fixture.UVs, "uv");
                Assert.That(fixture.Triangles, Is.EqualTo(new[]
                {
                    0, 1, 4, 5, 4, 1,
                    2, 3, 6, 7, 6, 3,
                    0, 2, 8, 9, 8, 2,
                    1, 3, 10, 11, 10, 3
                }));
            }
        }

        // A square plane cannot tell strideAxisX from strideAxisZ in those arguments. With
        // strideAxisX = 2 and strideAxisZ = 1 (A = 3, B = 2) the native calls are Z(0, 6), Z(3, 4),
        // X(3, 8, 0) and X(3, 10, 2) (0x6D8DB9, 0x6D8E3F/0x6D8E46, 0x6D8EA4..0x6D8EB0 with
        // r9d = 0, 0x6D8F12); the second edge starts at A * strideAxisZ = 3, not B * strideAxisX = 4.
        [Test]
        public void SkirtPointsKeepTheShippedIndicesOnANonSquarePlane()
        {
            using (var fixture = new PlaneFixture(2f, 1f, 1f))
            {
                Assert.That(fixture.Generate("GenerateSkirtPoints", 2, 1), Is.True);
                Assert.That(fixture.LastIndex, Is.EqualTo(10));
                AreClose(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(2f, 0f, 0f), new Vector3(2f, 0f, 1f),
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f),
                    new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(2f, 0f, 1f)
                }, fixture.Vertices, "vertex");
                AreClose(new[]
                {
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f)
                }, fixture.UVs, "uv");
                Assert.That(fixture.Triangles, Is.EqualTo(new[]
                {
                    0, 1, 6, 7, 6, 1,
                    3, 4, 7, 8, 7, 4,
                    0, 3, 10, 11, 10, 3,
                    3, 6, 11, 12, 11, 6,
                    2, 5, 12, 13, 12, 5,
                    5, 8, 13, 14, 13, 8
                }));
            }
        }

        // UpdateSamplePoints 0x6D82A0 returns false while either axis spline is null (0x6D831E,
        // 0x6D834E), before MeshTrianglesClear, whatever forceGenerate is.
        [TestCase(true)]
        [TestCase(false)]
        public void UpdateSamplePointsNeedsBothAxisSplines(bool forceGenerate)
        {
            using (var fixture = new PlaneFixture(1f, 1f, 1f))
            {
                fixture.Triangles.Add(42);
                Set(fixture.Plane, "m_splineAxisZ", null);
                Assert.That(Call(fixture.Plane, "UpdateSamplePoints", forceGenerate), Is.False);
                Set(fixture.Plane, "m_splineAxisX", null);
                Assert.That(Call(fixture.Plane, "UpdateSamplePoints", forceGenerate), Is.False);
                Assert.That(fixture.Triangles, Is.EqualTo(new[] { 42 }));
            }
        }
    }
}
