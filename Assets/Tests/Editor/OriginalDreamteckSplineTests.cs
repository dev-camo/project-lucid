using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.Spline. Expected values are derived from the x86-64 bodies (addresses in each comment) and from the
    // arithmetic of the shipped formulas (worked by hand or with an independent script), not read back from the restored source.
    // Where a plausible textbook version would give a different answer, the inputs are chosen to tell the two apart.
    public sealed class OriginalDreamteckSplineTests
    {
        private const float Tolerance = 1e-4f;

        private static void Near(Vector3 actual, Vector3 expected, string message, float tolerance = Tolerance)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private static Spline Build(Spline.Type type, params Vector3[] positions)
        {
            Spline spline = new Spline(type);
            spline.points = new SplinePoint[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                spline.points[i] = new SplinePoint(positions[i]);
            }

            return spline;
        }

        private static Spline BuildBezierPair()
        {
            Spline spline = new Spline(Spline.Type.Bezier);
            spline.points = new[]
            {
                new SplinePoint(Vector3.zero, new Vector3(-3f, 0f, 0f), new Vector3(0f, 3f, 0f), Vector3.up, 1f, Color.white),
                new SplinePoint(new Vector3(6f, 0f, 0f), new Vector3(6f, 3f, 0f), new Vector3(9f, 0f, 0f), Vector3.up, 1f, Color.white),
            };
            return spline;
        }

        private static bool ReadClosed(Spline spline)
        {
            return (bool)typeof(Spline).GetField("closed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spline);
        }

        private static void WriteClosed(Spline spline, bool value)
        {
            typeof(Spline).GetField("closed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spline, value);
        }

        // Original constructors 0x4da5b0 / 0x4da650 run the field initialisers (empty points, Bezier, linearAverageDirection,
        // sampleRate 10), then store the type (and sample rate) and a fresh empty point array.
        [Test]
        public void ConstructorsStartEmptyWithTheShippedDefaults()
        {
            Spline linear = new Spline(Spline.Type.Linear);
            Assert.That(linear.type, Is.EqualTo(Spline.Type.Linear));
            Assert.That(linear.points, Is.Not.Null);
            Assert.That(linear.points.Length, Is.EqualTo(0));
            Assert.That(linear.sampleRate, Is.EqualTo(10));
            Assert.That(linear.linearAverageDirection, Is.True);
            Assert.That(linear.customValueInterpolation, Is.Null);
            Assert.That(linear.customNormalInterpolation, Is.Null);
            Assert.That(linear.isClosed, Is.False);
            Assert.That(linear.knotParametrization, Is.EqualTo(0f));

            Spline custom = new Spline(Spline.Type.BSpline, 4);
            Assert.That(custom.type, Is.EqualTo(Spline.Type.BSpline));
            Assert.That(custom.sampleRate, Is.EqualTo(4));
            Assert.That(custom.points.Length, Is.EqualTo(0));
            Assert.That(custom.linearAverageDirection, Is.True);
        }

        // Original isClosed 0x4da470 needs closed && points >= 3, and Close 0x4de430 refuses (with an error log) to close fewer
        // than three points.
        [Test]
        public void CloseNeedsThreePointsAndIsClosedNeedsThemToo()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, Vector3.right);
            LogAssert.Expect(LogType.Error, "Points need to be at least 3 to close the spline");
            spline.Close();
            Assert.That(ReadClosed(spline), Is.False);
            Assert.That(spline.isClosed, Is.False);

            WriteClosed(spline, true);
            Assert.That(spline.isClosed, Is.False, "closed with two points does not count as closed");

            Spline three = Build(Spline.Type.Linear, Vector3.zero, Vector3.right, Vector3.up);
            three.Close();
            Assert.That(three.isClosed, Is.True);
        }

        // Original moveStep 0x4da4a0 and iterations 0x4da500. Linear: iterations = points (+1 closed), moveStep = 1f / (points - 1)
        // for open and closed alike. Other types: (sampleRate - 1) * (points, or points - 1 open) segments, iterations = that + 1 and
        // moveStep = 1f / segments, with no cast in the text. The evidence for the shipped single-precision quotient is the
        // disassembly (divss, then cvtss2sd); this test cannot tell float from double arithmetic, because the editor's Mono evaluates
        // the uncast quotient at double precision (the IL has no conv.r4 after the div). It pins the type/closure rules, the value the
        // editor returns (1.0 / n) and that this value narrows to the float 1f / n.
        [Test]
        public void MoveStepAndIterationsFollowTheSplineTypeAndClosure()
        {
            Vector3[] square = { Vector3.zero, Vector3.right, new Vector3(1f, 1f, 0f), Vector3.up };

            Spline linear = Build(Spline.Type.Linear, square);
            Assert.That(linear.iterations, Is.EqualTo(4));
            AssertMoveStep(linear.moveStep, 3);
            linear.Close();
            Assert.That(linear.iterations, Is.EqualTo(5));
            AssertMoveStep(linear.moveStep, 3, "Linear moveStep ignores closure");

            Spline bezier = Build(Spline.Type.Bezier, square);
            Assert.That(bezier.iterations, Is.EqualTo(9 * 3 + 1));
            AssertMoveStep(bezier.moveStep, 27);
            bezier.Close();
            Assert.That(bezier.iterations, Is.EqualTo(9 * 4 + 1));
            AssertMoveStep(bezier.moveStep, 36);

            Spline coarse = new Spline(Spline.Type.CatmullRom, 4);
            coarse.points = new[] { new SplinePoint(Vector3.zero), new SplinePoint(Vector3.right), new SplinePoint(Vector3.up) };
            Assert.That(coarse.iterations, Is.EqualTo(3 * 2 + 1));
            AssertMoveStep(coarse.moveStep, 6);
        }

        // The editor (Mono) value of the uncast `1f / n` is 1.0 / n; its float narrowing is 1f / n.
        private static void AssertMoveStep(double actual, int n, string message = null)
        {
            Assert.That(actual, Is.EqualTo(1.0 / n), message);
            Assert.That((float)actual, Is.EqualTo(1f / n), message);
        }

        // Original knotParametrization setter 0x4da580 clamps to [0, 1] (Mathf.Clamp01); the moveStep setter 0x4da560 is empty.
        [Test]
        public void KnotParametrizationClampsAndTheMoveStepSetterIsInert()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, Vector3.right);
            spline.knotParametrization = 2f;
            Assert.That(spline.knotParametrization, Is.EqualTo(1f));
            spline.knotParametrization = -1f;
            Assert.That(spline.knotParametrization, Is.EqualTo(0f));
            spline.knotParametrization = 0.4f;
            Assert.That(spline.knotParametrization, Is.EqualTo(0.4f));

            double before = spline.moveStep;
            spline.moveStep = 0.123;
            Assert.That(spline.moveStep, Is.EqualTo(before));
        }

        // Original GetPointPercent 0x4dc020: Clamp01(index / (points, or points - 1 when open)).
        [Test]
        public void PointPercentUsesThePointCountWhenClosedAndClamps()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, Vector3.right, Vector3.up, Vector3.forward);
            Assert.That(spline.GetPointPercent(0), Is.EqualTo(0.0));
            Assert.That(spline.GetPointPercent(1), Is.EqualTo(1.0 / 3.0).Within(1e-15));
            Assert.That(spline.GetPointPercent(3), Is.EqualTo(1.0));
            Assert.That(spline.GetPointPercent(5), Is.EqualTo(1.0), "clamped above");
            Assert.That(spline.GetPointPercent(-1), Is.EqualTo(0.0), "clamped below");
            spline.Close();
            Assert.That(spline.GetPointPercent(1), Is.EqualTo(0.25));
            Assert.That(spline.GetPointPercent(4), Is.EqualTo(1.0));
        }

        // Original CalculateBezierPosition 0x4dfa00 / CalculateBezierTangent 0x4e03a0: the cubic through point.position,
        // point.tangent2, next.tangent, next.position and its derivative (not normalised). Past the last point the next index wraps
        // to point 0 even when open. EvaluateTangent at 1.0 therefore differentiates the segment that starts at the last point.
        [Test]
        public void BezierEvaluatesTheCubicAndItsUnnormalisedDerivative()
        {
            Spline spline = BuildBezierPair();
            Near(spline.EvaluatePosition(0.0), Vector3.zero, "start");
            Near(spline.EvaluatePosition(1.0), new Vector3(6f, 0f, 0f), "end");
            Near(spline.EvaluatePosition(0.5), new Vector3(3f, 2.25f, 0f), "0.125 * P0 + 0.375 * P1 + 0.375 * P2 + 0.125 * P3");

            Vector3 tangent = Vector3.zero;
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(9f, 0f, 0f), "3u^2 (P1 - P0) + 6ut (P2 - P1) + 3t^2 (P3 - P2) at t = 0.5");
            spline.EvaluateTangent(0.0, ref tangent);
            Near(tangent, new Vector3(0f, 9f, 0f), "3 (P1 - P0) at t = 0");
            spline.EvaluateTangent(1.0, ref tangent);
            Near(tangent, new Vector3(9f, 0f, 0f), "the wrapped segment from the last point to point 0: 3 (tangent2 - position)");

            Vector3 position = Vector3.zero;
            Vector3 both = Vector3.zero;
            spline.EvaluatePositionAndTangent(ref position, ref both, 0.5);
            Near(position, new Vector3(3f, 2.25f, 0f), "combined position");
            Near(both, new Vector3(9f, 0f, 0f), "combined tangent");

            SplineSample sample = spline.Evaluate(0.5);
            Near(sample.position, new Vector3(3f, 2.25f, 0f), "sample position");
            Near(sample.forward, Vector3.right, "sample forward is the normalised tangent");
            Assert.That(sample.percent, Is.EqualTo(0.5));
        }

        // Original CalculateBezierPosition 0x4dfa00: the segment after the last point of a closed spline runs back to point 0 (the next
        // index wraps), here with both handles on the positions so the middle of the segment is the midpoint (weights 0.5 / 0.5).
        [Test]
        public void ClosedBezierRunsTheLastSegmentBackToPointZero()
        {
            Spline spline = new Spline(Spline.Type.Bezier);
            Vector3[] corners = { new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(4f, 4f, 0f) };
            spline.points = Array.ConvertAll(corners, corner => new SplinePoint(corner, corner, corner, Vector3.up, 1f, Color.white));
            spline.Close();
            Near(spline.EvaluatePosition(1.5 / 3.0), new Vector3(4f, 2f, 0f), "middle of the segment from point 1 to point 2");
            Near(spline.EvaluatePosition(2.5 / 3.0), new Vector3(2f, 2f, 0f), "middle of the wrapped segment from point 2 back to point 0");
            Near(spline.EvaluatePosition(1.0), new Vector3(0f, 0f, 0f), "full circle");
        }

        // Original CalculateLinearPosition 0x4dfd80 / CalculateLinearTangent 0x4e0540: a lerp between the two middle control
        // positions, and the segment direction or (linearAverageDirection) the halfway Slerp of the previous and current segment
        // directions. A missing first neighbour is extrapolated by mirroring (ComputeCatPoints 0x4dec10).
        [Test]
        public void LinearEvaluatesBetweenPointsAndAveragesTangentsOnRequest()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f));
            Near(spline.EvaluatePosition(0.0), new Vector3(0f, 0f, 0f), "start");
            Near(spline.EvaluatePosition(0.25), new Vector3(5f, 0f, 0f), "quarter");
            Near(spline.EvaluatePosition(0.5), new Vector3(10f, 0f, 0f), "corner");
            Near(spline.EvaluatePosition(0.75), new Vector3(10f, 5f, 0f), "three quarters");
            Near(spline.EvaluatePosition(1.0), new Vector3(10f, 10f, 0f), "end");

            Vector3 tangent = Vector3.zero;
            spline.EvaluateTangent(0.25, ref tangent);
            Near(tangent, new Vector3(10f, 0f, 0f), "first segment: the extrapolated neighbour makes both directions equal");
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(7.0710678f, 7.0710678f, 0f), "average: Slerp((10,0,0), (0,10,0), 0.5)");
            spline.linearAverageDirection = false;
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(0f, 10f, 0f), "plain segment direction");
        }

        // Original CalculateCatmullRomPositionFast 0x4df2d0 / CalculateCatmullRomTangentFast 0x4dfec0 (knot parametrisation
        // below 1e-6): the uniform Catmull-Rom polynomial 0.5 * (2P1 + (P2-P0)t + (2P0-5P1+4P2-P3)t^2 + (-P0+3P1-3P2+P3)t^3) and its
        // derivative. An open spline extrapolates the missing end neighbours by mirroring.
        [Test]
        public void UniformCatmullRomUsesThePolynomialAndItsDerivative()
        {
            Spline spline = Build(Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 1f, 0f));
            Vector3 tangent = Vector3.zero;

            Near(spline.EvaluatePosition(0.5), new Vector3(1.5f, 0.5f, 0f), "middle segment, t = 0.5");
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(1f, -1.5f, 0f), "0.5 * (c1 + 2 c2 t + 3 c3 t^2)");

            Near(spline.EvaluatePosition(1.0 / 6.0), new Vector3(0.5f, 0.625f, 0f), "first segment uses P0 = 2 * p0 - p1");
            spline.EvaluateTangent(1.0 / 6.0, ref tangent);
            Near(tangent, new Vector3(1f, 1.25f, 0f), "first segment tangent");

            spline.EvaluateTangent(0.0, ref tangent);
            Near(tangent, new Vector3(1f, 1f, 0f), "at the first point the tangent is (P2 - P0) / 2 with the mirrored P0 = (-1, -1)");
            Near(spline.EvaluatePosition(0.0), new Vector3(0f, 0f, 0f), "start");
            Near(spline.EvaluatePosition(1.0), new Vector3(3f, 1f, 0f), "end");
        }

        // Original CalculateCatmullRomComponents 0x4df510, CalculateCatmullRomPosition 0x4df920 and CalculateCatmullRomTangent
        // 0x4e0090 (knot parametrisation from 1e-6): the Barry-Goldman pyramid with knot intervals |P(n)-P(n+1)|^knot, and the
        // derivative of the pyramid with respect to the knot parameter. The reference numbers come from an independent
        // Barry-Goldman implementation.
        [Test]
        public void ChordalCatmullRomUsesTheBarryGoldmanPyramidAndItsDerivative()
        {
            Spline spline = Build(Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(3f, 0f, 0f), new Vector3(4f, 2f, 0f));
            Vector3 tangent = Vector3.zero;

            spline.knotParametrization = 1f;
            Near(spline.EvaluatePosition(0.5), new Vector3(2.0304271f, 0.5101424f, 0f), "knot 1 position");
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(0.9790158f, -0.7916954f, 0f), "knot 1 tangent");

            spline.knotParametrization = 0.5f;
            Near(spline.EvaluatePosition(0.5), new Vector3(2.0107970f, 0.4696792f, 0f), "knot 0.5 position");
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(1.4902249f, -1.1297420f, 0f), "knot 0.5 tangent");

            Vector3 positionAndTangentPosition = Vector3.zero;
            Vector3 positionAndTangent = Vector3.zero;
            spline.EvaluatePositionAndTangent(ref positionAndTangentPosition, ref positionAndTangent, 0.5);
            Near(positionAndTangentPosition, new Vector3(2.0107970f, 0.4696792f, 0f), "combined position");
            Near(positionAndTangent, new Vector3(1.4902249f, -1.1297420f, 0f), "combined tangent");
        }

        // Original CalculatePosition 0x4ddcb0 chooses the uniform polynomial only below 1e-6 (0x29a6e18 is 9.99999997e-07). The
        // pyramid with a vanishing knot exponent approaches the uniform curve from the other side of the threshold, so the curve is
        // continuous across it.
        [Test]
        public void PyramidAtTheKnotThresholdApproachesTheUniformCurve()
        {
            Spline spline = Build(Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(3f, 0f, 0f), new Vector3(4f, 2f, 0f));
            Vector3 uniform = new Vector3(2f, 0.4375f, 0f);

            spline.knotParametrization = 0f;
            Near(spline.EvaluatePosition(0.5), uniform, "uniform polynomial", 1e-5f);
            spline.knotParametrization = 0.0000009f;
            Near(spline.EvaluatePosition(0.5), uniform, "still below the threshold", 1e-5f);
            spline.knotParametrization = 0.000001f;
            Near(spline.EvaluatePosition(0.5), uniform, "pyramid at the threshold stays within 1e-3 of the uniform curve", 1e-3f);
        }

        // Original CalculateBSplinePosition 0x4dfb70: uniform cubic B-spline (P0 + 4 P1 + P2) / 6 + ..., for the four control
        // positions around the segment. Evaluate derives the forward vector from positions one moveStep either side (0x4dc1d0).
        [Test]
        public void BSplineSmoothsThroughTheControlsAndDerivesForwardFromNeighbours()
        {
            Spline spline = Build(Spline.Type.BSpline, new Vector3(0f, 0f, 0f), new Vector3(1f, 2f, 0f), new Vector3(3f, 0f, 0f), new Vector3(4f, 2f, 0f));
            Near(spline.EvaluatePosition(0.5), new Vector3(2f, 1f, 0f), "segment 1 at t = 0.5");
            Near(spline.EvaluatePosition(0.0), new Vector3(0f, 0f, 0f), "(P0 + 4 P1 + P2) / 6 with the mirrored P0");
            Near(spline.EvaluatePosition(1.0 / 6.0), new Vector3(0.5208333f, 0.9166667f, 0f), "first segment uses P0 = 2 * p0 - p1");

            SplineSample sample = spline.Evaluate(0.5);
            Near(sample.position, new Vector3(2f, 1f, 0f), "sample position");
            Near(sample.forward, new Vector3(0.87126f, -0.4908218f, 0f), "normalised (pos(p + 1/27) - pos(p - 1/27))");

            Vector3 tangent = new Vector3(5f, 5f, 5f);
            spline.EvaluateTangent(0.5, ref tangent);
            Near(tangent, new Vector3(5f, 5f, 5f), "CalculateTangent leaves a B-spline tangent untouched");
        }

        // Original Evaluate 0x4dc864-0x4dca4a: within one step of an open end the forward vector is
        // Slerp(after - position, position - before, |position - before| / |after - position|), normalised. With sampleRate 2 the
        // step is 1 / 3, so percent 0.2 clamps `before` to the start; neither after - before nor swapped Slerp operands give this.
        [Test]
        public void OpenBSplineNearAnEndSlerpsTheOutgoingAndIncomingDirections()
        {
            Spline spline = new Spline(Spline.Type.BSpline, 2);
            spline.points = new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)), new SplinePoint(new Vector3(1f, 2f, 0f)),
                new SplinePoint(new Vector3(3f, 0f, 0f)), new SplinePoint(new Vector3(4f, 2f, 0f)),
            };
            SplineSample sample = spline.Evaluate(0.2);
            Near(sample.position, new Vector3(0.636f, 1.056f, 0f), "position");
            Near(sample.forward, new Vector3(0.696911f, 0.717158f, 0f), "end-branch forward", 1e-4f);
        }

        // Original Evaluate 0x4dc1d0 for a closed B-spline: below one moveStep (1 / (9 * 4)) the neighbours are percent - step + 1
        // and percent + step, so the forward vector wraps around the seam. ComputeCatPoints wraps the control indices.
        [Test]
        public void ClosedBSplineWrapsItsNeighboursAcrossTheSeam()
        {
            Spline spline = Build(Spline.Type.BSpline, new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f));
            spline.Close();
            SplineSample sample = spline.Evaluate(0.0);
            Near(sample.position, new Vector3(1f / 6f, 1f / 6f, 0f), "position at the seam");
            Near(sample.forward, new Vector3(0.70710678f, -0.70710678f, 0f), "forward across the seam");
        }

        // Original ComputeCatPoints 0x4dec10: a closed spline wraps the neighbours of the last and first points.
        [Test]
        public void ClosedUniformCatmullRomWrapsItsControlIndices()
        {
            Spline spline = Build(Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f));
            spline.Close();
            Near(spline.EvaluatePosition(0.875), new Vector3(-0.125f, 0.5f, 0f), "segment from the last point back to the first");
            Near(spline.EvaluatePosition(1.0), new Vector3(0f, 0f, 0f), "full circle returns to the first point");
            Near(spline.EvaluatePosition(0.0), new Vector3(0f, 0f, 0f), "start");
        }

        // Original Evaluate 0x4dc1d0: size and color use the value curve, the up vector uses the normal curve (Slerp), a curve
        // with no keys is ignored, and percent is stored clamped. The forward vector is normalised.
        [Test]
        public void EvaluateInterpolatesSizeColorAndNormalThroughTheCustomCurves()
        {
            Spline spline = new Spline(Spline.Type.Linear);
            spline.points = new[]
            {
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 1f, Color.black),
                new SplinePoint(new Vector3(10f, 0f, 0f), Vector3.zero, Vector3.forward, 3f, Color.white),
            };

            SplineSample plain = spline.Evaluate(0.5);
            Near(plain.position, new Vector3(5f, 0f, 0f), "position");
            Near(plain.forward, Vector3.right, "forward");
            Near(plain.up, new Vector3(0f, 0.70710678f, 0.70710678f), "up slerps with t = 0.5");
            Assert.That(plain.size, Is.EqualTo(2f).Within(1e-6f));
            Assert.That(plain.color.r, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(plain.percent, Is.EqualTo(0.5));

            spline.customValueInterpolation = new AnimationCurve();
            spline.customNormalInterpolation = new AnimationCurve();
            SplineSample emptyCurves = spline.Evaluate(0.5);
            Assert.That(emptyCurves.size, Is.EqualTo(2f).Within(1e-6f), "an empty curve is ignored");

            spline.customValueInterpolation = AnimationCurve.Constant(0f, 1f, 0.25f);
            SplineSample valueOnly = spline.Evaluate(0.5);
            Assert.That(valueOnly.size, Is.EqualTo(1.5f).Within(1e-6f), "the value curve drives size");
            Assert.That(valueOnly.color.r, Is.EqualTo(0.25f).Within(1e-6f), "the value curve drives color");
            Near(valueOnly.up, new Vector3(0f, 0.70710678f, 0.70710678f), "the value curve does not drive up");

            spline.customNormalInterpolation = AnimationCurve.Constant(0f, 1f, 0.75f);
            SplineSample both = spline.Evaluate(0.5);
            Near(both.up, new Vector3(0f, 0.3826834f, 0.9238795f), "the normal curve drives up: Slerp(up, forward, 0.75)");
            Assert.That(both.size, Is.EqualTo(1.5f).Within(1e-6f), "the normal curve does not drive size");

            Assert.That(spline.Evaluate(2.0).percent, Is.EqualTo(1.0), "percent is clamped");
            Assert.That(spline.Evaluate(-1.0).percent, Is.EqualTo(0.0), "percent is clamped");
        }

        // Original Evaluate 0x4dc357-0x4dc375: a closed spline clamps index to points.Length - 1 and wraps nextIndex to 0 before
        // interpolating size, color and normal. At 0.8 the segment is 2 -> 0 with t = 0.4; at 1.0 the index clamps to 2 with t = 1.
        [Test]
        public void ClosedEvaluateWrapsTheNextPointForSizeAndColor()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f));
            float[] sizes = { 1f, 2f, 4f };
            float[] reds = { 0f, 0.5f, 1f };
            for (int i = 0; i < sizes.Length; i++)
            {
                spline.points[i].size = sizes[i];
                spline.points[i].color = new Color(reds[i], 0f, 0f, 1f);
            }

            spline.Close();
            SplineSample wrapped = spline.Evaluate(0.8);
            Assert.That(wrapped.size, Is.EqualTo(2.8f).Within(1e-5f), "Lerp(4, 1, 0.4); an open-style clamp would give 4");
            Assert.That(wrapped.color.r, Is.EqualTo(0.6f).Within(1e-5f), "Lerp(1, 0, 0.4)");
            SplineSample end = spline.Evaluate(1.0);
            Assert.That(end.size, Is.EqualTo(1f).Within(1e-5f), "index clamps to 2, next wraps to 0, t = 1; a modulo index would give 2");
            Assert.That(end.color.r, Is.EqualTo(0f).Within(1e-5f));
        }

        // Original EvaluateTangent 0x4ddedc-0x4ddf06 clamps the integer index only for a Bezier spline (cmp type, 2). A closed linear
        // square at percent 1 keeps index 4, so ComputeCatPoints(4) wraps to P0 = p3, P1 = p0, P2 = p1 and the averaged tangent is
        // Slerp((0, -10, 0), (10, 0, 0), 0.5); clamping to index 3 would give (-7.07, -7.07, 0) instead.
        [Test]
        public void ClosedLinearTangentAtTheSeamKeepsTheUnclampedIndex()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f), new Vector3(0f, 10f, 0f));
            spline.linearAverageDirection = true;
            spline.Close();
            Vector3 tangent = Vector3.zero;
            spline.EvaluateTangent(1.0, ref tangent);
            Near(tangent, new Vector3(7.0710678f, -7.0710678f, 0f), "seam tangent", 1e-3f);
        }

        // Original Evaluate 0x4dc1d0 zeroes the sample for an empty spline, answers the lone point with world forward, and reopens a
        // closed spline that has two or fewer points (writing the field).
        [Test]
        public void EvaluateHandlesEmptyOnePointAndClosedPairLikeTheNativeBody()
        {
            Spline empty = new Spline(Spline.Type.Bezier);
            SplineSample junk = new SplineSample(Vector3.one, Vector3.one, Vector3.one, Color.red, 7f, 7.0);
            empty.Evaluate(0.3, ref junk);
            Near(junk.position, Vector3.zero, "empty position");
            Near(junk.forward, Vector3.zero, "empty forward");
            Assert.That(junk.size, Is.EqualTo(0f));
            Assert.That(junk.percent, Is.EqualTo(0.0));
            Assert.That(junk.color, Is.EqualTo(new Color(0f, 0f, 0f, 0f)));
            Near(empty.EvaluatePosition(0.3), Vector3.zero, "empty EvaluatePosition");
            Vector3 tangent = Vector3.one;
            empty.EvaluateTangent(0.3, ref tangent);
            Near(tangent, Vector3.forward, "empty EvaluateTangent");
            Vector3 position = Vector3.one;
            empty.EvaluatePositionAndTangent(ref position, ref tangent, 0.3);
            Near(position, Vector3.zero, "empty combined position");
            Near(tangent, Vector3.forward, "empty combined tangent");

            Spline single = new Spline(Spline.Type.Bezier);
            single.points = new[] { new SplinePoint(new Vector3(1f, 2f, 3f), Vector3.zero, new Vector3(0f, 0f, 1f), 4f, Color.green) };
            SplineSample one = single.Evaluate(0.3);
            Near(one.position, new Vector3(1f, 2f, 3f), "single position");
            Near(one.up, new Vector3(0f, 0f, 1f), "single up is the point normal");
            Near(one.forward, Vector3.forward, "single forward");
            Assert.That(one.size, Is.EqualTo(4f));
            Assert.That(one.color, Is.EqualTo(Color.green));
            Assert.That(one.percent, Is.EqualTo(0.3));
            Near(single.EvaluatePosition(0.9), new Vector3(1f, 2f, 3f), "single EvaluatePosition");
            single.EvaluateTangent(0.9, ref tangent);
            Near(tangent, Vector3.forward, "single EvaluateTangent");
            single.EvaluatePositionAndTangent(ref position, ref tangent, 0.9);
            Near(position, new Vector3(1f, 2f, 3f), "single combined position");
            Near(tangent, Vector3.forward, "single combined tangent");

            Spline pair = Build(Spline.Type.Linear, Vector3.zero, new Vector3(10f, 0f, 0f));
            WriteClosed(pair, true);
            SplineSample reopened = pair.Evaluate(0.5);
            Assert.That(ReadClosed(pair), Is.False, "Evaluate reopens a closed spline with two points");
            Near(reopened.position, new Vector3(5f, 0f, 0f), "evaluated as an open spline");
        }

        // Original Evaluate(int) 0x4dca80 / Evaluate(int, ref) 0x4dcb20 evaluate at GetPointPercent.
        [Test]
        public void EvaluateByPointIndexUsesThePointPercent()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(8f, 0f, 0f));
            SplineSample middle = spline.Evaluate(1);
            Assert.That(middle.percent, Is.EqualTo(0.5));
            Near(middle.position, new Vector3(4f, 0f, 0f), "middle point");
            SplineSample beyond = spline.Evaluate(9);
            Assert.That(beyond.percent, Is.EqualTo(1.0), "clamped");
            Near(beyond.position, new Vector3(8f, 0f, 0f), "clamped position");
            SplineSample filled = new SplineSample();
            spline.Evaluate(2, ref filled);
            Assert.That(filled.percent, Is.EqualTo(1.0));
        }

        // Original Evaluate(ref SplineSample[], from, to) 0x4dcd70: floor((iterations - 1) * from) .. ceil((iterations - 1) * to)
        // samples, one DMath.Move(step) apart, the end clamped by Move; `to` is clamped into [from, 1].
        [Test]
        public void EvaluateIntoAnArrayWalksFromFloorToCeilAndRepeatsTheEndPercent()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(6f, 0f, 0f), new Vector3(9f, 0f, 0f));
            // The editor (Mono) moveStep is 1.0 / 3 (see MoveStepAndIterationsFollowTheSplineTypeAndClosure).
            double step = 1.0 / 3.0;

            SplineSample[] samples = null;
            spline.Evaluate(ref samples);
            Assert.That(samples.Length, Is.EqualTo(4));
            Assert.That(samples[0].percent, Is.EqualTo(0.0));
            Assert.That(samples[1].percent, Is.EqualTo(step).Within(1e-12));
            Assert.That(samples[2].percent, Is.EqualTo(2 * step).Within(1e-9));
            Assert.That(samples[3].percent, Is.EqualTo(1.0));
            Near(samples[2].position, new Vector3(6f, 0f, 0f), "third sample", 1e-3f);

            SplineSample[] reused = samples;
            spline.Evaluate(ref samples);
            Assert.That(ReferenceEquals(samples, reused), Is.True, "an array of the right length is reused");

            spline.Evaluate(ref samples, 0.25, 0.75);
            Assert.That(samples.Length, Is.EqualTo(4), "ceil(3 * 0.75) - floor(3 * 0.25) + 1");
            Assert.That(samples[0].percent, Is.EqualTo(0.25));
            Assert.That(samples[1].percent, Is.EqualTo(0.25 + step).Within(1e-12));
            Assert.That(samples[2].percent, Is.EqualTo(0.75), "Move stops at `to`");
            Assert.That(samples[3].percent, Is.EqualTo(0.75), "a Move that starts on its target stays there");

            spline.Evaluate(ref samples, 0.0, 0.1);
            Assert.That(samples.Length, Is.EqualTo(2), "a different count replaces the array");
            Assert.That(ReferenceEquals(samples, reused), Is.False);

            spline.Evaluate(ref samples, 0.5, 0.25);
            Assert.That(samples.Length, Is.EqualTo(2), "`to` is clamped up to `from`: ceil(1.5) - floor(1.5) + 1");
            Assert.That(samples[0].percent, Is.EqualTo(0.5));
            Assert.That(samples[1].percent, Is.EqualTo(0.5));

            Spline none = new Spline(Spline.Type.Linear);
            SplineSample[] stale = new SplineSample[3];
            none.Evaluate(ref stale);
            Assert.That(stale.Length, Is.EqualTo(0));
        }

        // Original EvaluatePositions 0x4dd980 follows the same walk for positions and dereferences the array (a null one faults).
        // Original Evaluate(ref SplineSample[]) 0x4dceec-0x4dcf37 reads moveStep before the first Evaluate call, which reopens a
        // closed two-point spline (0x4dc23d); the closed step 1 / ((10 - 1) * 2) is therefore kept for the whole walk.
        [Test]
        public void EvaluateIntoAnArrayReadsTheMoveStepBeforeReopeningAClosedPair()
        {
            Spline spline = Build(Spline.Type.Bezier, Vector3.zero, new Vector3(10f, 0f, 0f));
            WriteClosed(spline, true);
            SplineSample[] samples = null;
            spline.Evaluate(ref samples);
            Assert.That(samples.Length, Is.EqualTo(19));
            Assert.That(samples[1].percent, Is.EqualTo(1.0 / 18.0).Within(1e-12), "a step read after the reopen would be 1 / 9");
            Assert.That((float)samples[1].percent, Is.EqualTo(1f / 18f), "the editor step narrows to the float 1f / 18");
            Assert.That(ReadClosed(spline), Is.False);
        }

        [Test]
        public void EvaluatePositionsFollowsTheSameWalkAndFaultsOnANullArray()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(6f, 0f, 0f), new Vector3(9f, 0f, 0f));
            Vector3[] positions = new Vector3[1];
            spline.EvaluatePositions(ref positions);
            Assert.That(positions.Length, Is.EqualTo(4));
            Near(positions[0], Vector3.zero, "first", 1e-3f);
            Near(positions[1], new Vector3(3f, 0f, 0f), "second", 1e-3f);
            Near(positions[3], new Vector3(9f, 0f, 0f), "last", 1e-3f);

            spline.EvaluatePositions(ref positions, 0.25, 0.75);
            Assert.That(positions.Length, Is.EqualTo(4));
            Near(positions[0], new Vector3(2.25f, 0f, 0f), "range start", 1e-3f);
            Near(positions[2], new Vector3(6.75f, 0f, 0f), "range end", 1e-3f);
            Near(positions[3], new Vector3(6.75f, 0f, 0f), "range end repeats", 1e-3f);

            Vector3[] missing = null;
            Assert.Throws<NullReferenceException>(() => spline.EvaluatePositions(ref missing));

            Spline none = new Spline(Spline.Type.Linear);
            Vector3[] stale = new Vector3[2];
            none.EvaluatePositions(ref stale);
            Assert.That(stale.Length, Is.EqualTo(0));
        }

        // Original EvaluateUniform 0x4dd0c0: CalculateLength / (iterations - 1) apart via Travel; the first and last samples sit on
        // `from` and `to`. originalSamplePercents keeps the percent each sample was found at while the samples are renumbered to evenly
        // spaced percents. Uneven point spacing (1 then 10) separates the arc-length walk from a percent walk.
        [Test]
        public void EvaluateUniformSpacesSamplesByArcLength()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(11f, 0f, 0f));
            SplineSample[] samples = null;
            double[] original = null;
            spline.EvaluateUniform(ref samples, ref original);
            Assert.That(samples.Length, Is.EqualTo(3));
            Assert.That(original.Length, Is.EqualTo(3));
            Near(samples[0].position, new Vector3(0f, 0f, 0f), "first position");
            Near(samples[1].position, new Vector3(5.5f, 0f, 0f), "middle position is half the arc length");
            Near(samples[2].position, new Vector3(11f, 0f, 0f), "last position");
            Assert.That(original[0], Is.EqualTo(0.0));
            Assert.That(original[1], Is.EqualTo(0.725).Within(1e-5), "the percent the middle sample was found at");
            Assert.That(original[2], Is.EqualTo(1.0));
            Assert.That(samples[0].percent, Is.EqualTo(0.0));
            Assert.That(samples[1].percent, Is.EqualTo(0.5), "renumbered to the even percent");
            Assert.That(samples[2].percent, Is.EqualTo(1.0));

            SplineSample[] tail = null;
            double[] tailOriginal = null;
            spline.EvaluateUniform(ref tail, ref tailOriginal, 0.5, 1.0);
            Assert.That(tail.Length, Is.EqualTo(2), "ceil(2 * 1) - floor(2 * 0.5) + 1");
            Near(tail[0].position, new Vector3(1f, 0f, 0f), "range start");
            Near(tail[1].position, new Vector3(11f, 0f, 0f), "range end");
            Assert.That(tailOriginal[0], Is.EqualTo(0.5));
            Assert.That(tailOriginal[1], Is.EqualTo(1.0));

            Spline none = new Spline(Spline.Type.Linear);
            SplineSample[] noneSamples = new SplineSample[2];
            double[] untouched = null;
            none.EvaluateUniform(ref noneSamples, ref untouched);
            Assert.That(noneSamples.Length, Is.EqualTo(0));
            Assert.That(untouched, Is.Null, "an empty spline does not allocate the percents");
        }

        // Original Travel 0x4dd580: walks the chord grid from the nearest grid point in `direction`, adding chord lengths into `moved`
        // until `distance` is covered, then interpolates back inside the last chord by 1 - (moved - distance) / chord.
        [Test]
        public void TravelWalksChordsAndInterpolatesInsideTheLastOne()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(11f, 0f, 0f));
            float moved;

            Assert.That(spline.Travel(0.0, 5.5f, out moved, Spline.Direction.Forward), Is.EqualTo(0.725).Within(1e-5));
            Assert.That(moved, Is.EqualTo(11f).Within(1e-5f), "moved includes the whole last chord");
            Assert.That(spline.Travel(1.0, 5.5f, out moved, Spline.Direction.Backward), Is.EqualTo(0.725).Within(1e-5));
            Assert.That(moved, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(spline.Travel(0.8, 3f, out moved, Spline.Direction.Forward), Is.EqualTo(0.95).Within(1e-5), "x goes from 7 to 10");
            Assert.That(moved, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(spline.Travel(0.0, 5.5f), Is.EqualTo(0.725).Within(1e-5), "the short overload walks forward");
            Assert.That(spline.Travel(0.75, 2f, out moved, Spline.Direction.Backward), Is.EqualTo(0.65).Within(1e-5),
                "off the grid a backward walk starts at the floor step (0.5); a ceil start would give 0.85");
            Assert.That(moved, Is.EqualTo(5f).Within(1e-5f));

            Assert.That(spline.Travel(0.5, 100f, out moved, Spline.Direction.Forward), Is.EqualTo(1.0), "ran out of spline");
            Assert.That(moved, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(spline.Travel(0.5, 100f, out moved, Spline.Direction.Backward), Is.EqualTo(0.0), "ran out of spline backwards");
            Assert.That(moved, Is.EqualTo(1f).Within(1e-5f));

            Assert.That(spline.Travel(1.0, 5f, out moved, Spline.Direction.Forward), Is.EqualTo(1.0));
            Assert.That(moved, Is.EqualTo(0f));
            Assert.That(spline.Travel(0.0, 5f, out moved, Spline.Direction.Backward), Is.EqualTo(0.0));
            Assert.That(spline.Travel(0.3, 0f, out moved, Spline.Direction.Forward), Is.EqualTo(0.3), "zero distance answers the clamped start");
            Assert.That(spline.Travel(1.7, 0f, out moved, Spline.Direction.Backward), Is.EqualTo(1.0), "zero distance clamps the start");

            Spline single = Build(Spline.Type.Linear, Vector3.one);
            moved = 5f;
            Assert.That(single.Travel(0.4, 1f, out moved, Spline.Direction.Forward), Is.EqualTo(0.0), "fewer than two points");
            Assert.That(moved, Is.EqualTo(0f), "moved is cleared first");
        }

        // Original CalculateLength 0x4da700: chord sum in moveStep / resolution steps from `from` to max(from, to). A `from` above
        // `to` takes a single step from `from` that DMath.Move turns into `from` itself, so the length is zero.
        [Test]
        public void CalculateLengthSumsChordsAndReturnsZeroForAnInvertedRange()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f));
            Assert.That(spline.CalculateLength(), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(spline.CalculateLength(0.0, 1.0, 0.5), Is.EqualTo(14.142136f).Within(1e-4f), "resolution 0.5 takes one step and cuts the corner");
            Assert.That(spline.CalculateLength(0.5, 1.0), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(spline.CalculateLength(0.5, 0.25), Is.EqualTo(0f), "inverted range");
            Assert.That(spline.CalculateLength(0.0, 1.0, 0.0), Is.EqualTo(0f), "zero resolution");
            Assert.That(spline.CalculateLength(0.0, 1.0, -3.0), Is.EqualTo(0f), "negative resolution clamps to zero");
            Assert.That(spline.CalculateLength(0.0, 1.0, 5.0), Is.EqualTo(20f).Within(1e-4f), "resolution clamps to one");
            Assert.That(new Spline(Spline.Type.Linear).CalculateLength(), Is.EqualTo(0f));
        }

        // Original Project 0x4daad0 / GetClosestPoint 0x4db160. Recursion with subdivide 0 compares only the two ends and answers the
        // midpoint on an exact tie.
        [Test]
        public void ProjectFindsTheClosestPercent()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f));
            Assert.That(spline.Project(new Vector3(5f, 3f, 0f)), Is.EqualTo(0.25).Within(1e-4));
            Assert.That(spline.Project(new Vector3(12f, 12f, 0f)), Is.EqualTo(1.0).Within(1e-4));
            Assert.That(spline.Project(new Vector3(-3f, -3f, 0f)), Is.EqualTo(0.0).Within(1e-4));
            Assert.That(spline.Project(new Vector3(5f, 3f, 0f), 4, 0.5, 1.0), Is.EqualTo(0.65).Within(1e-4), "restricted to the second half: nearest to (10, 3, 0)");

            Assert.That(spline.Project(new Vector3(1f, 0f, 0f), 0), Is.EqualTo(0.0), "subdivide 0: the start is nearer");
            Assert.That(spline.Project(new Vector3(9f, 9f, 0f), 0), Is.EqualTo(1.0), "subdivide 0: the end is nearer");
            Assert.That(spline.Project(new Vector3(5f, 5f, 0f), 0), Is.EqualTo(0.5), "subdivide 0: an exact tie answers the midpoint");

            Assert.That(new Spline(Spline.Type.Linear).Project(Vector3.one), Is.EqualTo(0.0), "empty spline");
        }

        // Original Project 0x4daad0 for a closed spline over its full range: when the first answer lies within one moveStep of the
        // start it also searches [0.5, 1] and keeps the nearer candidate. The point (-1, 0.3, 0) is nearest to the left edge just
        // before the seam (percent 0.9925), but the first pass lands on the seam's other side (0) because its first sample ties.
        [Test]
        public void ProjectOnAClosedSplineChecksTheSecondHalfNearTheSeam()
        {
            Spline spline = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f), new Vector3(0f, 10f, 0f));
            spline.Close();
            Assert.That(spline.Project(new Vector3(-1f, 0.3f, 0f)), Is.EqualTo(0.9925).Within(2e-3), "second pass wins");
            Assert.That(spline.Project(new Vector3(-1f, 1f, 0f)), Is.EqualTo(0.975).Within(2e-3), "single pass already right");
            Assert.That(spline.Project(new Vector3(5f, -2f, 0f)), Is.EqualTo(0.125).Within(2e-3), "first answer is within one step of the start but the second pass finds nothing nearer");
        }

        private sealed class ColliderScope : IDisposable
        {
            private readonly List<GameObject> objects = new List<GameObject>();

            public void AddBox(Vector3 center)
            {
                GameObject box = new GameObject("DreamteckSplineTestBox");
                box.transform.position = center;
                BoxCollider collider = box.AddComponent<BoxCollider>();
                collider.size = new Vector3(1f, 4f, 4f);
                objects.Add(box);
                Physics.SyncTransforms();
            }

            public void Dispose()
            {
                foreach (GameObject box in objects)
                {
                    UnityEngine.Object.DestroyImmediate(box);
                }
            }
        }

        // Original Raycast 0x4db4e0: Linecasts each moveStep / resolution segment. The hit percent is the lerp between the segment's
        // end percents at the squared-distance ratio |hit - start|^2 / |end - start|^2 (not the plain distance ratio), and it stays 0
        // when nothing is hit. A resolution of 0 clears the RaycastHit and reports no hit.
        [Test]
        public void RaycastReportsTheSquaredDistanceRatioOfTheFirstHitSegment()
        {
            Vector3 lift = new Vector3(0f, 5000f, 0f);
            Spline spline = Build(Spline.Type.Linear, lift, lift + new Vector3(10f, 0f, 0f), lift + new Vector3(20f, 0f, 0f));
            using (ColliderScope scope = new ColliderScope())
            {
                scope.AddBox(lift + new Vector3(15f, 0f, 0f));
                RaycastHit hit;
                double hitPercent;
                Assert.That(spline.Raycast(out hit, out hitPercent, ~0), Is.True);
                Assert.That(hit.point.x, Is.EqualTo(14.5f).Within(1e-3f));
                Assert.That(hitPercent, Is.EqualTo(0.60125).Within(1e-4), "0.5 + 0.5 * (4.5^2 / 10^2)");

                Assert.That(spline.Raycast(out hit, out hitPercent, ~0, 1.0, 0.0, 0.4), Is.False, "the hit lies beyond `to`");
                Assert.That(hitPercent, Is.EqualTo(0.0));

                Assert.That(spline.Raycast(out hit, out hitPercent, ~0, 0.0), Is.False, "zero resolution");
                Assert.That(hit.collider, Is.Null);
                Assert.That(hitPercent, Is.EqualTo(0.0));
            }
        }

        // Original RaycastAll 0x4db850: Physics.RaycastAll over each segment (origin, end - origin, chord length). Every hit adds its
        // squared-distance-ratio percent and the RaycastHit; a resolution of 0 answers empty arrays and false.
        [Test]
        public void RaycastAllCollectsEveryHitWithItsPercent()
        {
            Vector3 lift = new Vector3(0f, 5000f, 0f);
            Spline spline = Build(Spline.Type.Linear, lift, lift + new Vector3(10f, 0f, 0f));
            using (ColliderScope scope = new ColliderScope())
            {
                scope.AddBox(lift + new Vector3(3f, 0f, 0f));
                scope.AddBox(lift + new Vector3(7f, 0f, 0f));
                RaycastHit[] hits;
                double[] percents;
                Assert.That(spline.RaycastAll(out hits, out percents, ~0), Is.True);
                Assert.That(hits.Length, Is.EqualTo(2));
                Assert.That(percents.Length, Is.EqualTo(2));
                for (int i = 0; i < hits.Length; i++)
                {
                    float x = hits[i].point.x;
                    double expected = x < 5f ? 2.5 * 2.5 / 100.0 : 6.5 * 6.5 / 100.0;
                    Assert.That(x, Is.EqualTo(x < 5f ? 2.5f : 6.5f).Within(1e-3f), "hit point " + i);
                    Assert.That(percents[i], Is.EqualTo(expected).Within(1e-4), "percent " + i);
                }

                Assert.That(spline.RaycastAll(out hits, out percents, ~0, 0.0), Is.False);
                Assert.That(hits, Is.Not.Null);
                Assert.That(hits.Length, Is.EqualTo(0));
                Assert.That(percents.Length, Is.EqualTo(0));

                Assert.That(spline.RaycastAll(out hits, out percents, ~0, 1.0, 0.0, 0.1), Is.False, "no hits before x = 1");
                Assert.That(hits.Length, Is.EqualTo(0));
                Assert.That(percents.Length, Is.EqualTo(0));
            }
        }

        // Original Break 0x4de260: a closed spline rotates its points so that point `at` comes first and is opened; an open spline
        // or an index outside [0, count) changes nothing.
        [Test]
        public void BreakRotatesTheClosedPointsAndOpensTheSpline()
        {
            Vector3[] corners = { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f) };

            Spline spline = Build(Spline.Type.Linear, corners);
            spline.Close();
            spline.Break(2);
            Assert.That(spline.points.Length, Is.EqualTo(4));
            Assert.That(Array.ConvertAll(spline.points, point => point.position.x), Is.EqualTo(new[] { 2f, 3f, 0f, 1f }));
            Assert.That(ReadClosed(spline), Is.False);

            Spline whole = Build(Spline.Type.Linear, corners);
            whole.Close();
            whole.Break();
            Assert.That(Array.ConvertAll(whole.points, point => point.position.x), Is.EqualTo(new[] { 0f, 1f, 2f, 3f }));
            Assert.That(ReadClosed(whole), Is.False);

            foreach (int at in new[] { -1, 4, 9 })
            {
                Spline untouched = Build(Spline.Type.Linear, corners);
                untouched.Close();
                untouched.Break(at);
                Assert.That(ReadClosed(untouched), Is.True, "Break(" + at + ") is ignored");
                Assert.That(Array.ConvertAll(untouched.points, point => point.position.x), Is.EqualTo(new[] { 0f, 1f, 2f, 3f }));
            }

            Spline open = Build(Spline.Type.Linear, corners);
            open.Break(2);
            Assert.That(Array.ConvertAll(open.points, point => point.position.x), Is.EqualTo(new[] { 0f, 1f, 2f, 3f }), "an open spline is left alone");
        }

        // Original CatToBezierTangents 0x4de4b0. Linear: Broken points with both tangents on the position. Catmull-Rom: SmoothMirrored
        // points with tangent2 = position + tangent / 3 (evenly spaced collinear controls give 1/3 of the spacing, mirrored back
        // for tangent). Any other type only changes type to Bezier.
        [Test]
        public void CatToBezierTangentsRewritesLinearAndUniformCatmullRomPoints()
        {
            Spline linear = Build(Spline.Type.Linear, new Vector3(0f, 0f, 0f), new Vector3(5f, 1f, 0f), new Vector3(9f, 2f, 0f));
            linear.CatToBezierTangents();
            Assert.That(linear.type, Is.EqualTo(Spline.Type.Bezier));
            foreach (SplinePoint point in linear.points)
            {
                Assert.That(point.type, Is.EqualTo(SplinePoint.Type.Broken));
                Near(point.tangent, point.position, "tangent sits on the position");
                Near(point.tangent2, point.position, "tangent2 sits on the position");
            }

            Spline uniform = Build(Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f));
            uniform.CatToBezierTangents();
            Assert.That(uniform.type, Is.EqualTo(Spline.Type.Bezier));
            for (int i = 0; i < 4; i++)
            {
                SplinePoint point = uniform.points[i];
                Assert.That(point.type, Is.EqualTo(SplinePoint.Type.SmoothMirrored), "point " + i);
                Near(point.tangent2, new Vector3(i + 1f / 3f, 0f, 0f), "tangent2 of point " + i);
                Near(point.tangent, new Vector3(i - 1f / 3f, 0f, 0f), "tangent of point " + i);
            }

            Spline bspline = Build(Spline.Type.BSpline, Vector3.zero, Vector3.right, Vector3.up);
            SplinePoint before = bspline.points[1];
            bspline.CatToBezierTangents();
            Assert.That(bspline.type, Is.EqualTo(Spline.Type.Bezier));
            Assert.That(bspline.points[1] == before, Is.True, "B-spline points are not rewritten");
        }

        // Original CatToBezierTangents 0x4de4b0 with a knot parametrisation above zero: tangent2 = position + normalised tangent *
        // |P0 - P2| / 6 where P0 and P2 are the neighbours around the point. The expected offsets come from an independent
        // Barry-Goldman derivative; a plain tangent / 3 (the zero-knot rule) would give different lengths.
        [Test]
        public void CatToBezierTangentsScalesByTheNeighbourDistanceWhenTheKnotIsPositive()
        {
            Spline spline = Build(Spline.Type.CatmullRom, new Vector3(0f, 0f, 0f), new Vector3(2f, 1f, 0f), new Vector3(3f, 5f, 0f), new Vector3(7f, 6f, 0f));
            spline.knotParametrization = 1f;
            spline.CatToBezierTangents();
            Near(spline.points[1].tangent2, new Vector3(2.705024f, 1.668869f, 0f), "point 1 tangent2");
            Near(spline.points[1].tangent, new Vector3(1.294976f, 0.331131f, 0f), "point 1 mirrored tangent");
            Near(spline.points[2].tangent2, new Vector3(3.833333f, 5.833333f, 0f), "point 2 tangent2");
        }

        // Original FormatFromTo 0x4e06f0: both ends are clamped to [0, 1]; with preventInvert an inverted pair is SWAPPED.
        [Test]
        public void FormatFromToClampsAndSwapsInvertedPairs()
        {
            (double from, double to, bool prevent, double expectedFrom, double expectedTo)[] rows =
            {
                (0.2, 0.8, true, 0.2, 0.8),
                (0.8, 0.2, true, 0.2, 0.8),
                (0.8, 0.2, false, 0.8, 0.2),
                (-1.0, 2.0, true, 0.0, 1.0),
                (-1.0, 2.0, false, 0.0, 1.0),
                (2.0, -1.0, true, 0.0, 1.0),
                (2.0, -1.0, false, 1.0, 0.0),
                (0.5, 0.5, true, 0.5, 0.5),
            };
            foreach (var row in rows)
            {
                double from = row.from;
                double to = row.to;
                Spline.FormatFromTo(ref from, ref to, row.prevent);
                Assert.That(from, Is.EqualTo(row.expectedFrom), "from for " + row.from + ", " + row.to + ", " + row.prevent);
                Assert.That(to, Is.EqualTo(row.expectedTo), "to for " + row.from + ", " + row.to + ", " + row.prevent);
            }

            double defaultFrom = 0.9;
            double defaultTo = 0.1;
            Spline.FormatFromTo(ref defaultFrom, ref defaultTo);
            Assert.That(defaultFrom, Is.EqualTo(0.1), "preventInvert defaults to true");
            Assert.That(defaultTo, Is.EqualTo(0.9));
        }

        // Original obsolete overloads 0x4dcd00, 0x4dcd60, 0x4dde70 forward to the current argument order.
