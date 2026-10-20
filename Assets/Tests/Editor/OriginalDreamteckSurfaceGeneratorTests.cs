using System;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SurfaceGenerator on a closed Linear square in the XY plane, corners (0,0,0), (10,0,0), (10,10,0), (0,10,0)
    // with the point normals (0,0,1). The closed spline has five samples (the first one repeated), four of which are the
    // outline (vertexCount). The average normal is (0,0,1), whose look rotation is the identity, so the projected outline is
    // the corner positions relative to the centre (5,5,0): (-5,-5), (5,-5), (5,5), (-5,5). The shipped IsClockwise sum of that
    // outline is -100, so it counts as clockwise. Expected values are hand arithmetic from the native bodies cited per test
    // (x86-64 addresses in the 0x4d2b.. - 0x4d6.. range; Generate is 0x4d3080).
    public sealed class OriginalDreamteckSurfaceGeneratorTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

        private static readonly Vector3[] Corners =
        {
            new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f), new Vector3(0f, 10f, 0f)
        };

        private static readonly Vector2[] Outline =
        {
            new Vector2(-5f, -5f), new Vector2(5f, -5f), new Vector2(5f, 5f), new Vector2(-5f, 5f)
        };

        // The surface UV of each corner: U along x and V flipped along y over the bounding square.
        private static readonly Vector2[] SurfaceUv =
        {
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f)
        };

        private SplineComputer NewSquare()
        {
            SplinePoint[] points = new SplinePoint[4];
            for (int i = 0; i < 4; i++)
            {
                points[i] = new SplinePoint(Corners[i], Corners[i], new Vector3(0f, 0f, 1f), 1f, Color.white);
            }

            SplineComputer square = _fixture.NewComputer(points);
            square.Close();
            square.RebuildImmediate();
            return square;
        }

        // The same square with the corners reversed: the shipped IsClockwise sum is +300, so it does not count as clockwise.
        private SplineComputer NewReversedSquare()
        {
            SplinePoint[] points = new SplinePoint[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = Corners[(4 - i) % 4];
                points[i] = new SplinePoint(corner, corner, new Vector3(0f, 0f, 1f), 1f, Color.white);
            }

            SplineComputer square = _fixture.NewComputer(points);
            square.Close();
            square.RebuildImmediate();
            return square;
        }

        // A straight Linear spline along +z from the origin: two samples, forward (0,0,1), up (0,1,0), right (1,0,0).
        private SplineComputer NewSweep()
        {
            return _fixture.NewComputer(new SplinePoint(Vector3.zero), new SplinePoint(new Vector3(0f, 0f, 10f)));
        }

        // A surface built once with the final settings, so that no earlier build leaves old values in the arrays.
        private SurfaceGenerator NewSurface(SplineComputer computer, Action<SurfaceGenerator> configure = null, Vector3? firstForward = null)
        {
            SurfaceGenerator surface = _fixture.NewBare<SurfaceGenerator>();
            surface.calculateTangents = false;
            if (configure != null)
            {
                configure(surface);
            }

            surface.spline = computer;
            surface.RebuildImmediate();
            Assert.That(surface.sampleCount, Is.EqualTo(5), "the closed square repeats its first sample");
            SplineSample first = new SplineSample();
            surface.GetSample(0, ref first);
            Near(first.forward, firstForward ?? Vector3.right, "first edge direction");
            Near(first.up, new Vector3(0f, 0f, 1f), "point normal");
            return surface;
        }

        private static SplineSample Sample(SurfaceGenerator surface, int index)
        {
            SplineSample sample = new SplineSample();
            surface.GetSample(index, ref sample);
            return sample;
        }

        private static int[] Triangulated(Vector2[] outline)
        {
            int[] triangles = new int[0];
            MeshUtility.Triangulate(outline, ref triangles);
            return triangles;
        }

        private static int[] Offset(int[] indices, int offset)
        {
            return indices.Select(index => index + offset).ToArray();
        }

        private static int[] Reversed(int[] triangles, int offset)
        {
            int[] result = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                result[i] = triangles[i + 2] + offset;
                result[i + 1] = triangles[i + 1] + offset;
                result[i + 2] = triangles[i] + offset;
            }

            return result;
        }

        private static int[] CountBuilds(SurfaceGenerator surface)
        {
            int[] counter = new int[1];
            surface.onPostBuild += delegate { counter[0]++; };
            DreamteckGeneratorFixture.Update(surface);
            return counter;
        }

        // Field initialisers of the ctor 0x4d6480 on top of the MeshGenerator ctor, get_meshName 0x4d2f70.
        [Test]
        public void FreshSurfaceKeepsTheShippedDefaults()
        {
            SurfaceGenerator surface = _fixture.NewBare<SurfaceGenerator>();
            Assert.That(surface.expand, Is.EqualTo(0f));
            Assert.That(surface.extrude, Is.EqualTo(0f));
            Assert.That(surface.extrudeClipFrom, Is.EqualTo(0.0));
            Assert.That(surface.extrudeClipTo, Is.EqualTo(1.0));
            Assert.That(surface.sideUvScale, Is.EqualTo(Vector2.one));
            Assert.That(surface.sideUvOffset, Is.EqualTo(Vector2.zero));
            Assert.That(surface.sideUvRotation, Is.EqualTo(0f));
            Assert.That(surface.extrudeSpline == null, Is.True);
            Assert.That(surface.extrudeOffset, Is.EqualTo(Vector3.zero));
            Assert.That(surface.uniformUvs, Is.False);
            Assert.That(DreamteckGeneratorFixture.Invoke(surface, "get_meshName"), Is.EqualTo("Surface"));
            Assert.That(DreamteckGeneratorFixture.Field<SplineSample[]>(surface, "extrudeResults"), Is.Empty);
            Assert.That(DreamteckGeneratorFixture.Field<Vector3[]>(surface, "identityVertices"), Is.Empty);
            Assert.That(DreamteckGeneratorFixture.Field<Vector3[]>(surface, "identityNormals"), Is.Empty);
            Assert.That(DreamteckGeneratorFixture.Field<Vector2[]>(surface, "projectedVerts"), Is.Empty);
            Assert.That(DreamteckGeneratorFixture.Field<int[]>(surface, "surfaceTris"), Is.Empty);
            Assert.That(DreamteckGeneratorFixture.Field<int[]>(surface, "wallTris"), Is.Empty);
            Assert.That(surface.GetComponent<MeshFilter>() == null, Is.False, "RequireComponent adds the filter");
            Assert.That(surface.GetComponent<MeshRenderer>() == null, Is.False, "and the renderer");
        }

        // set_expand 0x4d2b20, set_extrude 0x4d2b70, set_extrudeClipFrom 0x4d2bc0, set_extrudeClipTo 0x4d2c10,
        // set_extrudeOffset 0x4d2eb0 and set_uniformUvs 0x4d2f40 change and rebuild; an equal value does nothing.
        [Test]
        public void PlainSettersRebuildOnChangeOnly()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            int[] builds = CountBuilds(surface);

            surface.expand = 0f;
            surface.extrude = 0f;
            surface.extrudeClipFrom = 0.0;
            surface.extrudeClipTo = 1.0;
            surface.extrudeOffset = Vector3.zero;
            surface.uniformUvs = false;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(0), "equal values");

            surface.expand = 1f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));
            surface.extrude = 2f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(2));
            surface.extrudeClipFrom = 0.25;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3));
            surface.extrudeClipTo = 0.75;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(4));
            surface.extrudeOffset = new Vector3(1f, 2f, 3f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(5));
            surface.extrudeOffset = new Vector3(1f + 1e-6f, 2f, 3f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(5), "the Vector3 equality threshold ignores 1e-6");
            surface.uniformUvs = true;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(6));
            Assert.That(surface.expand, Is.EqualTo(1f));
            Assert.That(surface.extrude, Is.EqualTo(2f));
            Assert.That(surface.extrudeClipFrom, Is.EqualTo(0.25));
            Assert.That(surface.extrudeClipTo, Is.EqualTo(0.75));
            Assert.That(surface.extrudeOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(surface.uniformUvs, Is.True);
        }

        // set_sideUvScale 0x4d2c60, set_sideUvOffset 0x4d2cc0 and set_sideUvRotation 0x4d2d20 store the value before they compare
        // it with the old one (the store at 0x4d2c6c / 0x4d2ccc / 0x4d2d2f precedes the test), so a difference below the Vector2
        // equality threshold is kept without a rebuild.
        [Test]
        public void SideUvSettersStoreTheValueBeforeTheyCompare()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            int[] builds = CountBuilds(surface);

            surface.sideUvScale = Vector2.one;
            surface.sideUvOffset = Vector2.zero;
            surface.sideUvRotation = 0f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(0), "equal values");

            surface.sideUvScale = new Vector2(1f + 1e-6f, 1f);
            Assert.That(surface.sideUvScale.x, Is.EqualTo(1f + 1e-6f), "stored although equal for the operator");
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(0), "but no rebuild");
            surface.sideUvScale = new Vector2(2f, 3f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));

            surface.sideUvOffset = new Vector2(1e-6f, 0f);
            Assert.That(surface.sideUvOffset.x, Is.EqualTo(1e-6f));
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));
            surface.sideUvOffset = new Vector2(0.5f, 0.25f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(2));

            surface.sideUvRotation = 45f;
            Assert.That(surface.sideUvRotation, Is.EqualTo(45f));
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3));
            surface.sideUvRotation = 45f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3), "equal value");
        }

        // set_extrudeSpline 0x4d2d70: a different computer is unsubscribed from the old one, subscribed to the new one (unless
        // null) and a rebuild is queued; the same computer is ignored.
        [Test]
        public void ExtrudeSplineFollowsTheComputerItIsGiven()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            SplineComputer first = NewSweep();
            SplineComputer second = NewSweep();
            int[] builds = CountBuilds(surface);

            surface.extrudeSpline = first;
            Assert.That(surface.extrudeSpline == first, Is.True);
            Assert.That(first.IsSubscribed(surface), Is.True);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));

            surface.extrudeSpline = first;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1), "same computer");

            surface.extrudeSpline = second;
            Assert.That(first.IsSubscribed(surface), Is.False, "unsubscribed from the old one");
            Assert.That(second.IsSubscribed(surface), Is.True);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(2));

            surface.extrudeSpline = null;
            Assert.That(second.IsSubscribed(surface), Is.False);
            Assert.That(surface.extrudeSpline == null, Is.True);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3));
        }

        // IsClockwise 0x4d5f50 sums (next.x - current.x) * (current.y + next.y) over the edges that start at the points 1 to n - 1
        // (the last one wraps to point 0) and adds the edge from the last point to the first once more; the result is `sum <= 0`.
        // The edge from the first to the second point is not in the sum and the closing edge is in it twice, so the triangle
        // (0,10), (10,10), (10,0) counts as clockwise (sum -100 - 100) although its plain shoelace sum is +100.
        [Test]
        public void IsClockwiseFollowsTheShippedSum()
        {
            SurfaceGenerator surface = _fixture.NewBare<SurfaceGenerator>();
            Func<Vector2[], bool> isClockwise = points => (bool)DreamteckGeneratorFixture.Invoke(surface, "IsClockwise", new object[] { points });

            Assert.That(isClockwise(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }), Is.True, "sum -2");
            Assert.That(isClockwise(new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) }), Is.False, "sum +2");
            Assert.That(isClockwise(new[] { new Vector2(0f, 10f), new Vector2(10f, 10f), new Vector2(10f, 0f) }), Is.True, "sum -200");
            Assert.That(isClockwise(new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f) }), Is.False, "sum +1");
            Assert.That(isClockwise(new[] { new Vector2(3f, 4f) }), Is.True, "a single point: every term is zero");
            Assert.That(isClockwise(new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) }), Is.True, "two points: (0 - 1) * (1 + 0) + (0 - 1) * (0 + 1) = -2");
            Assert.That(isClockwise(Outline), Is.True, "the test square: sum -100");
            Assert.Throws<TargetInvocationException>(() => isClockwise(new Vector2[0]), "the closing edge needs a first point");
        }

        // WriteTris 0x4d62e0 copies the list into the target from trisOffset on, adds vertexOffset to every index, reverses each
        // triangle when flipped, and returns trisOffset + tris.Length.
        [Test]
        public void WriteTrisShiftsAndReversesTheTriangles()
        {
            SurfaceGenerator surface = _fixture.NewBare<SurfaceGenerator>();
            MethodInfo method = typeof(SurfaceGenerator).GetMethod("WriteTris", AnyInstance);
            int[] tris = { 0, 1, 2, 2, 1, 3 };

            int[] target = new int[12];
            object[] arguments = { tris, target, 10, 3, false };
            Assert.That(method.Invoke(surface, arguments), Is.EqualTo(9));
            Assert.That(target, Is.EqualTo(new[] { 0, 0, 0, 10, 11, 12, 12, 11, 13, 0, 0, 0 }));

            target = new int[12];
            arguments = new object[] { tris, target, 10, 3, true };
            Assert.That(method.Invoke(surface, arguments), Is.EqualTo(9));
            Assert.That(target, Is.EqualTo(new[] { 0, 0, 0, 12, 11, 10, 13, 11, 12, 0, 0, 0 }));

            target = new int[3];
            arguments = new object[] { new int[0], target, 7, 3, true };
            Assert.That(method.Invoke(surface, arguments), Is.EqualTo(3), "nothing to copy");
            Assert.That(target, Is.EqualTo(new[] { 0, 0, 0 }));
        }

        // GenerateSurfaceTris 0x4d6020 triangulates the projected outline into surfaceTris and reverses it on request.
        [Test]
        public void GenerateSurfaceTrisTriangulatesTheOutlineAndFlipsOnRequest()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            int[] expected = Triangulated(Outline);
            DreamteckGeneratorFixture.Invoke(surface, "GenerateSurfaceTris", false);
            Assert.That(DreamteckGeneratorFixture.Field<int[]>(surface, "surfaceTris"), Is.EqualTo(expected));
            DreamteckGeneratorFixture.Invoke(surface, "GenerateSurfaceTris", true);
            int[] flipped = (int[])expected.Clone();
            MeshUtility.FlipTriangles(ref flipped);
            Assert.That(DreamteckGeneratorFixture.Field<int[]>(surface, "surfaceTris"), Is.EqualTo(flipped));
            Assert.That(flipped, Is.Not.EqualTo(expected));
        }

        // GetProjectedVertices 0x4d5600: the centre is the mean of position + transform offset, the normal the normalised sum of
        // the sample ups; every sample is projected onto the right and up axes of LookRotation(normal, up) as the signed length of
        // Project(relative, axis). The offset moves positions and centre alike.
        [Test]
        public void ProjectedVerticesAreSignedDistancesAlongThePlaneAxes()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            MethodInfo method = typeof(SurfaceGenerator).GetMethod("GetProjectedVertices", AnyInstance);
            object[] arguments = { 4, null, null };
            method.Invoke(surface, arguments);
            Near((Vector3)arguments[1], new Vector3(5f, 5f, 0f), "centre");
            Near((Vector3)arguments[2], new Vector3(0f, 0f, 1f), "normal");
            Vector2[] projected = DreamteckGeneratorFixture.Field<Vector2[]>(surface, "projectedVerts");
            Assert.That(projected.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Near(projected[i], Outline[i], "projected " + i);
            }

            surface.offset = new Vector3(3f, -1f, 0f);
            arguments = new object[] { 4, null, null };
            method.Invoke(surface, arguments);
            Near((Vector3)arguments[1], new Vector3(8f, 4f, 0f), "the offset moves the centre (trs axes are the identity)");
            projected = DreamteckGeneratorFixture.Field<Vector2[]>(surface, "projectedVerts");
            for (int i = 0; i < 4; i++)
            {
                Near(projected[i], Outline[i], "the outline relative to the centre is unchanged " + i);
            }

            arguments = new object[] { 2, null, null };
            method.Invoke(surface, arguments);
            Near((Vector3)arguments[1], new Vector3(8f, -1f, 0f), "mean of the first two corners (0,0,0), (10,0,0) is (5,0,0), plus the offset");
            Assert.That(DreamteckGeneratorFixture.Field<Vector2[]>(surface, "projectedVerts").Length, Is.EqualTo(2), "the array is resized to the count");
        }

        // GetIdentityVerts 0x4d60c0: the sample positions relative to the centre, pushed out along the (clockwise: reversed) sample
        // right by expand, and the rights themselves, both turned by the inverse look rotation of the normal. The first sample
        // sits at the origin with right (0,1,0). For the normal (1,0,0) the look rotation maps (x,y,z) to (z,y,-x), the inverse
        // maps (a,b,c) to (-c,b,a).
        [Test]
        public void IdentityVertsAreRelativeToTheCentreInTheNormalFrame()
        {
            SurfaceGenerator surface = NewSurface(NewSquare(), s => s.expand = 1f);
            MethodInfo method = typeof(SurfaceGenerator).GetMethod("GetIdentityVerts", AnyInstance);
            method.Invoke(surface, new object[] { new Vector3(5f, 5f, 0f), new Vector3(0f, 0f, 1f), false });
            Vector3[] vertices = DreamteckGeneratorFixture.Field<Vector3[]>(surface, "identityVertices");
            Vector3[] normals = DreamteckGeneratorFixture.Field<Vector3[]>(surface, "identityNormals");
            Assert.That(vertices.Length, Is.EqualTo(5));
            Assert.That(normals.Length, Is.EqualTo(5));
            Near(vertices[0], new Vector3(-5f, -4f, 0f), "(0,0,0) - centre + right * expand");
            Near(normals[0], new Vector3(0f, 1f, 0f), "right");

            method.Invoke(surface, new object[] { new Vector3(5f, 5f, 0f), new Vector3(0f, 0f, 1f), true });
            Near(vertices[0], new Vector3(-5f, -6f, 0f), "clockwise: the right is reversed");
            Near(normals[0], new Vector3(0f, -1f, 0f), "clockwise: the right is reversed");

            method.Invoke(surface, new object[] { new Vector3(5f, 5f, 0f), new Vector3(1f, 0f, 0f), false });
            Near(vertices[0], new Vector3(0f, -4f, -5f), "inverse look rotation of (1,0,0): (-5,-4,0) -> (-c,b,a)");
            Near(normals[0], new Vector3(0f, 1f, 0f), "inverse look rotation of (1,0,0): (0,1,0) -> (0,1,0)");
        }

        // A flat build (extrude 0, no extrude spline): the outline vertices only. BuildMesh 0x4d3030 hands over to Generate; the
        // arrays are sized for two surfaces (the triangle array has room for the flipped copy that a flat build never writes).
        [Test]
        public void FlatSurfaceFollowsTheOutline()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            TS_Mesh ts = Ts(surface);
            Assert.That(ts.vertices.Length, Is.EqualTo(4));
            Assert.That(ts.normals.Length, Is.EqualTo(4));
            Assert.That(ts.colors.Length, Is.EqualTo(4));
            Assert.That(ts.uv.Length, Is.EqualTo(4));
            int[] surfaceTriangles = Triangulated(Outline);
            Assert.That(surfaceTriangles.Length, Is.EqualTo(6), "a quad is two triangles");
            Assert.That(ts.triangles.Length, Is.EqualTo(12), "2 * surface");
            Assert.That(ts.triangles.Take(6).ToArray(), Is.EqualTo(surfaceTriangles));
            Assert.That(ts.triangles.Skip(6).All(index => index == 0), Is.True, "written once");
            for (int i = 0; i < 4; i++)
            {
                Near(ts.vertices[i], Corners[i], "vertex " + i);
                Near(ts.normals[i], new Vector3(0f, 0f, 1f), "normal " + i);
                DreamteckGeneratorFixture.Near(ts.colors[i], Color.white, "colour " + i);
                Near(ts.uv[i], SurfaceUv[i], "uv " + i);
            }
        }

        // The colour is the generator colour times the sample colour; the transform axes feed the offset; the UV is scaled
        // around the centre of the unit square (Scale(uv, uvScale) - uvScale / 2 + uvOffset + 0.5) and turned by uvRotation.
        [Test]
        public void ColourOffsetAndUvTransformShapeTheFlatSurface()
        {
            SurfaceGenerator surface = NewSurface(NewSquare(), s =>
            {
                s.color = new Color(1f, 0.5f, 1f, 0.5f);
                s.offset = new Vector3(1f, 2f, 3f);
                s.uvScale = new Vector2(2f, 4f);
                s.uvOffset = new Vector2(0.25f, 0.5f);
            });
            TS_Mesh ts = Ts(surface);
            DreamteckGeneratorFixture.Near(ts.colors[0], new Color(1f, 0.5f, 1f, 0.5f), "colour");
            Near(ts.vertices[0], new Vector3(1f, 2f, 3f), "corner 0 + offset (the trs axes are the identity)");
            Near(ts.vertices[2], new Vector3(11f, 12f, 3f), "corner 2 + offset");
            // corner 0 has (u, v) = (0, 1): (0 * 2 - 1 + 0.25 + 0.5, 1 * 4 - 2 + 0.5 + 0.5) = (-0.25, 3)
            Near(ts.uv[0], new Vector2(-0.25f, 3f), "uv 0");
            // corner 2 has (1, 0): (2 - 1 + 0.25 + 0.5, 0 - 2 + 0.5 + 0.5) = (1.75, -1)
            Near(ts.uv[2], new Vector2(1.75f, -1f), "uv 2");

            surface.uvScale = Vector2.one;
            surface.uvOffset = Vector2.zero;
            surface.uvRotation = 90f;
            surface.RebuildImmediate();
            // (u, v) = (0, 1) turned 90 degrees about z is (-1, 0)
            Near(Ts(surface).uv[0], new Vector2(-1f, 0f), "uv 0 after a quarter turn");
            Near(Ts(surface).uv[2], new Vector2(0f, 1f), "uv 2 after a quarter turn");
        }

        // The expand moves each outline vertex along the sample right (reversed for a clockwise outline): the right of the first
        // sample is (0,1,0) (Cross(up, forward)), so the first corner moves to (0,-1,0).
        [Test]
        public void ExpandPushesTheOutlineAlongTheReversedSampleRight()
        {
            SurfaceGenerator surface = NewSurface(NewSquare(), s => s.expand = 1f);
            TS_Mesh ts = Ts(surface);
            Near(ts.vertices[0], new Vector3(0f, -1f, 0f), "corner 0");
            for (int i = 0; i < 4; i++)
            {
                SplineSample sample = Sample(surface, i);
                Near(ts.vertices[i], sample.position - sample.right, "corner " + i + " moved against its right");
            }
        }

        // With an extrude distance: the surface is lifted by normal * extrude, a copy of the unlifted surface follows with the
        // negated normal and the U mirrored, then two rims of vertexCount + 1 wall vertices (rim A lowered by the extrude from the
        // lifted surface, rim B at the lifted surface). Rim A takes U from j / (vertexCount - 1), rim B from j / vertexCount; a
        // clockwise outline mirrors U. The wall normal is the reversed sample right. The triangles are the surface, the flipped
        // copy shifted by vertexCount, and the two-row wall plane shifted by 2 * vertexCount.
        [Test]
        public void ExtrudeBuildsTheBottomCopyAndTwoWallRims()
        {
            SurfaceGenerator surface = NewSurface(NewSquare(), s => s.extrude = 2f);
            TS_Mesh ts = Ts(surface);
            Assert.That(ts.vertices.Length, Is.EqualTo(18), "4 * 4 + 2");
            Assert.That(ts.normals.Length, Is.EqualTo(18));
            Assert.That(ts.colors.Length, Is.EqualTo(18));
            Assert.That(ts.uv.Length, Is.EqualTo(18));
            int[] surfaceTriangles = Triangulated(Outline);
            Assert.That(ts.triangles.Length, Is.EqualTo(72), "2 * surface + sampleCount * 12");

            for (int i = 0; i < 4; i++)
            {
                Near(ts.vertices[i], Corners[i] + new Vector3(0f, 0f, 2f), "lifted surface " + i);
                Near(ts.normals[i], new Vector3(0f, 0f, 1f), "surface normal " + i);
                Near(ts.uv[i], SurfaceUv[i], "surface uv " + i);
                Near(ts.vertices[4 + i], Corners[i], "bottom copy " + i);
                Near(ts.normals[4 + i], new Vector3(0f, 0f, -1f), "bottom normal " + i);
                Near(ts.uv[4 + i], new Vector2(1f - SurfaceUv[i].x, SurfaceUv[i].y), "bottom uv " + i);
            }

            for (int j = 0; j < 5; j++)
            {
                SplineSample sample = Sample(surface, j % 4);
                Near(ts.vertices[8 + j], Corners[j % 4], "rim A vertex " + j);
                Near(ts.vertices[13 + j], Corners[j % 4] + new Vector3(0f, 0f, 2f), "rim B vertex " + j);
                Near(ts.normals[8 + j], -sample.right, "rim A normal " + j);
                Near(ts.normals[13 + j], -sample.right, "rim B normal " + j);
                Near(ts.uv[8 + j], new Vector2(1f - j / 3f, 0f), "rim A uv " + j);
                Near(ts.uv[13 + j], new Vector2(1f - j / 4f, 1f), "rim B uv " + j);
            }

            for (int i = 0; i < 18; i++)
            {
                DreamteckGeneratorFixture.Near(ts.colors[i], Color.white, "colour " + i);
            }

            Assert.That(ts.triangles.Take(6).ToArray(), Is.EqualTo(surfaceTriangles));
            Assert.That(ts.triangles.Skip(6).Take(6).ToArray(), Is.EqualTo(Reversed(surfaceTriangles, 4)));
            Assert.That(ts.triangles.Skip(12).Take(24).ToArray(), Is.EqualTo(Offset(MeshUtility.GeneratePlaneTriangles(4, 2, false), 8)));
            Assert.That(ts.triangles.Skip(36).All(index => index == 0), Is.True);
        }

        // A negative extrude lifts downwards, reverses the surface triangles and negates its normals (the surface faces away),
        // and the wall plane flips with it: a clockwise outline walls are flipped when the surface is.
        [Test]
        public void NegativeExtrudeTurnsTheSurfaceAndTheWallsOver()
        {
            SurfaceGenerator surface = NewSurface(NewSquare(), s => s.extrude = -2f);
            TS_Mesh ts = Ts(surface);
            int[] surfaceTriangles = Triangulated(Outline);
            int[] turned = (int[])surfaceTriangles.Clone();
            MeshUtility.FlipTriangles(ref turned);
            for (int i = 0; i < 4; i++)
            {
                Near(ts.vertices[i], Corners[i] + new Vector3(0f, 0f, -2f), "surface lowered " + i);
                Near(ts.normals[i], new Vector3(0f, 0f, -1f), "surface normal turned " + i);
                Near(ts.normals[4 + i], new Vector3(0f, 0f, 1f), "bottom copy normal " + i);
                Near(ts.vertices[4 + i], Corners[i], "copy " + i);
            }

            Assert.That(ts.triangles.Take(6).ToArray(), Is.EqualTo(turned));
            Assert.That(ts.triangles.Skip(6).Take(6).ToArray(), Is.EqualTo(Reversed(turned, 4)));
            Assert.That(ts.triangles.Skip(12).Take(24).ToArray(), Is.EqualTo(Offset(MeshUtility.GeneratePlaneTriangles(4, 2, true), 8)),
                "flipWalls = clockwise for a negative extrude");
        }

        // The wall UV: uniform UVs use (j / vertexCount, extrude) scaled by sideUvScale at rim B; the side offset and rotation
        // apply to rim B only (rim A takes neither the rotation nor the Y scale); a clockwise outline mirrors U afterwards.
        [Test]
        public void SideUvScaleOffsetRotationAndUniformModeShapeTheWallCoordinates()
        {
            SurfaceGenerator surface = NewSurface(NewSquare(), s =>
            {
                s.extrude = 2f;
                s.sideUvScale = new Vector2(2f, 3f);
                s.sideUvOffset = new Vector2(0.5f, 0.25f);
            });
            TS_Mesh ts = Ts(surface);
            // rim A j = 1: (1 / 3 * 2 + 0.5, 0 + 0.25) mirrored: 1 - 1.1667
            Near(ts.uv[9], new Vector2(1f - (2f / 3f + 0.5f), 0.25f), "rim A");
            // rim B j = 1: (1 / 4 * 2 + 0.5, 3 + 0.25) mirrored
            Near(ts.uv[14], new Vector2(1f - 1f, 3.25f), "rim B");

            surface.uniformUvs = true;
            surface.RebuildImmediate();
            ts = Ts(surface);
            // rim B j = 1: offset + scale * (1 / 4, extrude 2) = (0.5 + 0.5, 0.25 + 6) mirrored
            Near(ts.uv[14], new Vector2(1f - 1f, 6.25f), "uniform rim B");
            Near(ts.uv[9], new Vector2(1f - (2f / 3f + 0.5f), 0.25f), "rim A is not affected");

            surface.sideUvRotation = 90f;
            surface.RebuildImmediate();
            ts = Ts(surface);
            // (1, 6.25) turned 90 degrees about z is (-6.25, 1), mirrored: 7.25
            Near(ts.uv[14], new Vector2(7.25f, 1f), "rotated rim B");
            Near(ts.uv[9], new Vector2(1f - (2f / 3f + 0.5f), 0.25f), "rim A takes no rotation");
        }

        // Extruding along a second spline: the surface (in the frame of the average normal, relative to the centre) is placed at
        // the first sample of the extrude spline (a copy: normal backwards, U mirrored) and the original at the last sample, with
        // a wall row per extrude sample. Here the extrude spline runs from the origin to (0,0,10) with the identity rotation.
        [Test]
        public void ExtrudeSplineSweepsTheOutlineAlongTheSpline()
        {
            SplineComputer sweep = NewSweep();
            SurfaceGenerator surface = NewSurface(NewSquare(), s => s.extrudeSpline = sweep);
            Assert.That(DreamteckGeneratorFixture.Field<SplineSample[]>(surface, "extrudeResults").Length, Is.EqualTo(2));
            TS_Mesh ts = Ts(surface);
            Assert.That(ts.vertices.Length, Is.EqualTo(18), "2 * 5 + 2 * 4");
            Assert.That(ts.triangles.Length, Is.EqualTo(72), "2 * surface + sampleCount * 2 * 6");

            Vector3[] relative = { new Vector3(-5f, -5f, 0f), new Vector3(5f, -5f, 0f), new Vector3(5f, 5f, 0f), new Vector3(-5f, 5f, 0f), new Vector3(-5f, -5f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                Near(ts.vertices[4 + i], relative[i], "copy at the first sample " + i);
                Near(ts.vertices[i], relative[i] + new Vector3(0f, 0f, 10f), "outline at the last sample " + i);
                Near(ts.normals[4 + i], new Vector3(0f, 0f, -1f), "copy normal " + i);
                Near(ts.normals[i], new Vector3(0f, 0f, 1f), "outline normal " + i);
                Near(ts.uv[i], SurfaceUv[i], "outline uv " + i);
                Near(ts.uv[4 + i], new Vector2(1f - SurfaceUv[i].x, SurfaceUv[i].y), "copy uv " + i);
                DreamteckGeneratorFixture.Near(ts.colors[4 + i], Color.white, "copy colour " + i);
                DreamteckGeneratorFixture.Near(ts.colors[i], Color.white, "outline colour " + i);
            }

            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 5; k++)
                {
                    int index = 8 + 5 * j + k;
                    Near(ts.vertices[index], relative[k] + new Vector3(0f, 0f, 10f * j), "wall " + j + "," + k);
                    Near(ts.normals[index], -Sample(surface, k).right, "wall normal " + j + "," + k);
                    Near(ts.uv[index], new Vector2(1f - k / 4f, j), "wall uv " + j + "," + k);
                    DreamteckGeneratorFixture.Near(ts.colors[index], new Color(0f, 0f, 0f, 0f), "walls are not coloured " + j + "," + k);
                }
            }

            int[] surfaceTriangles = Triangulated(Outline);
            Assert.That(ts.triangles.Take(6).ToArray(), Is.EqualTo(surfaceTriangles));
            Assert.That(ts.triangles.Skip(6).Take(6).ToArray(), Is.EqualTo(Reversed(surfaceTriangles, 4)));
            Assert.That(ts.triangles.Skip(12).Take(24).ToArray(), Is.EqualTo(Offset(MeshUtility.GeneratePlaneTriangles(4, 2, false), 8)));
            Assert.That(ts.triangles.Skip(36).All(index => index == 0), Is.True);
        }

        // The extrude offset is applied in the frame of each extrude sample (right, up and forward scaled by its size). The
        // sweep samples have right (1,0,0), up (0,1,0), forward (0,0,1), so (1,2,3) moves copy and outline by (1,2,3). The
        // uniform wall UV uses the walked length of the extrude spline (10 at the second row); sideUvRotation turns it.
        [Test]
        public void ExtrudeOffsetAndUniformUvsFollowTheExtrudeSamples()
        {
            SplineComputer sweep = NewSweep();
            SurfaceGenerator surface = NewSurface(NewSquare(), s =>
            {
                s.extrudeSpline = sweep;
                s.extrudeOffset = new Vector3(1f, 2f, 3f);
                s.uniformUvs = true;
                s.sideUvScale = new Vector2(2f, 3f);
                s.sideUvOffset = new Vector2(0.5f, 0.25f);
            });
            TS_Mesh ts = Ts(surface);
            Near(ts.vertices[4], new Vector3(-5f, -5f, 0f) + new Vector3(1f, 2f, 3f), "copy");
            Near(ts.vertices[0], new Vector3(-5f, -5f, 10f) + new Vector3(1f, 2f, 3f), "outline");
            // wall row 1, k = 2: offset + scale * (2 / 4, 10) = (0.5 + 1, 0.25 + 30) = (1.5, 30.25), mirrored U: -0.5
            Near(ts.uv[8 + 5 + 2], new Vector2(-0.5f, 30.25f), "uniform wall uv");
            Near(ts.uv[8 + 2], new Vector2(-0.5f, 0.25f), "first row walked 0");

            surface.sideUvRotation = 90f;
            surface.RebuildImmediate();
            // (1.5, 30.25) turned 90 degrees about z is (-30.25, 1.5), then mirrored: 31.25
            Near(Ts(surface).uv[8 + 5 + 2], new Vector2(31.25f, 1.5f), "rotated wall uv");
        }

        // Every other Generate test uses the clockwise outline. With the corners reversed (sum +300, not clockwise) the branches
        // take the other side: the expand follows the sample right (0x4d3b84), the wall normal is the sample right itself
        // (0x4d4ff2/0x4d5020), U is not mirrored (0x4d5107, 0x4d5324) and a positive extrude flips the wall plane (0x4d54e8).
        // The first sample runs along +y, so its right is Cross(up, forward) = (-1,0,0).
        [Test]
        public void AnOutlineThatIsNotClockwiseTakesTheOtherBranches()
        {
            Vector3 firstForward = Vector3.up;
            SurfaceGenerator expanded = NewSurface(NewReversedSquare(), s => s.expand = 1f, firstForward);
            Near(Ts(expanded).vertices[0], new Vector3(-1f, 0f, 0f), "corner 0 moved along its right");

            SurfaceGenerator surface = NewSurface(NewReversedSquare(), s => s.extrude = 2f, firstForward);
            TS_Mesh ts = Ts(surface);
            Near(Sample(surface, 0).right, new Vector3(-1f, 0f, 0f), "first sample right");
            Near(ts.normals[8], new Vector3(-1f, 0f, 0f), "rim A normal is the sample right");
            Near(ts.normals[13], new Vector3(-1f, 0f, 0f), "rim B normal is the sample right");
            Near(ts.uv[9], new Vector2(1f / 3f, 0f), "rim A U is not mirrored");
            Near(ts.uv[14], new Vector2(1f / 4f, 1f), "rim B U is not mirrored");
            Assert.That(ts.triangles.Skip(12).Take(24).ToArray(), Is.EqualTo(Offset(MeshUtility.GeneratePlaneTriangles(4, 2, true), 8)),
                "flipWalls = !clockwise for a positive extrude");
        }

        // The sweep runs along +x, so its samples have forward (1,0,0), up (0,1,0) and right (0,0,-1), and the rotation R maps
        // (x, y, z) to (z, y, -x). The outline, the copy, the walls and the wall normals are all turned by it (0x4d3d6b get_rotation
        // and 0x4d3da0 op_Multiply; end 0x4d4120/0x4d4155; walls 0x4d460f/0x4d4648; wall normals 0x4d4762/0x4d479a), and the extrude
        // offset is applied along the sample axes (0x4d3dc4-0x4d3e47): (1,2,3) is right * 1 + up * 2 + forward * 3 = (3,2,-1).
        [Test]
        public void ExtrudeSplineRotatesTheOutlineAndAppliesTheOffsetInTheSampleFrame()
        {
            SplineComputer sweep = _fixture.NewComputer(new SplinePoint(Vector3.zero), new SplinePoint(new Vector3(10f, 0f, 0f)));
            SurfaceGenerator surface = NewSurface(NewSquare(), s => s.extrudeSpline = sweep);
            TS_Mesh ts = Ts(surface);
            Quaternion rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            Vector3[] relative = { new Vector3(-5f, -5f, 0f), new Vector3(5f, -5f, 0f), new Vector3(5f, 5f, 0f), new Vector3(-5f, 5f, 0f), new Vector3(-5f, -5f, 0f) };
            Near(ts.vertices[4], new Vector3(0f, -5f, 5f), "copy at the first sample");
            Near(ts.vertices[0], new Vector3(10f, -5f, 5f), "outline at the last sample");
            Near(ts.normals[4], new Vector3(-1f, 0f, 0f), "copy normal");
            Near(ts.normals[0], new Vector3(1f, 0f, 0f), "outline normal");
            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 5; k++)
                {
                    int index = 8 + 5 * j + k;
                    Near(ts.vertices[index], rotation * relative[k] + new Vector3(10f * j, 0f, 0f), "wall " + j + "," + k);
                    Near(ts.normals[index], rotation * -Sample(surface, k).right, "wall normal " + j + "," + k);
                }
            }

            SurfaceGenerator offset = NewSurface(NewSquare(), s =>
            {
                s.extrudeSpline = sweep;
                s.extrudeOffset = new Vector3(1f, 2f, 3f);
            });
            Near(Ts(offset).vertices[4], new Vector3(3f, -3f, 4f), "copy: rotated corner + (3,2,-1)");
            Near(Ts(offset).vertices[0], new Vector3(13f, -3f, 4f), "outline: rotated corner + (3,2,-1)");
        }

        // Awake 0x4d2fa0 reads the transform axes after the base Awake; LateUpdate 0x4d5550 refreshes them only for a threaded
        // generator whose transform has changed.
        [Test]
        public void TransformAxesAreCachedAtAwakeAndRefreshedByLateUpdateWhenThreaded()
        {
            GameObject owner = _fixture.NewObject("turned");
            owner.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            owner.AddComponent<MeshFilter>();
            owner.AddComponent<MeshRenderer>();
            SurfaceGenerator surface = owner.AddComponent<SurfaceGenerator>();
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsRight"), new Vector3(0f, 1f, 0f), "right at Awake");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsUp"), new Vector3(-1f, 0f, 0f), "up at Awake");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsForward"), new Vector3(0f, 0f, 1f), "forward at Awake");

            owner.transform.rotation = Quaternion.identity;
            owner.transform.hasChanged = true;
            surface.multithreaded = false;
            DreamteckGeneratorFixture.Invoke(surface, "LateUpdate");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsRight"), new Vector3(0f, 1f, 0f), "not threaded: kept");

            surface.multithreaded = true;
            owner.transform.hasChanged = false;
            DreamteckGeneratorFixture.Invoke(surface, "LateUpdate");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsRight"), new Vector3(0f, 1f, 0f), "threaded but unchanged: kept");

            owner.transform.hasChanged = true;
            DreamteckGeneratorFixture.Invoke(surface, "LateUpdate");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsRight"), Vector3.right, "threaded and changed: refreshed");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsUp"), Vector3.up, "up");
            Near(DreamteckGeneratorFixture.Field<Vector3>(surface, "_trsForward"), Vector3.forward, "forward");
            surface.multithreaded = false;
        }

        // BuildMesh 0x4d3030 reads the spline point count first: a spline without points builds nothing, a missing spline throws.
        [Test]
        public void BuildMeshNeedsASplineWithPoints()
        {
            SurfaceGenerator surface = _fixture.NewBare<SurfaceGenerator>();
            Assert.Throws<TargetInvocationException>(() => DreamteckGeneratorFixture.Invoke(surface, "BuildMesh"), "no spline");

            surface.spline = _fixture.NewObject("empty").AddComponent<SplineComputer>();
            surface.RebuildImmediate();
            Assert.That(Ts(surface).vertices.Length, Is.EqualTo(0));
            Assert.DoesNotThrow(() => DreamteckGeneratorFixture.Invoke(surface, "BuildMesh"), "no points: nothing to build");
            Assert.That(Ts(surface).vertices.Length, Is.EqualTo(0));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndMenu()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string[] serialized =
            {
                "_expand", "_extrude", "_sideUvScale", "_sideUvOffset", "_sideUvRotation", "_extrudeSpline", "_extrudeOffset",
                "extrudeResults", "identityVertices", "identityNormals", "projectedVerts", "surfaceTris", "wallTris", "_extrudeFrom",
                "_extrudeTo", "_uniformUvs"
            };
            string[] cached = { "_trsRight", "_trsUp", "_trsForward" };
            Assert.That(typeof(SurfaceGenerator).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(serialized.Concat(cached).ToArray()));
            foreach (string name in serialized)
            {
                FieldInfo field = typeof(SurfaceGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name);
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name);
            }

            foreach (string name in cached)
            {
                FieldInfo field = typeof(SurfaceGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(0), name);
            }

            AddComponentMenu menu = (AddComponentMenu)typeof(SurfaceGenerator).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Surface Generator"));
            // Reflection does not guarantee the attribute order, so only the set is asserted.
            Assert.That(typeof(SurfaceGenerator).GetCustomAttributes(typeof(RequireComponent), false).Cast<RequireComponent>().Select(r => r.m_Type0).ToArray(),
                Is.EquivalentTo(new[] { typeof(MeshFilter), typeof(MeshRenderer) }));
            Assert.That(typeof(SurfaceGenerator).BaseType, Is.EqualTo(typeof(MeshGenerator)));
        }
    }
}
