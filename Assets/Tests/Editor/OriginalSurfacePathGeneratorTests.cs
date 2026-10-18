using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.SurfacePathGenerator (TypeDef 0x02000aad) and its ellipse, line, sine,
    // spiral and animation-curve paths with their ribbon subtypes (0x02000aaa..0x02000ab4,
    // x86-64 0x6DAB30..0x6DCAC0). The generators are plain serializable objects, so each case
    // builds its own instances and lists and touches no shared state.
    public sealed class OriginalSurfacePathGeneratorTests
    {
        private static readonly float Root2 = Mathf.Sqrt(0.5f);

        private static void SameRotation(Quaternion expected, Quaternion actual, string label)
        {
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThanOrEqualTo(0.01f),
                label + ": expected " + expected.eulerAngles.ToString("F3") + " but was " + actual.eulerAngles.ToString("F3"));
        }

        private static T Path<T>(SurfacePathGenerator.AxisAlignment alignment) where T : SurfacePathGenerator, new()
        {
            var path = new T();
            Set(path, "m_axisAlignment", alignment);
            return path;
        }

        private static SurfacePathEllipseGenerator Ellipse(bool clockwise)
        {
            var ellipse = Path<SurfacePathEllipseGenerator>(SurfacePathGenerator.AxisAlignment.XZ);
            Set(ellipse, "m_curveSegments", 2);
            Set(ellipse, "m_axisLengths", new Vector2(2f, 1f));
            Set(ellipse, "m_angleRange", new Vector2(0f, 90f));
            Set(ellipse, "m_clockwise", clockwise);
            return ellipse;
        }

        private static SurfacePathSpiralGenerator Spiral(SurfacePathGenerator.AxisAlignment alignment, bool clockwise)
        {
            var spiral = Path<SurfacePathSpiralGenerator>(alignment);
            Set(spiral, "m_length", 2f);
            Set(spiral, "m_curveSegments", 2);
            Set(spiral, "m_axisLengths", new Vector2(1f, 1f));
            Set(spiral, "m_angleRange", new Vector2(0f, 180f));
            Set(spiral, "m_clockwise", clockwise);
            return spiral;
        }

        private static List<LightweightTransform> Knots(SurfacePathGenerator path, params LightweightTransform[] existing)
        {
            var knots = new List<LightweightTransform>(existing);
            path.GenerateKnots(knots);
            return knots;
        }

        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(SurfacePathGenerator).Attributes, Is.EqualTo(0x00102081));
            Assert.That((int)typeof(SurfacePathGenerator.AxisAlignment).Attributes, Is.EqualTo(0x00000102));
            Assert.That(System.Enum.GetNames(typeof(SurfacePathGenerator.AxisAlignment)), Is.EqualTo(new[] { "XZ", "XY", "YX", "YZ", "ZX", "ZY" }));
            Assert.That(Fields(typeof(SurfacePathGenerator)), Is.EqualTo(new[] { "m_axisAlignment:AxisAlignment:0001" }));
            Assert.That(Methods(typeof(SurfacePathGenerator)), Is.EqualTo(new[] { "GenerateKnots:05c6", "GetOrigin:0084", "GetAxis:0084", ".ctor:1884" }));

            foreach (System.Type type in new[]
            {
                typeof(SurfacePathEllipseGenerator), typeof(SurfacePathLineGenerator), typeof(SurfacePathSineCurveGenerator),
                typeof(SurfacePathSpiralGenerator), typeof(SurfacePathAnimCurveGenerator), typeof(RibbonPathEllipseGenerator),
                typeof(RibbonPathLineGenerator), typeof(RibbonPathSineCurveGenerator), typeof(RibbonPathSpiralGenerator)
            })
                Assert.That((int)type.Attributes, Is.EqualTo(0x00102001), type.Name);

            Assert.That(Fields(typeof(SurfacePathEllipseGenerator)), Is.EqualTo(new[]
                { "m_curveSegments:Int32:0001", "m_axisLengths:Vector2:0001", "m_angleRange:Vector2:0001", "m_clockwise:Boolean:0001" }));
            Assert.That(Methods(typeof(SurfacePathEllipseGenerator)), Is.EqualTo(new[]
                { "get_AngleStart:0881", "get_AngleEnd:0881", "GenerateKnots:00c6", "GetPosition:0081", "GetTangent:0081", ".ctor:1886" }));
            Assert.That(Fields(typeof(SurfacePathLineGenerator)), Is.EqualTo(new[]
                { "m_length:Single:0001", "m_curveSegments:Int32:0001", "m_orientation:Vector3:0001" }));
            Assert.That(Methods(typeof(SurfacePathLineGenerator)), Is.EqualTo(new[] { "GenerateKnots:00c6", ".ctor:1886" }));
            Assert.That(Fields(typeof(SurfacePathSineCurveGenerator)), Is.EqualTo(new[]
                { "m_curveSegments:Int32:0001", "m_curveHeight:Single:0001", "m_curveAmplitude:Single:0001", "m_curveLength:Single:0001" }));
            Assert.That(Methods(typeof(SurfacePathSineCurveGenerator)), Is.EqualTo(new[] { "GenerateKnots:00c6", "GetPosition:0081", ".ctor:1886" }));
            Assert.That(Fields(typeof(SurfacePathSpiralGenerator)), Is.EqualTo(new[]
            {
                "m_length:Single:0001", "m_curveSegments:Int32:0001", "m_axisLengths:Vector2:0001", "m_angleRange:Vector2:0001",
                "m_clockwise:Boolean:0001"
            }));
            Assert.That(Methods(typeof(SurfacePathSpiralGenerator)), Is.EqualTo(new[]
                { "get_AngleStart:0881", "get_AngleEnd:0881", "GenerateKnots:00c6", "GetPosition:0081", ".ctor:1886" }));
            Assert.That(Methods(typeof(SurfacePathAnimCurveGenerator)), Is.EqualTo(new[] { "GenerateKnots:00c6", ".ctor:1886" }));
            foreach (System.Type ribbon in new[]
                { typeof(RibbonPathEllipseGenerator), typeof(RibbonPathLineGenerator), typeof(RibbonPathSineCurveGenerator), typeof(RibbonPathSpiralGenerator) })
            {
                Assert.That(Fields(ribbon), Is.Empty, ribbon.Name);
                Assert.That(Methods(ribbon), Is.EqualTo(new[] { ".ctor:1886" }), ribbon.Name);
            }

            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(SurfacePathSineCurveGenerator).GetField("m_curveAmplitude", field)), Is.EqualTo("MinAttribute(0.001) SerializeField()"));
            Assert.That(AttributeRow(typeof(SurfacePathSineCurveGenerator).GetField("m_curveLength", field)), Is.EqualTo("SerializeField() MinAttribute(1)"));
            Assert.That(AttributeRow(typeof(SurfacePathLineGenerator).GetField("m_length", field)), Is.EqualTo("MinAttribute(1) SerializeField()"));
            Assert.That(AttributeRow(typeof(SurfacePathSpiralGenerator).GetField("m_curveSegments", field)), Is.EqualTo("SerializeField() MinAttribute(1)"));
        }

        // GetAxis 0x6DB600: v0 is sign-flipped (xorps 0x29a6cc0) when clockwise, then the six-entry
        // jump table at 0x6DB674 places (v0, v1); an alignment above 5 (unsigned) takes the XZ row.
        [TestCase(0, 1f, 0f, 2f)]
        [TestCase(1, 1f, 2f, 0f)]
        [TestCase(2, 2f, 1f, 0f)]
        [TestCase(3, 0f, 1f, 2f)]
        [TestCase(4, 2f, 0f, 1f)]
        [TestCase(5, 0f, 2f, 1f)]
        [TestCase(6, 1f, 0f, 2f)]
        [TestCase(-1, 1f, 0f, 2f)]
        public void GetAxisPlacesBothValuesOnTheAlignedAxes(int alignment, float x, float y, float z)
        {
            var path = Path<SurfacePathLineGenerator>((SurfacePathGenerator.AxisAlignment)alignment);
            Assert.That((Vector3)Call(path, "GetAxis", 1f, 2f, false), Is.EqualTo(new Vector3(x, y, z)));
            Vector3 clockwise = (Vector3)Call(path, "GetAxis", 1f, 2f, true);
            Assert.That(clockwise, Is.EqualTo(new Vector3(x == 1f ? -1f : x, y == 1f ? -1f : y, z == 1f ? -1f : z)), "only v0 is negated");
        }

        // GetOrigin 0x6DB380: the last knot's location and orientation, else zero and identity.
        [Test]
        public void GetOriginContinuesFromTheLastKnot()
        {
            var path = new SurfacePathLineGenerator();
            var args = new object[] { null, null, new List<LightweightTransform>() };
            MethodInfo getOrigin = typeof(SurfacePathGenerator).GetMethod("GetOrigin", BindingFlags.Instance | BindingFlags.NonPublic);
            getOrigin.Invoke(path, args);
            Assert.That(args[0], Is.EqualTo(Vector3.zero));
            Assert.That(args[1], Is.EqualTo(Quaternion.identity));

            Quaternion last = Quaternion.Euler(0f, 30f, 0f);
            args[2] = new List<LightweightTransform>
            {
                new LightweightTransform(new Vector3(9f, 9f, 9f), Quaternion.identity),
                new LightweightTransform(new Vector3(1f, 2f, 3f), last)
            };
            getOrigin.Invoke(path, args);
            Assert.That(args[0], Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(args[1], Is.EqualTo(last));
        }

        // Line 0x6DB6B0: rotation = origin * Euler(m_orientation) (0x6DB81B, then the inlined
        // product), positions step m_length / m_curveSegments along its forward; the i == 0 knot is
        // added only to an empty list (0x6DB941); a negative segment count adds nothing (0x6DB92C).
        [Test]
        public void LineStepsAlongItsTurnedForwardAndSkipsTheSharedStart()
        {
            var line = new SurfacePathLineGenerator();
            Set(line, "m_length", 4f);
            Set(line, "m_curveSegments", 2);
            Set(line, "m_orientation", new Vector3(0f, 90f, 0f));
            List<LightweightTransform> knots = Knots(line);
            Assert.That(knots, Has.Count.EqualTo(3));
            AreClose(Vector3.zero, knots[0].Location, "start");
            AreClose(new Vector3(2f, 0f, 0f), knots[1].Location, "middle");
            AreClose(new Vector3(4f, 0f, 0f), knots[2].Location, "end");
            foreach (LightweightTransform knot in knots) SameRotation(Quaternion.Euler(0f, 90f, 0f), knot.Orientation, "shared rotation");

            List<LightweightTransform> continued = Knots(line, new LightweightTransform(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, -90f, 0f)));
            Assert.That(continued, Has.Count.EqualTo(3), "the existing knot replaces the start");
            AreClose(new Vector3(1f, 2f, 5f), continued[1].Location, "continued middle");
            AreClose(new Vector3(1f, 2f, 7f), continued[2].Location, "continued end");
            SameRotation(Quaternion.identity, continued[2].Orientation, "origin rotation composed with the orientation");

            Set(line, "m_curveSegments", 0);
            Assert.That(Knots(line), Has.Count.EqualTo(1), "zero segments keep only the start");
            Set(line, "m_curveSegments", -1);
            Assert.That(Knots(line), Is.Empty, "negative segments add nothing");
        }

        // Sine 0x6DBB50: angle and distance start at 0 and advance by (m_curveAmplitude,
        // m_curveLength) / segments (divps 0x6DBCDB); GetPosition 0x6DBF90 is
        // GetAxis(distance, sin(angle * Deg2Rad) * m_curveHeight, false). Knots keep the origin rotation.
        [TestCase(SurfacePathGenerator.AxisAlignment.XY, 2f, 2f, 0f)]
        [TestCase(SurfacePathGenerator.AxisAlignment.ZY, 0f, 2f, 2f)]
        [TestCase(SurfacePathGenerator.AxisAlignment.XZ, 2f, 0f, 2f)]
        public void SineAdvancesTheAngleAndTheDistanceTogether(SurfacePathGenerator.AxisAlignment alignment, float x, float y, float z)
        {
            var sine = Path<SurfacePathSineCurveGenerator>(alignment);
            Set(sine, "m_curveSegments", 2);
            Set(sine, "m_curveHeight", 2f);
            Set(sine, "m_curveAmplitude", 180f);
            Set(sine, "m_curveLength", 4f);
            List<LightweightTransform> knots = Knots(sine);
            Assert.That(knots, Has.Count.EqualTo(3));
            AreClose(Vector3.zero, knots[0].Location, "start");
            AreClose(new Vector3(x, y, z), knots[1].Location, "quarter wave at half length");
            foreach (LightweightTransform knot in knots) SameRotation(Quaternion.identity, knot.Orientation, "origin rotation");

            Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
            List<LightweightTransform> continued = Knots(sine, new LightweightTransform(new Vector3(0f, 1f, 0f), turn));
            Assert.That(continued, Has.Count.EqualTo(3));
            AreClose(turn * new Vector3(x, y, z) + new Vector3(0f, 1f, 0f), continued[1].Location, "rotated about the origin knot");
            SameRotation(turn, continued[2].Orientation, "origin rotation is kept");
        }

        // Ellipse 0x6DAB80: offset = origin - rot * GetPosition(start); each knot is
        // rot * GetPosition(angle) + offset facing LookRotation(rot * GetTangent(angle), rot * up).
        // GetPosition 0x6DB490 = GetAxis(len.x * cos, len.y * sin, clockwise); GetTangent 0x6DB550 =
        // GetAxis(-sin, cos, clockwise) with no axis lengths. The step is (end - start) * Deg2Rad / n.
        [Test]
        public void EllipseStartsOnTheOriginAndFacesAlongItsTangent()
        {
            List<LightweightTransform> knots = Knots(Ellipse(false));
            Assert.That(knots, Has.Count.EqualTo(3));
            AreClose(Vector3.zero, knots[0].Location, "start");
            AreClose(new Vector3(2f * Root2 - 2f, 0f, Root2), knots[1].Location, "45 degrees");
            AreClose(new Vector3(-2f, 0f, 1f), knots[2].Location, "90 degrees");
            AreClose(Vector3.forward, knots[0].Orientation * Vector3.forward, "start tangent");
            AreClose(new Vector3(-Root2, 0f, Root2), knots[1].Orientation * Vector3.forward, "45 degree tangent ignores the lengths");
            AreClose(Vector3.left, knots[2].Orientation * Vector3.forward, "end tangent");
            AreClose(Vector3.up, knots[2].Orientation * Vector3.up, "up is the origin up");

            List<LightweightTransform> clockwise = Knots(Ellipse(true));
            AreClose(Vector3.zero, clockwise[0].Location, "clockwise start");
            AreClose(new Vector3(2f, 0f, 1f), clockwise[2].Location, "clockwise mirrors x");
            AreClose(Vector3.right, clockwise[2].Orientation * Vector3.forward, "clockwise tangent");

            Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
            List<LightweightTransform> continued = Knots(Ellipse(false), new LightweightTransform(Vector3.zero, turn));
            Assert.That(continued, Has.Count.EqualTo(3), "the existing knot replaces the start");
            AreClose(new Vector3(1f, 0f, 2f), continued[2].Location, "offset is taken after the origin rotation");
            AreClose(Vector3.forward, continued[2].Orientation * Vector3.forward, "tangent is rotated with the origin");
        }

        // Spiral 0x6DC090: the height axis is Vector3.one - GetAxis(1, 1, false) (constants
        // 0x29a6fe0/0x29a6df0/0x29a6d40 plus Vector3.one at 0x6DC447); each knot is
        // rot * (GetPosition(angle) + axis * distance) + offset and turns by
        // AngleAxis(angle * (clockwise ? -Rad2Deg : Rad2Deg), axis) (table 0x29a7cb0) after rot.
        [Test]
        public void SpiralRisesAlongTheRemainingAxisAndTurnsAboutIt()
        {
            List<LightweightTransform> knots = Knots(Spiral(SurfacePathGenerator.AxisAlignment.XZ, false));
            Assert.That(knots, Has.Count.EqualTo(3));
            AreClose(Vector3.zero, knots[0].Location, "start");
            AreClose(new Vector3(-1f, 1f, 1f), knots[1].Location, "90 degrees, one unit up");
            AreClose(new Vector3(-2f, 2f, 0f), knots[2].Location, "180 degrees, two units up");
            SameRotation(Quaternion.identity, knots[0].Orientation, "start turn");
            SameRotation(Quaternion.AngleAxis(90f, Vector3.up), knots[1].Orientation, "90 degree turn");
            SameRotation(Quaternion.AngleAxis(180f, Vector3.up), knots[2].Orientation, "180 degree turn");

            List<LightweightTransform> clockwise = Knots(Spiral(SurfacePathGenerator.AxisAlignment.XZ, true));
            AreClose(new Vector3(1f, 1f, 1f), clockwise[1].Location, "clockwise mirrors x");
            SameRotation(Quaternion.AngleAxis(-90f, Vector3.up), clockwise[1].Orientation, "clockwise turns the other way");

            List<LightweightTransform> xy = Knots(Spiral(SurfacePathGenerator.AxisAlignment.XY, false));
            AreClose(new Vector3(-1f, 1f, 1f), xy[1].Location, "XY rises along z");
            SameRotation(Quaternion.AngleAxis(90f, Vector3.forward), xy[1].Orientation, "XY turns about z");
            List<LightweightTransform> yz = Knots(Spiral(SurfacePathGenerator.AxisAlignment.YZ, false));
            AreClose(new Vector3(1f, -1f, 1f), yz[1].Location, "YZ rises along x");
            SameRotation(Quaternion.AngleAxis(90f, Vector3.right), yz[1].Orientation, "YZ turns about x");

            List<LightweightTransform> continued = Knots(Spiral(SurfacePathGenerator.AxisAlignment.XZ, false),
                new LightweightTransform(new Vector3(0f, 5f, 0f), Quaternion.identity));
            Assert.That(continued, Has.Count.EqualTo(3), "the existing knot replaces the start");
            AreClose(new Vector3(-1f, 6f, 1f), continued[1].Location, "continued from the last knot");
        }

        // SurfacePathAnimCurveGenerator.GenerateKnots 0x6DAB30 is an empty body.
        [Test]
        public void AnimationCurvePathAddsNoKnots()
        {
            var existing = new LightweightTransform(Vector3.one, Quaternion.identity);
            Assert.That(Knots(new SurfacePathAnimCurveGenerator(), existing), Is.EqualTo(new[] { existing }));
            Assert.That(Knots(new SurfacePathAnimCurveGenerator()), Is.Empty);
        }

        // The ribbon subtypes' constructors tail-call Object..ctor (0x6DB6A0, 0x6DBB40, 0x6DC060,
        // 0x6DCAC0): they generate exactly as their surface bases.
        [Test]
        public void RibbonPathsGenerateAsTheirSurfaceBases()
        {
            var ribbonLine = new RibbonPathLineGenerator();
            Set(ribbonLine, "m_length", 2f);
            Set(ribbonLine, "m_curveSegments", 1);
            List<LightweightTransform> knots = Knots(ribbonLine);
            Assert.That(knots, Has.Count.EqualTo(2));
            AreClose(new Vector3(0f, 0f, 2f), knots[1].Location, "line forward");
            Assert.That(new RibbonPathEllipseGenerator(), Is.InstanceOf<SurfacePathEllipseGenerator>());
            Assert.That(new RibbonPathSineCurveGenerator(), Is.InstanceOf<SurfacePathSineCurveGenerator>());
            Assert.That(new RibbonPathSpiralGenerator(), Is.InstanceOf<SurfacePathSpiralGenerator>());
        }
    }
}