#pragma warning disable CS0618
        [Test]
        public void ObsoleteOverloadsForwardToTheCurrentOnes()
        {
            Spline spline = Build(Spline.Type.Linear, Vector3.zero, new Vector3(4f, 0f, 0f), new Vector3(8f, 0f, 0f));
            SplineSample byIndex = new SplineSample();
            spline.Evaluate(ref byIndex, 1);
            Assert.That(byIndex.percent, Is.EqualTo(0.5));
            SplineSample byPercent = new SplineSample();
            spline.Evaluate(ref byPercent, 0.75);
            Assert.That(byPercent.percent, Is.EqualTo(0.75));
            Near(byPercent.position, new Vector3(6f, 0f, 0f), "evaluated position");
            Vector3 position = Vector3.zero;
            spline.EvaluatePosition(ref position, 0.25);
            Near(position, new Vector3(2f, 0f, 0f), "old-order EvaluatePosition");
        }
#pragma warning restore CS0618

        // Original Spline metadata: [Serializable] class; fields in this order with the closed flag and knot parametrisation
        // serialized; the Barry-Goldman helper keeps its compiler-generated local-function name (ordinal 65 counts the two
        // nested enums, 17 fields, 4 properties and 42 earlier methods); three overloads carry [Obsolete] with the shipped text.
        [Test]
        public void SerializedShapeAndCompilerGeneratedNamesMatchTheShippedAssembly()
        {
            Assert.That(typeof(Spline).IsSerializable, Is.True);
            BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(Spline).GetFields(declared | BindingFlags.Instance).OrderBy(field => field.MetadataToken).Select(field => field.Name),
                Is.EqualTo(new[] { "points", "type", "linearAverageDirection", "customValueInterpolation", "customNormalInterpolation", "sampleRate", "closed", "_knotParametrization" }));
            Assert.That(typeof(Spline).GetFields(declared | BindingFlags.Static).OrderBy(field => field.MetadataToken).Select(field => field.Name),
                Is.EqualTo(new[] { "P", "A1", "A2", "A3", "B1", "B2", "t1", "t2", "t3" }));
            Assert.That(typeof(Spline).GetField("closed", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttribute<SerializeField>(), Is.Not.Null);
            FieldInfo knot = typeof(Spline).GetField("_knotParametrization", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(knot.GetCustomAttribute<SerializeField>(), Is.Not.Null);
            UnityEngine.RangeAttribute range = knot.GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That(range.min, Is.EqualTo(0f));
            Assert.That(range.max, Is.EqualTo(1f));

            MethodInfo helper = typeof(Spline).GetMethod("<CalculateCatmullRomComponents>g__GetInterval|65_0", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(helper, Is.Not.Null);
            Assert.That(helper.ReturnType, Is.EqualTo(typeof(float)));

            Assert.That(Enum.GetNames(typeof(Spline.Type)), Is.EqualTo(new[] { "CatmullRom", "BSpline", "Bezier", "Linear" }));
            Assert.That((int)Spline.Direction.Backward, Is.EqualTo(-1));
            Assert.That((int)Spline.Direction.Forward, Is.EqualTo(1));

            string[] obsoleteMessages =
            {
                "This override is obsolete. Use Evaluate(int pointIndex, ref SplineSample sample) instead",
                "This override is obsolete. Use Evaluate(double percent, ref SplineSample sample) instead",
                "This override is obsolete. Use EvaluatePosition(double percent, ref Vector3 position) instead",
            };
            List<string> found = new List<string>();
            foreach (MethodInfo method in typeof(Spline).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                ObsoleteAttribute obsolete = method.GetCustomAttribute<ObsoleteAttribute>();
                if (obsolete != null)
                {
                    found.Add(obsolete.Message);
                }
            }

            Assert.That(found, Is.EquivalentTo(obsoleteMessages));
        }
    }
}
