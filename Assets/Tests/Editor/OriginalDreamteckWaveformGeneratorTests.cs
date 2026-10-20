using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.WaveformGenerator on the Linear line (0,0,0) -> (10,0,0) lifted to y = 2: two samples with forward
    // (1,0,0), up (0,1,0), size 1, white, at percents 0 and 1. With the default axis Y the spline point is dropped to y = 0 (the
    // baseline) and the vertex row runs from there back up to the sample. The sample right vector is Cross(up, forward) =
    // (0,0,-1) and the surface normal Cross(axis, forward) = (0,0,-1). Expected values are hand arithmetic from the native body
    // Generate 0x4d96d0 (Generate cites below are offsets in it).
    public sealed class OriginalDreamteckWaveformGeneratorTests
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

        private static void Near(Vector2 actual, Vector2 expected, string message)
        {
            DreamteckGeneratorFixture.Near(actual, expected, message);
        }

        private static TS_Mesh Ts(MeshGenerator generator)
        {
            return DreamteckGeneratorFixture.TsMeshOf(generator);
        }

        private SplineComputer NewRaisedLine(float height)
        {
            return _fixture.NewComputer(new SplinePoint(new Vector3(0f, height, 0f)), new SplinePoint(new Vector3(10f, height, 0f)));
        }

        private WaveformGenerator NewWaveform(float height = 2f)
        {
            // The cached play state makes a setter queue the rebuild for the next Update, as in the shipped build; the editor
            // compile of SplineUser would rebuild at once while Application.isPlaying is false.
            WaveformGenerator waveform = DreamteckPlayState.Cache(_fixture.NewGenerator<WaveformGenerator>(NewRaisedLine(height)));
            SplineSample first = new SplineSample();
            waveform.GetSample(0, ref first);
            Near(first.forward, Vector3.right, "line forward");
            Near(first.up, Vector3.up, "line up");
            Assert.That(waveform.sampleCount, Is.EqualTo(2));
            return waveform;
        }

        // The ctor 0x4da220 sets the axis to Y and one slice on top of the MeshGenerator ctor; get_meshName 0x4d9680.
        [Test]
        public void FreshWaveformKeepsTheShippedDefaults()
        {
            WaveformGenerator waveform = _fixture.NewBare<WaveformGenerator>();
            Assert.That(waveform.axis, Is.EqualTo(WaveformGenerator.Axis.Y));
            Assert.That(waveform.symmetry, Is.False);
            Assert.That(waveform.uvWrapMode, Is.EqualTo(WaveformGenerator.UVWrapMode.Clamp));
            Assert.That(waveform.slices, Is.EqualTo(1));
            Assert.That(waveform.size, Is.EqualTo(1f), "MeshGenerator defaults");
            Assert.That(DreamteckGeneratorFixture.Invoke(waveform, "get_meshName"), Is.EqualTo("Waveform"));
            Assert.That(waveform.GetComponent<MeshFilter>() == null, Is.False, "RequireComponent adds the filter");
            Assert.That(waveform.GetComponent<MeshRenderer>() == null, Is.False, "and the renderer");
        }

        // set_axis 0x4d9580, set_symmetry 0x4d95c0 and set_uvWrapMode 0x4d9600 change and rebuild; set_slices 0x4d9640 raises the
        // count to one (cmp 2 / cmovge) and rebuilds for any value that differs from the stored one.
        [Test]
        public void SettersRebuildOnChangeAndSlicesClampAtOne()
        {
            WaveformGenerator waveform = NewWaveform();
            int builds = 0;
            waveform.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(waveform);

            waveform.axis = WaveformGenerator.Axis.Y;
            waveform.symmetry = false;
            waveform.uvWrapMode = WaveformGenerator.UVWrapMode.Clamp;
            waveform.slices = 1;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(0), "equal values");

            waveform.axis = WaveformGenerator.Axis.Z;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(1));
            waveform.symmetry = true;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(2));
            waveform.uvWrapMode = WaveformGenerator.UVWrapMode.Uniform;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(3));
            waveform.slices = 0;
            Assert.That(waveform.slices, Is.EqualTo(1), "clamped");
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(4), "0 differs from the stored 1, so it still rebuilds");
            waveform.slices = -3;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(5));
            waveform.slices = 4;
            Assert.That(waveform.slices, Is.EqualTo(4));
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(6));
            Assert.That(waveform.axis, Is.EqualTo(WaveformGenerator.Axis.Z));
            Assert.That(waveform.symmetry, Is.True);
            Assert.That(waveform.uvWrapMode, Is.EqualTo(WaveformGenerator.UVWrapMode.Uniform));
        }

        // Generate 0x4d96d0 allocates (slices + 1) * sampleCount vertices and (sampleCount - 1) * slices * 6 indices. For axis Y
        // the baseline point is the sample with y dropped to 0 (InverseTransformPoint, y := 0, TransformPoint), the vertices run
        // from it to the sample (Lerp), the normal is Cross(axis, forward) and the colour the base colour times the generator
        // colour.
        [Test]
        public void SingleSliceRunsFromTheBaselineToTheSample()
        {
            WaveformGenerator waveform = NewWaveform();
            TS_Mesh ts = Ts(waveform);
            Assert.That(ts.vertices.Length, Is.EqualTo(4));
            Assert.That(ts.normals.Length, Is.EqualTo(4));
            Assert.That(ts.colors.Length, Is.EqualTo(4));
            Assert.That(ts.uv.Length, Is.EqualTo(4));
            Assert.That(ts.triangles.Length, Is.EqualTo(6));
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)), "positive values keep the winding");

            Near(ts.vertices[0], new Vector3(0f, 0f, 0f), "first baseline");
            Near(ts.vertices[1], new Vector3(0f, 2f, 0f), "first sample");
            Near(ts.vertices[2], new Vector3(10f, 0f, 0f), "second baseline");
            Near(ts.vertices[3], new Vector3(10f, 2f, 0f), "second sample");
            for (int i = 0; i < 4; i++)
            {
                Near(ts.normals[i], new Vector3(0f, 0f, -1f), "normal " + i);
                DreamteckGeneratorFixture.Near(ts.colors[i], Color.white, "colour " + i);
            }

            // Clamp: U is the sample percent and V the slice percent.
            Near(ts.uv[0], new Vector2(0f, 0f), "uv 0");
            Near(ts.uv[1], new Vector2(0f, 1f), "uv 1");
            Near(ts.uv[2], new Vector2(1f, 0f), "uv 2");
            Near(ts.uv[3], new Vector2(1f, 1f), "uv 3");

            waveform.color = new Color(1f, 0.5f, 1f, 0.5f);
            waveform.RebuildImmediate();
            DreamteckGeneratorFixture.Near(Ts(waveform).colors[2], new Color(1f, 0.5f, 1f, 0.5f), "generator colour");
        }

        // More slices interpolate linearly between the baseline and the sample; symmetry mirrors the baseline to -y.
        [Test]
        public void SlicesInterpolateAndSymmetryMirrorsTheBaseline()
        {
            WaveformGenerator waveform = NewWaveform();
            waveform.slices = 2;
            waveform.RebuildImmediate();
            TS_Mesh ts = Ts(waveform);
            Assert.That(ts.vertices.Length, Is.EqualTo(6));
            Assert.That(ts.triangles.Length, Is.EqualTo(12));
            Near(ts.vertices[0], new Vector3(0f, 0f, 0f), "slice 0");
            Near(ts.vertices[1], new Vector3(0f, 1f, 0f), "slice 1");
            Near(ts.vertices[2], new Vector3(0f, 2f, 0f), "slice 2");
            Near(ts.vertices[5], new Vector3(10f, 2f, 0f), "second sample, slice 2");
            Near(ts.uv[1], new Vector2(0f, 0.5f), "slice 1 V");

            waveform.symmetry = true;
            waveform.RebuildImmediate();
            ts = Ts(waveform);
            Near(ts.vertices[0], new Vector3(0f, -2f, 0f), "the baseline is the mirrored point");
            Near(ts.vertices[1], new Vector3(0f, 0f, 0f), "middle");
            Near(ts.vertices[2], new Vector3(0f, 2f, 0f), "sample");
            Near(ts.vertices[3], new Vector3(10f, -2f, 0f), "second baseline");
        }

        // The axis picks the coordinate of the spline-local sample that is dropped (set to 0, or mirrored), the direction of the
        // normal and the value that is summed: X drops x, so the first baseline equals the sample and the second runs from
        // (0,2,0) to (10,2,0); the normal Cross((1,0,0), (1,0,0)) is zero and normalises to zero. Z drops z, which is 0
        // already; its normal is Cross((0,0,1), (1,0,0)) = (0,1,0).
        [Test]
        public void AxisChoosesTheDroppedCoordinate()
        {
            WaveformGenerator waveform = NewWaveform();
            waveform.axis = WaveformGenerator.Axis.X;
            waveform.RebuildImmediate();
            TS_Mesh ts = Ts(waveform);
            Near(ts.vertices[0], new Vector3(0f, 2f, 0f), "first baseline is the sample");
            Near(ts.vertices[1], new Vector3(0f, 2f, 0f), "first sample");
            Near(ts.vertices[2], new Vector3(0f, 2f, 0f), "second baseline x := 0");
            Near(ts.vertices[3], new Vector3(10f, 2f, 0f), "second sample");
            Near(ts.normals[0], Vector3.zero, "parallel axis and forward");
            Near(ts.normals[3], Vector3.zero, "parallel axis and forward");
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)), "x values 0 and 10 are positive");

            waveform.axis = WaveformGenerator.Axis.Z;
            waveform.RebuildImmediate();
            ts = Ts(waveform);
            Near(ts.vertices[0], new Vector3(0f, 2f, 0f), "z is 0 already: the baseline is the sample");
            Near(ts.vertices[3], new Vector3(10f, 2f, 0f), "z is 0 already");
            Near(ts.normals[0], new Vector3(0f, 1f, 0f), "Cross(z axis, forward)");
        }

        // The faces are turned over (GeneratePlaneTriangles flip) when the summed signed values divided by the sample count are
        // below zero (0x4da19b: divss, then 0 > value).
        [Test]
        public void ANegativeAverageValueFlipsTheWinding()
        {
            WaveformGenerator below = NewWaveform(-2f);
            TS_Mesh ts = Ts(below);
            Near(ts.vertices[0], new Vector3(0f, 0f, 0f), "baseline");
            Near(ts.vertices[1], new Vector3(0f, -2f, 0f), "sample");
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, true)));
            Assert.That(MeshUtility.GeneratePlaneTriangles(1, 2, true), Is.Not.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)));

            WaveformGenerator flat = NewWaveform(0f);
            Assert.That(Ts(flat).triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)), "an average of zero is not below zero");
        }

        // The U/V pair per UV wrap mode (Generate 0x4d9f0a jump table), then Scale(uvScale) + uvOffset. Distance is the walk along
        // the samples (10 at the second one), the height |value| * uvScale.y with value the local y of 2. UniformY and Uniform
        // multiply V by the height (6 for a scale of 3) and the scale again; a mode outside the enum writes nothing.
        [Test]
        public void UvWrapModesTruthTable()
        {
            WaveformGenerator waveform = NewWaveform();
            waveform.uvScale = new Vector2(2f, 3f);
            waveform.uvOffset = new Vector2(0.5f, 0.25f);
            object[][] table =
            {
                // mode, sample 0 slice 0, sample 1 slice 1
                new object[] { WaveformGenerator.UVWrapMode.Clamp, new Vector2(0.5f, 0.25f), new Vector2(2.5f, 3.25f) },
                new object[] { WaveformGenerator.UVWrapMode.UniformX, new Vector2(0.5f, 0.25f), new Vector2(20.5f, 3.25f) },
                new object[] { WaveformGenerator.UVWrapMode.UniformY, new Vector2(0.5f, 0.25f), new Vector2(2.5f, 18.25f) },
                new object[] { WaveformGenerator.UVWrapMode.Uniform, new Vector2(0.5f, 0.25f), new Vector2(20.5f, 18.25f) }
            };
            foreach (object[] row in table)
            {
                waveform.uvWrapMode = (WaveformGenerator.UVWrapMode)row[0];
                waveform.RebuildImmediate();
                TS_Mesh ts = Ts(waveform);
                Near(ts.uv[0], (Vector2)row[1], row[0] + " sample 0");
                Near(ts.uv[3], (Vector2)row[2], row[0] + " sample 1");
            }

            // AllocateMesh keeps the arrays when the vertex count is unchanged (0x49dd46 cmp/je skips every SZArrayNew) and
            // Generate skips the UV store for a mode above 3 (0x4d9f0a cmp 3 / ja), so a sentinel survives the rebuild.
            Ts(waveform).uv[3] = new Vector2(-7f, 9f);
            DreamteckGeneratorFixture.SetField(waveform, "_uvWrapMode", (WaveformGenerator.UVWrapMode)4);
            waveform.RebuildImmediate();
            Near(Ts(waveform).uv[3], new Vector2(-7f, 9f), "no UV is written for an unknown mode");
            Near(Ts(waveform).vertices[3], new Vector3(10f, 2f, 0f), "the vertices are still rewritten");
        }

        // An axis outside the enum keeps the TransformDirection(right) default (0x4d97b0 jne 0x4d9858 skips the Y/Z overrides),
        // drops no coordinate and adds nothing to the value sum (0x4d9be1), and the V height stays 1.0 (0x4d9ad7 jne 0x4d9b30,
        // the constant at 0x29a6be0 is the Lerp clamp maximum 1.0). The UniformY V is height * slice * uvScale.y (0x4d9feb / 0x4da029 mulss by
        // the height, then mulps by uvScale), so with the height 1 it is 1 * 1 * 3 = 3; the height of a real axis already carries
        // one uvScale.y factor (0x4d9bf1), which is where the other tests get their second factor.
        [Test]
        public void AnAxisOutsideTheEnumDropsNothing()
        {
            WaveformGenerator waveform = NewWaveform();
            waveform.uvScale = new Vector2(2f, 3f);
            DreamteckGeneratorFixture.SetField(waveform, "_axis", (WaveformGenerator.Axis)3);
            DreamteckGeneratorFixture.SetField(waveform, "_uvWrapMode", WaveformGenerator.UVWrapMode.UniformY);
            waveform.RebuildImmediate();
            TS_Mesh ts = Ts(waveform);
            Near(ts.vertices[0], new Vector3(0f, 2f, 0f), "the baseline is the sample");
            Near(ts.vertices[1], new Vector3(0f, 2f, 0f), "sample");
            Near(ts.normals[0], Vector3.zero, "Cross(right, forward) is zero");
            Near(ts.uv[3], new Vector2(2f, 3f), "percent 1 * 2, slice 1 * height 1 * 3");
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)), "the value sum stays 0");
        }

        // The offset moves the vertices along the axis direction by offset.y and along Cross(up, forward) = (0,0,-1) by offset.x,
        // both times the base size; offset.z is not used.
        [Test]
        public void OffsetMovesAlongTheAxisAndTheSampleRight()
        {
            WaveformGenerator waveform = NewWaveform();
            waveform.offset = new Vector3(1f, 2f, 5f);
            waveform.RebuildImmediate();
            TS_Mesh ts = Ts(waveform);
            Near(ts.vertices[0], new Vector3(0f, 2f, -1f), "baseline + (0,2,0) + (0,0,-1)");
            Near(ts.vertices[1], new Vector3(0f, 4f, -1f), "sample + (0,2,0) + (0,0,-1)");

            SplineComputer wide = _fixture.NewComputer(
                new SplinePoint(new Vector3(0f, 2f, 0f), new Vector3(0f, 2f, 0f), Vector3.up, 2f, Color.white),
                new SplinePoint(new Vector3(10f, 2f, 0f), new Vector3(10f, 2f, 0f), Vector3.up, 2f, Color.white));
            WaveformGenerator scaled = _fixture.NewGenerator<WaveformGenerator>(wide);
            scaled.offset = new Vector3(1f, 2f, 5f);
            scaled.RebuildImmediate();
            Near(Ts(scaled).vertices[0], new Vector3(0f, 4f, -2f), "the offset is scaled by the sample size");
            scaled.useSplineSize = false;
            scaled.RebuildImmediate();
            Near(Ts(scaled).vertices[0], new Vector3(0f, 2f, -1f), "and by 1 without it");
        }

        // The baseline is the plane of the spline computer: the samples are made local with InverseTransformPoint, the axis
        // coordinate is dropped there and the point goes back with TransformPoint. A computer standing at y = 5 with points at
        // y = 7 (local y 2) drops the baseline to y = 5, and the axis direction is TransformDirection(up).
        [Test]
        public void TheBaselineLiesInTheSplinePlane()
        {
            GameObject owner = _fixture.NewObject("raised");
            owner.transform.position = new Vector3(0f, 5f, 0f);
            SplineComputer raised = owner.AddComponent<SplineComputer>();
            raised.type = Spline.Type.Linear;
            raised.SetPoints(new[] { new SplinePoint(new Vector3(0f, 7f, 0f)), new SplinePoint(new Vector3(10f, 7f, 0f)) });
            raised.RebuildImmediate();

            WaveformGenerator waveform = _fixture.NewGenerator<WaveformGenerator>(raised);
            TS_Mesh ts = Ts(waveform);
            Near(ts.vertices[0], new Vector3(0f, 5f, 0f), "baseline at the computer y");
            Near(ts.vertices[1], new Vector3(0f, 7f, 0f), "sample");
            Near(ts.vertices[2], new Vector3(10f, 5f, 0f), "second baseline");
            Near(ts.vertices[3], new Vector3(10f, 7f, 0f), "second sample");
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)), "the local value 2 is positive");
        }

        // A single sample builds nothing: Build 0x49d810 clears the mesh below two samples.
        [Test]
        public void SingleSampleBuildsAnEmptyMesh()
        {
            WaveformGenerator waveform = _fixture.NewGenerator<WaveformGenerator>(_fixture.NewComputer(new SplinePoint(Vector3.zero)));
            Assert.That(waveform.sampleCount, Is.LessThan(2));
            Assert.That(Ts(waveform).vertices.Length, Is.EqualTo(0));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndMenu()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string[] names = { "_axis", "_symmetry", "_uvWrapMode", "_slices" };
            Assert.That(typeof(WaveformGenerator).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(names));
            foreach (string name in names)
            {
                FieldInfo field = typeof(WaveformGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            AddComponentMenu menu = (AddComponentMenu)typeof(WaveformGenerator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Waveform Generator"));
            // Reflection does not guarantee the attribute order, so only the set is asserted.
            Assert.That(typeof(WaveformGenerator).GetCustomAttributes(typeof(RequireComponent), false).Cast<RequireComponent>().Select(r => r.m_Type0).ToArray(),
                Is.EquivalentTo(new[] { typeof(MeshFilter), typeof(MeshRenderer) }));
            Assert.That(typeof(WaveformGenerator).BaseType, Is.EqualTo(typeof(MeshGenerator)));

            Assert.That(System.Enum.GetNames(typeof(WaveformGenerator.Axis)), Is.EqualTo(new[] { "X", "Y", "Z" }));
            Assert.That(System.Enum.GetValues(typeof(WaveformGenerator.Axis)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(System.Enum.GetNames(typeof(WaveformGenerator.Space)), Is.EqualTo(new[] { "World", "Local" }));
            Assert.That(System.Enum.GetValues(typeof(WaveformGenerator.Space)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(System.Enum.GetNames(typeof(WaveformGenerator.UVWrapMode)), Is.EqualTo(new[] { "Clamp", "UniformX", "UniformY", "Uniform" }));
            Assert.That(System.Enum.GetValues(typeof(WaveformGenerator.UVWrapMode)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }
    }
}
