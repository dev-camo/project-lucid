using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Dreamteck.Splines;
using Dreamteck.Splines.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.IO.SVG (x86-64 0x4f0c40..0x4f91b0). The importer is driven through small svg files written to a per-test
    // temporary folder (no network, no shared state); the SVG y axis points down, so every imported y is negated. Expected points
    // are hand arithmetic from the native bodies cited per test, the arc geometry (PathArcTo) from the SVG endpoint conversion
    // those bodies implement. The helpers are private, so they are reached by reflection.
    public sealed class OriginalDreamteckSvgTests
    {
        private const float Tolerance = 1e-3f;

        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly List<GameObject> _owned = new List<GameObject>();

        private string _directory;

        private CultureInfo _culture;

        [SetUp]
        public void CreateTemporaryDirectory()
        {
            _directory = Path.Combine(Application.temporaryCachePath, "svgtests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _culture = Thread.CurrentThread.CurrentCulture;
        }

        [TearDown]
        public void DestroyOwnedObjectsAndFiles()
        {
            Thread.CurrentThread.CurrentCulture = _culture;
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private string WriteFile(string fileName, string content)
        {
            string path = Path.Combine(_directory, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        private static string Wrap(string body)
        {
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\">" + body + "</svg>";
        }

        // An SVG read from "shapes.svg" holding the body.
        private SVG Read(string body)
        {
            return new SVG(WriteFile("shapes.svg", Wrap(body)));
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ", expected " + expected.ToString("F5") + ")");
        }

        private static void AssertPositions(Spline spline, params Vector3[] expected)
        {
            Assert.That(spline.points.Length, Is.EqualTo(expected.Length), "point count");
            for (int i = 0; i < expected.Length; i++)
            {
                Near(spline.points[i].position, expected[i], "point " + i);
            }
        }

        private static object Call(object target, string method, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethods(InstanceAny).First(m => m.Name == method && m.GetParameters().Length == arguments.Length);
            try
            {
                return info.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        private static T Get<T>(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, InstanceAny);
            Assert.That(info, Is.Not.Null, field);
            return (T)info.GetValue(target);
        }

        private List<SplineComputer> Computers(SVG svg, SVG.Element elements = SVG.Element.All)
        {
            List<SplineComputer> computers = svg.CreateSplineComputers(Vector3.zero, Quaternion.identity, elements);
            foreach (SplineComputer computer in computers)
            {
                _owned.Add(computer.gameObject);
            }

            return computers;
        }

        // The public enums keep the shipped values; the private nested types and the cached lambda sit where the shipped compiler
        // put them: the field order, and the lambda <>c.<ReadPath>b__25_0 whose "25" is the member ordinal of ReadPath (eight
        // fields, three nested types and fourteen methods precede it).
        [Test]
        public void NestedTypesFieldsAndTheCachedLambdaKeepTheShippedLayout()
        {
            Assert.That(new[] { (int)SVG.Axis.X, (int)SVG.Axis.Y, (int)SVG.Axis.Z }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(new[] { (int)SVG.Element.All, (int)SVG.Element.Path, (int)SVG.Element.Polygon, (int)SVG.Element.Ellipse, (int)SVG.Element.Rectangle, (int)SVG.Element.Line }, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(typeof(SVG).BaseType, Is.EqualTo(typeof(SplineParser)));
            string[] fields = typeof(SVG).GetFields(InstanceAny | BindingFlags.DeclaredOnly).Select(f => f.Name).ToArray();
            Assert.That(fields, Is.EqualTo(new[] { "culture", "style", "paths", "polygons", "ellipses", "rectangles", "lines", "transformBuffer" }));

            string[] nested = typeof(SVG).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.That(nested, Is.EqualTo(new[] { "<>c", "Axis", "Element", "PathSegment" }));
            Type segment = typeof(SVG).GetNestedType("PathSegment", BindingFlags.NonPublic);
            Assert.That(segment.IsNestedAssembly, Is.True);
            Assert.That(segment.GetFields(InstanceAny | BindingFlags.DeclaredOnly).Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "startTangent", "endTangent", "endPoint" }));
            Type segmentType = segment.GetNestedType("Type", BindingFlags.NonPublic);
            Assert.That(Enum.GetNames(segmentType), Is.EqualTo(new[] { "Cubic", "CubicShort", "Quadratic", "QuadraticShort" }));

            Type cache = typeof(SVG).GetNestedType("<>c", BindingFlags.NonPublic);
            Assert.That(cache.GetField("<>9", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(cache.GetField("<>9__25_0", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(cache.GetMethod("<ReadPath>b__25_0", InstanceAny), Is.Not.Null);
        }

        // The file constructor 0x4f0c40: a missing file does nothing, a wrong extension logs an error but keeps the file name, svg and
        // xml (any case) are read, a document that is not XML logs the parser's message.
        [Test]
        public void FileConstructorHandlesMissingWrongAndBrokenFiles()
        {
            SVG missing = new SVG(Path.Combine(_directory, "missing.svg"));
            Assert.That(missing.name, Is.EqualTo(""));
            Assert.That(missing.CreateSplines().Count, Is.EqualTo(0));

            string wrong = WriteFile("shapes.txt", Wrap("<rect x=\"0\" y=\"0\" width=\"1\" height=\"1\"/>"));
            LogAssert.Expect(LogType.Error, "SVG Parsing ERROR: Wrong format. Please use SVG or XML");
            SVG other = new SVG(wrong);
            Assert.That(other.name, Is.EqualTo("shapes"));
            Assert.That(other.CreateSplines().Count, Is.EqualTo(0));

            SVG xml = new SVG(WriteFile("a.xml", Wrap("<rect x=\"0\" y=\"0\" width=\"1\" height=\"1\"/>")));
            Assert.That(xml.CreateSplines().Count, Is.EqualTo(1));
            SVG upper = new SVG(WriteFile("b.SVG", Wrap("<rect x=\"0\" y=\"0\" width=\"1\" height=\"1\"/>")));
            Assert.That(upper.name, Is.EqualTo("b"));
            Assert.That(upper.CreateSplines().Count, Is.EqualTo(1));

            LogAssert.Expect(LogType.Error, new Regex(".+"));
            SVG broken = new SVG(WriteFile("broken.svg", "this is not xml"));
            Assert.That(broken.name, Is.EqualTo("broken"));
            Assert.That(broken.CreateSplines().Count, Is.EqualTo(0));
        }

        // ReadRectangle 0x4f4130: four corners around the centre (x + w/2, -y - h/2), the first the top left, closed and linear;
        // the name is the id or fileName + "_rectangle" + the number of stored rectangles + 1; a missing required attribute
        // reads nothing.
        [Test]
        public void RectangleBecomesAClosedLinearRectangleWithTheYFlip()
        {
            SVG svg = Read("<rect x=\"10\" y=\"20\" width=\"30\" height=\"40\"/><rect id=\"box\" x=\"0\" y=\"0\" width=\"2\" height=\"2\"/><rect x=\"1\" y=\"2\" width=\"3\"/>");
            List<Spline> splines = svg.CreateSplines(SVG.Element.Rectangle);
            Assert.That(splines.Count, Is.EqualTo(2), "the rect without a height is ignored");
            Assert.That(splines[0].type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(splines[0].isClosed, Is.True);
            AssertPositions(splines[0], new Vector3(10f, -20f, 0f), new Vector3(40f, -20f, 0f), new Vector3(40f, -60f, 0f), new Vector3(10f, -60f, 0f));
            List<SplineComputer> computers = Computers(svg, SVG.Element.Rectangle);
            Assert.That(computers[0].name, Is.EqualTo("shapes_rectangle1"));
            Assert.That(computers[1].name, Is.EqualTo("box"));
        }

        // ReadRectangle: rx or ry makes it a RoundedRectangle (a missing ry copies rx): eight Bezier points around the same centre.
        // x=0, y=0, 10 x 10, radius 2: point 0 is (-5, 3) and point 1 (-3, 5) from the centre (5, -5).
        [Test]
        public void ARadiusMakesARoundedRectangleWithTheRadiusOnBothAxes()
        {
            SVG svg = Read("<rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" rx=\"2\"/>");
            Spline spline = svg.CreateSplines(SVG.Element.Rectangle).Single();
            Assert.That(spline.type, Is.EqualTo(Spline.Type.Bezier));
            Assert.That(spline.points.Length, Is.EqualTo(8));
            Assert.That(spline.isClosed, Is.True);
            Near(spline.points[0].position, new Vector3(0f, -2f, 0f), "point 0");
            Near(spline.points[1].position, new Vector3(2f, 0f, 0f), "point 1");
            Near(spline.points[4].position, new Vector3(10f, -8f, 0f), "point 4");
            Assert.That(Computers(svg, SVG.Element.Rectangle).Single().name, Is.EqualTo("shapes_roundedRectangle1"));
        }

        // ReadEllipse 0x4f3a40: a circle (r) and an ellipse (rx, ry) are Ellipse primitives centred at (cx, -cy); the id defaults to
        // fileName + "_circle" or "_ellipse" + the number of stored ellipses + 1. An ellipse needs both radii.
        [Test]
        public void CirclesAndEllipsesUseTheEllipsePrimitiveAtTheFlippedCentre()
        {
            SVG svg = Read("<circle cx=\"5\" cy=\"6\" r=\"2\"/><ellipse cx=\"1\" cy=\"2\" rx=\"3\" ry=\"1\"/><ellipse cx=\"1\" cy=\"2\" rx=\"3\"/><circle cy=\"6\" r=\"2\"/>");
            List<Spline> splines = svg.CreateSplines(SVG.Element.Ellipse);
            Assert.That(splines.Count, Is.EqualTo(2));
            Assert.That(splines[0].type, Is.EqualTo(Spline.Type.Bezier));
            Assert.That(splines[0].isClosed, Is.True);
            AssertPositions(splines[0], new Vector3(5f, -4f, 0f), new Vector3(3f, -6f, 0f), new Vector3(5f, -8f, 0f), new Vector3(7f, -6f, 0f));
            AssertPositions(splines[1], new Vector3(1f, -1f, 0f), new Vector3(-2f, -2f, 0f), new Vector3(1f, -3f, 0f), new Vector3(4f, -2f, 0f));
            List<SplineComputer> computers = Computers(svg, SVG.Element.Ellipse);
            Assert.That(computers[0].name, Is.EqualTo("shapes_circle1"));
            Assert.That(computers[1].name, Is.EqualTo("shapes_ellipse2"));
        }

        // ReadLine 0x4f3e00: two linear points with the y flip; an unnamed line is numbered after the stored ellipses
        // (here one), not after the lines.
        [Test]
        public void LineBecomesTwoLinearPointsAndIsNumberedAfterTheEllipses()
        {
            SVG svg = Read("<circle cx=\"0\" cy=\"0\" r=\"1\"/><line x1=\"1\" y1=\"2\" x2=\"4\" y2=\"6\"/><line x1=\"1\" y1=\"2\" x2=\"4\"/>");
            List<Spline> lines = svg.CreateSplines(SVG.Element.Line);
            Assert.That(lines.Count, Is.EqualTo(1), "the line without y2 is ignored");
            Assert.That(lines[0].type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(lines[0].isClosed, Is.False);
            AssertPositions(lines[0], new Vector3(1f, -2f, 0f), new Vector3(4f, -6f, 0f));
            Assert.That(Computers(svg, SVG.Element.Line).Single().name, Is.EqualTo("shapes_line2"));
        }

        // ReadPolygon 0x4f3740: number pairs with the y flip; a polygon gets a closing copy of the first point and is closed, a
        // polyline stays open; an odd number of values warns and reads nothing. Unnamed shapes are numbered after the stored polygons
        // and polylines (shared list), the polygon suffix has a trailing space.
        [Test]
        public void PolygonsAndPolylinesReadPointPairs()
        {
            SVG svg = Read("<polygon points=\"0,0 10,0 10,10\"/><polyline points=\"1,1 2,2\"/>");
            List<Spline> splines = svg.CreateSplines(SVG.Element.Polygon);
            Assert.That(splines.Count, Is.EqualTo(2));
            Assert.That(splines[0].type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(splines[0].isClosed, Is.True);
            AssertPositions(splines[0], new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, -10f, 0f), new Vector3(0f, 0f, 0f));
            Assert.That(splines[1].isClosed, Is.False);
            AssertPositions(splines[1], new Vector3(1f, -1f, 0f), new Vector3(2f, -2f, 0f));
            List<SplineComputer> computers = Computers(svg, SVG.Element.Polygon);
            Assert.That(computers[0].name, Is.EqualTo("shapes_polygon 1"));
            Assert.That(computers[1].name, Is.EqualTo("shapes_polyline2"));

            LogAssert.Expect(LogType.Warning, "There is an error with one of the polygon shapes.");
            SVG odd = Read("<polygon points=\"0,0 10\"/>");
            Assert.That(odd.CreateSplines().Count, Is.EqualTo(0));
        }

        // ReadPath, PathStart, PathLineTo, PathHorizontalLineTo, PathVerticalLineTo, PathClose (0x4f2bd0, 0x4f6c80..0x4f7130): a path is a
        // Bezier definition of linear points. h moves x by the value, v moves y down (so subtracts), l adds the flipped pair, H sets x,
        // V sets y to the negated value, z/Z adds a closing copy of the first point. Unnamed paths are "_path " + number.
        [Test]
        public void PathLinesFollowTheAbsoluteAndRelativeCommands()
        {
            SVG svg = Read("<path d=\"M0,0 L10,0 L10,10 Z\"/><path d=\"M5,5 h10 v10 l-3,0 H20 V30 z\"/>");
            List<Spline> splines = svg.CreateSplines(SVG.Element.Path);
            Assert.That(splines.Count, Is.EqualTo(2));
            Assert.That(splines[0].type, Is.EqualTo(Spline.Type.Bezier));
            Assert.That(splines[0].isClosed, Is.True);
            AssertPositions(splines[0], new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, -10f, 0f), new Vector3(0f, 0f, 0f));
            AssertPositions(splines[1], new Vector3(5f, -5f, 0f), new Vector3(15f, -5f, 0f), new Vector3(15f, -15f, 0f), new Vector3(12f, -15f, 0f), new Vector3(20f, -15f, 0f), new Vector3(20f, -30f, 0f), new Vector3(5f, -5f, 0f));
            Near(splines[0].points[1].tangent, new Vector3(10f, 0f, 0f), "linear points keep the tangent on the position");
            Assert.That(Computers(svg, SVG.Element.Path)[0].name, Is.EqualTo("shapes_path 1"));
        }

        // PathCurveTo 0x4f7210, cubic: the previous point turns Broken and its tangent2 becomes the segment's first control point
        // (plus the previous position for c); the end point becomes a Broken point with the second control point as tangent.
        [Test]
        public void CubicCurvesSetTheControlPointsAndRelativeOnesAddThePreviousPosition()
        {
            Spline absolute = Read("<path d=\"M0,0 C1,2 3,4 5,6\"/>").CreateSplines().Single();
            Assert.That(absolute.points.Length, Is.EqualTo(2));
            Assert.That(absolute.points[0].type, Is.EqualTo(SplinePoint.Type.Broken));
            Near(absolute.points[0].tangent2, new Vector3(1f, -2f, 0f), "first control point");
            Near(absolute.points[1].position, new Vector3(5f, -6f, 0f), "end point");
            Near(absolute.points[1].tangent, new Vector3(3f, -4f, 0f), "second control point");
            Near(absolute.points[1].tangent2, new Vector3(5f, -6f, 0f), "tangent2 on the end point");
            Assert.That(absolute.points[1].type, Is.EqualTo(SplinePoint.Type.Broken));

            Spline relative = Read("<path d=\"M10,10 c1,2 3,4 5,6\"/>").CreateSplines().Single();
            Near(relative.points[0].tangent2, new Vector3(11f, -12f, 0f), "first control point + previous");
            Near(relative.points[1].position, new Vector3(15f, -16f, 0f), "end point + previous");
            Near(relative.points[1].tangent, new Vector3(13f, -14f, 0f), "second control point + previous");
        }

        // PathCurveTo, CubicShort: the previous point becomes SmoothMirrored (tangent2 mirrored through the position) instead of
        // taking a control point, the segment gives the second control point and the end point.
        [Test]
        public void ShortCubicCurvesMirrorThePreviousTangent()
        {
            Spline spline = Read("<path d=\"M0,0 C1,2 3,4 5,6 S7,8 9,10\"/>").CreateSplines().Single();
            Assert.That(spline.points.Length, Is.EqualTo(3));
            Assert.That(spline.points[1].type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Near(spline.points[1].tangent2, new Vector3(7f, -8f, 0f), "mirrored: (5,-6) + ((5,-6) - (3,-4))");
            Near(spline.points[2].position, new Vector3(9f, -10f, 0f), "end point");
            Near(spline.points[2].tangent, new Vector3(7f, -8f, 0f), "second control point");
            Assert.That(spline.points[2].type, Is.EqualTo(SplinePoint.Type.Broken));
        }

        // PathCurveTo, CubicShort with relative coordinates: the end point and the second control point add the previous position, but
        // the mirrored tangent2 does not (0x4f74c4 sets ecx=0 and jumps past the relative addps at 0x4f74f1). "c1,2 3,4 5,6" from
        // (10,10) ends at (15,-16) with tangent (13,-14); "s2,2 4,4" ends at (19,-20) with tangent (15,-16) + (2,-2) = (17,-18), and the
        // previous point's tangent2 is 2 * (15,-16) - (13,-14) = (17,-18) (not 32,-34).
        [Test]
        public void RelativeShortCubicCurvesDoNotOffsetTheMirroredTangent()
        {
            Spline spline = Read("<path d=\"M10,10 c1,2 3,4 5,6 s2,2 4,4\"/>").CreateSplines().Single();
            Assert.That(spline.points.Length, Is.EqualTo(3));
            Assert.That(spline.points[1].type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Near(spline.points[1].tangent2, new Vector3(17f, -18f, 0f), "mirrored tangent2");
            Near(spline.points[2].position, new Vector3(19f, -20f, 0f), "end point");
            Near(spline.points[2].tangent, new Vector3(17f, -18f, 0f), "second control point");
        }

        // PathCurveTo, Quadratic and QuadraticShort: each control point moves two thirds of the way from the end towards the quadratic
        // control. Q3,6 from (0,0) to (6,0): tangent2 = (2,-4), tangent = (4,-4). T then reflects the pen tangent through the
        // position: control (8,4), second control (9.333, 2.667) for the end (12,0).
        [Test]
        public void QuadraticCurvesBecomeCubicControlsAtTwoThirds()
        {
            Spline spline = Read("<path d=\"M0,0 Q3,6 6,0 T12,0\"/>").CreateSplines().Single();
            Assert.That(spline.points.Length, Is.EqualTo(3));
            Assert.That(spline.points[0].type, Is.EqualTo(SplinePoint.Type.Broken));
            Near(spline.points[0].tangent2, new Vector3(2f, -4f, 0f), "first quadratic control");
            Near(spline.points[1].position, new Vector3(6f, 0f, 0f), "quadratic end");
            Near(spline.points[1].tangent, new Vector3(4f, -4f, 0f), "second quadratic control");
            Assert.That(spline.points[1].type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            Near(spline.points[1].tangent2, new Vector3(8f, 4f, 0f), "reflected control");
            Near(spline.points[2].position, new Vector3(12f, 0f, 0f), "short end");
            Near(spline.points[2].tangent, new Vector3(28f / 3f, 8f / 3f, 0f), "short second control");
        }

        // PathArcTo 0x4f76f0: the half circle from (0,0) to (10,0) with radius 5, large-arc 0 and sweep 1 passes through the top
        // ((5, 5) after the y flip) in two Bezier pieces cut at the quarter turn: points (0,0), (5,5), (10,0).
        [Test]
        public void AnArcBecomesBezierPiecesOnTheEllipse()
        {
            Spline spline = Read("<path d=\"M0,0 A5,5 0 0 1 10,0\"/>").CreateSplines().Single();
            Assert.That(spline.points.Length, Is.EqualTo(3));
            Near(spline.points[0].position, new Vector3(0f, 0f, 0f), "start");
            Near(spline.points[1].position, new Vector3(5f, 5f, 0f), "top");
            Near(spline.points[2].position, new Vector3(10f, 0f, 0f), "end");
            Assert.That(spline.points[0].type, Is.EqualTo(SplinePoint.Type.Broken));
            Assert.That(spline.points[1].type, Is.EqualTo(SplinePoint.Type.Broken));

            // fewer than seven numbers index past the array (0x4f8138 raises IndexOutOfRange)
            Assert.Throws<IndexOutOfRangeException>(() => Read("<path d=\"M0,0 A5,5 0 0 1 10\"/>"));
            Assert.Throws<IndexOutOfRangeException>(() => Read("<path d=\"M0,0 A\"/>"));
        }

        // PathArcTo 0x4f76f0, relative end point (v5 + last.x, v6 - last.y; addss x, subss y at 0x4f79c4..0x4f79d7): from (10,10) the
        // relative half circle "a5,5 0 0 1 10,0" ends at (20,10) in the flipped frame. Tangent signs (0x4f7e08..0x4f7ea6, 0x4f800b): from
        // (0,0) to (10,0) over the top, the path leaves the start upwards, and passes the top at (5,5) moving along +x, so tangent
        // lies before the top (x below 5) and tangent2 after it (x above 5), both level with it.
        [Test]
        public void ArcsAcceptRelativeEndPointsAndKeepTheTravelDirectionInTheTangents()
        {
            Spline relative = Read("<path d=\"M10,10 a5,5 0 0 1 10,0\"/>").CreateSplines().Single();
            AssertPositions(relative, new Vector3(10f, -10f, 0f), new Vector3(15f, -5f, 0f), new Vector3(20f, -10f, 0f));

            Spline arc = Read("<path d=\"M0,0 A5,5 0 0 1 10,0\"/>").CreateSplines().Single();
            Assert.That(arc.points[1].tangent.y, Is.EqualTo(5f).Within(1e-2f), "top tangent level");
            Assert.That(arc.points[1].tangent.x, Is.LessThan(5f), "top tangent before the top");
            Assert.That(arc.points[1].tangent2.y, Is.EqualTo(5f).Within(1e-2f), "top tangent2 level");
            Assert.That(arc.points[1].tangent2.x, Is.GreaterThan(5f), "top tangent2 after the top");
            Assert.That(arc.points[0].tangent2.x, Is.EqualTo(0f).Within(1e-2f), "start leaves straight up");
            Assert.That(arc.points[0].tangent2.y, Is.GreaterThan(0f));
        }

        // CalculateEllipseParams 0x4f87c0, the SVG endpoint conversion. The half circle (0,0) -> (10,0), radius 5, flags (0, 1): centre
        // (5,0), start angle pi, sweep pi; radii too small are scaled up by sqrt(lambda) and negative radii made positive; the
        // quarter arc (0,0) -> (5,5) gives centre (0,5), start -pi/2, sweep pi/2; a false sweep flag turns the other way (-pi).
        [Test]
        public void EllipseParametersFollowTheEndpointConversion()
        {
            SVG svg = Read(string.Empty);
            object[] half = { new Vector2(0f, 0f), new Vector2(10f, 0f), 0f, 5f, 5f, false, true, null, null, null, null, null };
            Call(svg, "CalculateEllipseParams", half);
            Assert.That(Vector2.Distance((Vector2)half[7], new Vector2(5f, 0f)), Is.LessThan(Tolerance), "centre");
            Assert.That((float)half[8], Is.EqualTo(Mathf.PI).Within(Tolerance), "start angle");
            Assert.That((float)half[9], Is.EqualTo(Mathf.PI).Within(Tolerance), "sweep");
            Assert.That((float)half[10], Is.EqualTo(5f).Within(Tolerance));
            Assert.That((float)half[11], Is.EqualTo(5f).Within(Tolerance));

            object[] tooSmall = { new Vector2(0f, 0f), new Vector2(10f, 0f), 0f, 2f, 2f, false, true, null, null, null, null, null };
            Call(svg, "CalculateEllipseParams", tooSmall);
            Assert.That((float)tooSmall[10], Is.EqualTo(5f).Within(Tolerance), "2 * sqrt(6.25)");
            Assert.That((float)tooSmall[11], Is.EqualTo(5f).Within(Tolerance));
            Assert.That(Vector2.Distance((Vector2)tooSmall[7], new Vector2(5f, 0f)), Is.LessThan(Tolerance));

            object[] negative = { new Vector2(0f, 0f), new Vector2(10f, 0f), 0f, -5f, -5f, false, true, null, null, null, null, null };
            Call(svg, "CalculateEllipseParams", negative);
            Assert.That((float)negative[10], Is.EqualTo(5f).Within(Tolerance));
            Assert.That((float)negative[11], Is.EqualTo(5f).Within(Tolerance));

            object[] quarter = { new Vector2(0f, 0f), new Vector2(5f, 5f), 0f, 5f, 5f, false, true, null, null, null, null, null };
            Call(svg, "CalculateEllipseParams", quarter);
            Assert.That(Vector2.Distance((Vector2)quarter[7], new Vector2(0f, 5f)), Is.LessThan(Tolerance), "quarter centre");
            Assert.That((float)quarter[8], Is.EqualTo(-Mathf.PI / 2f).Within(Tolerance), "quarter start angle");
            Assert.That((float)quarter[9], Is.EqualTo(Mathf.PI / 2f).Within(Tolerance), "quarter sweep");

            object[] backwards = { new Vector2(0f, 0f), new Vector2(10f, 0f), 0f, 5f, 5f, false, false, null, null, null, null, null };
            Call(svg, "CalculateEllipseParams", backwards);
            Assert.That((float)backwards[9], Is.EqualTo(-Mathf.PI).Within(Tolerance), "sweep flag 0 turns the other way");

            // Both flags set from (0,0) to (5,5): fa == fs negates the centre coefficient, giving centre (5,0), start angle pi and
            // VectorAngle((-1,0), (0,1)) = -90 degrees; a set sweep flag adds 360 to a negative delta (0x4f8a7a..0x4f8a82): 3 pi / 2.
            object[] wrapped = { new Vector2(0f, 0f), new Vector2(5f, 5f), 0f, 5f, 5f, true, true, null, null, null, null, null };
            Call(svg, "CalculateEllipseParams", wrapped);
            Assert.That(Vector2.Distance((Vector2)wrapped[7], new Vector2(5f, 0f)), Is.LessThan(Tolerance), "wrapped centre");
            Assert.That((float)wrapped[8], Is.EqualTo(Mathf.PI).Within(Tolerance), "wrapped start angle");
            Assert.That((float)wrapped[9], Is.EqualTo(3f * Mathf.PI / 2f).Within(Tolerance), "negative delta gets 360 degrees added");
        }

        // VectorAngle 0x4f8e00 (signed by the cross product, positive for zero), ModP 0x4f8af0 / 0x4f8dd0 (never negative),
        // GetArcSegmentPercentages 0x4f8b20 (both ends and every quarter strictly between, reversed when start > end) and
        // FlipTangents 0x4f8ac0 (swaps tangent and tangent2).
        [Test]
        public void ArcHelpersMatchTheirDefinitions()
        {
            SVG svg = Read(string.Empty);
            Assert.That((float)Call(svg, "VectorAngle", new Vector2(1f, 0f), new Vector2(0f, 1f)), Is.EqualTo(Mathf.PI / 2f).Within(1e-5f));
            Assert.That((float)Call(svg, "VectorAngle", new Vector2(1f, 0f), new Vector2(0f, -1f)), Is.EqualTo(-Mathf.PI / 2f).Within(1e-5f));
            Assert.That((float)Call(svg, "VectorAngle", new Vector2(1f, 0f), new Vector2(-2f, 0f)), Is.EqualTo(Mathf.PI).Within(1e-5f));
            Assert.That((float)Call(svg, "VectorAngle", new Vector2(3f, 0f), new Vector2(5f, 0f)), Is.EqualTo(0f).Within(1e-5f));

            MethodInfo floatModP = typeof(SVG).GetMethod("ModP", InstanceAny, null, new[] { typeof(float), typeof(float) }, null);
            MethodInfo doubleModP = typeof(SVG).GetMethod("ModP", InstanceAny, null, new[] { typeof(double), typeof(double) }, null);
            Assert.That((float)floatModP.Invoke(svg, new object[] { -0.25f, 1f }), Is.EqualTo(0.75f));
            Assert.That((float)floatModP.Invoke(svg, new object[] { 1.25f, 1f }), Is.EqualTo(0.25f));
            Assert.That((double)doubleModP.Invoke(svg, new object[] { -0.125, 1.0 }), Is.EqualTo(0.875));
            Assert.That((double)doubleModP.Invoke(svg, new object[] { 2.5, 1.0 }), Is.EqualTo(0.5));

            Assert.That((double[])Call(svg, "GetArcSegmentPercentages", 0.1, 0.6), Is.EqualTo(new[] { 0.1, 0.25, 0.5, 0.6 }));
            Assert.That((double[])Call(svg, "GetArcSegmentPercentages", 0.6, 0.1), Is.EqualTo(new[] { 0.6, 0.5, 0.25, 0.1 }));
            Assert.That((double[])Call(svg, "GetArcSegmentPercentages", 0.5, 1.0), Is.EqualTo(new[] { 0.5, 0.75, 1.0 }));
            Assert.That((double[])Call(svg, "GetArcSegmentPercentages", 0.3, 0.3), Is.EqualTo(new[] { 0.3, 0.3 }));
            Assert.That((double[])Call(svg, "GetArcSegmentPercentages", 0.25, 0.75), Is.EqualTo(new[] { 0.25, 0.5, 0.75 }));
            // The ends are ordered with minsd end,start (0x4f8bc1) and maxsd start,end (0x4f8c07): they take the second operand when
            // either is NaN, so a NaN end leaves the start in place and only the quarter after it is added, where Math.Min and
            // Math.Max would turn both ends into NaN.
            Assert.That((double[])Call(svg, "GetArcSegmentPercentages", 0.6, double.NaN), Is.EqualTo(new[] { 0.6, 0.75 }));

            SplinePoint point = new SplinePoint(Vector3.zero);
            point.type = SplinePoint.Type.Broken;
            point.tangent = new Vector3(1f, 2f, 3f);
            point.tangent2 = new Vector3(4f, 5f, 6f);
            object[] arguments = { point };
            Call(svg, "FlipTangents", arguments);
            SplinePoint flipped = (SplinePoint)arguments[0];
            Near(flipped.tangent, new Vector3(4f, 5f, 6f), "tangent");
            Near(flipped.tangent2, new Vector3(1f, 2f, 3f), "tangent2");
        }

        private static Vector3[][] Segments(object segments)
        {
            List<Vector3[]> result = new List<Vector3[]>();
            foreach (object segment in (IEnumerable)segments)
            {
                result.Add(new[]
                {
                    Get<Vector3>(segment, "startTangent"),
                    Get<Vector3>(segment, "endTangent"),
                    Get<Vector3>(segment, "endPoint")
                });
            }

            return result.ToArray();
        }

        private static object SegmentType(int value)
        {
            Type segment = typeof(SVG).GetNestedType("PathSegment", BindingFlags.NonPublic);
            return Enum.ToObject(segment.GetNestedType("Type", BindingFlags.NonPublic), value);
        }

        // ParsePathSegment 0x4f8140: C takes 6 numbers per segment (start control, end control, end point), S and Q 4, T 2, all with
        // the y flip; S has no start control and Q no end control. The index of every type but C advances 4 numbers per segment, so a
        // T with a second pair runs off the numbers. Too few numbers (or an unknown type) log an error and give one empty segment.
        [Test]
        public void ParsePathSegmentGroupsNumbersPerCommandType()
        {
            SVG svg = Read(string.Empty);
            Vector3[][] cubic = Segments(Call(svg, "ParsePathSegment", "C1,2 3,4 5,6 7,8 9,10 11,12", SegmentType(0)));
            Assert.That(cubic.Length, Is.EqualTo(2));
            Near(cubic[0][0], new Vector3(1f, -2f, 0f), "cubic start 0");
            Near(cubic[0][1], new Vector3(3f, -4f, 0f), "cubic end tangent 0");
            Near(cubic[0][2], new Vector3(5f, -6f, 0f), "cubic end 0");
            Near(cubic[1][0], new Vector3(7f, -8f, 0f), "cubic start 1");
            Near(cubic[1][2], new Vector3(11f, -12f, 0f), "cubic end 1");

            Vector3[][] smooth = Segments(Call(svg, "ParsePathSegment", "S1,2 3,4 5,6 7,8", SegmentType(1)));
            Assert.That(smooth.Length, Is.EqualTo(2));
            Near(smooth[0][0], Vector3.zero, "short start");
            Near(smooth[0][1], new Vector3(1f, -2f, 0f), "short end tangent 0");
            Near(smooth[0][2], new Vector3(3f, -4f, 0f), "short end 0");
            Near(smooth[1][1], new Vector3(5f, -6f, 0f), "short end tangent 1");
            Near(smooth[1][2], new Vector3(7f, -8f, 0f), "short end 1");

            Vector3[][] quadratic = Segments(Call(svg, "ParsePathSegment", "Q1,2 3,4", SegmentType(2)));
            Assert.That(quadratic.Length, Is.EqualTo(1));
            Near(quadratic[0][0], new Vector3(1f, -2f, 0f), "quadratic control");
            Near(quadratic[0][1], Vector3.zero, "quadratic end tangent");
            Near(quadratic[0][2], new Vector3(3f, -4f, 0f), "quadratic end");

            Vector3[][] shortQuadratic = Segments(Call(svg, "ParsePathSegment", "T1,2", SegmentType(3)));
            Assert.That(shortQuadratic.Length, Is.EqualTo(1));
            Near(shortQuadratic[0][0], Vector3.zero, "T start");
            Near(shortQuadratic[0][2], new Vector3(1f, -2f, 0f), "T end");
            Assert.Throws<ArgumentOutOfRangeException>(() => Call(svg, "ParsePathSegment", "T1,2 3,4", SegmentType(3)), "the second pair is read from index 4");

            LogAssert.Expect(LogType.Log, "Error in C1,2 Cubic");
            Vector3[][] empty = Segments(Call(svg, "ParsePathSegment", "C1,2", SegmentType(0)));
            Assert.That(empty.Length, Is.EqualTo(1));
            Near(empty[0][2], Vector3.zero, "empty segment");
            LogAssert.Expect(LogType.Log, "Error in C1,2,3,4,5,6 9");
            Assert.That(Segments(Call(svg, "ParsePathSegment", "C1,2,3,4,5,6", SegmentType(9))).Length, Is.EqualTo(1));
        }

        private static List<object> Transformations(SVG svg, string content)
        {
            return ((IEnumerable)Call(svg, "ParseTransformations", content)).Cast<object>().ToList();
        }

        // ParseTransformations 0x4f5c10 (numbers are parsed with the current culture, so the invariant one is set for the test): every
        // supported call, case folded; translate and scale need two numbers, matrix six, the others one; surplus numbers are ignored.
        [Test]
        public void TransformationCallsAreParsedWithTheirParameters()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            SVG svg = Read(string.Empty);
            List<object> list = Transformations(svg, "Translate(10, 20) rotate(45) scale(2 3) skewX(30) skewY(-15) matrix(1,0,0,1,5,6)");
            Assert.That(list.Select(t => t.GetType().Name).ToArray(), Is.EqualTo(new[] { "Translate", "Rotate", "Scale", "SkewX", "SkewY", "MatrixTransform" }));
            Assert.That(Get<Vector2>(list[0], "offset"), Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(Get<float>(list[1], "angle"), Is.EqualTo(45f));
            Assert.That(Get<Vector2>(list[2], "multiplier"), Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(Get<float>(list[3], "amount"), Is.EqualTo(30f));
            Assert.That(Get<float>(list[4], "amount"), Is.EqualTo(-15f));
            Matrix4x4 matrix = Get<Matrix4x4>(list[5], "transformMatrix");
            Assert.That(matrix.GetRow(0), Is.EqualTo(new Vector4(1f, 0f, 0f, 5f)));
            Assert.That(matrix.GetRow(1), Is.EqualTo(new Vector4(0f, 1f, 0f, -6f)));

            Assert.That(Transformations(svg, "translate(5) scale(2) matrix(1,2,3) rotate() unknown(1)").Count, Is.EqualTo(0), "too few numbers");
            List<object> surplus = Transformations(svg, "rotate(10 20) translate(-1.5, 2.25, 9)");
            Assert.That(Get<float>(surplus[0], "angle"), Is.EqualTo(10f));
            Assert.That(Get<Vector2>(surplus[1], "offset"), Is.EqualTo(new Vector2(-1.5f, 2.25f)));
        }

        // ParseTransformations 0x4f5c10 calls Single.Parse(string) (0x4f605b, token 0x0600097f), which uses the current culture: under
        // de-DE the '.' of "1.5" is a group separator, so translate(1.5, 2) reads (15, 2) where an invariant parse would give (1.5, 2).
        [Test]
        public void TransformationNumbersAreParsedWithTheCurrentCulture()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            SVG svg = Read(string.Empty);
            List<object> list = Transformations(svg, "translate(1.5, 2)");
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(Get<Vector2>(list[0], "offset"), Is.EqualTo(new Vector2(15f, 2f)));
        }

        // Traverse 0x4f2510 / ParseTransformation 0x4f2b00 / WriteBufferTo 0x4f5b50: a group's transformations apply to every shape
        // inside and are dropped after its subtree; they multiply parent-first. translate(10,20) moves by (10,-20);
        // scale(2,3) inside it first scales the y-flipped points: (10,-10) -> (20,-30) -> (30,-50).
        [Test]
        public void GroupTransformationsApplyToTheirSubtreeOnly()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            SVG svg = Read("<g transform=\"translate(10,20)\"><polyline points=\"0,0 10,0\"/></g><polyline points=\"0,0 10,0\"/><g transform=\"translate(10,20)\"><g transform=\"scale(2,3)\"><polyline points=\"0,0 10,10\"/></g><polyline points=\"1,1 2,2\"/></g>");
            List<Spline> splines = svg.CreateSplines(SVG.Element.Polygon);
            Assert.That(splines.Count, Is.EqualTo(4));
            AssertPositions(splines[0], new Vector3(10f, -20f, 0f), new Vector3(20f, -20f, 0f));
            AssertPositions(splines[1], new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f));
            AssertPositions(splines[2], new Vector3(10f, -20f, 0f), new Vector3(30f, -50f, 0f));
            AssertPositions(splines[3], new Vector3(11f, -21f, 0f), new Vector3(12f, -22f, 0f));
        }

        // ReadPath: after the path is stored with the group transformations, the node's own transform is pushed and every definition
        // it made is transformed by the whole stack again - an own transform applies once, a group's twice. Own translate(5,5) moves
        // (0,0),(10,0) to (5,-5),(15,-5); inside translate(10,20) the same path is moved by (10,-20) twice.
        [Test]
        public void APathIsTransformedASecondTimeByTheWholeStack()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            SVG own = Read("<path d=\"M0,0 L10,0\" transform=\"translate(5,5)\"/>");
            AssertPositions(own.CreateSplines().Single(), new Vector3(5f, -5f, 0f), new Vector3(15f, -5f, 0f));
            SVG grouped = Read("<g transform=\"translate(10,20)\"><path d=\"M0,0 L10,0\"/></g>");
            AssertPositions(grouped.CreateSplines().Single(), new Vector3(20f, -40f, 0f), new Vector3(30f, -40f, 0f));
        }

        // PathStart 0x4f6c80: every M or m starts another definition; a relative m continues from the last point of the last stored
        // path (m5,5 after (2,-1) gives (7,-6)); with no stored path it faults on Enumerable.Last.
        [Test]
        public void EachMoveStartsAPathAndARelativeMoveContinuesFromTheLastStoredOne()
        {
            SVG svg = Read("<path id=\"p\" d=\"M1,1 L2,1 m5,5 l1,0\"/>");
            List<Spline> splines = svg.CreateSplines(SVG.Element.Path);
            Assert.That(splines.Count, Is.EqualTo(2));
            AssertPositions(splines[0], new Vector3(1f, -1f, 0f), new Vector3(2f, -1f, 0f));
            AssertPositions(splines[1], new Vector3(7f, -6f, 0f), new Vector3(8f, -6f, 0f));
            List<SplineComputer> computers = Computers(svg, SVG.Element.Path);
            Assert.That(computers.Select(c => c.name).ToArray(), Is.EqualTo(new[] { "p", "p" }));

            string file = WriteFile("relative.svg", Wrap("<path d=\"m1,1\"/>"));
            Assert.Throws<InvalidOperationException>(() => new SVG(file));
        }

        // CreateSplineComputers 0x4f4690 / CreateSplines 0x4f50d0: paths, polygons, ellipses, rectangles and lines, in that order,
        // each only for All or its own element; the computers take the position and rotation.
        [Test]
        public void ElementSelectionAndOrderOfTheCreatedSplines()
        {
            SVG svg = Read("<line id=\"l\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"/><rect id=\"r\" x=\"0\" y=\"0\" width=\"1\" height=\"1\"/><circle id=\"c\" cx=\"0\" cy=\"0\" r=\"1\"/><polygon id=\"g\" points=\"0,0 1,0 1,1\"/><path id=\"p\" d=\"M0,0 L1,1\"/>");
            Assert.That(Computers(svg).Select(c => c.name).ToArray(), Is.EqualTo(new[] { "p", "g", "c", "r", "l" }));
            SVG.Element[] elements = { SVG.Element.Path, SVG.Element.Polygon, SVG.Element.Ellipse, SVG.Element.Rectangle, SVG.Element.Line };
            string[] names = { "p", "g", "c", "r", "l" };
            for (int i = 0; i < elements.Length; i++)
            {
                Assert.That(Computers(svg, elements[i]).Select(c => c.name).ToArray(), Is.EqualTo(new[] { names[i] }), elements[i].ToString());
                Assert.That(svg.CreateSplines(elements[i]).Count, Is.EqualTo(1), elements[i].ToString());
            }

            Assert.That(svg.CreateSplines().Count, Is.EqualTo(5));
            List<SplineComputer> placed = svg.CreateSplineComputers(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f), SVG.Element.Line);
            _owned.Add(placed[0].gameObject);
            Near(placed[0].transform.position, new Vector3(1f, 2f, 3f), "computer position");
            Assert.That(Quaternion.Angle(placed[0].transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-3f));
        }

        // MapPoint 0x4f24c0 and GetAttributeContent 0x4f59f0.
        [Test]
        public void MapPointProjectsOntoTheAxisPlaneAndAttributeContentFallsBackToError()
        {
            SVG svg = Read(string.Empty);
            Vector3 point = new Vector3(1f, 2f, 3f);
            Assert.That((Vector2)Call(svg, "MapPoint", point, SVG.Axis.X), Is.EqualTo(new Vector2(3f, -2f)));
            Assert.That((Vector2)Call(svg, "MapPoint", point, SVG.Axis.Y), Is.EqualTo(new Vector2(1f, -3f)));
            Assert.That((Vector2)Call(svg, "MapPoint", point, SVG.Axis.Z), Is.EqualTo(new Vector2(1f, -2f)));
            Assert.That((Vector2)Call(svg, "MapPoint", point, (SVG.Axis)7), Is.EqualTo(new Vector2(1f, 2f)), "an unknown axis keeps (x, y)");

            XmlDocument document = new XmlDocument();
            document.LoadXml("<a b=\"1\" c=\"two words\"/>");
            Assert.That((string)Call(svg, "GetAttributeContent", document.DocumentElement, "b"), Is.EqualTo("1"));
            Assert.That((string)Call(svg, "GetAttributeContent", document.DocumentElement, "c"), Is.EqualTo("two words"));
            Assert.That((string)Call(svg, "GetAttributeContent", document.DocumentElement, "zz"), Is.EqualTo("ERROR"));
        }

        private SplineComputer NewComputer(string name, Spline.Type type, params SplinePoint[] points)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            computer.type = type;
            computer.SetPoints(points);
            return computer;
        }

        // The computer constructor 0x4f1160 and Write 0x4f16d0 (culture set to the invariant one for the number text): a Bezier
        // computer is written as a path "M x,y C c1,c2,point" with the previous point's tangent2, this point's tangent and position, a
        // linear one as a polyline, all stroked black, 3 wide and unfilled, attributes in the order id, d/points, stroke,
        // stroke-width, fill and then version and xmlns on the root. Axis picks the plane: Z (x,-y), Y (x,-z), X (z,-y).
        [Test]
        public void WriteEncodesBezierPathsAndLinearPolylinesForEachAxis()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            SplineComputer bezier = NewComputer(
                "curve",
                Spline.Type.Bezier,
                new SplinePoint(new Vector3(0f, 1f, 3f), new Vector3(-1f, 1f, 3f), new Vector3(1f, 1f, 3f), Vector3.up, 1f, Color.white),
                new SplinePoint(new Vector3(10f, 2f, 4f), new Vector3(9f, 2f, 4f), new Vector3(11f, 2f, 4f), Vector3.up, 1f, Color.white));
            SplineComputer line = NewComputer(
                "poly",
                Spline.Type.Linear,
                new SplinePoint(new Vector3(0f, 1f, 3f)),
                new SplinePoint(new Vector3(10f, 2f, 4f)),
                new SplinePoint(new Vector3(20f, 3f, 5f)));
            SVG svg = new SVG(new List<SplineComputer> { null, bezier, line });
            Assert.That(svg.CreateSplines().Count, Is.EqualTo(2), "the null computer is skipped");

            string[] axisPaths = { "M3,-1C3,-1,4,-2,4,-2", "M0,-3C1,-3,9,-4,10,-4", "M0,-1C1,-1,9,-2,10,-2" };
            SVG.Axis[] axes = { SVG.Axis.X, SVG.Axis.Y, SVG.Axis.Z };
            string[] axisPolylines = { "3,-1,4,-2,5,-3", "0,-3,10,-4,20,-5", "0,-1,10,-2,20,-3" };
            for (int i = 0; i < 3; i++)
            {
                string output = Path.Combine(_directory, "out" + i + ".svg");
                svg.Write(output, axes[i]);
                XmlDocument document = new XmlDocument();
                document.Load(output);
                XmlElement root = document.DocumentElement;
                Assert.That(root.Name, Is.EqualTo("svg"));
                Assert.That(root.Attributes.Cast<XmlAttribute>().Select(a => a.Name + "=" + a.Value).ToArray(), Is.EqualTo(new[] { "version=1.1", "xmlns=http://www.w3.org/2000/svg" }));
                Assert.That(root.ChildNodes.Count, Is.EqualTo(2));
                XmlElement path = (XmlElement)root.ChildNodes[0];
                Assert.That(path.Name, Is.EqualTo("path"));
                Assert.That(path.Attributes.Cast<XmlAttribute>().Select(a => a.Name).ToArray(), Is.EqualTo(new[] { "id", "d", "stroke", "stroke-width", "fill" }));
                Assert.That(path.GetAttribute("id"), Is.EqualTo("curve"));
                Assert.That(path.GetAttribute("d"), Is.EqualTo(axisPaths[i]), axes[i].ToString());
                Assert.That(path.GetAttribute("stroke"), Is.EqualTo("black"));
                Assert.That(path.GetAttribute("stroke-width"), Is.EqualTo("3"));
                Assert.That(path.GetAttribute("fill"), Is.EqualTo("none"));
                XmlElement polyline = (XmlElement)root.ChildNodes[1];
                Assert.That(polyline.Name, Is.EqualTo("polyline"));
                Assert.That(polyline.Attributes.Cast<XmlAttribute>().Select(a => a.Name).ToArray(), Is.EqualTo(new[] { "id", "points", "stroke", "stroke-width", "fill" }));
                Assert.That(polyline.GetAttribute("points"), Is.EqualTo(axisPolylines[i]), axes[i].ToString());
            }
        }

        // The computer constructor: Catmull-Rom (and B-Spline) computers are converted to Bezier, a closed computer gives a closed
        // definition that Write ends with "z" (a closed linear one becomes a polygon).
        [Test]
        public void ComputersAreConvertedToBezierAndClosedOnesAreClosed()
        {
            SplineComputer catmull = NewComputer("cat", Spline.Type.CatmullRom, new SplinePoint(new Vector3(0f, 0f, 0f)), new SplinePoint(new Vector3(1f, 1f, 0f)), new SplinePoint(new Vector3(2f, 0f, 0f)));
            SplineComputer closed = NewComputer("closed", Spline.Type.Linear, new SplinePoint(new Vector3(0f, 0f, 0f)), new SplinePoint(new Vector3(1f, 1f, 0f)), new SplinePoint(new Vector3(2f, 0f, 0f)));
            closed.Close();
            SVG svg = new SVG(new List<SplineComputer> { catmull, closed });
            List<Spline> splines = svg.CreateSplines();
            Assert.That(splines.Count, Is.EqualTo(2));
            Assert.That(splines[0].type, Is.EqualTo(Spline.Type.Bezier), "converted");
            Assert.That(splines[0].isClosed, Is.False);
            Assert.That(splines[1].isClosed, Is.True);

            string output = Path.Combine(_directory, "closed.svg");
            svg.Write(output);
            XmlDocument document = new XmlDocument();
            document.Load(output);
            XmlElement root = document.DocumentElement;
            Assert.That(((XmlElement)root.ChildNodes[0]).Name, Is.EqualTo("path"));
            Assert.That(((XmlElement)root.ChildNodes[1]).Name, Is.EqualTo("polygon"));
        }
    }
}
