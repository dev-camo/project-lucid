using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using Dreamteck.Splines.Primitives;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.Primitives. Expected values are derived from the x86-64 bodies (addresses in each comment) and worked by
    // hand from their constants and formulas, not read back from the restored source. The handle constant is 2 * 0.41421354 / 3
    // = 0.27614236 for the capsule, 2 * 0.41421354 / 1.5 = 0.5522847 for the ellipse and 2 * 0.27614236 for the rounded rectangle.
    // Each test owns the game objects it creates and DestroyImmediate removes them in TearDown.
    public sealed class OriginalDreamteckSplinePrimitiveTests
    {
        private const float Tolerance = 1e-4f;

        private const float CapsuleHandle = 0.27614236f;

        private const float EllipseHandle = 0.5522847f;

        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ", expected " + expected.ToString("F6") + ")");
        }

        private static void AssertPoint(SplinePoint point, Vector3 position, Vector3 tangent, Vector3 tangent2, string message)
        {
            Near(point.position, position, message + " position");
            Near(point.tangent, tangent, message + " tangent");
            Near(point.tangent2, tangent2, message + " tangent2");
        }

        // Constructors 0x4e84e0 (Capsule), 0x4e8c10 (Ellipse), 0x4e8df0 (Line), 0x4e8fb0 (Ngon), 0x4e9400 (Rectangle), 0x4ea780
        // (RoundedRectangle), 0x4eb000 (Spiral), 0x4ebb30 (Star) store their defaults before the base constructor; the base
        // constructor (0x4e8500) allocates an empty point array and zeroes offset and rotation.
        [Test]
        public void FreshPrimitivesKeepTheShippedDefaults()
        {
            Capsule capsule = new Capsule();
            Assert.That(capsule.radius, Is.EqualTo(1f));
            Assert.That(capsule.height, Is.EqualTo(2f));

            Ellipse ellipse = new Ellipse();
            Assert.That(ellipse.xRadius, Is.EqualTo(1f));
            Assert.That(ellipse.yRadius, Is.EqualTo(1f));

            Line line = new Line();
            Assert.That(line.mirror, Is.True);
            Assert.That(line.length, Is.EqualTo(1f));
            Assert.That(line.segments, Is.EqualTo(1));

            Ngon ngon = new Ngon();
            Assert.That(ngon.radius, Is.EqualTo(1f));
            Assert.That(ngon.sides, Is.EqualTo(3));

            Rectangle rectangle = new Rectangle();
            Assert.That(rectangle.size, Is.EqualTo(Vector2.one));

            RoundedRectangle rounded = new RoundedRectangle();
            Assert.That(rounded.size, Is.EqualTo(Vector2.one));
            Assert.That(rounded.xRadius, Is.EqualTo(0.25f));
            Assert.That(rounded.yRadius, Is.EqualTo(0.25f));

            Spiral spiral = new Spiral();
            Assert.That(spiral.startRadius, Is.EqualTo(1f));
            Assert.That(spiral.endRadius, Is.EqualTo(1f));
            Assert.That(spiral.stretch, Is.EqualTo(1f));
            Assert.That(spiral.iterations, Is.EqualTo(3));
            Assert.That(spiral.clockwise, Is.True);
            Assert.That(spiral.curve, Is.Not.Null);
            Assert.That(spiral.curve.length, Is.EqualTo(0), "a new AnimationCurve has no keys");

            Star star = new Star();
            Assert.That(star.radius, Is.EqualTo(1f));
            Assert.That(star.depth, Is.EqualTo(0.5f));
            Assert.That(star.sides, Is.EqualTo(5));

            foreach (SplinePrimitive primitive in new SplinePrimitive[] { capsule, ellipse, line, ngon, rectangle, rounded, spiral, star })
            {
                Assert.That(primitive.offset, Is.EqualTo(Vector3.zero));
                Assert.That(primitive.rotation, Is.EqualTo(Vector3.zero));
                Assert.That(primitive.is2D, Is.False);
                Assert.That(primitive.GetPoints(), Is.Empty);
                Assert.That(primitive.GetIsClosed(), Is.False);
            }
        }

        // GetSplineType bodies: the base 0x4eb900 returns 0 (CatmullRom); Capsule 0x4e77d0, Ellipse 0x4e85b0, RoundedRectangle
        // 0x4e9450 and Spiral 0x4ea7e0 return 2 (Bezier); Line 0x4e8c30, Ngon 0x4e8e10, Rectangle 0x4e8fd0 and Star 0x4eb920
        // return 3 (Linear).
        [Test]
        public void SplineTypesFollowTheNativeReturnValues()
        {
            Assert.That(new SplinePrimitive().GetSplineType(), Is.EqualTo(Spline.Type.CatmullRom));
            Assert.That(new Capsule().GetSplineType(), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(new Ellipse().GetSplineType(), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(new RoundedRectangle().GetSplineType(), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(new Spiral().GetSplineType(), Is.EqualTo(Spline.Type.Bezier));
            Assert.That(new Line().GetSplineType(), Is.EqualTo(Spline.Type.Linear));
            Assert.That(new Ngon().GetSplineType(), Is.EqualTo(Spline.Type.Linear));
            Assert.That(new Rectangle().GetSplineType(), Is.EqualTo(Spline.Type.Linear));
            Assert.That(new Star().GetSplineType(), Is.EqualTo(Spline.Type.Linear));
        }

        // The base Generate (0x4e8310) is empty, so Calculate (0x4eb080: Generate, then ApplyOffset) leaves a bare primitive empty
        // and open.
        [Test]
        public void BasePrimitiveCalculatesToNothing()
        {
            SplinePrimitive primitive = new SplinePrimitive();
            primitive.Calculate();
            Assert.That(primitive.GetPoints(), Is.Empty);
            Assert.That(primitive.GetIsClosed(), Is.False);
        }

        // Capsule Generate 0x4e77e0: closed, CreatePoints(6, SmoothMirrored). With radius 1 and height 2 the points sit on the
        // caps; every tangent is the position plus 0.27614236 * radius along the listed direction and tangent2 is the mirror
        // (2 * position - tangent). Point 0 uses down although it is on the top right, so the directions are not a rotation of
        // each other; an up/down swap would flip the sign of every tangent offset.
        [Test]
        public void CapsuleBuildsSixSmoothPointsWithQuarterCircleHandles()
        {
            Capsule capsule = new Capsule();
            capsule.Calculate();
            SplinePoint[] points = capsule.GetPoints();
            Assert.That(capsule.GetIsClosed(), Is.True);
            Assert.That(points.Length, Is.EqualTo(6));
            float k = CapsuleHandle;
            AssertPoint(points[0], new Vector3(0.5f, 1f, 0f), new Vector3(0.5f, 1f - k, 0f), new Vector3(0.5f, 1f + k, 0f), "point 0");
            AssertPoint(points[1], new Vector3(0f, 1.5f, 0f), new Vector3(k, 1.5f, 0f), new Vector3(-k, 1.5f, 0f), "point 1");
            AssertPoint(points[2], new Vector3(-0.5f, 1f, 0f), new Vector3(-0.5f, 1f + k, 0f), new Vector3(-0.5f, 1f - k, 0f), "point 2");
            AssertPoint(points[3], new Vector3(-0.5f, -1f, 0f), new Vector3(-0.5f, -1f + k, 0f), new Vector3(-0.5f, -1f - k, 0f), "point 3");
            AssertPoint(points[4], new Vector3(0f, -1.5f, 0f), new Vector3(-k, -1.5f, 0f), new Vector3(k, -1.5f, 0f), "point 4");
            AssertPoint(points[5], new Vector3(0.5f, -1f, 0f), new Vector3(0.5f, -1f - k, 0f), new Vector3(0.5f, -1f + k, 0f), "point 5");
            foreach (SplinePoint point in points)
            {
                Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
                Near(point.normal, Vector3.up, "normal");
                Assert.That(point.color, Is.EqualTo(Color.white));
                Assert.That(point.size, Is.EqualTo(1f));
            }
        }

        [Test]
        public void CapsuleScalesWithRadiusAndHeight()
        {
            Capsule capsule = new Capsule { radius = 2f, height = 6f };
            capsule.Calculate();
            SplinePoint[] points = capsule.GetPoints();
            float k = CapsuleHandle * 2f;
            AssertPoint(points[0], new Vector3(1f, 3f, 0f), new Vector3(1f, 3f - k, 0f), new Vector3(1f, 3f + k, 0f), "point 0");
            AssertPoint(points[1], new Vector3(0f, 4f, 0f), new Vector3(k, 4f, 0f), new Vector3(-k, 4f, 0f), "point 1");
            AssertPoint(points[4], new Vector3(0f, -4f, 0f), new Vector3(-k, -4f, 0f), new Vector3(k, -4f, 0f), "point 4");
        }

        // Ellipse Generate 0x4e85c0: closed, four SmoothMirrored points up, left, down, right at the radii, tangent offset
        // 2 * 0.41421354 / 1.5 times the radius of the perpendicular axis (note 1.5 where the capsule divides by 3).
        [Test]
        public void EllipseBuildsFourSmoothPointsOnTheAxes()
        {
            Ellipse ellipse = new Ellipse { xRadius = 2f, yRadius = 3f };
            ellipse.Calculate();
            SplinePoint[] points = ellipse.GetPoints();
            Assert.That(ellipse.GetIsClosed(), Is.True);
            Assert.That(points.Length, Is.EqualTo(4));
            float kx = EllipseHandle * 2f;
            float ky = EllipseHandle * 3f;
            AssertPoint(points[0], new Vector3(0f, 3f, 0f), new Vector3(kx, 3f, 0f), new Vector3(-kx, 3f, 0f), "up");
            AssertPoint(points[1], new Vector3(-2f, 0f, 0f), new Vector3(-2f, ky, 0f), new Vector3(-2f, -ky, 0f), "left");
            AssertPoint(points[2], new Vector3(0f, -3f, 0f), new Vector3(-kx, -3f, 0f), new Vector3(kx, -3f, 0f), "down");
            AssertPoint(points[3], new Vector3(2f, 0f, 0f), new Vector3(2f, -ky, 0f), new Vector3(2f, ky, 0f), "right");
            foreach (SplinePoint point in points)
            {
                Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            }
        }

        // Line Generate 0x4e8c40: open, segments + 1 points; point i sits at origin + up * length * (i / (count - 1)) and the
        // origin is up * length * -0.5 when mirrored. CreatePoints (0x4e8320) only reallocates when the length changes.
        [Test]
        public void LineSpreadsPointsEvenlyAndMirrorCentresIt()
        {
            Line line = new Line { length = 2f, segments = 4 };
            line.Calculate();
            SplinePoint[] points = line.GetPoints();
            Assert.That(line.GetIsClosed(), Is.False);
            Assert.That(points.Length, Is.EqualTo(5));
            float[] centred = { -1f, -0.5f, 0f, 0.5f, 1f };
            for (int i = 0; i < points.Length; i++)
            {
                Near(points[i].position, new Vector3(0f, centred[i], 0f), "mirrored " + i);
            }

            line.mirror = false;
            line.Calculate();
            Assert.That(line.GetPoints(), Is.SameAs(points), "an equal point count reuses the array");
            for (int i = 0; i < points.Length; i++)
            {
                Near(points[i].position, new Vector3(0f, centred[i] + 1f, 0f), "unmirrored " + i);
            }

            line.segments = 5;
            line.Calculate();
            Assert.That(line.GetPoints(), Is.Not.SameAs(points), "a different point count allocates a new array");
            Assert.That(line.GetPoints().Length, Is.EqualTo(6));
        }

        [Test]
        public void LineDefaultsGiveASingleCentredSegment()
        {
            Line line = new Line();
            line.Calculate();
            SplinePoint[] points = line.GetPoints();
            Assert.That(points.Length, Is.EqualTo(2));
            Near(points[0].position, new Vector3(0f, -0.5f, 0f), "start");
            Near(points[1].position, new Vector3(0f, 0.5f, 0f), "end");
        }

        // CreatePoints (0x4e8320) stores the type through the setter and resets normal, color and size on every point, even on a
        // reused array.
        [Test]
        public void CreatePointsResetsTheSharedPointFieldsOnEveryRun()
        {
            Line line = new Line();
            line.Calculate();
            SplinePoint[] points = line.GetPoints();
            points[0].size = 9f;
            points[0].normal = Vector3.right;
            points[0].color = Color.red;
            line.Calculate();
            Assert.That(line.GetPoints(), Is.SameAs(points));
            Assert.That(points[0].size, Is.EqualTo(1f));
            Near(points[0].normal, Vector3.up, "normal");
            Assert.That(points[0].color, Is.EqualTo(Color.white));
            Assert.That(points[0].type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
        }

        // Ngon Generate 0x4e8e20: closed, `sides` points; point i is up rotated by i / sides * 360 degrees about forward, scaled
        // by the radius, placed with SetPosition (so both tangents end on the position). Four sides of radius 2 are up, left,
        // down, right. Star Generate 0x4eb930: 2 * sides points starting at 180 degrees; even indices (float modulo 2 == 0) use
        // the radius and odd ones radius * depth.
        [Test]
        public void NgonAndStarPlaceVerticesWithSetPosition()
        {
            Ngon ngon = new Ngon { sides = 4, radius = 2f };
            ngon.Calculate();
            SplinePoint[] square = ngon.GetPoints();
            Assert.That(ngon.GetIsClosed(), Is.True);
            Assert.That(square.Length, Is.EqualTo(4));
            Vector3[] corners = { new Vector3(0f, 2f, 0f), new Vector3(-2f, 0f, 0f), new Vector3(0f, -2f, 0f), new Vector3(2f, 0f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                AssertPoint(square[i], corners[i], corners[i], corners[i], "ngon " + i);
            }

            Star star = new Star { sides = 2, radius = 2f, depth = 0.5f };
            star.Calculate();
            SplinePoint[] tips = star.GetPoints();
            Assert.That(star.GetIsClosed(), Is.True);
            Assert.That(tips.Length, Is.EqualTo(4));
            Vector3[] expected = { new Vector3(0f, -2f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 2f, 0f), new Vector3(-1f, 0f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                AssertPoint(tips[i], expected[i], expected[i], expected[i], "star " + i);
            }
        }

        // Rectangle Generate 0x4e8fe0: each corner stores one value into tangent and position (a chained assignment) and never
        // writes tangent2, which keeps the zero CreatePoints gave it. The corners run left-up, right-up, right-down, left-down.
        [Test]
        public void RectangleSetsPositionAndTangentButNotTangent2()
        {
            Rectangle rectangle = new Rectangle { size = new Vector2(2f, 4f) };
            rectangle.Calculate();
            SplinePoint[] points = rectangle.GetPoints();
            Assert.That(rectangle.GetIsClosed(), Is.True);
            Assert.That(points.Length, Is.EqualTo(4));
            Vector3[] corners = { new Vector3(-1f, 2f, 0f), new Vector3(1f, 2f, 0f), new Vector3(1f, -2f, 0f), new Vector3(-1f, -2f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                AssertPoint(points[i], corners[i], corners[i], Vector3.zero, "corner " + i);
            }
        }

        // RoundedRectangle Generate 0x4e9460: closed, eight Broken points. With size (4, 2) and radii (1, 0.5) the straight parts
        // are 4 - 2 = 2 wide and 2 - 1 = 1 high. Points 0, 3, 4, 7 are where the vertical edges end (inset by yRadius), points
        // 1, 2, 5, 6 where the horizontal ones do (inset by xRadius). Handle length is radius * 0.27614236 * 2. Points 0, 2, 4,
        // 6 move tangent2 and leave tangent on the position (SetPosition left both there); points 1, 3, 5, 7 do the opposite.
        [Test]
        public void RoundedRectangleInsetsTheEdgePointsAndAlternatesHandles()
        {
            RoundedRectangle rounded = new RoundedRectangle { size = new Vector2(4f, 2f), xRadius = 1f, yRadius = 0.5f };
            rounded.Calculate();
            SplinePoint[] points = rounded.GetPoints();
            Assert.That(rounded.GetIsClosed(), Is.True);
            Assert.That(points.Length, Is.EqualTo(8));
            float x = 0.5522848f;
            float y = 0.27614236f;
            AssertPoint(points[0], new Vector3(-2f, 0.5f, 0f), new Vector3(-2f, 0.5f, 0f), new Vector3(-2f, 0.5f + y, 0f), "point 0");
            AssertPoint(points[1], new Vector3(-1f, 1f, 0f), new Vector3(-1f - x, 1f, 0f), new Vector3(-1f, 1f, 0f), "point 1");
            AssertPoint(points[2], new Vector3(1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f + x, 1f, 0f), "point 2");
            AssertPoint(points[3], new Vector3(2f, 0.5f, 0f), new Vector3(2f, 0.5f + y, 0f), new Vector3(2f, 0.5f, 0f), "point 3");
            AssertPoint(points[4], new Vector3(2f, -0.5f, 0f), new Vector3(2f, -0.5f, 0f), new Vector3(2f, -0.5f - y, 0f), "point 4");
            AssertPoint(points[5], new Vector3(1f, -1f, 0f), new Vector3(1f + x, -1f, 0f), new Vector3(1f, -1f, 0f), "point 5");
            AssertPoint(points[6], new Vector3(-1f, -1f, 0f), new Vector3(-1f, -1f, 0f), new Vector3(-1f - x, -1f, 0f), "point 6");
            AssertPoint(points[7], new Vector3(-2f, -0.5f, 0f), new Vector3(-2f, -0.5f - y, 0f), new Vector3(-2f, -0.5f, 0f), "point 7");
            foreach (SplinePoint point in points)
            {
                Assert.That(point.type, Is.EqualTo(SplinePoint.Type.Broken));
            }
        }

        // The handle constant is the float at 0x29a73a0 (0x3e8d6288), doubled at 0x4ea0c6. With radii 1 and 0.25 and size (2, 1)
        // the straight width is 0, so point 1 sits at x = 0 and its tangent x is exactly -(1 * C * 2) = 0xbf0d6288; the nearby
        // literal 0.2761424f (0x3e8d6289) would give 0xbf0d6289.
        [Test]
        public void RoundedRectangleHandleUsesTheExactNativeConstant()
        {
            RoundedRectangle rounded = new RoundedRectangle { size = new Vector2(2f, 1f), xRadius = 1f, yRadius = 0.25f };
            rounded.Calculate();
            SplinePoint[] points = rounded.GetPoints();
            Assert.That(BitConverter.SingleToInt32Bits(points[1].tangent.x), Is.EqualTo(unchecked((int)0xbf0d6288)));
        }

        // CreatePoints stores the type through the setter (0x4e83d0-0x4e841d): isDirty = (_type != type), and SmoothMirrored then
        // writes tangent2 = 2 * position - tangent. Rectangle.Generate never writes tangent2, so on the reused array the second
        // Calculate leaves the mirror of the first run's corner: 2 * (-1, 2, 0) - (-1, 2, 0).
        [Test]
        public void CreatePointsMirrorsTangent2ThroughTheTypeSetterOnAReusedArray()
        {
            Rectangle rectangle = new Rectangle { size = new Vector2(2f, 4f) };
            rectangle.Calculate();
            rectangle.Calculate();
            SplinePoint[] points = rectangle.GetPoints();
            Near(points[0].tangent2, new Vector3(-1f, 2f, 0f), "mirrored tangent2");
            Assert.That(points[0].isDirty, Is.True);
        }

        private static void AssertSpiralPoint(SplinePoint point, Vector3 position, Vector3 tangent, string message)
        {
            Near(point.position, position, message + " position");
            Near(point.tangent, tangent, message + " tangent");
            Near(point.tangent2, 2f * position - tangent, message + " tangent2 mirrors the tangent");
        }

        // Spiral Generate 0x4ea7f0: open, iterations * 4 + 1 points. Each quarter turn adds 90 degrees (clockwise) and stretch /
        // 4 of height; the radius is Lerp(start, end, curve) and the position is the rotated forward times 0.5 * radius plus the
        // height. A flat curve of 0 with equal radii leaves the lean at 0, so the tangent is
        // position + (up * stretch * 0.25 + (turn * right) * radius) * 2 * -0.41421354 / 3 for clockwise.
        [Test]
        public void ClockwiseSpiralClimbsAQuarterTurnPerPoint()
        {
            Spiral spiral = new Spiral { iterations = 1 };
            spiral.Calculate();
            SplinePoint[] points = spiral.GetPoints();
            Assert.That(spiral.GetIsClosed(), Is.False);
            Assert.That(points.Length, Is.EqualTo(5));
            AssertSpiralPoint(points[0], new Vector3(0f, 0f, 0.5f), new Vector3(-0.276142f, -0.069036f, 0.5f), "point 0");
            AssertSpiralPoint(points[1], new Vector3(0.5f, 0.25f, 0f), new Vector3(0.5f, 0.180964f, 0.276142f), "point 1");
            AssertSpiralPoint(points[2], new Vector3(0f, 0.5f, -0.5f), new Vector3(0.276142f, 0.430964f, -0.5f), "point 2");
            AssertSpiralPoint(points[3], new Vector3(-0.5f, 0.75f, 0f), new Vector3(-0.5f, 0.680964f, -0.276142f), "point 3");
            AssertSpiralPoint(points[4], new Vector3(0f, 1f, 0.5f), new Vector3(-0.276142f, 0.930964f, 0.5f), "point 4");
            foreach (SplinePoint point in points)
            {
                Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored));
            }
        }

        // Counterclockwise: the angle step and the lean are negated, the stretch term uses -0.25 and the tangent factor is
        // 2 * +0.41421354 / 3, so the sideways direction mirrors and the up term flips.
        [Test]
        public void CounterclockwiseSpiralMirrorsTheTurnAndFlipsTheTangentTerms()
        {
            Spiral spiral = new Spiral { iterations = 1, clockwise = false };
            spiral.Calculate();
            SplinePoint[] points = spiral.GetPoints();
            AssertSpiralPoint(points[0], new Vector3(0f, 0f, 0.5f), new Vector3(0.276142f, -0.069036f, 0.5f), "point 0");
            AssertSpiralPoint(points[1], new Vector3(-0.5f, 0.25f, 0f), new Vector3(-0.5f, 0.180964f, 0.276142f), "point 1");
            AssertSpiralPoint(points[3], new Vector3(0.5f, 0.75f, 0f), new Vector3(0.5f, 0.680964f, -0.276142f), "point 3");
        }

        // The lean is Lerp(0, 14.4 * direction, curve * |end - start| / max(|start|, |end|)) when the radius does not grow
        // (start >= end) and Lerp(0, -14.4 * direction, (1 - curve) * ratio) when it grows. The two branches give different
        // tangents for the same curve, so swapping them (or the radius ratio) changes the numbers below.
        [Test]
        public void SpiralLeanFollowsTheRadiusChangeAndItsDirection()
        {
            Spiral shrinking = new Spiral { iterations = 1, startRadius = 2f, endRadius = 1f, curve = AnimationCurve.Linear(0f, 0f, 1f, 1f) };
            shrinking.Calculate();
            SplinePoint[] points = shrinking.GetPoints();
            AssertSpiralPoint(points[0], new Vector3(0f, 0f, 1f), new Vector3(-0.552285f, -0.069036f, 1f), "shrinking point 0");
            AssertSpiralPoint(points[1], new Vector3(0.875f, 0.25f, 0f), new Vector3(0.890179f, 0.180964f, 0.483011f), "shrinking point 1");
            AssertSpiralPoint(points[4], new Vector3(0f, 1f, 0.5f), new Vector3(-0.273965f, 0.930964f, 0.534610f), "shrinking point 4");

            Spiral growing = new Spiral { iterations = 1, startRadius = 1f, endRadius = 2f, curve = AnimationCurve.Linear(0f, 0f, 1f, 1f) };
            growing.Calculate();
            points = growing.GetPoints();
            AssertSpiralPoint(points[0], new Vector3(0f, 0f, 0.5f), new Vector3(-0.273965f, -0.069036f, 0.465390f), "growing point 0");
            AssertSpiralPoint(points[1], new Vector3(0.625f, 0.25f, 0f), new Vector3(0.592516f, 0.180964f, 0.343646f), "growing point 1");
            AssertSpiralPoint(points[4], new Vector3(0f, 1f, 1f), new Vector3(-0.552285f, 0.930964f, 1f), "growing point 4");

            Spiral counter = new Spiral { iterations = 1, startRadius = 1f, endRadius = 2f, clockwise = false, curve = AnimationCurve.Linear(0f, 0f, 1f, 1f) };
            counter.Calculate();
            points = counter.GetPoints();
            AssertSpiralPoint(points[0], new Vector3(0f, 0f, 0.5f), new Vector3(0.273965f, -0.069036f, 0.465390f), "counterclockwise growing point 0");
            AssertSpiralPoint(points[3], new Vector3(0.875f, 0.75f, 0f), new Vector3(0.859821f, 0.680964f, -0.483011f), "counterclockwise growing point 3");
        }

        // ApplyOffset 0x4eb0b0: the rotation is Quaternion.Euler(rotation) and is applied to position, tangent, tangent2 and
        // normal; then SetPosition(position + offset) moves both tangents with the position, so a zero tangent2 ends on the
        // offset (the shape's own rotation does not move it).
        [Test]
        public void CalculateRotatesThenOffsetsEveryTangent()
        {
            Rectangle rectangle = new Rectangle { size = new Vector2(2f, 4f), offset = new Vector3(1f, 2f, 3f), rotation = new Vector3(0f, 0f, 90f) };
            rectangle.Calculate();
            SplinePoint[] points = rectangle.GetPoints();
            Vector3 offset = new Vector3(1f, 2f, 3f);
            Vector3[] rotated = { new Vector3(-2f, -1f, 0f), new Vector3(-2f, 1f, 0f), new Vector3(2f, 1f, 0f), new Vector3(2f, -1f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                AssertPoint(points[i], rotated[i] + offset, rotated[i] + offset, offset, "corner " + i);
                Near(points[i].normal, new Vector3(-1f, 0f, 0f), "normal is rotated too");
            }
        }

        // With is2D the rotation is AngleAxis(-rotation.z, forward) * AngleAxis(90, right): the quarter turn about right lays the
        // XY shape onto XZ, then the roll about forward is applied (so a plain 90 degrees of z becomes the planar angle) and x/y of
        // the rotation vector are ignored. Reversing the product order would give (2, 0, 1) for the first corner.
        [Test]
        public void TwoDimensionalRotationRollsAboutForwardAfterTheQuarterTurn()
        {
            Rectangle rectangle = new Rectangle { size = new Vector2(2f, 4f), is2D = true, rotation = new Vector3(45f, 30f, 90f) };
            rectangle.Calculate();
            SplinePoint[] points = rectangle.GetPoints();
            Vector3[] expected = { new Vector3(0f, 1f, 2f), new Vector3(0f, -1f, 2f), new Vector3(0f, -1f, -2f), new Vector3(0f, 1f, -2f) };
            for (int i = 0; i < 4; i++)
            {
                Near(points[i].position, expected[i], "corner " + i);
                Near(points[i].tangent, expected[i], "tangent " + i);
            }

            Rectangle flat = new Rectangle { size = new Vector2(2f, 4f), is2D = true };
            flat.Calculate();
            Near(flat.GetPoints()[0].position, new Vector3(-1f, 0f, 2f), "a zero roll keeps just the quarter turn about right");
        }

        // CreateSpline 0x4eb480: Generate, ApplyOffset, then a Spline of GetSplineType() that shares the primitive's point array
        // and, for a closed primitive, closes itself (Spline.Close is inlined, error text included).
        [Test]
        public void CreateSplineSharesThePointsAndClosesClosedPrimitives()
        {
            Rectangle rectangle = new Rectangle();
            Spline closed = rectangle.CreateSpline();
            Assert.That(closed.type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(closed.points, Is.SameAs(rectangle.GetPoints()));
            Assert.That(closed.isClosed, Is.True);

            Line line = new Line { segments = 3 };
            Spline open = line.CreateSpline();
            Assert.That(open.type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(open.points.Length, Is.EqualTo(4));
            Assert.That(open.isClosed, Is.False);

            Ngon twoSides = new Ngon { sides = 2 };
            LogAssert.Expect(LogType.Error, "Points need to be at least 3 to close the spline");
            Spline tooShort = twoSides.CreateSpline();
            Assert.That(tooShort.isClosed, Is.False, "two points cannot be closed");
        }

        // UpdateSpline 0x4eb610: the same, onto an existing spline. A closed primitive closes it; an open one calls Break only when
        // the spline reports closed after taking the new points (closed flag and at least three points).
        [Test]
        public void UpdateSplineClosesOrBreaksOnTheSplineClosedState()
        {
            Spline spline = new Spline(Spline.Type.CatmullRom);
            new Rectangle().UpdateSpline(spline);
            Assert.That(spline.type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(spline.points.Length, Is.EqualTo(4));
            Assert.That(spline.isClosed, Is.True);

            new Line { segments = 3 }.UpdateSpline(spline);
            Assert.That(spline.points.Length, Is.EqualTo(4));
            Assert.That(spline.isClosed, Is.False, "an open primitive breaks a closed spline");

            Spline closedFlag = new Spline(Spline.Type.Linear);
            new Rectangle().UpdateSpline(closedFlag);
            FieldInfo closedField = typeof(Spline).GetField("closed", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(closedField, Is.Not.Null);
            new Line().UpdateSpline(closedFlag);
            Assert.That(closedFlag.points.Length, Is.EqualTo(2));
            Assert.That((bool)closedField.GetValue(closedFlag), Is.True, "two points are not isClosed, so Break is skipped and the flag stays");
        }

        // CreateSplineComputer 0x4eb720: Generate, ApplyOffset, a game object named `name`, a SplineComputer on it, SetPoints in
        // Local space, Close for a closed primitive, then position and rotation of its transform. It never assigns the type,
        // so the computer keeps its default CatmullRom; UpdateSplineComputer (0x4eb850) does assign it.
        [Test]
        public void CreateSplineComputerBuildsALocalSpaceComputerWithoutSettingItsType()
        {
            Rectangle rectangle = new Rectangle { size = new Vector2(2f, 4f) };
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
            SplineComputer computer = rectangle.CreateSplineComputer("rectangle", new Vector3(5f, 6f, 7f), rotation);
            _owned.Add(computer.gameObject);
            Assert.That(computer.name, Is.EqualTo("rectangle"));
            Near(computer.transform.position, new Vector3(5f, 6f, 7f), "position");
            Assert.That(Quaternion.Angle(computer.transform.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(computer.type, Is.EqualTo(Spline.Type.CatmullRom), "no type assignment in the native body");
            Assert.That(computer.isClosed, Is.True);
            SplinePoint[] local = computer.GetPoints(SplineComputer.Space.Local);
            Assert.That(local.Length, Is.EqualTo(4));
            Near(local[0].position, new Vector3(-1f, 2f, 0f), "local corner 0");
            Near(local[2].position, new Vector3(1f, -2f, 0f), "local corner 2");

            Line line = new Line { segments = 2 };
            SplineComputer open = line.CreateSplineComputer("line", Vector3.zero, Quaternion.identity);
            _owned.Add(open.gameObject);
            Assert.That(open.isClosed, Is.False);
            Assert.That(open.GetPoints(SplineComputer.Space.Local).Length, Is.EqualTo(3));
        }

        [Test]
        public void UpdateSplineComputerAssignsTheTypeAndClosesOrBreaks()
        {
            GameObject owner = new GameObject("update-target");
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            new Rectangle().UpdateSplineComputer(computer);
            Assert.That(computer.type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(computer.GetPoints(SplineComputer.Space.Local).Length, Is.EqualTo(4));
            Assert.That(computer.isClosed, Is.True);

            new Line { segments = 3 }.UpdateSplineComputer(computer);
            Assert.That(computer.GetPoints(SplineComputer.Space.Local).Length, Is.EqualTo(4));
            Assert.That(computer.isClosed, Is.False, "an open primitive breaks a closed computer");
        }

        // Field order and visibility from the original metadata: SplinePrimitive keeps closed / points protected and the rest
        // public; each shape adds its fields in the order below.
        [Test]
        public void PrimitiveFieldsKeepTheShippedOrderAndVisibility()
        {
            BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplinePrimitive).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "closed", "points", "offset", "rotation", "is2D" }));
            Assert.That(typeof(SplinePrimitive).GetField("closed", declared).IsFamily, Is.True);
            Assert.That(typeof(SplinePrimitive).GetField("points", declared).IsFamily, Is.True);
            Assert.That(typeof(SplinePrimitive).GetField("offset", declared).IsPublic, Is.True);
            Assert.That(typeof(Capsule).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "radius", "height" }));
            Assert.That(typeof(Ellipse).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "xRadius", "yRadius" }));
            Assert.That(typeof(Line).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "mirror", "length", "segments" }));
            Assert.That(typeof(Ngon).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "radius", "sides" }));
            Assert.That(typeof(Rectangle).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "size" }));
            Assert.That(typeof(RoundedRectangle).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "size", "xRadius", "yRadius" }));
            Assert.That(typeof(Spiral).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "startRadius", "endRadius", "stretch", "iterations", "clockwise", "curve" }));
            Assert.That(typeof(Star).GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "radius", "depth", "sides" }));
            Assert.That(typeof(SplinePrimitive).GetMethod("Generate", BindingFlags.Instance | BindingFlags.NonPublic).IsFamily, Is.True);
        }
    }
}
