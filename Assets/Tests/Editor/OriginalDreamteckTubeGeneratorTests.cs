using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.TubeGenerator on the Linear line (0,0,0) -> (10,0,0) with four sides: two samples with forward (1,0,0),
    // up (0,1,0), size 1, white, at percents 0 and 1. The sample right vector is Cross(up, forward) = (0,0,-1). A ring vertex n
    // is the right vector turned about the forward by 360 * n / 4 + 180 degrees (BuildMesh 0x4d6a60, Generate 0x4d6ba0), so the
    // first ring of the line is (0,0,0.5), (0,-0.5,0), (0,0,-0.5), (0,0.5,0), (0,0,0.5) for the default size 1. Expected values
    // are hand arithmetic from the native bodies cited per test (x86-64 addresses in the 0x4d67.. - 0x4d9.. range).
    public sealed class OriginalDreamteckTubeGeneratorTests
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

        // A tube on the computer with the given number of sides, built once.
        private TubeGenerator NewTube(int sides = 4, SplineComputer computer = null)
        {
            TubeGenerator tube = _fixture.NewGenerator<TubeGenerator>(computer ?? _fixture.NewLine());
            tube.sides = sides;
            tube.RebuildImmediate();
            return tube;
        }

        // A closed Linear square, so that the end rings meet.
        private SplineComputer NewClosedSquare()
        {
            SplineComputer square = _fixture.NewComputer(
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f)),
                new SplinePoint(new Vector3(0f, 0f, 10f)));
            square.Close();
            square.RebuildImmediate();
            return square;
        }

        private static int[] CountBuilds(TubeGenerator tube)
        {
            int[] counter = new int[1];
            tube.onPostBuild += delegate { counter[0]++; };
            DreamteckGeneratorFixture.Update(tube);
            return counter;
        }

        // Field initialisers of the ctor 0x4d9500 (12 sides, 6 latitude rings, revolve 360, cap UV scale 1) on top of the
        // MeshGenerator ctor, get_meshName 0x4d6a20.
        [Test]
        public void FreshTubeKeepsTheShippedDefaults()
        {
            TubeGenerator tube = _fixture.NewBare<TubeGenerator>();
            Assert.That(tube.sides, Is.EqualTo(12));
            Assert.That(tube.roundCapLatitude, Is.EqualTo(6));
            Assert.That(tube.capMode, Is.EqualTo(TubeGenerator.CapMethod.None));
            Assert.That(tube.revolve, Is.EqualTo(360f));
            Assert.That(tube.capUVScale, Is.EqualTo(1f));
            Assert.That(tube.uvTwist, Is.EqualTo(0f));
            Assert.That(tube.size, Is.EqualTo(1f), "MeshGenerator defaults");
            Assert.That(DreamteckGeneratorFixture.Invoke(tube, "get_meshName"), Is.EqualTo("Tube"));
            Assert.That(tube.GetComponent<MeshFilter>() == null, Is.False, "RequireComponent adds the filter");
            Assert.That(tube.GetComponent<MeshRenderer>() == null, Is.False, "and the renderer");
        }

        // set_sides 0x4d6770 compares the raw value with the stored one, then stores max(value, 3) and queues a rebuild even when
        // the raised value equals the stored one (cmp 4 / cmovge).
        [Test]
        public void SidesClampAtThreeAndRebuildOnAnyDifferentRawValue()
        {
            TubeGenerator tube = NewTube(12);
            int[] builds = CountBuilds(tube);

            tube.sides = 12;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(0), "equal value");
            tube.sides = 2;
            Assert.That(tube.sides, Is.EqualTo(3), "raised to three");
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(1));
            tube.sides = 2;
            Assert.That(tube.sides, Is.EqualTo(3));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2), "2 differs from the stored 3, so it still rebuilds");
            tube.sides = 40;
            Assert.That(tube.sides, Is.EqualTo(40));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(3));
        }

        // set_roundCapLatitude 0x4d6800 raises the count to one (cmp 2 / cmovge), compares the raised value, and rebuilds only
        // for the round cap mode.
        [Test]
        public void RoundCapLatitudeClampsAtOneAndOnlyRoundCapsRebuild()
        {
            TubeGenerator tube = NewTube(4);
            int[] builds = CountBuilds(tube);

            tube.roundCapLatitude = 3;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(3));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(0), "no round cap: stored without a rebuild");
            tube.roundCapLatitude = -5;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(1), "raised to one");
            tube.roundCapLatitude = 0;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(1));

            tube.capMode = TubeGenerator.CapMethod.Round;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(1));
            tube.roundCapLatitude = 4;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(4));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2), "round caps depend on the latitude");
            tube.roundCapLatitude = 4;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2), "equal value");
        }

        // set_capMode 0x4d67c0, set_revolve 0x4d6850, set_capUVScale 0x4d68a0 and set_uvTwist 0x4d68f0 are plain
        // change-and-rebuild setters.
        [Test]
        public void PlainSettersRebuildOnChangeOnly()
        {
            TubeGenerator tube = NewTube(4);
            int[] builds = CountBuilds(tube);

            tube.capMode = TubeGenerator.CapMethod.None;
            tube.revolve = 360f;
            tube.capUVScale = 1f;
            tube.uvTwist = 0f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(0), "equal values");

            tube.capMode = TubeGenerator.CapMethod.Flat;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(1));
            tube.revolve = 180f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2));
            tube.capUVScale = 2f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(3));
            tube.uvTwist = 0.5f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(4));
            Assert.That(tube.capMode, Is.EqualTo(TubeGenerator.CapMethod.Flat));
            Assert.That(tube.revolve, Is.EqualTo(180f));
            Assert.That(tube.capUVScale, Is.EqualTo(2f));
            Assert.That(tube.uvTwist, Is.EqualTo(0.5f));
        }

        // BuildMesh 0x4d6a60 allocates (sides + 1) * sampleCount vertices and sides * 6 * (sampleCount - 1) indices; Generate
        // 0x4d6ba0 puts the ring vertices at the centre plus the right vector turned about the forward, the normal along the
        // vertex minus the centre, the UV at (anglePercent, percent) after the 180 degree turn about (0.5, 0.5).
        [Test]
        public void RingsFollowTheLine()
        {
            TubeGenerator tube = NewTube(4);
            SplineSample first = new SplineSample();
            tube.GetSample(0, ref first);
            Near(first.forward, Vector3.right, "line forward");
            Near(first.up, Vector3.up, "line up");

            TS_Mesh ts = Ts(tube);
            Assert.That(ts.vertices.Length, Is.EqualTo(10));
            Assert.That(ts.normals.Length, Is.EqualTo(10));
            Assert.That(ts.colors.Length, Is.EqualTo(10));
            Assert.That(ts.uv.Length, Is.EqualTo(10));
            Assert.That(ts.triangles.Length, Is.EqualTo(24));
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(4, 2, false)));

            Vector3[] ring = { new Vector3(0f, 0f, 0.5f), new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0f, 0.5f) };
            for (int n = 0; n < 5; n++)
            {
                Near(ts.vertices[n], ring[n], "first ring vertex " + n);
                Near(ts.vertices[5 + n], ring[n] + new Vector3(10f, 0f, 0f), "second ring vertex " + n);
                Near(ts.normals[n], ring[n].normalized, "first ring normal " + n);
                Near(ts.normals[5 + n], ring[n].normalized, "second ring normal " + n);
                DreamteckGeneratorFixture.Near(ts.colors[n], Color.white, "colour " + n);
                Near(ts.uv[n], new Vector2(n / 4f, 0f), "first ring uv " + n);
                Near(ts.uv[5 + n], new Vector2(n / 4f, 1f), "second ring uv " + n);
            }
        }

        // The radius is size * sample size / 2; the rotation adds to the ring angle; the offset moves the centre by right, up and
        // forward scaled by the sample size (0x4d6e00 ff.); a revolve below 360 spreads the ring over a partial turn.
        [Test]
        public void SizeRotationOffsetAndRevolveShapeTheRing()
        {
            TubeGenerator tube = NewTube(4);
            tube.size = 2f;
            tube.RebuildImmediate();
            Near(Ts(tube).vertices[0], new Vector3(0f, 0f, 1f), "size 2 doubles the radius");
            Near(Ts(tube).vertices[1], new Vector3(0f, -1f, 0f), "size 2 doubles the radius");

            tube.size = 1f;
            tube.rotation = 90f;
            tube.RebuildImmediate();
            Near(Ts(tube).vertices[0], new Vector3(0f, -0.5f, 0f), "rotation 90 turns vertex 0 from 180 to 270 degrees");
            Near(Ts(tube).vertices[1], new Vector3(0f, 0f, -0.5f), "and vertex 1 from 270 to 360");

            tube.rotation = 0f;
            tube.offset = new Vector3(1f, 2f, 3f);
            tube.RebuildImmediate();
            // centre (0,0,0) + right * 1 + up * 2 + forward * 3 = (0,0,-1) + (0,2,0) + (3,0,0) = (3,2,-1)
            Near(Ts(tube).vertices[0], new Vector3(3f, 2f, -0.5f), "offset centre, vertex 0");
            Near(Ts(tube).vertices[5], new Vector3(13f, 2f, -0.5f), "offset centre, second ring");
            Near(Ts(tube).normals[0], new Vector3(0f, 0f, 1f), "the normal leaves from the offset centre");

            tube.offset = Vector3.zero;
            tube.revolve = 180f;
            tube.RebuildImmediate();
            float s = 0.5f * Mathf.Sqrt(0.5f);
            Near(Ts(tube).vertices[0], new Vector3(0f, 0f, 0.5f), "half turn: 180 degrees");
            Near(Ts(tube).vertices[1], new Vector3(0f, -s, s), "half turn: 225 degrees");
            Near(Ts(tube).vertices[2], new Vector3(0f, -0.5f, 0f), "half turn: 270 degrees");
            Near(Ts(tube).vertices[3], new Vector3(0f, -s, -s), "half turn: 315 degrees");
            Near(Ts(tube).vertices[4], new Vector3(0f, 0f, -0.5f), "half turn: 360 degrees");
        }

        // The vertex colour is the base colour (sample colour with useSplineColor, else white) times the generator colour.
        [Test]
        public void VertexColourMultipliesTheBaseColourAndTheGeneratorColour()
        {
            SplineComputer coloured = _fixture.NewComputer(
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 1f, new Color(0f, 1f, 0.5f, 1f)),
                new SplinePoint(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.up, 1f, new Color(0f, 1f, 0.5f, 1f)));
            TubeGenerator tube = NewTube(4, coloured);
            tube.color = new Color(1f, 0.5f, 1f, 0.5f);
            tube.RebuildImmediate();
            DreamteckGeneratorFixture.Near(Ts(tube).colors[0], new Color(0f, 0.5f, 0.5f, 0.5f), "sample colour times generator colour");
            DreamteckGeneratorFixture.Near(Ts(tube).colors[9], new Color(0f, 0.5f, 0.5f, 0.5f), "last vertex");
            tube.useSplineColor = false;
            tube.RebuildImmediate();
            DreamteckGeneratorFixture.Near(Ts(tube).colors[7], new Color(1f, 0.5f, 1f, 0.5f), "white times generator colour");
        }

        // The UV twist shifts U by the spline percent times the twist (0x4d70d7 ff.); the uniform clip mode advances V with the
        // distance walked.
        [Test]
        public void UvTwistAndUvModesShapeTheCoordinates()
        {
            TubeGenerator tube = NewTube(4);
            tube.uvTwist = 0.5f;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            Near(ts.uv[1], new Vector2(0.25f, 0f), "percent 0 has no twist");
            Near(ts.uv[6], new Vector2(0.25f + 0.5f, 1f), "percent 1 shifts U by the twist");

            tube.uvTwist = 0f;
            tube.uvMode = MeshGenerator.UVMode.UniformClip;
            tube.uvScale = new Vector2(1f, 0.1f);
            tube.RebuildImmediate();
            ts = Ts(tube);
            Near(ts.uv[2], new Vector2(0.5f, 0f), "first ring starts at 0");
            Near(ts.uv[7], new Vector2(0.5f, 1f), "second ring is 10 units on, scaled by 0.1");
        }

        // BuildMesh 0x4d6a60 builds nothing for fewer than three sides; only the serialised field can hold such a value.
        [Test]
        public void FewerThanThreeSidesBuildNothing()
        {
            TubeGenerator tube = _fixture.NewBare<TubeGenerator>();
            tube.calculateTangents = false;
            DreamteckGeneratorFixture.SetField(tube, "_sides", 2);
            tube.spline = _fixture.NewLine();
            tube.RebuildImmediate();
            Assert.That(tube.sampleCount, Is.EqualTo(2));
            Assert.That(Ts(tube).vertices.Length, Is.EqualTo(0));
            Assert.That(Ts(tube).triangles.Length, Is.EqualTo(0));
        }

        // get_useCap 0x4d6930: no cap without a mode; an open spline shows its ends; a closed one only when the clip range leaves
        // a gap (span below one). Each flat cap adds a ring of sides + 1 vertices at both ends.
        [Test]
        public void CapsAreOnlyBuiltWhereTheEndsAreVisible()
        {
            TubeGenerator open = NewTube(4);
            Assert.That(Ts(open).vertices.Length, Is.EqualTo(10), "no cap mode");
            open.capMode = TubeGenerator.CapMethod.Flat;
            open.RebuildImmediate();
            Assert.That(Ts(open).vertices.Length, Is.EqualTo(20), "an open spline is capped");

            TubeGenerator closed = NewTube(4, NewClosedSquare());
            closed.capMode = TubeGenerator.CapMethod.Flat;
            closed.RebuildImmediate();
            int bodyVertices = 5 * closed.sampleCount;
            Assert.That(Ts(closed).vertices.Length, Is.EqualTo(bodyVertices), "a closed spline over its whole range has no ends");

            closed.clipTo = 0.5;
            closed.RebuildImmediate();
            Assert.That(Ts(closed).vertices.Length, Is.EqualTo(5 * closed.sampleCount + 10), "a gap in the clip range shows the ends");
            closed.clipTo = 1.0;
            closed.capMode = TubeGenerator.CapMethod.Round;
            closed.RebuildImmediate();
            Assert.That(Ts(closed).vertices.Length, Is.EqualTo(bodyVertices), "round caps follow the same rule");
        }

        // GenerateFlatCaps 0x4d7410: a fan over a copy of the first ring (normal backwards) and one over a copy of the last ring
        // (normal forwards). The cap UV is (0.5, 0.5) + (cos, sin) * capUVScale / 2 of the angle revolve * percent, with percent
        // i / (sides - 1) at the start (0x4d7604) and, as shipped, the vertex index over (sides - 1) at the end (0x4d7938): the
        // last ring starts at index 5, so its first vertex sits at 600 degrees.
        [Test]
        public void FlatCapsCopyTheEndRingsIntoFans()
        {
            TubeGenerator tube = NewTube(4);
            tube.capMode = TubeGenerator.CapMethod.Flat;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            Assert.That(ts.vertices.Length, Is.EqualTo(20));
            Assert.That(ts.normals.Length, Is.EqualTo(20));
            Assert.That(ts.triangles.Length, Is.EqualTo(24 + 2 * 18), "body + 2 * (sides * 6 - 6)");

            for (int i = 0; i < 5; i++)
            {
                Near(ts.vertices[10 + i], ts.vertices[i], "start cap vertex " + i);
                Near(ts.vertices[15 + i], ts.vertices[5 + i], "end cap vertex " + i);
                Near(ts.normals[10 + i], new Vector3(-1f, 0f, 0f), "start cap normal " + i);
                Near(ts.normals[15 + i], new Vector3(1f, 0f, 0f), "end cap normal " + i);
                DreamteckGeneratorFixture.Near(ts.colors[10 + i], Color.white, "start cap colour " + i);
                DreamteckGeneratorFixture.Near(ts.colors[15 + i], Color.white, "end cap colour " + i);
            }

            float h = 0.5f * Mathf.Sqrt(3f) * 0.5f;
            Vector2 right = new Vector2(1f, 0.5f);
            Vector2 upperLeft = new Vector2(0.25f, 0.5f + h);
            Vector2 lowerLeft = new Vector2(0.25f, 0.5f - h);
            Vector2[] start = { right, upperLeft, lowerLeft, right, upperLeft };
            Vector2[] end = { lowerLeft, right, upperLeft, lowerLeft, right };
            for (int i = 0; i < 5; i++)
            {
                Near(ts.uv[10 + i], start[i], "start cap uv " + i);
                Near(ts.uv[15 + i], end[i], "end cap uv " + i);
            }

            Assert.That(ts.triangles.Skip(24).Take(12).ToArray(), Is.EqualTo(new[] { 12, 11, 10, 13, 12, 10, 15, 16, 17, 15, 17, 18 }),
                "a full revolve leaves the closing corner out: sides - 2 triangles per cap");
            Assert.That(ts.triangles.Skip(36).All(index => index == 0), Is.True, "the allocation is larger than the fans");
        }

        // The cap UV scale and the revolve enter the cap UV; a partial revolve keeps the last corner (sides - 1 triangles per cap).
        [Test]
        public void FlatCapUvAndFanFollowTheCapScaleAndRevolve()
        {
            TubeGenerator tube = NewTube(4);
            tube.capMode = TubeGenerator.CapMethod.Flat;
            tube.capUVScale = 0.5f;
            tube.revolve = 180f;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            // percent i / 3 turned by 180 * percent: i = 1 is 60 degrees, radius 0.25
            Near(ts.uv[10], new Vector2(0.75f, 0.5f), "start cap uv 0");
            Near(ts.uv[11], new Vector2(0.5f + 0.25f * 0.5f, 0.5f + 0.25f * 0.5f * Mathf.Sqrt(3f)), "start cap uv 1");
            Assert.That(ts.triangles.Skip(24).Take(18).ToArray(), Is.EqualTo(new[] { 12, 11, 10, 13, 12, 10, 14, 13, 10, 15, 16, 17, 15, 17, 18, 15, 18, 19 }));
        }

        // GenerateRoundCaps 0x4d7c40: roundCapLatitude rings per end bending from the end ring to the axis point. For the start
        // cap with the line frame the vertex of ring angle 90 n degrees at latitude a is 0.5 * (-sin a, -cos a * sin(90 n),
        // cos a * cos(90 n)); the end cap mirrors it about the end ring centre (10,0,0) with x = +sin a. Ring 2 (90 degrees) is
        // the apex.
        [Test]
        public void RoundCapsBendFromTheEndRingsToTheAxis()
        {
            TubeGenerator tube = NewTube(4);
            tube.roundCapLatitude = 2;
            tube.capMode = TubeGenerator.CapMethod.Round;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            Assert.That(ts.vertices.Length, Is.EqualTo(30), "10 + 2 * 5 * 2");
            Assert.That(ts.triangles.Length, Is.EqualTo(24 + 2 * 48), "body + 2 * sides * 6 * latitude");

            float c = 0.5f * Mathf.Sqrt(0.5f);
            Vector3[] startRing1 = { new Vector3(-c, 0f, c), new Vector3(-c, -c, 0f), new Vector3(-c, 0f, -c), new Vector3(-c, c, 0f), new Vector3(-c, 0f, c) };
            Vector3[] endRing1 = { new Vector3(10f + c, 0f, c), new Vector3(10f + c, -c, 0f), new Vector3(10f + c, 0f, -c), new Vector3(10f + c, c, 0f), new Vector3(10f + c, 0f, c) };
            for (int n = 0; n < 5; n++)
            {
                Near(ts.vertices[10 + n], startRing1[n], "start cap ring 1 vertex " + n);
                Near(ts.vertices[15 + n], new Vector3(-0.5f, 0f, 0f), "start cap apex " + n);
                Near(ts.vertices[20 + n], endRing1[n], "end cap ring 1 vertex " + n);
                Near(ts.vertices[25 + n], new Vector3(10.5f, 0f, 0f), "end cap apex " + n);
                Near(ts.normals[10 + n], startRing1[n].normalized, "start cap ring 1 normal " + n);
                Near(ts.normals[15 + n], new Vector3(-1f, 0f, 0f), "start cap apex normal " + n);
                Near(ts.normals[20 + n], (endRing1[n] - new Vector3(10f, 0f, 0f)).normalized, "end cap ring 1 normal " + n);
                Near(ts.normals[25 + n], new Vector3(1f, 0f, 0f), "end cap apex normal " + n);
                DreamteckGeneratorFixture.Near(ts.colors[10 + n], Color.white, "start cap colour " + n);
                DreamteckGeneratorFixture.Near(ts.colors[25 + n], Color.white, "end cap colour " + n);
            }

            int[] expected =
            {
                // start cap: i = -1 joins ring 0 of the body (n) to the first cap ring (10 + n), i = 0 joins ring 1 to ring 2
                11, 1, 0, 10, 11, 0,
                12, 2, 1, 11, 12, 1,
                13, 3, 2, 12, 13, 2,
                14, 4, 3, 13, 14, 3,
                16, 11, 10, 15, 16, 10,
                17, 12, 11, 16, 17, 11,
                18, 13, 12, 17, 18, 12,
                19, 14, 13, 18, 19, 13,
                // end cap, mirrored winding: i = -1 joins the last body ring (5 + n) to the first cap ring (20 + n)
                6, 21, 20, 20, 5, 6,
                7, 22, 21, 21, 6, 7,
                8, 23, 22, 22, 7, 8,
                9, 24, 23, 23, 8, 9,
                21, 26, 25, 25, 20, 21,
                22, 27, 26, 26, 21, 22,
                23, 28, 27, 27, 22, 23,
                24, 29, 28, 28, 23, 24
            };
            Assert.That(ts.triangles.Skip(24).ToArray(), Is.EqualTo(expected));
        }

        // The cap V continues the body V: Clip starts at the sample percent and runs by half a size in percent
        // (0.5 / length = 0.05), UniformClip by half a size in length, Clamp and UniformClamp like their body modes. The end cap
        // reuses the scale of the start cap (0x4d8b57 only stores the offset): Clip begins at percent 1, UniformClip at the
        // length of the line up to it (10), Clamp at 1 and UniformClamp at the full length (10).
        [Test]
        public void RoundCapVContinuesTheBodyInEveryUvMode()
        {
            TubeGenerator tube = NewTube(4);
            tube.roundCapLatitude = 2;
            tube.capMode = TubeGenerator.CapMethod.Round;
            // mode, start ring 1 V, start ring 2 V, end ring 1 V, end ring 2 V
            object[][] table =
            {
                new object[] { MeshGenerator.UVMode.Clip, 0.025f, 0.05f, 1.025f, 1.05f },
                new object[] { MeshGenerator.UVMode.UniformClip, 0.25f, 0.5f, 10.25f, 10.5f },
                new object[] { MeshGenerator.UVMode.Clamp, 0.025f, 0.05f, 1.025f, 1.05f },
                new object[] { MeshGenerator.UVMode.UniformClamp, 0.25f, 0.5f, 10.25f, 10.5f }
            };
            foreach (object[] row in table)
            {
                tube.uvMode = (MeshGenerator.UVMode)row[0];
                tube.RebuildImmediate();
                TS_Mesh ts = Ts(tube);
                string mode = row[0].ToString();
                Near(ts.uv[11], new Vector2(0.25f, (float)row[1]), mode + " start ring 1");
                Near(ts.uv[16], new Vector2(0.25f, (float)row[2]), mode + " start ring 2");
                Near(ts.uv[21], new Vector2(0.25f, (float)row[3]), mode + " end ring 1");
                Near(ts.uv[26], new Vector2(0.25f, (float)row[4]), mode + " end ring 2");
            }

            // The twist subtracts V times the twist from U at the start and adds it at the end.
            tube.uvMode = MeshGenerator.UVMode.Clip;
            tube.uvTwist = 1f;
            tube.RebuildImmediate();
            TS_Mesh twisted = Ts(tube);
            Near(twisted.uv[10], new Vector2(-0.025f, 0.025f), "start cap ring 1, vertex 0");
            Near(twisted.uv[20], new Vector2(1.025f, 1.025f), "end cap ring 1, vertex 0");
        }

        // The same table on a clip range of 0.2..0.8 on the 10-unit line, where Clip and Clamp (and their uniform pairs) differ:
        // Clip starts at the sample percent and scales by 0.5 / CalculateLength(0, 1) (0x4d7ef7), UniformClip by half a size in
        // length from CalculateLength(0, percent) (0x4d7f3e), Clamp by 0.5 / CalculateLength(clipFrom, clipTo) (0x4d7f7e) and
        // UniformClamp by 0.5 / span (0x4d8ac3). The end cap starts at percent, CalculateLength(0, percent), 1 and
        // CalculateLength(0, 1) (table 0x4d94e8). Ring 1 of two sits at latitude percent 0.5.
        [Test]
        public void RoundCapVFollowsTheClipRangeInEveryUvMode()
        {
            TubeGenerator tube = NewTube(4);
            tube.roundCapLatitude = 2;
            tube.capMode = TubeGenerator.CapMethod.Round;
            tube.clipFrom = 0.2;
            tube.clipTo = 0.8;
            // mode, start ring 1 V, end ring 1 V
            object[][] table =
            {
                new object[] { MeshGenerator.UVMode.Clip, 0.225f, 0.825f },
                new object[] { MeshGenerator.UVMode.UniformClip, 2.25f, 8.25f },
                new object[] { MeshGenerator.UVMode.Clamp, 0.5f / 6f * 0.5f, 1f + 0.5f / 6f * 0.5f },
                new object[] { MeshGenerator.UVMode.UniformClamp, 0.5f / 0.6f * 0.5f, 10f + 0.5f / 0.6f * 0.5f }
            };
            foreach (object[] row in table)
            {
                tube.uvMode = (MeshGenerator.UVMode)row[0];
                tube.RebuildImmediate();
                TS_Mesh ts = Ts(tube);
                int bodyVertexCount = 5 * tube.sampleCount;
                int capVertexCount = 5 * tube.roundCapLatitude;
                string mode = row[0].ToString();
                Near(ts.uv[bodyVertexCount + 1], new Vector2(0.25f, (float)row[1]), mode + " start ring 1");
                Near(ts.uv[bodyVertexCount + capVertexCount + 1], new Vector2(0.25f, (float)row[2]), mode + " end ring 1");
            }
        }

        // The wrapper is 0.5 + AngleAxis(uvRotation + 180, forward) * (0.5 - coordinate) (body 0x4d7020 load, 0x4d7069 + 180,
        // 0x4d7073 AngleAxis, 0x4d714c + 0.5; start cap 0x4d85ec; end cap 0x4d9173). At 90 degrees the turn is 270, which maps
        // (x, y) to (y, -x): vertex 1 has coordinate (0.25, 0), so 0.5 - c = (0.25, 0.5) -> (0.5, -0.25) -> uv (1, 0.25). The
        // round cap vertex 1 of ring 1 has (0.25, 0.025) -> (0.475, -0.25) -> (0.975, 0.25).
        [Test]
        public void UvRotationTurnsTheBodyAndTheCapCoordinates()
        {
            TubeGenerator tube = NewTube(4);
            tube.roundCapLatitude = 2;
            tube.capMode = TubeGenerator.CapMethod.Round;
            tube.uvRotation = 90f;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            Near(ts.uv[1], new Vector2(1f, 0.25f), "body vertex 1");
            Near(ts.uv[11], new Vector2(0.975f, 0.25f), "start cap ring 1 vertex 1");
        }

        // Round caps scale by the sample size directly (0x4d83e3 mulss [r12+0x194] at the start, 0x4d8f6d at the end) while the
        // body ring uses GetBaseSize (0x4d6d87), which is 1 without the spline size.
        [Test]
        public void RoundCapsScaleBySampleSizeEvenWithoutSplineSize()
        {
            SplineComputer wide = _fixture.NewComputer(
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 2f, Color.white),
                new SplinePoint(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.up, 2f, Color.white));
            TubeGenerator tube = NewTube(4, wide);
            tube.roundCapLatitude = 1;
            tube.capMode = TubeGenerator.CapMethod.Round;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            Near(ts.vertices[0], new Vector3(0f, 0f, 1f), "body ring scaled by the spline size");
            Near(ts.vertices[10], new Vector3(-1f, 0f, 0f), "start apex");
            Near(ts.vertices[15], new Vector3(11f, 0f, 0f), "end apex");

            tube.useSplineSize = false;
            tube.RebuildImmediate();
            ts = Ts(tube);
            Near(ts.vertices[0], new Vector3(0f, 0f, 0.5f), "body ring without the spline size");
            Near(ts.vertices[10], new Vector3(-1f, 0f, 0f), "start apex keeps the sample size");
            Near(ts.vertices[15], new Vector3(11f, 0f, 0f), "end apex keeps the sample size");
        }

        // The offset moves the cap centres like the body centres. Latitude 1 leaves only the apex ring, which rotation cannot move, so rotation
        // is only observable on the body vertex.
        [Test]
        public void RoundCapCentresFollowTheOffset()
        {
            TubeGenerator tube = NewTube(4);
            tube.roundCapLatitude = 1;
            tube.capMode = TubeGenerator.CapMethod.Round;
            tube.offset = new Vector3(0f, 2f, 0f);
            tube.rotation = 90f;
            tube.RebuildImmediate();
            TS_Mesh ts = Ts(tube);
            // latitude 1 of 1 is the apex ring: centre (0,2,0) - (0.5,0,0) at the start and (10.5,2,0) at the end
            for (int n = 0; n < 5; n++)
            {
                Near(ts.vertices[10 + n], new Vector3(-0.5f, 2f, 0f), "start apex " + n);
                Near(ts.vertices[15 + n], new Vector3(10.5f, 2f, 0f), "end apex " + n);
            }

            Near(ts.vertices[0], new Vector3(0f, 1.5f, 0f), "body vertex 0 turned to 270 degrees around the offset centre");
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndMenu()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(TubeGenerator).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_sides", "_roundCapLatitude", "_capMode", "_revolve", "_capUVScale", "_uvTwist",
                "bodyVertexCount", "bodyTrisCount", "capVertexCount", "capTrisCount"
            }));
            foreach (string name in new[] { "_sides", "_roundCapLatitude", "_capMode", "_revolve", "_capUVScale", "_uvTwist" })
            {
                FieldInfo field = typeof(TubeGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            foreach (string name in new[] { "bodyVertexCount", "bodyTrisCount", "capVertexCount", "capTrisCount" })
            {
                FieldInfo field = typeof(TubeGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(0), name);
            }

            UnityEngine.RangeAttribute range = (UnityEngine.RangeAttribute)typeof(TubeGenerator).GetField("_revolve", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false).Single();
            Assert.That(range.min, Is.EqualTo(0f));
            Assert.That(range.max, Is.EqualTo(360f));
            AddComponentMenu menu = (AddComponentMenu)typeof(TubeGenerator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Tube Generator"));
            // Reflection does not guarantee the attribute order, so only the set is asserted.
            Assert.That(typeof(TubeGenerator).GetCustomAttributes(typeof(RequireComponent), false).Cast<RequireComponent>().Select(r => r.m_Type0).ToArray(),
                Is.EquivalentTo(new[] { typeof(MeshFilter), typeof(MeshRenderer) }));
            Assert.That(typeof(TubeGenerator).BaseType, Is.EqualTo(typeof(MeshGenerator)));

            System.Type capMethod = typeof(TubeGenerator.CapMethod);
            Assert.That(capMethod.IsNestedPublic, Is.True);
            Assert.That(System.Enum.GetNames(capMethod), Is.EqualTo(new[] { "None", "Flat", "Round" }));
            Assert.That(System.Enum.GetValues(capMethod).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
        }
    }
}
