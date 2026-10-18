using System;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.SurfaceMeshCrossSectionPlaneGenerator (TypeDef 0x02000aa4, x86-64 0x6D4780..0x6D4CE0),
    // SurfaceMeshCrossSectionEllipseGenerator (0x02000aa1, 0x6D3230..0x6D3EE0) and SurfaceMeshGenerator
    // (0x02000aa6, 0x6D4D40). The section generators are plain serializable classes, so they are
    // built with their own constructors and fed hand-made KnotStepData; the expected vertices come
    // from the arithmetic of the native bodies (cited per case), not from the implementation.
    public sealed class OriginalSurfaceMeshCrossSectionTests
    {
        private const float Tolerance = 1e-4f;

        private static SurfaceMeshCrossSectionPlaneGenerator NewPlane(float width, Vector3 offsetLocal, float angle)
        {
            var plane = new SurfaceMeshCrossSectionPlaneGenerator();
            Set(plane, "m_width", width);
            Set(plane, "m_offsetLocal", offsetLocal);
            Set(plane, "m_angle", angle);
            return plane;
        }

        private static SurfaceMeshCrossSectionEllipseGenerator NewEllipse(float width, float height, float angleStep,
            Vector2 angleRange, bool clockwise, Vector3 offsetLocal)
        {
            var ellipse = new SurfaceMeshCrossSectionEllipseGenerator();
            Set(ellipse, "m_width", width);
            Set(ellipse, "m_height", height);
            Set(ellipse, "m_angleStep", angleStep);
            Set(ellipse, "m_angleRange", angleRange);
            Set(ellipse, "m_angleClockwise", clockwise);
            Set(ellipse, "m_offsetLocal", offsetLocal);
            return ellipse;
        }

        private static SurfaceMeshCrossSectionGenerator.KnotStepData Knot(Vector3 origin, Quaternion rotation) =>
            new SurfaceMeshCrossSectionGenerator.KnotStepData { m_origin = origin, m_rotation = rotation };

        private static void AreClose(Vector3 expected, Vector3 actual, string label) =>
            WorldMemberAccess.AreClose(expected, actual, label, Tolerance);

        // ---- declarations ----

        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(SurfaceMeshCrossSectionPlaneGenerator).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(SurfaceMeshCrossSectionPlaneGenerator).BaseType, Is.EqualTo(typeof(SurfaceMeshCrossSectionGenerator)));
            Assert.That(Fields(typeof(SurfaceMeshCrossSectionPlaneGenerator)), Is.EqualTo(new[]
                { "VertexStride:Int32:8051", "m_angle:Single:0004" }));
            Assert.That(Methods(typeof(SurfaceMeshCrossSectionPlaneGenerator)), Is.EqualTo(new[]
            {
                "get_Type:08c6", "GetOrigin:00c6", "GenerateMeshCrossSection:00c6", "CalculateLocationLocal:00c4",
                "CalculateRotationLocal:0081", ".ctor:1886"
            }));
            Assert.That(AttributeRow(typeof(SurfaceMeshCrossSectionPlaneGenerator).GetField("m_angle", BindingFlags.Instance | BindingFlags.NonPublic)),
                Is.EqualTo("SerializeField()"));

            Assert.That((int)typeof(SurfaceMeshCrossSectionEllipseGenerator).Attributes, Is.EqualTo(0x00102001));
            Assert.That(typeof(SurfaceMeshCrossSectionEllipseGenerator).BaseType, Is.EqualTo(typeof(SurfaceMeshCrossSectionGenerator)));
            Assert.That(Fields(typeof(SurfaceMeshCrossSectionEllipseGenerator)), Is.EqualTo(new[]
            {
                "m_height:Single:0001", "m_angleStep:Single:0001", "m_angleRange:Vector2:0001", "m_angleClockwise:Boolean:0001"
            }));
            Assert.That(Methods(typeof(SurfaceMeshCrossSectionEllipseGenerator)), Is.EqualTo(new[]
            {
                "get_Type:08c6", "get_AngleStart:0881", "get_AngleEnd:0881", "GetOrigin:00c6", "GenerateMeshCrossSection:00c6",
                "GetPosition:0081", "GetTangent:0081", "CalculateLocationLocal:00c4", ".ctor:1886"
            }));
            BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(SurfaceMeshCrossSectionEllipseGenerator).GetField("m_height", privateInstance)),
                Is.EqualTo("TooltipAttribute(The height of the ellipse.) SerializeField() MinAttribute(0.01)"));
            Assert.That(AttributeRow(typeof(SurfaceMeshCrossSectionEllipseGenerator).GetField("m_angleStep", privateInstance)),
                Is.EqualTo("MinAttribute(1) TooltipAttribute(Step size for each evaluated angle.) SerializeField()"));
            Assert.That(AttributeRow(typeof(SurfaceMeshCrossSectionEllipseGenerator).GetField("m_angleRange", privateInstance)),
                Is.EqualTo("SerializeField() TooltipAttribute(Start and end angles.)"));
            Assert.That(AttributeRow(typeof(SurfaceMeshCrossSectionEllipseGenerator).GetField("m_angleClockwise", privateInstance)),
                Is.EqualTo("SerializeField() TooltipAttribute(Direction to step around ellipse.)"));

            Assert.That((int)typeof(SurfaceMeshGenerator).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(SurfaceMeshGenerator).BaseType, Is.EqualTo(typeof(SurfaceGenerator)));
            Assert.That(Attribute.IsDefined(typeof(SurfaceMeshGenerator), typeof(ExecuteInEditMode)), Is.True);
            Assert.That(Fields(typeof(SurfaceMeshGenerator)), Is.EqualTo(new[]
            {
                "m_material:Material:0001", "m_doubleSided:Boolean:0001", "m_centreCrossSectionGenerator:SurfaceMeshCrossSectionGenerator:0001",
                "m_leftCrossSections:List`1:0001", "m_rightCrossSections:List`1:0001"
            }));
            Assert.That(Methods(typeof(SurfaceMeshGenerator)), Is.EqualTo(new[] { ".ctor:1886" }));
            Assert.That(AttributeRow(typeof(SurfaceMeshGenerator).GetField("m_material", privateInstance)),
                Is.EqualTo("SerializeField() TooltipAttribute(Material to assign to the mesh.)"));
            Assert.That(AttributeRow(typeof(SurfaceMeshGenerator).GetField("m_leftCrossSections", privateInstance)),
                Is.EqualTo("SerializeReference()"));
        }

        // The cross-section types the generator selects between: 0x6D3234 and 0x6D4784 return the literals.
        [Test]
        public void TypesReturnTheirSerializedLiterals()
        {
            Assert.That(new SurfaceMeshCrossSectionEllipseGenerator().Type, Is.EqualTo(SurfaceCrossSectionType.Ellipse));
            Assert.That((int)new SurfaceMeshCrossSectionEllipseGenerator().Type, Is.EqualTo(unchecked((int)0x8CB9878F)));
            Assert.That(new SurfaceMeshCrossSectionPlaneGenerator().Type, Is.EqualTo(SurfaceCrossSectionType.Plane));
            Assert.That((int)new SurfaceMeshCrossSectionPlaneGenerator().Type, Is.EqualTo(unchecked((int)0xC91810CF)));
        }

        // SurfaceMeshGenerator..ctor 0x6D4D40 stores new lists in +0x50 and +0x58, then the path list
        // in +0x30, before the MonoBehaviour constructor; the centre generator stays unset.
        [Test]
        public void MeshGeneratorStartsWithEmptySectionAndPathLists()
        {
            using (var objects = new OwnedWorldObjects("lucid-surface-mesh-generator"))
            {
                GameObject host = objects.Create("generator");
                host.SetActive(false);
                var generator = host.AddComponent<SurfaceMeshGenerator>();
                Assert.That(Get(generator, "m_leftCrossSections"), Is.InstanceOf<List<SurfaceMeshCrossSectionGenerator>>().And.Empty);
                Assert.That(Get(generator, "m_rightCrossSections"), Is.InstanceOf<List<SurfaceMeshCrossSectionGenerator>>().And.Empty);
                Assert.That(Get(generator, "m_surfacePaths"), Is.InstanceOf<List<SurfacePathGenerator>>().And.Empty);
                Assert.That(Get(generator, "m_leftCrossSections"), Is.Not.SameAs(Get(generator, "m_rightCrossSections")));
                Assert.That(Get(generator, "m_centreCrossSectionGenerator"), Is.Null);
                Assert.That(Get(generator, "m_doubleSided"), Is.False);
            }
        }

        // ---- plane ----

        // GetOrigin 0x6D4790: direction = knot rotation * (AngleAxis(angle, Vector3.left) * Vector3.forward)
        // (0x6D47EF..0x6D484F); the result is origin + offsetLocal - direction * (offsetMultiplier * width)
        // (0x6D4854..0x6D4885). AngleAxis about left by 90 degrees turns forward into up.
        [Test]
        public void PlaneOriginIsPulledBackAlongTheTiltedDirection()
        {
            Vector3 origin = new Vector3(1f, 2f, 3f);
            Vector3 offset = new Vector3(0.5f, 0.25f, 0.125f);

            SurfaceMeshCrossSectionPlaneGenerator flat = NewPlane(2f, offset, 0f);
            AreClose(new Vector3(1.5f, 2.25f, 2.125f), flat.GetOrigin(Knot(origin, Quaternion.identity), 0.5f), "flat");
            AreClose(new Vector3(1.5f, 2.25f, 3.125f), flat.GetOrigin(Knot(origin, Quaternion.identity), 0f), "no multiplier");
            AreClose(new Vector3(0.5f, 2.25f, 3.125f), flat.GetOrigin(Knot(origin, Quaternion.Euler(0f, 90f, 0f)), 0.5f), "knot turned to face +x");

            SurfaceMeshCrossSectionPlaneGenerator tilted = NewPlane(2f, offset, 90f);
            AreClose(new Vector3(1.5f, 1.25f, 3.125f), tilted.GetOrigin(Knot(origin, Quaternion.identity), 0.5f), "tilted up");
        }

        // GenerateMeshCrossSection 0x6D4900: adds the origin and origin + direction * width (0x6D4A35..0x6D4B27),
        // stores the far point into the knot origin (0x6D4B2A) and joins the pair to the next knot with
        // AddQuad(first index, VertexStride 2) while knotStepCount - 1 > knotStepIndex (0x6D4B37..0x6D4B5C).
        [Test]
        public void PlaneSectionAddsTwoVerticesMovesTheKnotOriginAndJoinsTheNextKnot()
        {
            SurfaceMeshCrossSectionPlaneGenerator plane = NewPlane(2f, Vector3.zero, 90f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var knot = Knot(new Vector3(1f, 2f, 3f), Quaternion.identity);

            plane.GenerateMeshCrossSection(vertices, triangles, knot, 0, 2, false);
            AreCloseAll(new[] { new Vector3(1f, 2f, 3f), new Vector3(1f, 4f, 3f) }, vertices, "first section");
            AreClose(new Vector3(1f, 4f, 3f), knot.m_origin, "knot origin");
            Assert.That(triangles, Is.EqualTo(new[] { 0, 1, 2, 3, 2, 1 }));

            // The second knot is the last: it adds its pair, now from the moved origin, and no quad.
            plane.GenerateMeshCrossSection(vertices, triangles, knot, 1, 2, false);
            Assert.That(vertices.Count, Is.EqualTo(4));
            AreClose(new Vector3(1f, 4f, 3f), vertices[2], "second origin");
            AreClose(new Vector3(1f, 6f, 3f), vertices[3], "second far point");
            Assert.That(triangles.Count, Is.EqualTo(6), "the last knot adds no quad");

            // The quad starts at the first new vertex, and invertOrder reverses the winding.
            var inverted = new List<int>();
            plane.GenerateMeshCrossSection(vertices, inverted, Knot(Vector3.zero, Quaternion.identity), 0, 5, true);
            Assert.That(inverted, Is.EqualTo(new[] { 6, 5, 4, 5, 6, 7 }));
        }

        // CalculateLocationLocal 0x6D4B80: rotationLocal = AngleAxis(angle, Vector3.left) and
        // positionLocal = rotationLocal * (Vector3.right * distance) (0x6D4C21..0x6D4C3B).
        // CalculateRotationLocal 0x6D48A0 returns the same rotation.
        [Test]
        public void PlaneLocationFollowsTheTiltRotation()
        {
            SurfaceMeshCrossSectionPlaneGenerator plane = NewPlane(2f, Vector3.zero, 90f);
            var arguments = new object[] { 3f, null, null };
            Call(plane, "CalculateLocationLocal", arguments);
            AreClose(new Vector3(3f, 0f, 0f), (Vector3)arguments[1], "position along right");
            var rotation = (Quaternion)arguments[2];
            AreClose(Vector3.up, rotation * Vector3.forward, "forward tilts up");
            AreClose(Vector3.right, rotation * Vector3.right, "the tilt axis is unchanged");

            var rotationLocal = (Quaternion)Call(plane, "CalculateRotationLocal");
            AreClose(Vector3.up, rotationLocal * Vector3.forward, "CalculateRotationLocal");
        }

        // ---- ellipse ----

        // GetPosition 0x6D33B0: (0, sin(angle) * height, (1 - cos(angle)) * width) with angle in degrees.
        [Test]
        public void EllipsePositionIsTheSineHeightAndTheCosineWidthOffset()
        {
            SurfaceMeshCrossSectionEllipseGenerator ellipse = NewEllipse(2f, 3f, 45f, new Vector2(0f, 90f), true, Vector3.zero);
            AreClose(Vector3.zero, (Vector3)Call(ellipse, "GetPosition", 0f), "0 degrees");
            AreClose(new Vector3(0f, 2.1213203f, 0.5857864f), (Vector3)Call(ellipse, "GetPosition", 45f), "45 degrees");
            AreClose(new Vector3(0f, 3f, 2f), (Vector3)Call(ellipse, "GetPosition", 90f), "90 degrees");
            AreClose(new Vector3(0f, 0f, 4f), (Vector3)Call(ellipse, "GetPosition", 180f), "180 degrees");
            // GetTangent 0x6D3B60: (0, cos, sin) of the angle in degrees.
            AreClose(new Vector3(0f, 0.7071068f, 0.7071068f), (Vector3)Call(ellipse, "GetTangent", 45f), "tangent");
        }

        // GetOrigin 0x6D3260 reads the start angle when clockwise and the end angle otherwise
        // (sete at 0x6D327C), and returns origin - forward * (width * multiplier) - rotation * position.
        [Test]
        public void EllipseOriginStartsFromTheFirstSweptAngle()
        {
            Vector3 origin = new Vector3(1f, 2f, 3f);
            SurfaceMeshCrossSectionEllipseGenerator clockwise = NewEllipse(2f, 3f, 45f, new Vector2(0f, 90f), true, Vector3.zero);
            AreClose(new Vector3(1f, 2f, 2f), clockwise.GetOrigin(Knot(origin, Quaternion.identity), 0.5f), "clockwise starts at 0");
            SurfaceMeshCrossSectionEllipseGenerator anticlockwise = NewEllipse(2f, 3f, 45f, new Vector2(0f, 90f), false, Vector3.zero);
            AreClose(new Vector3(1f, -1f, 0f), anticlockwise.GetOrigin(Knot(origin, Quaternion.identity), 0.5f), "anticlockwise starts at 90");
        }

        // GenerateMeshCrossSection 0x6D33F0 clockwise: count = (int)((end - start) / step) = 2 (0x6D346D); the
        // loop visits indices 0..count + 1 (0x6D3514), the last at the end angle (0x6D351C..0x6D352B);
        // each vertex is rotation * (offsetLocal + position) + origin and also becomes the knot origin
        // (0x6D3594); quads use stride count + 2 while knotStepCount - 1 > knotStepIndex and the index <= count
        // (0x6D35DC..0x6D362C). The final knot rotation multiplies LookRotation(-GetTangent(angle),
        // Vector3.right) (0x6D36A8..0x6D3784), with the angle converted to radians twice (0x6D3691 repeats the
        // multiply by the degree factor before GetTangent converts again).
        [Test]
        public void EllipseSectionClockwiseSweepsToTheEndAngleAndTurnsTheKnot()
        {
            SurfaceMeshCrossSectionEllipseGenerator ellipse = NewEllipse(2f, 3f, 45f, new Vector2(0f, 90f), true, Vector3.zero);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var knot = Knot(Vector3.zero, Quaternion.identity);

            ellipse.GenerateMeshCrossSection(vertices, triangles, knot, 0, 2, false);

            AreCloseAll(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 2.1213203f, 0.5857864f), new Vector3(0f, 3f, 2f), new Vector3(0f, 3f, 2f)
            }, vertices, "clockwise vertices");
            AreClose(new Vector3(0f, 3f, 2f), knot.m_origin, "knot origin is the last vertex");
            Assert.That(triangles, Is.EqualTo(new[] { 0, 1, 4, 5, 4, 1, 1, 2, 5, 6, 5, 2, 2, 3, 6, 7, 6, 3 }));

            // The tangent angle 90 degrees is converted twice: phi = 90 * (pi / 180)^2 = 0.0274156 rad, so
            // the knot looks along -(0, cos phi, sin phi) with the up hint Vector3.right.
            AreClose(new Vector3(0f, -0.9996242f, -0.0274121f), knot.m_rotation * Vector3.forward, "knot forward");
            AreClose(Vector3.right, knot.m_rotation * Vector3.up, "knot up follows the Vector3.right hint");
        }

        // The anticlockwise sweep starts at the end angle with a negative step (0x6D3483), finishes at the
        // start angle, and takes its origin from the end angle. Vertices are rotated by the knot and offset
        // by offsetLocal; the closing tangent is at angle 0, so the knot faces -(0, 1, 0).
        [Test]
        public void EllipseSectionAnticlockwiseSweepsBackwardsFromTheEndAngle()
        {
            SurfaceMeshCrossSectionEllipseGenerator ellipse = NewEllipse(2f, 3f, 45f, new Vector2(0f, 90f), false, new Vector3(1f, 0f, 0f));
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var knot = Knot(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            ellipse.GenerateMeshCrossSection(vertices, triangles, knot, 0, 2, true);

            AreCloseAll(new[]
            {
                new Vector3(10f, 0f, -1f), new Vector3(8.5857864f, -0.8786797f, -1f), new Vector3(8f, -3f, -1f), new Vector3(8f, -3f, -1f)
            }, vertices, "anticlockwise vertices");
            AreClose(new Vector3(8f, -3f, -1f), knot.m_origin, "knot origin");
            Assert.That(triangles, Is.EqualTo(new[] { 4, 1, 0, 1, 4, 5, 5, 2, 1, 2, 5, 6, 6, 3, 2, 3, 6, 7 }));
            AreClose(new Vector3(0f, -1f, 0f), knot.m_rotation * Vector3.forward, "knot forward");
            AreClose(new Vector3(0f, 0f, -1f), knot.m_rotation * Vector3.up, "knot up is the turned hint");

            // The last knot of the chain adds its vertices but no quad.
            var lastTriangles = new List<int>();
            ellipse.GenerateMeshCrossSection(new List<Vector3>(), lastTriangles, Knot(Vector3.zero, Quaternion.identity), 1, 2, false);
            Assert.That(lastTriangles, Is.Empty);
        }

        // CalculateLocationLocal 0x6D3B90: the arc length is the sum of the distances between the positions at
        // consecutive whole degrees from the start while the angle is below the end (0x6D3C4B..0x6D3CA9); the
        // angle is distance / length * (end - start) with no start offset (0x6D3DA3..0x6D3DAF, +0), and the rotation
        // is LookRotation(tangent, Vector3.up). For the range 30..120 (width 2, height 3) the 90 chords sum to
        // 3.53546; half of that is 45 degrees, not 75.
        [Test]
        public void EllipseLocationIsProportionalToTheArcLengthWithoutAStartOffset()
        {
            SurfaceMeshCrossSectionEllipseGenerator ellipse = NewEllipse(2f, 3f, 1f, new Vector2(30f, 120f), true, Vector3.zero);
            var arguments = new object[] { 1.7677319f, null, null };
            Call(ellipse, "CalculateLocationLocal", arguments);
            WorldMemberAccess.AreClose(new Vector3(0f, 2.1213203f, 0.5857864f), (Vector3)arguments[1], "position at 45 degrees", 1e-3f);
            var rotation = (Quaternion)arguments[2];
            WorldMemberAccess.AreClose(new Vector3(0f, 0.7071068f, 0.7071068f), rotation * Vector3.forward, "tangent at 45 degrees", 1e-3f);
            WorldMemberAccess.AreClose(new Vector3(0f, 0.7071068f, -0.7071068f), rotation * Vector3.up, "up hint", 1e-3f);

            arguments = new object[] { 0f, null, null };
            Call(ellipse, "CalculateLocationLocal", arguments);
            WorldMemberAccess.AreClose(Vector3.zero, (Vector3)arguments[1], "distance 0 is angle 0", 1e-3f);
        }

        private static void AreCloseAll(Vector3[] expected, List<Vector3> actual, string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length), label + " count");
            for (int i = 0; i < expected.Length; i++)
                AreClose(expected[i], actual[i], label + " " + i);
        }
    }
}
