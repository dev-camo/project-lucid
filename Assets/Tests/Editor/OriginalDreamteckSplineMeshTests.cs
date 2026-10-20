using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineMesh on the Linear line (0,0,0) -> (10,0,0) with the tetrahedron channel of SplineMeshFixture.
    // Every sample has forward (1,0,0), up (0,1,0) and right Cross(up, forward) = (0,0,-1), so the sample rotation sends the
    // local x axis to (0,0,-1), y to (0,1,0) and z to (1,0,0); the tetrahedron vertices are the origin and the three axes, so the
    // placed vertices read off the rotation and the scale directly. Expected values are hand arithmetic from the native bodies
    // cited per test (x86-64 addresses in the 0x4bc... / 0x4bd... / 0x4be... / 0x4bf... ranges). TearDown destroys every
    // game object and mesh a test made.
    public sealed class OriginalDreamteckSplineMeshTests
    {
        private readonly SplineMeshFixture _fixture = new SplineMeshFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            SplineMeshFixture.Near(actual, expected, message);
        }

        private static void Near(Vector2 actual, Vector2 expected, string message)
        {
            SplineMeshFixture.Near(actual, expected, message);
        }

        private static Vector3[] Vertices(SplineMesh generator)
        {
            return SplineMeshFixture.Ts(generator).vertices;
        }

        // The four vertices of one tetrahedron copy: its origin, then the images of the local x, y and z axes.
        private static void NearCopy(Vector3[] vertices, int copy, Vector3 origin, Vector3 x, Vector3 y, Vector3 z, string message)
        {
            Near(vertices[4 * copy], origin, message + " origin");
            Near(vertices[4 * copy + 1], origin + x, message + " x axis");
            Near(vertices[4 * copy + 2], origin + y, message + " y axis");
            Near(vertices[4 * copy + 3], origin + z, message + " z axis");
        }

        private static string Format(double percent)
        {
            return percent.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly Vector3 SampleX = new Vector3(0f, 0f, -1f);
        private static readonly Vector3 SampleY = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 SampleZ = new Vector3(1f, 0f, 0f);

        // get_meshName 0x4bc200 is the shipped literal; the channel list and the combine list start empty (ctor 0x4c0a30).
        [Test]
        public void FreshComponentKeepsTheShippedMeshName()
        {
            SplineMesh generator = _fixture.Generators.NewBare<SplineMesh>();
            Assert.That(DreamteckGeneratorFixture.Invoke(generator, "get_meshName"), Is.EqualTo("Custom Mesh"));
            Assert.That(DreamteckGeneratorFixture.Field<List<TS_Mesh>>(generator, "_combineMeshes"), Is.Empty);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_useLastResult"), Is.False);
            Assert.That(generator, Is.InstanceOf<MeshGenerator>());
        }

        // Reset 0x4bc240 runs the MeshGenerator reset, then adds the channel "Channel 1" (AddChannel 0x4bc290) without meshes.
        [Test]
        public void ResetAddsTheFirstChannel()
        {
            SplineMesh generator = _fixture.Generators.NewBare<SplineMesh>();
            int before = generator.GetChannelCount();
            DreamteckGeneratorFixture.Invoke(generator, "Reset");
            Assert.That(generator.GetChannelCount(), Is.EqualTo(before + 1));
            SplineMesh.Channel added = generator.GetChannel(before);
            Assert.That(added.name, Is.EqualTo("Channel 1"));
            Assert.That(added.GetMeshCount(), Is.EqualTo(0));
            Assert.That(DreamteckGeneratorFixture.Field<SplineMesh>(added, "owner") == generator, Is.True);
        }

        // AddChannel(string) 0x4bc290 only creates and stores the channel; AddChannel(Mesh, string) 0x4bc500 builds it with the mesh
        // as its first mesh, and that constructor (0x4bc5d0) asks the owner for a rebuild.
        [Test]
        public void AddChannelCreatesTheChannelAndOnlyTheMeshOverloadRebuilds()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            SplineMesh.Channel plain = generator.AddChannel("plain");
            Assert.That(plain.name, Is.EqualTo("plain"));
            Assert.That(plain.GetMeshCount(), Is.EqualTo(0));
            Assert.That(generator.GetChannelCount(), Is.EqualTo(1));
            Assert.That(generator.GetChannel(0), Is.SameAs(plain));
            Assert.That(DreamteckGeneratorFixture.Field<SplineMesh>(plain, "owner") == generator, Is.True);
            Assert.That(builds.Update(), Is.EqualTo(0), "no rebuild for a channel without mesh");

            Mesh tetra = _fixture.Tetra();
            SplineMesh.Channel withMesh = generator.AddChannel(tetra, "withMesh");
            Assert.That(withMesh.name, Is.EqualTo("withMesh"));
            Assert.That(withMesh.GetMeshCount(), Is.EqualTo(1));
            Assert.That(withMesh.GetMesh(0).mesh, Is.SameAs(tetra));
            Assert.That(generator.GetChannelCount(), Is.EqualTo(2));
            Assert.That(generator.GetChannel(1), Is.SameAs(withMesh));
            Assert.That(builds.Update(), Is.EqualTo(1), "one rebuild for the mesh channel");

            Assert.That(() => generator.GetChannel(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => generator.GetChannel(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // RemoveChannel 0x4bc360 removes by index and rebuilds; a bad index throws before the rebuild.
        [Test]
        public void RemoveChannelRebuildsAndRejectsABadIndex()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel first = generator.AddChannel("first");
            SplineMesh.Channel second = generator.AddChannel("second");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            Assert.That(() => generator.RemoveChannel(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => generator.RemoveChannel(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(generator.GetChannelCount(), Is.EqualTo(2));
            Assert.That(builds.Update(), Is.EqualTo(0), "a bad index queues nothing");

            generator.RemoveChannel(0);
            Assert.That(generator.GetChannelCount(), Is.EqualTo(1));
            Assert.That(generator.GetChannel(0), Is.SameAs(second));
            Assert.That(builds.Update(), Is.EqualTo(1));
            Assert.That(first.name, Is.EqualTo("first"));
        }

        // SwapChannels 0x4bc3d0 changes nothing for an index outside the list, otherwise exchanges the two channels and rebuilds
        // (also for equal indices).
        [Test]
        public void SwapChannelsExchangesTheChannelsAndIgnoresBadIndices()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel a = generator.AddChannel("a");
            SplineMesh.Channel b = generator.AddChannel("b");
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            foreach (int[] bad in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 3, 0 }, new[] { 0, 3 }, new[] { 5, 7 } })
            {
                generator.SwapChannels(bad[0], bad[1]);
                Assert.That(builds.Update(), Is.EqualTo(0), "(" + bad[0] + ", " + bad[1] + ") queues nothing");
                Assert.That(generator.GetChannel(0), Is.SameAs(a));
                Assert.That(generator.GetChannel(1), Is.SameAs(b));
                Assert.That(generator.GetChannel(2), Is.SameAs(c));
            }

            generator.SwapChannels(0, 2);
            Assert.That(builds.Update(), Is.EqualTo(1));
            Assert.That(generator.GetChannel(0), Is.SameAs(c));
            Assert.That(generator.GetChannel(1), Is.SameAs(b));
            Assert.That(generator.GetChannel(2), Is.SameAs(a));

            generator.SwapChannels(1, 1);
            Assert.That(builds.Update(), Is.EqualTo(1), "equal indices still rebuild");
            Assert.That(generator.GetChannel(1), Is.SameAs(b));
        }

        // Generate 0x4bcee0 clears the mesh when no channel contributes a copy: no channels, or channels without meshes.
        [Test]
        public void NoChannelsOrMeshesClearTheMesh()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 2);
            generator.RebuildImmediate();
            Assert.That(Vertices(generator).Length, Is.EqualTo(8));

            channel.RemoveMesh(0);
            generator.RebuildImmediate();
            Assert.That(Vertices(generator).Length, Is.EqualTo(0), "a channel without meshes adds no copy");

            generator.RemoveChannel(0);
            generator.RebuildImmediate();
            Assert.That(Vertices(generator).Length, Is.EqualTo(0));
            Assert.That(DreamteckGeneratorFixture.Field<List<TS_Mesh>>(generator, "_combineMeshes").Count, Is.EqualTo(2), "the combine list keeps its size when the mesh is cleared");
        }

        // Generate skips a channel without meshes in the count loop (0x4bd013 past the add at 0x4bcfa0) and in the build loop (0x4bd5f7 to
        // 0x4bd580), so it takes no combine slot between the channels that have meshes.
        [Test]
        public void AChannelWithoutMeshesBetweenOthersTakesNoCombineSlot()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            generator.AddChannel("empty");
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            generator.RebuildImmediate();

            List<TS_Mesh> combine = DreamteckGeneratorFixture.Field<List<TS_Mesh>>(generator, "_combineMeshes");
            Assert.That(combine.Count, Is.EqualTo(2));
            Assert.That(combine.All(mesh => mesh != null), Is.True);
            Assert.That(Vertices(generator).Length, Is.EqualTo(8));
        }

        // Generate 0x4bcee0 / Place 0x4beb70: a Place channel stamps `count` copies at Lerp(clipFrom, clipTo, i / max(count - 1, 1)),
        // here the percents 0, 0.5 and 1: the origin of each copy lies on the line and its axes follow the sample frame.
        [Test]
        public void PlaceStampsOneCopyPerCountAtEvenPercents()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            generator.RebuildImmediate();

            Vector3[] vertices = Vertices(generator);
            Assert.That(vertices.Length, Is.EqualTo(12));
            for (int copy = 0; copy < 3; copy++)
            {
                NearCopy(vertices, copy, new Vector3(5f * copy, 0f, 0f), SampleX, SampleY, SampleZ, "copy " + copy);
            }

            int[] triangles = SplineMeshFixture.Ts(generator).triangles;
            Assert.That(triangles.Length, Is.EqualTo(36));
            Assert.That(triangles.Take(12).ToArray(), Is.EqualTo(new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2 }));
            Assert.That(triangles.Skip(12).Take(12).ToArray(), Is.EqualTo(new[] { 4, 6, 5, 4, 5, 7, 5, 6, 7, 4, 7, 6 }), "triangles are rebased per copy");
        }

        // The channel clip range maps the copy index to the percent: Lerp(0.4, 0.9, i / (count - 1)).
        [Test]
        public void PlaceFollowsTheChannelClipRange()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 2);
            channel.clipFrom = 0.4;
            channel.clipTo = 0.9;
            generator.RebuildImmediate();

            Vector3[] vertices = Vertices(generator);
            Near(vertices[0], new Vector3(4f, 0f, 0f), "first copy at the clip start");
            Near(vertices[4], new Vector3(9f, 0f, 0f), "second copy at the clip end");

            channel.count = 1;
            generator.RebuildImmediate();
            Assert.That(Vertices(generator).Length, Is.EqualTo(4));
            Near(Vertices(generator)[0], new Vector3(4f, 0f, 0f), "a single copy stands at the clip start");
        }

        // Place: the offset is (random offset + custom offset + generator offset.xy) times the sample size along right and up,
        // plus generator offset.z along forward (not scaled).
        [Test]
        public void PlaceOffsetsMoveAlongTheSampleFrame()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.minOffset = new Vector2(2f, 3f);
            generator.offset = new Vector3(0.25f, 0.75f, 0.5f);
            generator.RebuildImmediate();
            Vector3 origin = new Vector3(0.5f, 3.75f, -2.25f);
            NearCopy(Vertices(generator), 0, origin, SampleX, SampleY, SampleZ, "size 1");

            generator.spline = _fixture.SizedLine(2f, Color.white);
            generator.RebuildImmediate();
            origin = new Vector3(0.5f, 7.5f, -4.5f);
            NearCopy(Vertices(generator), 0, origin, 2f * SampleX, 2f * SampleY, 2f * SampleZ, "size 2: the xy offsets and the scale follow the sample size, the forward offset does not");
        }

        // Place: the rotation is sample * (channel euler * AngleAxis(generator rotation, forward) * custom); the euler rotation of the
        // channel (90, 0, 0) turns the y axis onto z and z onto -y.
        [Test]
        public void PlaceRotationsComposeInTheShippedOrder()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.minRotation = new Vector3(90f, 0f, 0f);
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, Vector3.zero, new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f), "channel rotation");

            generator.rotation = 90f;
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f), "the generator rotation is applied first");
        }

        // Place 0x4beff6-0x4bf18e: with distinct channel (Rx 90), generator (Rz 90) and custom (Ry 90) rotations the order is
        // sample * ((channel * generator) * custom), so a local vector is turned by Ry, then Rz, then Rx, then the sample frame:
        // x -> -z -> -z -> +y -> +y, y -> y -> -x -> -x -> +z and z -> +x -> +y -> +z -> +x. Other orders put the x axis elsewhere.
        [Test]
        public void PlaceCustomRotationComesLastAfterTheRandomAndGeneratorRotations()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.minRotation = new Vector3(90f, 0f, 0f);
            channel.placeRotationHandler = percent => Quaternion.Euler(0f, 90f, 0f);
            generator.rotation = 90f;
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, Vector3.zero, new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), "channel, generator, custom");
        }

        // Place: scale = minScale * sample size; the normals use the inverse transpose, so (0,1,0) shrinks by the y scale.
        [Test]
        public void PlaceScaleAndSampleSizeScaleTheCopyAndTheNormals()
        {
            SplineMesh generator = _fixture.NewSplineMesh(_fixture.SizedLine(2f, Color.white));
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.minScale = new Vector3(2f, 3f, 4f);
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, Vector3.zero, 4f * SampleX, 6f * SampleY, 8f * SampleZ, "scale (2,3,4) times size 2");
            foreach (Vector3 normal in SplineMeshFixture.Ts(generator).normals)
            {
                Near(normal, new Vector3(0f, 1f / 6f, 0f), "inverse transpose of the scale (4,6,8)");
            }

            // Place reads the size through GetBaseSize 0x49e230, which honours useSplineSize.
            generator.useSplineSize = false;
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, Vector3.zero, 2f * SampleX, 3f * SampleY, 4f * SampleZ, "without the spline size");
        }

        // Place multiplies sample colour, mesh colour and generator colour; it reads the sample colour directly, so useSplineColor does
        // not matter.
        [Test]
        public void PlaceColoursMultiplySampleMeshAndGeneratorColours()
        {
            SplineMesh generator = _fixture.NewSplineMesh(_fixture.SizedLine(1f, new Color(0.5f, 1f, 1f, 1f)));
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            generator.color = new Color(0.5f, 0.5f, 1f, 1f);
            generator.RebuildImmediate();
            Color expected = new Color(0.25f, 0.5f, 1f, 1f);
            foreach (Color color in SplineMeshFixture.Ts(generator).colors)
            {
                SplineMeshFixture.Near(color, expected, "sample * white * generator");
            }

            generator.useSplineColor = false;
            generator.RebuildImmediate();
            foreach (Color color in SplineMeshFixture.Ts(generator).colors)
            {
                SplineMeshFixture.Near(color, expected, "useSplineColor is not consulted");
            }
        }

        // Place with a custom normal: forward = Cross(right, normal) and up = normal replace the sample vectors for the rotation, but the
        // position keeps using the sample's own up/right/forward (here the y offset of 2 stays along the sample up).
        [Test]
        public void PlaceCustomNormalReplacesTheSampleFrameAfterTheOffsets()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.minOffset = new Vector2(0f, 2f);
            channel.overrideNormal = true;
            channel.customNormal = new Vector3(1f, 0f, 0f);
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f), "custom frame");

            channel.overrideNormal = false;
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, new Vector3(0f, 2f, 0f), SampleX, SampleY, SampleZ, "sample frame");
        }

        // Place: the scale modifier sees the percent in spline space unless it works in clipped percent. With the generator clip
        // 0.5..1 the copy at clipped percent 0.8 is at spline percent 0.9; a key weighs 1 between its centre 0.25..0.75 and falls
        // linearly to 0 at 1: weight 0.8 at 0.8 (clipped) and 0.4 at 0.9 (spline).
        [Test]
        public void PlaceScaleModifierUsesThePercentSpaceOfItsFlag()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            generator.SetClipRange(0.5, 1.0);
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.clipFrom = 0.8;
            channel.scaleModifier.AddKey(0.0, 1.0);
            channel.scaleModifier.keys[0].scale = new Vector3(2f, 2f, 2f);

            channel.scaleModifier.useClippedPercent = false;
            generator.RebuildImmediate();
            float spline = 1f + 0.4f;
            NearCopy(Vertices(generator), 0, new Vector3(9f, 0f, 0f), spline * SampleX, spline * SampleY, spline * SampleZ, "spline percent 0.9");

            channel.scaleModifier.useClippedPercent = true;
            generator.RebuildImmediate();
            float clipped = 1f + 0.8f;
            NearCopy(Vertices(generator), 0, new Vector3(9f, 0f, 0f), clipped * SampleX, clipped * SampleY, clipped * SampleZ, "clipped percent 0.8");
        }

        // Generate restarts the random sequences of the channel (ResetIteration 0x4bdcf0) before it places the copies, and Place draws two
        // offset values, three rotation angles (x, y, z) and three scale factors (x, y, z) per copy, each from its own sequence. Two
        // builds therefore give the same copies.
        [Test]
        public void PlaceDrawsEachRandomSequenceInOrderAndRestartsItPerBuild()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 2);
            channel.randomOffset = true;
            channel.offsetSeed = 42;
            channel.minOffset = Vector2.zero;
            channel.maxOffset = new Vector2(2f, 4f);
            channel.randomRotation = true;
            channel.rotationSeed = 7;
            channel.minRotation = Vector3.zero;
            channel.maxRotation = new Vector3(0f, 0f, 90f);
            channel.randomScale = true;
            channel.scaleSeed = 11;
            channel.minScale = Vector3.one;
            channel.maxScale = new Vector3(3f, 3f, 3f);
            generator.RebuildImmediate();

            System.Random offsets = new System.Random(42);
            System.Random rotations = new System.Random(7);
            System.Random scales = new System.Random(11);
            Vector3[] vertices = Vertices(generator);
            for (int copy = 0; copy < 2; copy++)
            {
                float ox = Mathf.Lerp(0f, 2f, (float)offsets.NextDouble());
                float oy = Mathf.Lerp(0f, 4f, (float)offsets.NextDouble());
                rotations.NextDouble();
                rotations.NextDouble();
                float angle = Mathf.Lerp(0f, 90f, (float)rotations.NextDouble()) * Mathf.Deg2Rad;
                float sx = Mathf.Lerp(1f, 3f, (float)scales.NextDouble());
                float sy = Mathf.Lerp(1f, 3f, (float)scales.NextDouble());
                float sz = Mathf.Lerp(1f, 3f, (float)scales.NextDouble());
                Vector3 origin = new Vector3(10f * copy, oy, -ox);
                Vector3 xAxis = sx * (Mathf.Cos(angle) * SampleX + Mathf.Sin(angle) * SampleY);
                Vector3 yAxis = sy * (-Mathf.Sin(angle) * SampleX + Mathf.Cos(angle) * SampleY);
                NearCopy(vertices, copy, origin, xAxis, yAxis, sz * SampleZ, "copy " + copy);
            }

            Vector3[] first = (Vector3[])vertices.Clone();
            generator.RebuildImmediate();
            Vector3[] second = Vertices(generator);
            Assert.That(second.Length, Is.EqualTo(first.Length));
            for (int i = 0; i < first.Length; i++)
            {
                Near(second[i], first[i], "vertex " + i + " of the second build");
            }
        }

        // GetCustomPlaceValues 0x4c0260 runs the handlers (offset, rotation, scale) with the copy percent: offset + (1, 0) moves along
        // right, the scale handler's (2,2,2) multiplies the scale and the rotation handler turns the copy by 90 degrees about z.
        [Test]
        public void PlaceHandlersAddTheirValuesAndSeeThePlacePercent()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            List<string> calls = new List<string>();
            channel.offsetHandler = percent => { calls.Add("offset " + Format(percent)); return new Vector2(1f, 0f); };
            channel.placeRotationHandler = percent => { calls.Add("rotation " + Format(percent)); return Quaternion.Euler(0f, 0f, 90f); };
            channel.extrudeRotationHandler = percent => { calls.Add("extrude " + Format(percent)); return 45f; };
            channel.scaleHandler = percent => { calls.Add("scale " + Format(percent)); return new Vector3(2f, 2f, 2f); };
            calls.Clear();
            generator.RebuildImmediate();

            Assert.That(calls, Is.EqualTo(new[]
            {
                "offset 0", "rotation 0", "scale 0",
                "offset 0.5", "rotation 0.5", "scale 0.5",
                "offset 1", "rotation 1", "scale 1"
            }));
            Vector3[] vertices = Vertices(generator);
            for (int copy = 0; copy < 3; copy++)
            {
                // Rz(90) maps x to y and y to -x before the sample rotation; the scale is 2.
                NearCopy(vertices, copy, new Vector3(5f * copy, 0f, -1f), 2f * SampleY, 2f * -SampleX, 2f * SampleZ, "copy " + copy);
            }
        }

        // Extrude 0x4bde20: the vertex group at z 1 is evaluated at the end of the span and the group at z 0 at its start; the z of
        // the vertices is dropped (the depth is the position along the spline).
        [Test]
        public void ExtrudeBendsTheVertexGroupsOntoTheSpline()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            generator.RebuildImmediate();

            Vector3[] vertices = Vertices(generator);
            Assert.That(vertices.Length, Is.EqualTo(4));
            Near(vertices[0], new Vector3(0f, 0f, 0f), "group z 0, origin");
            Near(vertices[1], new Vector3(0f, 0f, -1f), "group z 0, x axis");
            Near(vertices[2], new Vector3(0f, 1f, 0f), "group z 0, y axis");
            Near(vertices[3], new Vector3(10f, 0f, 0f), "group z 1 lies at the end of the span with its z dropped");

            channel.count = 2;
            generator.RebuildImmediate();
            vertices = Vertices(generator);
            Assert.That(vertices.Length, Is.EqualTo(8));
            Near(vertices[3], new Vector3(5f, 0f, 0f), "first slice ends at 0.5");
            Near(vertices[4], new Vector3(5f, 0f, 0f), "second slice starts at 0.5");
            Near(vertices[5], new Vector3(5f, 0f, -1f), "second slice x axis");
            Near(vertices[7], new Vector3(10f, 0f, 0f), "second slice ends at 1");
        }

        // Generate: the half spacing is spacing / count / 2; each slice is inset by it at both ends. _useLastResult is set after an
        // Extrude channel without spacing and cleared at the start of every channel.
        [Test]
        public void ExtrudeSpacingInsetsEachSliceAndSetsTheLastResultFlag()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel extrude = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 2);
            extrude.spacing = 0.2;
            generator.RebuildImmediate();

            Vector3[] vertices = Vertices(generator);
            Near(vertices[0], new Vector3(0.5f, 0f, 0f), "first slice start 0.05");
            Near(vertices[3], new Vector3(4.5f, 0f, 0f), "first slice end 0.45");
            Near(vertices[4], new Vector3(5.5f, 0f, 0f), "second slice start 0.55");
            Near(vertices[7], new Vector3(9.5f, 0f, 0f), "second slice end 0.95");
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_useLastResult"), Is.False, "spacing keeps the flag cleared");

            extrude.spacing = 0.0;
            generator.RebuildImmediate();
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_useLastResult"), Is.True, "no spacing sets it");

            SplineMesh.Channel place = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            generator.RebuildImmediate();
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_useLastResult"), Is.False, "a later Place channel clears it");

            generator.SwapChannels(0, 1);
            generator.RebuildImmediate();
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_useLastResult"), Is.True, "an Extrude channel last sets it");
            Assert.That(place.type, Is.EqualTo(SplineMesh.Channel.Type.Place));
        }

        // Extrude: offset = (custom + (generator offset.xy + random)) * the modified sample size (also with useSplineSize off); the
        // angle is custom + (generator rotation + the z angle of the channel); the scale is (random x, random y, 1) * size; the
        // normals use the inverse transpose.
        [Test]
        public void ExtrudeOffsetAngleAndScaleUseXyAndTheZAngleOnly()
        {
            SplineMesh generator = _fixture.NewSplineMesh(_fixture.SizedLine(2f, Color.white));
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            channel.minOffset = new Vector2(2f, 3f);
            channel.minRotation = new Vector3(30f, 40f, 60f);
            channel.minScale = new Vector3(2f, 3f, 5f);
            generator.rotation = 30f;
            generator.offset = new Vector3(0f, 0f, 0.5f);
            generator.useSplineSize = false;
            generator.RebuildImmediate();

            // position (x + 0.5, 6, -4); Rz(90) maps x to y and y to -x, the scale is (4, 6, 2).
            Vector3[] vertices = Vertices(generator);
            Near(vertices[0], new Vector3(0.5f, 6f, -4f), "origin");
            Near(vertices[1], new Vector3(0.5f, 10f, -4f), "x axis scaled by 4, turned onto y");
            Near(vertices[2], new Vector3(0.5f, 6f, 2f), "y axis scaled by 6, turned onto -x and then onto +z");
            Near(vertices[3], new Vector3(10.5f, 6f, -4f), "the far group, z dropped");
            foreach (Vector3 normal in SplineMeshFixture.Ts(generator).normals)
            {
                Near(normal, new Vector3(0f, 0f, 1f / 6f), "inverse transpose of the scale (4,6,2)");
            }
        }

        // Extrude 0x4bde20 draws once per copy, before its vertex-group loop (0x4be191): two offset values (0x4bdf32-0x4be007), two scale
        // values with z constant 1 (0x4be066) and one rotation value for the z angle (0x4be076-0x4be0e7). Both groups of a copy
        // therefore carry the same offset, angle and xy scale, and copy k uses the draws 2k and 2k+1 (offset, scale) and k (rotation).
        [Test]
        public void ExtrudeDrawsOncePerCopyAndSharesTheDrawsBetweenItsGroups()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 2);
            channel.randomOffset = true;
            channel.offsetSeed = 42;
            channel.minOffset = Vector2.zero;
            channel.maxOffset = new Vector2(2f, 4f);
            channel.randomRotation = true;
            channel.rotationSeed = 7;
            channel.minRotation = Vector3.zero;
            channel.maxRotation = new Vector3(0f, 0f, 90f);
            channel.randomScale = true;
            channel.scaleSeed = 11;
            channel.minScale = Vector3.one;
            channel.maxScale = new Vector3(3f, 3f, 3f);
            generator.RebuildImmediate();

            System.Random offsets = new System.Random(42);
            System.Random rotations = new System.Random(7);
            System.Random scales = new System.Random(11);
            Vector3[] vertices = Vertices(generator);
            Assert.That(vertices.Length, Is.EqualTo(8));
            for (int copy = 0; copy < 2; copy++)
            {
                float ox = Mathf.Lerp(0f, 2f, (float)offsets.NextDouble());
                float oy = Mathf.Lerp(0f, 4f, (float)offsets.NextDouble());
                float angle = Mathf.Lerp(0f, 90f, (float)rotations.NextDouble()) * Mathf.Deg2Rad;
                float sx = Mathf.Lerp(1f, 3f, (float)scales.NextDouble());
                float sy = Mathf.Lerp(1f, 3f, (float)scales.NextDouble());
                Vector3 origin = new Vector3(5f * copy, oy, -ox);
                Vector3 xAxis = sx * (Mathf.Cos(angle) * SampleX + Mathf.Sin(angle) * SampleY);
                Vector3 yAxis = sy * (-Mathf.Sin(angle) * SampleX + Mathf.Cos(angle) * SampleY);
                Near(vertices[4 * copy], origin, "copy " + copy + " near origin");
                Near(vertices[4 * copy + 1], origin + xAxis, "copy " + copy + " x axis");
                Near(vertices[4 * copy + 2], origin + yAxis, "copy " + copy + " y axis");
                Near(vertices[4 * copy + 3], origin + new Vector3(5f, 0f, 0f), "copy " + copy + " far group shares the draws");
            }
        }

        // Extrude: a custom normal replaces forward and up for the rotation only; the offset still follows the sample up (y offset 2).
        [Test]
        public void ExtrudeCustomNormalReplacesTheSampleFrameAfterTheOffsets()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            channel.minOffset = new Vector2(0f, 2f);
            channel.overrideNormal = true;
            channel.customNormal = new Vector3(1f, 0f, 0f);
            generator.RebuildImmediate();

            Vector3[] vertices = Vertices(generator);
            Near(vertices[0], new Vector3(0f, 2f, 0f), "origin");
            Near(vertices[1], new Vector3(0f, 2f, -1f), "x axis");
            Near(vertices[2], new Vector3(1f, 2f, 0f), "y axis follows the custom normal");
            Near(vertices[3], new Vector3(10f, 2f, 0f), "far group");
        }

        // Extrude multiplies the modified sample colour, the colour written by the mesh and the generator colour.
        [Test]
        public void ExtrudeColoursMultiplySampleMeshAndGeneratorColours()
        {
            SplineMesh generator = _fixture.NewSplineMesh(_fixture.SizedLine(1f, new Color(0.5f, 1f, 1f, 1f)));
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            generator.color = new Color(0.5f, 0.5f, 1f, 1f);
            generator.useSplineColor = false;
            generator.RebuildImmediate();
            foreach (Color color in SplineMeshFixture.Ts(generator).colors)
            {
                SplineMeshFixture.Near(color, new Color(0.25f, 0.5f, 1f, 1f), "sample * white * generator");
            }
        }

        // Extrude: UVOverride None keeps the mesh uv; ClampU/V write the percent of the group into u/v and UniformU/V the spline length up to
        // it (the far group is at percent 1 = length 10). Then channel scale * (uv * generator scale) is moved by channel offset +
        // generator offset.
        [Test]
        public void ExtrudeUvOverridesFollowTheMode()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            channel.uvScale = new Vector2(2f, 3f);
            channel.uvOffset = new Vector2(0.5f, 0.25f);

            generator.RebuildImmediate();
            Near(SplineMeshFixture.Ts(generator).uv[0], new Vector2(0.5f, 0.25f), "None v0");
            Near(SplineMeshFixture.Ts(generator).uv[1], new Vector2(2.5f, 0.25f), "None v1");
            Near(SplineMeshFixture.Ts(generator).uv[2], new Vector2(0.5f, 3.25f), "None v2");
            Near(SplineMeshFixture.Ts(generator).uv[3], new Vector2(1.5f, 1.75f), "None v3");

            generator.uvScale = new Vector2(2f, 1f);
            generator.uvOffset = new Vector2(0.1f, 0f);
            generator.RebuildImmediate();
            Near(SplineMeshFixture.Ts(generator).uv[0], new Vector2(0.6f, 0.25f), "generator transform v0");
            Near(SplineMeshFixture.Ts(generator).uv[1], new Vector2(4.6f, 0.25f), "generator transform v1");
            Near(SplineMeshFixture.Ts(generator).uv[2], new Vector2(0.6f, 3.25f), "generator transform v2");
            Near(SplineMeshFixture.Ts(generator).uv[3], new Vector2(2.6f, 1.75f), "generator transform v3");
            generator.uvScale = Vector2.one;
            generator.uvOffset = Vector2.zero;

            channel.overrideUVs = SplineMesh.Channel.UVOverride.ClampU;
            generator.RebuildImmediate();
            Near(SplineMeshFixture.Ts(generator).uv[1], new Vector2(0.5f, 0.25f), "ClampU v1: u is the percent 0");
            Near(SplineMeshFixture.Ts(generator).uv[3], new Vector2(2.5f, 1.75f), "ClampU v3: u is the percent 1");

            channel.overrideUVs = SplineMesh.Channel.UVOverride.ClampV;
            generator.RebuildImmediate();
            Near(SplineMeshFixture.Ts(generator).uv[2], new Vector2(0.5f, 0.25f), "ClampV v2: v is the percent 0");
            Near(SplineMeshFixture.Ts(generator).uv[3], new Vector2(1.5f, 3.25f), "ClampV v3: v is the percent 1");

            channel.overrideUVs = SplineMesh.Channel.UVOverride.UniformU;
            generator.RebuildImmediate();
            Near(SplineMeshFixture.Ts(generator).uv[1], new Vector2(0.5f, 0.25f), "UniformU v1: length 0");
            Near(SplineMeshFixture.Ts(generator).uv[3], new Vector2(20.5f, 1.75f), "UniformU v3: u is the length 10");

            channel.overrideUVs = SplineMesh.Channel.UVOverride.UniformV;
            generator.RebuildImmediate();
            Near(SplineMeshFixture.Ts(generator).uv[2], new Vector2(0.5f, 0.25f), "UniformV v2: length 0");
            Near(SplineMeshFixture.Ts(generator).uv[3], new Vector2(1.5f, 30.25f), "UniformV v3: v is the length 10");
        }

        // GetCustomExtrudeValues 0x4c08a0 runs the offset, the extrude rotation and the scale handler with the percent of every vertex
        // group, the far group (percent 1) first. The rotation handler of Place is not used.
        [Test]
        public void ExtrudeHandlersSeeThePercentOfEachGroup()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            List<string> calls = new List<string>();
            channel.offsetHandler = percent => { calls.Add("offset " + Format(percent)); return new Vector2(1f, 2f); };
            channel.placeRotationHandler = percent => { calls.Add("place " + Format(percent)); return Quaternion.identity; };
            channel.extrudeRotationHandler = percent => { calls.Add("extrude " + Format(percent)); return 90f; };
            channel.scaleHandler = percent => { calls.Add("scale " + Format(percent)); return new Vector3(2f, 2f, 2f); };
            calls.Clear();
            generator.RebuildImmediate();

            Assert.That(calls, Is.EqualTo(new[] { "offset 1", "extrude 1", "scale 1", "offset 0", "extrude 0", "scale 0" }));
            Vector3[] vertices = Vertices(generator);
            Near(vertices[0], new Vector3(0f, 2f, -1f), "origin: offset (1,2) along right and up");
            Near(vertices[1], new Vector3(0f, 4f, -1f), "x axis scaled by 2 and turned by 90 degrees onto y");
            Near(vertices[2], new Vector3(0f, 2f, 1f), "y axis scaled by 2 and turned onto -x, then onto +z");
            Near(vertices[3], new Vector3(10f, 2f, -1f), "far group");
        }

        // Generate: an auto count channel fits the clipped length / average mesh length copies, rounded to even, at least one. A zero
        // average length leaves the count alone.
        [Test]
        public void AutoCountFitsTheCopiesToTheClippedLength()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            channel.autoCount = true;
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(10), "length 10 / bounds depth 1");
            Assert.That(Vertices(generator).Length, Is.EqualTo(40));

            channel.GetMesh(0).scale = new Vector3(1f, 1f, 4f);
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(2), "10 / 4 = 2.5 rounds to even, not to 3");

            channel.clipTo = 0.5;
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(1), "5 / 4 = 1.25");

            channel.clipTo = 0.01;
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(1), "0.1 / 4 rounds to 0 and the count is raised to 1");
            Assert.That(Vertices(generator).Length, Is.EqualTo(4));

            channel.clipTo = 1.0;
            SplineMesh.Channel.MeshDefinition second = new SplineMesh.Channel.MeshDefinition(_fixture.Tetra());
            second.scale = new Vector3(1f, 1f, 2f);
            channel.AddMesh(second);
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(3), "10 / ((4 + 2) / 2)");
        }

        // The average is not taken for a mesh whose bounds are flat along z (a front spacing of -1 cancels the depth), so the count stays.
        [Test]
        public void AutoCountKeepsTheCountForAFlatAverage()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            channel.autoCount = true;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "The size of [Tetra]'s bounds is too small! This could cause an issue if the [Auto Count] option is enabled!");
            channel.GetMesh(0).spacing = new SplineMesh.Channel.BoundsSpacing { front = -1f, back = 0f };
            Assert.That(channel.GetMesh(0).bounds.size.z, Is.EqualTo(0f).Within(1e-6f));
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(3));
            Assert.That(Vertices(generator).Length, Is.EqualTo(12));
        }

        // Native 0x4bd236 is `ucomiss average, 0.0; jbe skip`: the count is only fitted for an average greater than zero, so a negative
        // depth (front spacing -2 turns the bounds into z 0..-1) keeps the count as well.
        [Test]
        public void AutoCountKeepsTheCountForANegativeAverage()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            channel.autoCount = true;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "The size of [Tetra]'s bounds is too small! This could cause an issue if the [Auto Count] option is enabled!");
            channel.GetMesh(0).spacing = new SplineMesh.Channel.BoundsSpacing { front = -2f, back = 0f };
            Assert.That(channel.GetMesh(0).bounds.size.z, Is.EqualTo(-1f).Within(1e-6f));
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(3));
            Assert.That(Vertices(generator).Length, Is.EqualTo(12));
        }

        // Generate keeps one combine mesh per copy: the list grows by null entries and shrinks by RemoveRange(Count - 1 - difference,
        // difference), which keeps the first and the last objects when the list shrinks from five to three.
        [Test]
        public void CombineListFollowsTheTotalCopyCount()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel first = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 2);
            generator.RebuildImmediate();
            List<TS_Mesh> combine = DreamteckGeneratorFixture.Field<List<TS_Mesh>>(generator, "_combineMeshes");
            Assert.That(combine.Count, Is.EqualTo(5));
            Assert.That(combine.All(mesh => mesh != null), Is.True);
            Assert.That(Vertices(generator).Length, Is.EqualTo(20));
            TS_Mesh[] before = combine.ToArray();

            first.count = 1;
            generator.RebuildImmediate();
            Assert.That(combine.Count, Is.EqualTo(3));
            Assert.That(combine[0], Is.SameAs(before[0]));
            Assert.That(combine[1], Is.SameAs(before[1]), "removing starts at index 2: the second object stays");
            Assert.That(combine[2], Is.SameAs(before[4]), "and the last one moves up");
            Assert.That(Vertices(generator).Length, Is.EqualTo(12));

            first.count = 6;
            generator.RebuildImmediate();
            Assert.That(combine.Count, Is.EqualTo(8));
            Assert.That(combine.All(mesh => mesh != null), Is.True, "new entries are created before they are used");
            Assert.That(Vertices(generator).Length, Is.EqualTo(32));
        }

        // Generate 0x4bcee0 moves the combine index only inside the Extrude and Place loops (0x4bd587 reached from the loop exits
        // 0x4bd749 and 0x4bdb60); the default path 0x4bd580 reloads the old index. A channel with an unknown serialized type
        // therefore leaves its slots unwritten and the next channel takes the slot right after the previous copy. The unwritten
        // slots stay null, which the final Combine then trips over.
        [Test]
        public void UnknownChannelTypeDoesNotAdvanceTheCombineIndex()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            SplineMesh.Channel unknown = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 2);
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            typeof(SplineMesh.Channel).GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(unknown, (SplineMesh.Channel.Type)7);

            Assert.Throws<NullReferenceException>(() => generator.RebuildImmediate());

            List<TS_Mesh> combine = DreamteckGeneratorFixture.Field<List<TS_Mesh>>(generator, "_combineMeshes");
            Assert.That(combine.Count, Is.EqualTo(4));
            Assert.That(combine[0], Is.Not.Null, "first channel");
            Assert.That(combine[1], Is.Not.Null, "third channel takes the next slot");
            Assert.That(combine[2], Is.Null);
            Assert.That(combine[3], Is.Null);
        }

        // Write 0x4bf6a0 with a material id puts every triangle into that submesh and empties the others.
        [Test]
        public void MaterialIdWritesTheTrianglesIntoThatSubmesh()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 2);
            generator.RebuildImmediate();
            Assert.That(SplineMeshFixture.Ts(generator).subMeshes.Count, Is.EqualTo(1));
            Assert.That(SplineMeshFixture.Ts(generator).subMeshes[0].Length, Is.EqualTo(24));

            channel.overrideMaterialID = true;
            channel.targetMaterialID = 2;
            generator.RebuildImmediate();
            List<int[]> subMeshes = SplineMeshFixture.Ts(generator).subMeshes;
            Assert.That(subMeshes.Count, Is.EqualTo(3));
            Assert.That(subMeshes[0].Length, Is.EqualTo(0));
            Assert.That(subMeshes[1].Length, Is.EqualTo(0));
            Assert.That(subMeshes[2].Length, Is.EqualTo(24));
            Assert.That(subMeshes[2].Take(12).ToArray(), Is.EqualTo(new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2 }));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplineMesh).BaseType, Is.EqualTo(typeof(MeshGenerator)));
            Assert.That(typeof(SplineMesh).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "_channels", "_useLastResult", "_combineMeshes", "_vertexMatrix", "_normalMatrix", "_lastResult", "_modifiedResult"
            }));
            FieldInfo channels = typeof(SplineMesh).GetField("_channels", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(channels.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1));
            Assert.That(channels.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(((UnityEngine.Serialization.FormerlySerializedAsAttribute)channels.GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false).Single()).oldName, Is.EqualTo("channels"));
            foreach (string name in new[] { "_useLastResult", "_combineMeshes", "_vertexMatrix", "_normalMatrix", "_lastResult", "_modifiedResult" })
            {
                Assert.That(typeof(SplineMesh).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttributes(typeof(SerializeField), false), Is.Empty, name);
            }

            AddComponentMenu menu = (AddComponentMenu)typeof(SplineMesh).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Spline Mesh"));
            Assert.That(typeof(SplineMesh).GetCustomAttributes(typeof(RequireComponent), false).Cast<RequireComponent>().Select(r => r.m_Type0).ToArray(),
                Is.EquivalentTo(new[] { typeof(MeshFilter), typeof(MeshRenderer) }));
            Assert.That(typeof(SplineMesh).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { "Channel" }));
        }

        [Test]
        public void ChannelNestedTypesKeepTheShippedValues()
        {
            Type channel = typeof(SplineMesh.Channel);
            Assert.That(channel.IsSerializable, Is.True);
            Assert.That(channel.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EquivalentTo(new[] { "FloatHandler", "Vector2Handler", "Vector3Handler", "QuaternionHandler", "Type", "UVOverride", "BoundsSpacing", "MeshDefinition" }));

            Assert.That(Enum.GetNames(typeof(SplineMesh.Channel.Type)), Is.EqualTo(new[] { "Extrude", "Place" }));
            Assert.That((int)SplineMesh.Channel.Type.Extrude, Is.EqualTo(0));
            Assert.That((int)SplineMesh.Channel.Type.Place, Is.EqualTo(1));
            Assert.That(Enum.GetNames(typeof(SplineMesh.Channel.UVOverride)), Is.EqualTo(new[] { "None", "ClampU", "ClampV", "UniformU", "UniformV" }));
            Assert.That(Enum.GetValues(typeof(SplineMesh.Channel.UVOverride)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(Enum.GetNames(typeof(SplineMesh.Channel.MeshDefinition.MirrorMethod)), Is.EqualTo(new[] { "None", "X", "Y", "Z" }));
            Assert.That(Enum.GetValues(typeof(SplineMesh.Channel.MeshDefinition.MirrorMethod)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));

            Type spacing = typeof(SplineMesh.Channel.BoundsSpacing);
            Assert.That(spacing.IsValueType && spacing.IsSerializable, Is.True);
            Assert.That(spacing.GetFields().Select(f => f.Name + ":" + f.FieldType.Name).ToArray(), Is.EqualTo(new[] { "front:Single", "back:Single" }));

            AssertDelegate(typeof(SplineMesh.Channel.FloatHandler), typeof(float));
            AssertDelegate(typeof(SplineMesh.Channel.Vector2Handler), typeof(Vector2));
            AssertDelegate(typeof(SplineMesh.Channel.Vector3Handler), typeof(Vector3));
            AssertDelegate(typeof(SplineMesh.Channel.QuaternionHandler), typeof(Quaternion));
        }

        private static void AssertDelegate(Type handler, Type returnType)
        {
            Assert.That(handler.BaseType, Is.EqualTo(typeof(MulticastDelegate)));
            Assert.That(handler.IsSealed, Is.True);
            MethodInfo invoke = handler.GetMethod("Invoke");
            Assert.That(invoke.ReturnType, Is.EqualTo(returnType));
            Assert.That(invoke.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray(), Is.EqualTo(new[] { "Double percent" }));
        }

        // Place 0x4beb70 evaluates (0x4bee1b, Evaluate 0x4ca590 does not modify) and then modifies the sample in place (0x4bee28, the
        // one-argument ModifySample 0x4cf340). An offset key with offset (2, 3) over the whole spline, whose centre range is widened to
        // 0..1 so that it weighs 1 at percent 0, moves the sample by right * 2 + up * 3 = (0, 0, -2) + (0, 3, 0). The sample frame stays
        // that of SampleX/Y/Z, so every vertex of the single copy moves by (0, 3, -2).
        [Test]
        public void PlaceBuildsFromTheModifiedSample()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, Vector3.zero, SampleX, SampleY, SampleZ, "without keys");

            generator.offsetModifier.AddKey(new Vector2(2f, 3f), 0.0, 1.0);
            generator.offsetModifier.keys[0].centerStart = 0.0;
            generator.offsetModifier.keys[0].centerEnd = 1.0;
            generator.RebuildImmediate();
            NearCopy(Vertices(generator), 0, new Vector3(0f, 3f, -2f), SampleX, SampleY, SampleZ, "with the offset key");
        }

        // Extrude 0x4bde20 evaluates each vertex group (0x4be1e1) and copies the sample into the modified result with the two-argument
        // ModifySample 0x4cda40 (0x4be1f5), which jumps to 0x4cf340; the matrix is built from the modified copy. The same offset key
        // (centre range 0..1) weighs 1 at both ends, so the vertices of the single slice of ExtrudeBendsTheVertexGroupsOntoTheSpline
        // move by (0, 3, -2).
        [Test]
        public void ExtrudeBuildsFromTheModifiedSample()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Extrude, 1);
            generator.offsetModifier.AddKey(new Vector2(2f, 3f), 0.0, 1.0);
            generator.offsetModifier.keys[0].centerStart = 0.0;
            generator.offsetModifier.keys[0].centerEnd = 1.0;
            generator.RebuildImmediate();

            Vector3 shift = new Vector3(0f, 3f, -2f);
            Vector3[] vertices = Vertices(generator);
            Assert.That(vertices.Length, Is.EqualTo(4));
            Near(vertices[0], new Vector3(0f, 0f, 0f) + shift, "group z 0, origin");
            Near(vertices[1], new Vector3(0f, 0f, -1f) + shift, "group z 0, x axis");
            Near(vertices[2], new Vector3(0f, 1f, 0f) + shift, "group z 0, y axis");
            Near(vertices[3], new Vector3(10f, 0f, 0f) + shift, "group z 1 at the end of the span");
        }
    }
}
