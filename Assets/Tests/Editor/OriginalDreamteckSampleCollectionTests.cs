using System;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SampleCollection. All expected values are hand arithmetic over a three-sample polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10) at percents 0, 0.5, 1, so the two chords are 10 long and the whole length is 20.
    // The native bodies are cited per test. No scene objects are created.
    public sealed class OriginalDreamteckSampleCollectionTests
    {
        private const float Tolerance = 1e-4f;

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private static SampleCollection Polyline(SplineComputer.SampleMode mode = SplineComputer.SampleMode.Default)
        {
            SampleCollection collection = new SampleCollection();
            collection.samples = new[]
            {
                new SplineSample(new Vector3(0f, 0f, 0f), Vector3.up, new Vector3(1f, 0f, 0f), Color.black, 1f, 0.0),
                new SplineSample(new Vector3(10f, 0f, 0f), Vector3.up, new Vector3(0f, 0f, 1f), Color.black, 1f, 0.5),
                new SplineSample(new Vector3(10f, 0f, 10f), Vector3.up, new Vector3(0f, 0f, 1f), Color.black, 1f, 1.0)
            };
            collection.sampleMode = mode;
            return collection;
        }

        // Ctors 0x4d2a80 / 0x4da2b0, length 0x4cec30: the copy shares both arrays.
        [Test]
        public void ConstructorsAndLength()
        {
            SampleCollection empty = new SampleCollection();
            Assert.That(empty.length, Is.EqualTo(0));
            Assert.That(empty.samples, Is.Empty);
            Assert.That(empty.optimizedIndices, Is.Empty);
            Assert.That(empty.sampleMode, Is.EqualTo(SplineComputer.SampleMode.Default));

            SampleCollection source = Polyline(SplineComputer.SampleMode.Uniform);
            source.optimizedIndices = new[] { 0, 1, 2 };
            SampleCollection copy = new SampleCollection(source);
            Assert.That(copy.length, Is.EqualTo(3));
            Assert.That(copy.samples, Is.SameAs(source.samples));
            Assert.That(copy.optimizedIndices, Is.SameAs(source.optimizedIndices));
            Assert.That(copy.sampleMode, Is.EqualTo(SplineComputer.SampleMode.Uniform));
        }

        // GetSamplingValues 0x4ceac0: Default divides the sample range evenly. Optimized goes through one index per raw sample;
        // with optimizedIndices {0,0,0,1,2} the raw index 2 (percent 0.5) still points at sample 0, so the lerp is 1 there,
        // which a plain even split (index 1, lerp 0) would not give.
        [Test]
        public void SamplingValuesDefaultAndOptimized()
        {
            SampleCollection plain = Polyline();
            int index;
            double lerp;
            plain.GetSamplingValues(0.75, out index, out lerp);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(lerp, Is.EqualTo(0.5).Within(1e-9));
            plain.GetSamplingValues(1.0, out index, out lerp);
            Assert.That(index, Is.EqualTo(2));
            Assert.That(lerp, Is.EqualTo(0.0).Within(1e-9));
            plain.GetSamplingValues(0.0, out index, out lerp);
            Assert.That(index, Is.EqualTo(0));
            Assert.That(lerp, Is.EqualTo(0.0).Within(1e-9));

            SampleCollection optimized = Polyline(SplineComputer.SampleMode.Optimized);
            optimized.optimizedIndices = new[] { 0, 0, 0, 1, 2 };
            optimized.GetSamplingValues(0.5, out index, out lerp);
            Assert.That(index, Is.EqualTo(0));
            Assert.That(lerp, Is.EqualTo(1.0).Within(1e-9));
            optimized.GetSamplingValues(0.75, out index, out lerp);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(lerp, Is.EqualTo(0.5).Within(1e-9));
            optimized.GetSamplingValues(1.0, out index, out lerp);
            Assert.That(index, Is.EqualTo(2));
            Assert.That(lerp, Is.EqualTo(0.0).Within(1e-9));
        }

        // GetSamplingValues 0x4ceac0: rawPercent is only computed below the last raw index (0x4ceb39 jge skips the Lerp), so at
        // the last raw index it stays 0 and InverseLerp(0.5, 1.0, 0) clamps to 0. Using rawIndex/last (1.0) would give 1.
        [Test]
        public void OptimizedSamplingAtTheLastRawIndexLeavesRawPercentZero()
        {
            SampleCollection optimized = Polyline(SplineComputer.SampleMode.Optimized);
            optimized.optimizedIndices = new[] { 0, 0, 0, 1, 1 };
            optimized.GetSamplingValues(1.0, out int index, out double lerp);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(lerp, Is.EqualTo(0.0).Within(1e-9));
        }

        // GetClippedSampleCount 0x4cf780. Default uses floor/ceil of (n-1)*clip; other modes use the sampling values and take the
        // next sample when the end lies between two. A reversed range wraps over the end: n + (end - start) + 1.
        [Test]
        public void ClippedSampleCountTruthTable()
        {
            SampleCollection plain = Polyline();
            int start;
            int end;
            Assert.That(plain.GetClippedSampleCount(0.0, 1.0, out start, out end), Is.EqualTo(3));
            Assert.That(new[] { start, end }, Is.EqualTo(new[] { 0, 2 }));
            Assert.That(plain.GetClippedSampleCount(0.25, 0.75, out start, out end), Is.EqualTo(3));
            Assert.That(new[] { start, end }, Is.EqualTo(new[] { 0, 2 }));
            Assert.That(plain.GetClippedSampleCount(0.75, 0.25, out start, out end), Is.EqualTo(4));
            Assert.That(new[] { start, end }, Is.EqualTo(new[] { 1, 1 }));

            SampleCollection uniform = Polyline(SplineComputer.SampleMode.Uniform);
            Assert.That(uniform.GetClippedSampleCount(0.0, 0.4, out start, out end), Is.EqualTo(2));
            Assert.That(new[] { start, end }, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(uniform.GetClippedSampleCount(0.0, 0.5, out start, out end), Is.EqualTo(2));
            Assert.That(new[] { start, end }, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(uniform.GetClippedSampleCount(0.0, 0.6, out start, out end), Is.EqualTo(3));
            Assert.That(new[] { start, end }, Is.EqualTo(new[] { 0, 2 }));
        }

        // EvaluatePosition 0x4cf930, Evaluate 0x4cf690 / 0x4da380: empty answers zero, a lerp between neighbours otherwise.
        [Test]
        public void EvaluateInterpolatesAndEmptyAnswersZero()
        {
            SampleCollection collection = Polyline();
            Near(collection.EvaluatePosition(0.25), new Vector3(5f, 0f, 0f), "quarter");
            Near(collection.EvaluatePosition(0.75), new Vector3(10f, 0f, 5f), "three quarters");
            Near(collection.EvaluatePosition(0.5), new Vector3(10f, 0f, 0f), "exactly on a sample");
            Near(collection.EvaluatePosition(1.0), new Vector3(10f, 0f, 10f), "end");
            SplineSample sample = collection.Evaluate(0.75);
            Near(sample.position, new Vector3(10f, 0f, 5f), "sample position");
            Assert.That(sample.percent, Is.EqualTo(0.75).Within(1e-6));

            SampleCollection empty = new SampleCollection();
            Near(empty.EvaluatePosition(0.5), Vector3.zero, "empty position");
            SplineSample result = new SplineSample(Vector3.one, Vector3.up, Vector3.forward, Color.white, 2f, 0.3);
            empty.Evaluate(0.5, ref result);
            Near(result.position, Vector3.zero, "empty evaluate clears the result");
            Assert.That(result.size, Is.EqualTo(0f));
        }

        // Evaluate(ref array) 0x4cfbf0 and EvaluatePositions 0x4cffe0: the first and last entries are evaluated at from and to,
        // the entries between are the stored samples.
        [Test]
        public void RangeEvaluationReturnsEndsPlusStoredSamples()
        {
            SampleCollection collection = Polyline();
            SplineSample[] samples = null;
            collection.Evaluate(ref samples, 0.25, 0.75);
            Assert.That(samples.Length, Is.EqualTo(3));
            Near(samples[0].position, new Vector3(5f, 0f, 0f), "first");
            Near(samples[1].position, new Vector3(10f, 0f, 0f), "stored");
            Near(samples[2].position, new Vector3(10f, 0f, 5f), "last");

            Vector3[] positions = null;
            collection.EvaluatePositions(ref positions, 0.25, 0.75);
            Assert.That(positions.Length, Is.EqualTo(3));
            Near(positions[0], new Vector3(5f, 0f, 0f), "first position");
            Near(positions[1], new Vector3(10f, 0f, 0f), "stored position");
            Near(positions[2], new Vector3(10f, 0f, 5f), "last position");

            SampleCollection empty = new SampleCollection();
            SplineSample[] none = new SplineSample[4];
            empty.Evaluate(ref none);
            Assert.That(none, Is.Empty);
            Vector3[] nonePositions = new Vector3[4];
            empty.EvaluatePositions(ref nonePositions);
            Assert.That(nonePositions, Is.Empty);
        }

        // Travel 0x4d0250. Forward from 0 over 5 units: the zero first chord is skipped, the second sample is 10 away, the
        // overshoot of 5 interpolates back to 0.25 and is given back from moved.
        [Test]
        public void TravelWalksTheChordsAndGivesBackTheOvershoot()
        {
            SampleCollection collection = Polyline();
            float moved;
            Assert.That(collection.Travel(0.0, 5f, Spline.Direction.Forward, out moved), Is.EqualTo(0.25).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(collection.Travel(0.0, 15f, Spline.Direction.Forward, out moved), Is.EqualTo(0.75).Within(1e-6));
            Assert.That(moved, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(collection.Travel(0.0, 100f, Spline.Direction.Forward, out moved), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(moved, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(collection.Travel(1.0, 5f, Spline.Direction.Backward, out moved), Is.EqualTo(0.75).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(collection.Travel(0.0, 5f, Spline.Direction.Forward), Is.EqualTo(0.25).Within(1e-6), "short overload");
        }

        // Travel 0x4d0250 guards: no samples answer 0, the travelling end answers the clip end, no distance answers start.
        [Test]
        public void TravelGuards()
        {
            float moved = -1f;
            Assert.That(new SampleCollection().Travel(0.3, 5f, Spline.Direction.Forward, out moved), Is.EqualTo(0.0));
            Assert.That(moved, Is.EqualTo(0f));

            SampleCollection collection = Polyline();
            Assert.That(collection.Travel(1.0, 5f, Spline.Direction.Forward, out moved, 0.2, 0.8), Is.EqualTo(0.8));
            Assert.That(collection.Travel(0.0, 5f, Spline.Direction.Backward, out moved, 0.2, 0.8), Is.EqualTo(0.2));
            Assert.That(collection.Travel(0.3, 0f, Spline.Direction.Forward, out moved), Is.EqualTo(0.3));
            Assert.That(moved, Is.EqualTo(0f));
        }

        // Travel 0x4d0250, wrap path for clipFrom > clipTo (0x4d04c0..0x4d04ef): from 0.75 the walk goes to the last sample (5),
        // wraps to sample 0 with sampleIndex set to 1 and then incremented to 2, so sample 1 is skipped and the second chord is
        // sample 0 to sample 2 (sqrt(200)). The moved total reaches 19.142136, so the overshoot is 7.142136 of the 14.142136 chord, which gives 0.494975. Wrapping to
        // index 0 would give 0.35, and ignoring the wrap would stop at 1.0 with moved 5.
        [Test]
        public void TravelWrapsOverTheEndWhenTheClipIsInverted()
        {
            SampleCollection collection = Polyline();
            float moved;
            double result = collection.Travel(0.75, 12f, Spline.Direction.Forward, out moved, 0.75, 0.6);
            Assert.That(result, Is.EqualTo(1.0 - 7.142136 / 14.142136).Within(1e-5));
            Assert.That(moved, Is.EqualTo(12f).Within(1e-4f));
        }

        // The looped test is clipTo < clipFrom: Travel 0x4d04cb / 0x4d0601 and TravelWithOffset 0x4d0ca1 / 0x4d0eb6 compare with
        // ucomisd and branch with jbe straight to the break, so a NaN clip end (unordered) is not looped and the walk stops at the
        // last sample like an ordinary range, where a reading that only wraps when clipFrom <= clipTo is false would keep walking.
        [Test]
        public void TravelStopsAtTheLastSampleWhenAClipEndIsNaN()
        {
            SampleCollection collection = Polyline();
            float moved;
            Assert.That(collection.Travel(0.75, 12f, Spline.Direction.Forward, out moved, double.NaN, 0.6), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(collection.Travel(0.75, 12f, Spline.Direction.Forward, out moved, 0.75, double.NaN), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));

            Assert.That(collection.TravelWithOffset(0.75, 12f, Spline.Direction.Forward, Vector3.zero, out moved, double.NaN, 0.6), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(collection.TravelWithOffset(0.75, 12f, Spline.Direction.Forward, Vector3.zero, out moved, 0.75, double.NaN), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-4f));
        }

        // TravelWithOffset 0x4d07d0: the offset is applied in each sample's own axes. A forward offset of 1 moves sample 0
        // by (1,0,0) and samples 1 and 2 by (0,0,1), so the first chord is sqrt(82) long instead of 10 and the walk of 5 ends
        // earlier than the unshifted 0.25.
        [Test]
        public void TravelWithOffsetShiftsTheSamples()
        {
            SampleCollection collection = Polyline();
            float moved;
            double shifted = collection.TravelWithOffset(0.0, 5f, Spline.Direction.Forward, new Vector3(0f, 0f, 1f), out moved);
            double expected = 0.5 * (5.0 / Math.Sqrt(82.0));
            Assert.That(shifted, Is.EqualTo(expected).Within(1e-4));
            Assert.That(moved, Is.EqualTo(5f).Within(1e-3f));
        }

        // CalculateLength 0x4d1f30: the chords from the evaluated start through the stored samples to the evaluated end.
        [Test]
        public void CalculateLengthSumsTheChords()
        {
            SampleCollection collection = Polyline();
            Assert.That(collection.CalculateLength(), Is.EqualTo(20f).Within(1e-3f));
            Assert.That(collection.CalculateLength(0.25, 0.75), Is.EqualTo(10f).Within(1e-3f));
            Assert.That(collection.CalculateLength(0.75, 0.25), Is.EqualTo(10f).Within(1e-3f), "preventInvert orders the range");
            Assert.That(new SampleCollection().CalculateLength(), Is.EqualTo(0f));
        }

        // CalculateLengthWithOffset 0x4d2300: same walk on shifted samples. Forward offset 1 makes the chords sqrt(82) and 10.
        [Test]
        public void CalculateLengthWithOffsetMeasuresTheShiftedPolyline()
        {
            SampleCollection collection = Polyline();
            Assert.That(collection.CalculateLengthWithOffset(new Vector3(0f, 0f, 1f)), Is.EqualTo(10f + Mathf.Sqrt(82f)).Within(1e-3f));
            Assert.That(collection.CalculateLengthWithOffset(new Vector3(0f, 1f, 0f)), Is.EqualTo(20f).Within(1e-3f), "a pure up shift keeps the length");
            Assert.That(new SampleCollection().CalculateLengthWithOffset(Vector3.one), Is.EqualTo(0f));
        }

        // Project 0x4d1190: a point 2 above the second corner lands on the second chord at 80% of the way back from the end:
        // position (10,0,2), percent 0.6. No samples leave the result alone; one sample is copied.
        [Test]
        public void ProjectFindsTheNearestPointOnTheChords()
        {
            SampleCollection collection = Polyline();
            SplineSample result = new SplineSample();
            collection.Project(new Vector3(10f, 0f, 2f), 3, ref result);
            Near(result.position, new Vector3(10f, 0f, 2f), "projected position");
            Assert.That(result.percent, Is.EqualTo(0.6).Within(1e-4));

            SplineSample untouched = new SplineSample(Vector3.one, Vector3.up, Vector3.forward, Color.white, 2f, 0.3);
            new SampleCollection().Project(Vector3.zero, 3, ref untouched);
            Near(untouched.position, Vector3.one, "empty collection leaves the result");

            SampleCollection single = new SampleCollection();
            single.samples = new[] { collection.samples[1] };
            SplineSample copied = new SplineSample();
            single.Project(new Vector3(3f, 3f, 3f), 3, ref copied);
            Near(copied.position, new Vector3(10f, 0f, 0f), "single sample copy");
        }

        // Project 0x4d1190, Uniform branch (0x4d1c0f..0x4d1c5c): the percent is re-derived from the sample indices, not from the
        // stored sample percents. With percents (0, 0.3, 1) Uniform gives 0.6 (t = 0.8 between indices 2 and 1) while Default
        // interpolates the stored percents back from 1.0 to 0.3 and gives 0.44.
        [Test]
        public void ProjectUniformUsesTheSampleIndicesForThePercent()
        {
            double[] answers = new double[2];
            SplineComputer.SampleMode[] modes = { SplineComputer.SampleMode.Uniform, SplineComputer.SampleMode.Default };
            for (int i = 0; i < modes.Length; i++)
            {
                SampleCollection collection = Polyline(modes[i]);
                collection.samples[1].percent = 0.3;
                SplineSample result = new SplineSample();
                collection.Project(new Vector3(10f, 0f, 2f), 3, ref result);
                Near(result.position, new Vector3(10f, 0f, 2f), modes[i] + " position");
                answers[i] = result.percent;
            }

            Assert.That(answers[0], Is.EqualTo(0.6).Within(1e-4));
            Assert.That(answers[1], Is.EqualTo(0.44).Within(1e-4));
        }

        // Project 0x4d1190, looped-spline branch: when the full-range result lands before the second sample, the segment from the
        // last sample back to the one before it is tried too, and wins if its projection is nearer. The fraction along that segment
        // is InverseLerp(last, last - 1, projected) (0x4d1d22 keeps the ProjectOnLine result in xmm4/xmm5, 0x4d1dde reads it), so
        // a point behind the last sample ends exactly on it. The Lerp that consumes the fraction clamps it, so the earlier
        // reading (the raw position) differs only by float rounding and this expectation cannot tell the two apart; the test pins
        // the branch itself, which no other case reaches.
        [Test]
        public void ProjectPrefersTheClosingSegmentWhenItIsNearerThanTheFirstOne()
        {
            SampleCollection collection = new SampleCollection();
            collection.samples = new[]
            {
                new SplineSample(new Vector3(5f, 0f, 2f), Vector3.up, Vector3.forward, Color.black, 1f, 0.0),
                new SplineSample(new Vector3(6f, 0f, 5f), Vector3.up, Vector3.forward, Color.black, 1f, 1.0 / 3.0),
                new SplineSample(new Vector3(0f, 0f, -6f), Vector3.up, Vector3.forward, Color.black, 1f, 2.0 / 3.0),
                new SplineSample(new Vector3(1f, 0f, -2f), Vector3.up, Vector3.forward, Color.black, 1f, 1.0)
            };
            SplineSample result = new SplineSample();
            collection.Project(new Vector3(1f, 0f, 0f), 2, ref result);
            Near(result.position, new Vector3(1f, 0f, -2f), "the closing projection is clamped to the last sample");
            Assert.That(result.percent, Is.EqualTo(1.0).Within(1e-9));

            // A point whose closing projection falls inside the segment ends 3/17 of the way from the last sample toward the
            // one before it.
            collection.Project(new Vector3(6f, 0f, -4f), 2, ref result);
            Near(result.position, new Vector3(1f - 3f / 17f, 0f, -2f - 12f / 17f), "inside the closing segment");
            Assert.That(result.percent, Is.EqualTo(1.0 - 1.0 / 17.0).Within(1e-6));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrder()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SampleCollection).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "samples", "optimizedIndices", "sampleMode", "_workSample" }));
        }
    }
}
