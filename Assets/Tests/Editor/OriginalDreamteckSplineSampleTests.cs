using System;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineSample. Expected values are derived from the x86-64 bodies (addresses in each comment) and from
    // the arithmetic of the shipped formulas, not read back from the restored source.
    public sealed class OriginalDreamteckSplineSampleTests
    {
        private const float Tolerance = 1e-4f;

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private static SplineSample SampleA()
        {
            return new SplineSample(Vector3.zero, Vector3.up, Vector3.right, Color.black, 1f, 0.2);
        }

        private static SplineSample SampleB()
        {
            return new SplineSample(new Vector3(10f, 0f, 0f), Vector3.forward, Vector3.up, Color.white, 3f, 0.8);
        }

        private static void AssertSame(SplineSample actual, SplineSample expected, string message)
        {
            Near(actual.position, expected.position, message + " position");
            Near(actual.up, expected.up, message + " up");
            Near(actual.forward, expected.forward, message + " forward");
            Assert.That(actual.color, Is.EqualTo(expected.color), message + " color");
            Assert.That(actual.size, Is.EqualTo(expected.size).Within(1e-6f), message + " size");
            Assert.That(actual.percent, Is.EqualTo(expected.percent).Within(1e-9), message + " percent");
        }

        // Original constructor 0x4e2d70 stores the six arguments; FastCopy 0x4e2110 copies the six fields back.
        [Test]
        public void ConstructorAndFastCopyMoveEverySerializedField()
        {
            SplineSample source = new SplineSample(new Vector3(1f, 2f, 3f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 1f), new Color(0.1f, 0.2f, 0.3f, 0.4f), 5f, 0.75);
            Near(source.position, new Vector3(1f, 2f, 3f), "position");
            Near(source.up, Vector3.up, "up");
            Near(source.forward, Vector3.forward, "forward");
            Assert.That(source.color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(source.size, Is.EqualTo(5f));
            Assert.That(source.percent, Is.EqualTo(0.75));

            SplineSample target = SampleB();
            target.FastCopy(ref source);
            AssertSame(target, source, "FastCopy");
        }

        // Original rotation 0x4e2160: LookRotation(forward, up) unless up == forward (Unity vector equality). Then a world-up pair
        // answers LookRotation(Vector3.up, Vector3.back) and any other pair LookRotation(forward, Vector3.up).
        [Test]
        public void RotationFallsBackWhenUpEqualsForward()
        {
            SplineSample plain = new SplineSample(Vector3.zero, Vector3.up, Vector3.forward, Color.white, 1f, 0.0);
            Near(plain.rotation * Vector3.forward, Vector3.forward, "plain forward");
            Near(plain.rotation * Vector3.up, Vector3.up, "plain up");

            SplineSample tilted = new SplineSample(Vector3.zero, Vector3.right, Vector3.forward, Color.white, 1f, 0.0);
            Near(tilted.rotation * Vector3.forward, Vector3.forward, "tilted forward");
            Near(tilted.rotation * Vector3.up, Vector3.right, "LookRotation(forward, up) maps the local up to the sample's up");

            SplineSample worldUp = new SplineSample(Vector3.zero, Vector3.up, Vector3.up, Color.white, 1f, 0.0);
            Near(worldUp.rotation * Vector3.forward, Vector3.up, "world-up sample looks along up");
            Near(worldUp.rotation * Vector3.up, Vector3.back, "...with Vector3.back as its up");

            SplineSample sideways = new SplineSample(Vector3.zero, Vector3.right, Vector3.right, Color.white, 1f, 0.0);
            Near(sideways.rotation * Vector3.forward, Vector3.right, "an equal non-world-up pair looks along forward");
            Near(sideways.rotation * Vector3.up, Vector3.up, "...with Vector3.up as its up");
        }

        // Original right 0x4e22a0: Cross(up, forward) normalised (a zero cross product answers zero). An up == forward pair
        // answers Vector3.right for the world up and otherwise Cross(Vector3.up, forward) normalised.
        [Test]
        public void RightIsTheNormalisedCrossProductWithTheEqualPairFallbacks()
        {
            SplineSample basic = new SplineSample(Vector3.zero, new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 3f), Color.white, 1f, 0.0);
            Near(basic.right, Vector3.right, "Cross(up, forward) / |.|");

            SplineSample reversed = new SplineSample(Vector3.zero, new Vector3(0f, 0f, 3f), new Vector3(0f, 2f, 0f), Color.white, 1f, 0.0);
            Near(reversed.right, Vector3.left, "swapping the arguments flips the sign");

            SplineSample parallel = new SplineSample(Vector3.zero, Vector3.up, new Vector3(0f, 2f, 0f), Color.white, 1f, 0.0);
            Near(parallel.right, Vector3.zero, "parallel but unequal vectors cross to zero");

            SplineSample worldUp = new SplineSample(Vector3.zero, Vector3.up, Vector3.up, Color.white, 1f, 0.0);
            Near(worldUp.right, Vector3.right, "equal world-up pair");

            SplineSample forwardPair = new SplineSample(Vector3.zero, Vector3.forward, Vector3.forward, Color.white, 1f, 0.0);
            Near(forwardPair.right, Vector3.right, "equal pair along z: Cross(Vector3.up, forward)");

            SplineSample rightPair = new SplineSample(Vector3.zero, Vector3.right, Vector3.right, Color.white, 1f, 0.0);
            Near(rightPair.right, Vector3.back, "equal pair along x: Cross(Vector3.up, Vector3.right) = back");
        }

        // Original Lerp bodies 0x4e25f0, 0x4e2890, 0x4e2a00, 0x4e2760, 0x4e2b30, 0x4e2c50: position is a clamped linear lerp
        // (LerpVector3NonAlloc), forward and up are Slerps, color / size / percent are clamped lerps.
        [Test]
        public void LerpOverloadsInterpolateEachFieldWithItsNativeInterpolator()
        {
            SplineSample a = SampleA();
            SplineSample b = SampleB();
            SplineSample expected = new SplineSample(
                new Vector3(2.5f, 0f, 0f),
                new Vector3(0f, 0.9238795f, 0.3826834f),
                new Vector3(0.9238795f, 0.3826834f, 0f),
                new Color(0.25f, 0.25f, 0.25f, 1f),
                1.5f,
                0.35);

            AssertSame(SplineSample.Lerp(ref a, ref b, 0.25f), expected, "static float");
            AssertSame(SplineSample.Lerp(ref a, ref b, 0.25), expected, "static double");

            SplineSample junk = new SplineSample(new Vector3(9f, 9f, 9f), new Vector3(9f, 9f, 9f), new Vector3(9f, 9f, 9f), Color.magenta, 9f, 9.0);
            SplineSample.Lerp(ref a, ref b, 0.25f, ref junk);
            AssertSame(junk, expected, "target float");
            junk = new SplineSample(new Vector3(9f, 9f, 9f), new Vector3(9f, 9f, 9f), new Vector3(9f, 9f, 9f), Color.magenta, 9f, 9.0);
            SplineSample.Lerp(ref a, ref b, 0.25, ref junk);
            AssertSame(junk, expected, "target double");

            SplineSample inPlace = a;
            inPlace.Lerp(ref b, 0.25);
            AssertSame(inPlace, expected, "instance double");
            inPlace = a;
            inPlace.Lerp(ref b, 0.25f);
            AssertSame(inPlace, expected, "instance float");
            AssertSame(a, SampleA(), "the operands of the static overloads are not modified");
        }

        // Original Lerp bodies clamp t to [0, 1] in every interpolator (LerpVector3NonAlloc 0x4f9760, the Slerp, Color / Mathf
        // clamps in the bodies and DMath.Lerp 0x4f96e0), so out-of-range t answers an endpoint instead of extrapolating.
        [Test]
        public void LerpClampsTheParameterInEveryInterpolator()
        {
            SplineSample a = SampleA();
            SplineSample b = SampleB();
            AssertSame(SplineSample.Lerp(ref a, ref b, 1.5f), b, "float above 1");
            AssertSame(SplineSample.Lerp(ref a, ref b, 1.5), b, "double above 1");
            AssertSame(SplineSample.Lerp(ref a, ref b, -0.5f), a, "float below 0");
            AssertSame(SplineSample.Lerp(ref a, ref b, -0.5), a, "double below 0");
            SplineSample inPlace = a;
            inPlace.Lerp(ref b, 4.0);
            AssertSame(inPlace, b, "instance double above 1");
        }

        // Original SplineSample metadata: [Serializable] struct with these six public fields in this order.
        [Test]
        public void SerializedLayoutKeepsTheFieldOrder()
        {
            Assert.That(typeof(SplineSample).IsSerializable, Is.True);
            FieldInfo[] fields = typeof(SplineSample).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).ToArray();
            Assert.That(Array.ConvertAll(fields, field => field.Name), Is.EqualTo(new[] { "position", "up", "forward", "color", "size", "percent" }));
            Assert.That(Array.ConvertAll(fields, field => field.FieldType), Is.EqualTo(new[] { typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float), typeof(double) }));
        }
    }
}
