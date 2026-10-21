using System;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.PathGenerator on the Linear line (0,0,0) -> (10,0,0): two samples with forward (1,0,0), up (0,1,0),
    // size 1, white, at percents 0 and 1. The sample right vector is Cross(up, forward) = (0,0,-1), so the first vertex of a
    // slice row (slice percent 0) lies at +size/2 along it, i.e. at z = -0.5 for size 1. Expected values are hand arithmetic
    // from the native bodies cited per test (x86-64 addresses in the 0x4ac... / 0x4ad... range).
    public sealed class OriginalDreamteckPathGeneratorTests
    {
        private readonly DreamteckGeneratorFixture _fixture = new DreamteckGeneratorFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            DreamteckGeneratorFixture.Near(actual, expected, message);
        }

        // A path generator on the line, built once, with the line frame verified so the arithmetic below holds.
        private PathGenerator NewPath(SplineComputer computer = null)
        {
            computer = computer ?? _fixture.NewLine();
            PathGenerator path = _fixture.NewGenerator<PathGenerator>(computer);
            SplineSample first = new SplineSample();
            path.GetSample(0, ref first);
            Near(first.forward, Vector3.right, "line forward");
            Near(first.up, Vector3.up, "line up");
            Assert.That(path.sampleCount, Is.EqualTo(computer.pointCount));
            return path;
        }

        private static void Rebuild(PathGenerator path)
        {
            path.RebuildImmediate();
        }

        private static TS_Mesh Ts(MeshGenerator generator)
        {
            return DreamteckGeneratorFixture.TsMeshOf(generator);
        }

        // Field initialisers of the ctor 0x4ae6a0 (one slice, exposure 1) on top of the MeshGenerator ctor, get_meshName 0x4ad1f0.
        [Test]
        public void FreshPathGeneratorKeepsTheShippedDefaults()
        {
            PathGenerator path = _fixture.NewBare<PathGenerator>();
            Assert.That(path.slices, Is.EqualTo(1));
            Assert.That(path.shapeExposure, Is.EqualTo(1f));
            Assert.That(path.compensateCorners, Is.False);
            Assert.That(path.useShapeCurve, Is.False);
            Assert.That(path.shape, Is.Null);
            Assert.That(path.size, Is.EqualTo(1f), "MeshGenerator defaults");
            Assert.That(path.useSplineSize, Is.True);
            Assert.That(DreamteckGeneratorFixture.Invoke(path, "get_meshName"), Is.EqualTo("Path"));
            Assert.That(path.GetComponent<MeshFilter>() == null, Is.False, "RequireComponent adds the filter");
            Assert.That(path.GetComponent<MeshRenderer>() == null, Is.False, "and the renderer");
        }

        private static AnimationCurve Curve(params Keyframe[] keys)
        {
            return new AnimationCurve(keys);
        }

        // BuildMesh 0x4ad240: GenerateVertices, then the plane triangles of `slices` columns and sampleCount rows.
        // GenerateVertices 0x4ad2d0 sizes the arrays with AllocateMesh((slices + 1) * sampleCount, slices * (sampleCount - 1) * 6).
        [Test]
        public void SingleSliceRibbonFollowsTheLine()
        {
            PathGenerator path = NewPath();
            TS_Mesh ts = Ts(path);
            Assert.That(ts.vertices.Length, Is.EqualTo(4));
            Assert.That(ts.normals.Length, Is.EqualTo(4));
            Assert.That(ts.colors.Length, Is.EqualTo(4));
            Assert.That(ts.uv.Length, Is.EqualTo(4));
            Assert.That(ts.tangents.Length, Is.EqualTo(4), "the tangent array is allocated even though tangents are off");
            Assert.That(ts.triangles.Length, Is.EqualTo(6));
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)));

            Near(ts.vertices[0], new Vector3(0f, 0f, -0.5f), "first sample, +size/2 along right");
            Near(ts.vertices[1], new Vector3(0f, 0f, 0.5f), "first sample, -size/2");
            Near(ts.vertices[2], new Vector3(10f, 0f, -0.5f), "second sample");
            Near(ts.vertices[3], new Vector3(10f, 0f, 0.5f), "second sample");
            for (int i = 0; i < 4; i++)
            {
                Near(ts.normals[i], Vector3.up, "a single slice uses the sample up, normal " + i);
                DreamteckGeneratorFixture.Near(ts.colors[i], Color.white, "colour " + i);
            }

            // CalculateUVs(percent, 1 - slicePercent) in Clip mode, then the 180 degree turn about the centre (0.5, 0.5).
            DreamteckGeneratorFixture.Near(ts.uv[0], new Vector2(1f, 0f), "uv 0");
            DreamteckGeneratorFixture.Near(ts.uv[1], new Vector2(0f, 0f), "uv 1");
            DreamteckGeneratorFixture.Near(ts.uv[2], new Vector2(1f, 1f), "uv 2");
            DreamteckGeneratorFixture.Near(ts.uv[3], new Vector2(0f, 1f), "uv 3");
        }

        // Width: the sample size (useSplineSize) times the generator size; offset: right, up and forward scaled by the
        // sample size (0x4ad4e4 ff.); rotation turns the row about the sample forward and the single-slice normal with it.
        [Test]
        public void SizeOffsetAndRotationShapeTheRibbon()
        {
            SplineComputer wide = _fixture.NewComputer(
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 2f, Color.white),
                new SplinePoint(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.up, 2f, Color.white));
            PathGenerator path = NewPath(wide);
            TS_Mesh ts = Ts(path);
            Near(ts.vertices[0], new Vector3(0f, 0f, -1f), "sample size 2 doubles the width");
            Near(ts.vertices[1], new Vector3(0f, 0f, 1f), "sample size 2 doubles the width");

            path.useSplineSize = false;
            path.size = 3f;
            Rebuild(path);
            ts = Ts(path);
            Near(ts.vertices[0], new Vector3(0f, 0f, -1.5f), "size 3 without the sample size");
            Near(ts.vertices[1], new Vector3(0f, 0f, 1.5f), "size 3 without the sample size");

            path.useSplineSize = true;
            path.size = 1f;
            path.offset = new Vector3(1f, 2f, 3f);
            Rebuild(path);
            ts = Ts(path);
            // centre (0,0,0) + right * (1 * 2) + up * (2 * 2) + forward * (3 * 2) = (6, 4, -2); half width 1 along right
            Near(ts.vertices[0], new Vector3(6f, 4f, -3f), "offset scaled by the sample size, vertex 0");
            Near(ts.vertices[1], new Vector3(6f, 4f, -1f), "offset scaled by the sample size, vertex 1");
            Near(ts.vertices[2], new Vector3(16f, 4f, -3f), "second sample");

            PathGenerator rotated = NewPath();
            rotated.rotation = 90f;
            Rebuild(rotated);
            ts = Ts(rotated);
            Near(ts.vertices[0], new Vector3(0f, 0.5f, 0f), "right turned 90 degrees about forward is up");
            Near(ts.vertices[1], new Vector3(0f, -0.5f, 0f), "and the other side is down");
            Near(ts.normals[0], new Vector3(0f, 0f, 1f), "the single-slice normal is turned too");
            Near(ts.normals[3], new Vector3(0f, 0f, 1f), "at every sample");
        }

        // GenerateVertices 0x4ad2d0 turns the normal by the rotation only in the single-slice branch (0x4adb09-0x4adb87); with
        // two or more slices the facet normals come from vertices that are already rotated (0x4adbf0, 0x4ade41).
        [Test]
        public void RotationIsNotAppliedTwiceToMultiSliceFacetNormals()
        {
            PathGenerator path = NewPath();
            path.slices = 2;
            path.rotation = 90f;
            Rebuild(path);
            TS_Mesh ts = Ts(path);
            Near(ts.vertices[0], new Vector3(0f, 0.5f, 0f), "first slice turned up");
            Near(ts.vertices[1], new Vector3(0f, 0f, 0f), "middle");
            Near(ts.vertices[2], new Vector3(0f, -0.5f, 0f), "last slice turned down");
            // -normalize(Cross((1,0,0), (0,-0.5,0))) = (0,0,1); the middle one is the Slerp of two equal facets
            for (int i = 0; i < 3; i++)
            {
                Near(ts.normals[i], new Vector3(0f, 0f, 1f), "row 0 normal " + i);
            }
        }

        // The vertex colour is the base colour (sample colour with useSplineColor, else white) times the generator colour.
        [Test]
        public void VertexColourMultipliesTheBaseColourAndTheGeneratorColour()
        {
            SplineComputer coloured = _fixture.NewComputer(
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 1f, new Color(0f, 1f, 0.5f, 1f)),
                new SplinePoint(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.up, 1f, new Color(0f, 1f, 0.5f, 1f)));
            PathGenerator path = NewPath(coloured);
            path.color = new Color(1f, 0.5f, 1f, 0.5f);
            Rebuild(path);
            DreamteckGeneratorFixture.Near(Ts(path).colors[0], new Color(0f, 0.5f, 0.5f, 0.5f), "sample colour times generator colour");
            path.useSplineColor = false;
            Rebuild(path);
            DreamteckGeneratorFixture.Near(Ts(path).colors[3], new Color(1f, 0.5f, 1f, 0.5f), "white times generator colour");
        }

        // With a shape curve and two slices the row has three vertices; the facet normals come from the neighbouring vertices:
        // -normalize(Cross(forward, next - this)) at the first, a Slerp of the next and the previous facet in the middle and the
        // previous facet at the end. Curve keys (0,0), (0.5,1), (1,0) lift the middle vertex by shapeEvaluate * shapeExposure.
        [Test]
        public void TwoSlicesWithAShapeCurveGetFacetNormals()
        {
            PathGenerator path = NewPath();
            path.slices = 2;
            Rebuild(path);
            TS_Mesh ts = Ts(path);
            Assert.That(ts.vertices.Length, Is.EqualTo(6));
            Assert.That(ts.triangles.Length, Is.EqualTo(12));
            for (int i = 0; i < 6; i++)
            {
                Near(ts.normals[i], Vector3.up, "a flat ribbon has the up normal, " + i);
            }

            path.useShapeCurve = true;
            path.shape = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
            Rebuild(path);
            ts = Ts(path);
            Near(ts.vertices[0], new Vector3(0f, 0f, -0.5f), "slice 0");
            Near(ts.vertices[1], new Vector3(0f, 1f, 0f), "slice 1 is lifted by the curve value 1");
            Near(ts.vertices[2], new Vector3(0f, 0f, 0.5f), "slice 2");
            float s = 1f / Mathf.Sqrt(1.25f);
            Near(ts.normals[0], new Vector3(0f, 0.5f * s, -s), "first: -normalize(Cross(forward, (0,1,0.5)))");
            Near(ts.normals[1], Vector3.up, "middle: the Slerp of the two neighbouring facets");
            Near(ts.normals[2], new Vector3(0f, 0.5f * s, s), "last: the facet before it");
            Near(ts.vertices[4], new Vector3(10f, 1f, 0f), "second sample, lifted too");

            path.shapeExposure = 2f;
            Rebuild(path);
            Near(Ts(path).vertices[1], new Vector3(0f, 2f, 0f), "exposure scales the lift");
        }

        // GetSampleWithAngleCompensation 0x4cec50 inflates the corner sample by 1/sqrt((dot + 1) / 2); the row of that sample is
        // size 1 * sqrt(2) wide (the end samples keep 1).
        [Test]
        public void CompensatedCornersWidenTheCornerRow()
        {
            SplineComputer corner = _fixture.NewComputer(
                new SplinePoint(Vector3.zero),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f)));
            PathGenerator path = _fixture.NewGenerator<PathGenerator>(corner);
            Assert.That(path.sampleCount, Is.EqualTo(3));
            TS_Mesh ts = Ts(path);
            Assert.That(Vector3.Distance(ts.vertices[2], ts.vertices[3]), Is.EqualTo(1f).Within(1e-4f), "plain width");

            path.compensateCorners = true;
            Rebuild(path);
            ts = Ts(path);
            Assert.That(Vector3.Distance(ts.vertices[0], ts.vertices[1]), Is.EqualTo(1f).Within(1e-4f), "first sample");
            Assert.That(Vector3.Distance(ts.vertices[2], ts.vertices[3]), Is.EqualTo(Mathf.Sqrt(2f)).Within(1e-4f), "corner sample");
            Assert.That(Vector3.Distance(ts.vertices[4], ts.vertices[5]), Is.EqualTo(1f).Within(1e-4f), "last sample");
        }

        // The uniform UV modes advance V with the distance walked (AddUVDistance), the others use the percent.
        [Test]
        public void UvModesChangeTheVCoordinate()
        {
            PathGenerator path = NewPath();
            path.uvMode = MeshGenerator.UVMode.UniformClip;
            path.uvScale = new Vector2(1f, 0.1f);
            Rebuild(path);
            TS_Mesh ts = Ts(path);
            DreamteckGeneratorFixture.Near(ts.uv[0], new Vector2(1f, 0f), "first row starts at 0");
            DreamteckGeneratorFixture.Near(ts.uv[2], new Vector2(1f, 1f), "second row is 10 units on, scaled by 0.1");
            path.uvMode = MeshGenerator.UVMode.Clip;
            Rebuild(path);
            DreamteckGeneratorFixture.Near(Ts(path).uv[2], new Vector2(1f, 0.1f), "Clip uses percent 1 times the scale");
        }

        // Fewer than two samples clear the mesh instead of building it (Build 0x49d810 chooses ClearMesh).
        [Test]
        public void SingleSampleBuildsAnEmptyMesh()
        {
            PathGenerator path = _fixture.NewGenerator<PathGenerator>(_fixture.NewComputer(new SplinePoint(Vector3.zero)));
            Assert.That(path.sampleCount, Is.LessThan(2));
            Assert.That(Ts(path).vertices.Length, Is.EqualTo(0));
            Assert.That(DreamteckGeneratorFixture.MeshOf(path).vertexCount, Is.EqualTo(0));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndMenu()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(PathGenerator).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_slices", "_compensateCorners", "_useShapeCurve", "_shape", "_lastShape", "_shapeExposure"
            }));
            foreach (string name in new[] { "_slices", "_compensateCorners", "_useShapeCurve", "_shape", "_lastShape", "_shapeExposure" })
            {
                FieldInfo field = typeof(PathGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            TooltipAttribute tooltip = (TooltipAttribute)typeof(PathGenerator).GetField("_compensateCorners", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttributes(typeof(TooltipAttribute), false).Single();
            Assert.That(tooltip.tooltip, Is.EqualTo("This will inflate sample sizes based on the angle between two samples in order to preserve geometry width"));
            AddComponentMenu menu = (AddComponentMenu)typeof(PathGenerator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Path Generator"));
            Assert.That(typeof(PathGenerator).GetCustomAttributes(typeof(RequireComponent), false).Cast<RequireComponent>().Select(r => r.m_Type0).ToArray(),
                Is.EquivalentTo(new[] { typeof(MeshFilter), typeof(MeshRenderer) }));
            Assert.That(typeof(PathGenerator).BaseType, Is.EqualTo(typeof(MeshGenerator)));
        }
    }
}
