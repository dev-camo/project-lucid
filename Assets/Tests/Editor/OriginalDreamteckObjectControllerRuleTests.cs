using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.ObjectControllerCustomRuleBase, ObjectControllerSineRule and ObjectControllerSpiralRule. The rules are
    // ScriptableObjects fed a hand made SplineSample (position (1,2,3), looking along +z with the world up, size 2, percent
    // 0.5); the object percent is index / (total - 1). Expected values are hand arithmetic from the native bodies cited per test.
    public sealed class OriginalDreamteckObjectControllerRuleTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<ScriptableObject> _owned = new List<ScriptableObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (ScriptableObject owned in _owned)
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

        private T NewRule<T>() where T : ObjectControllerCustomRuleBase
        {
            T rule = ScriptableObject.CreateInstance<T>();
            _owned.Add(rule);
            return rule;
        }

        private static SplineSample Sample(double percent = 0.5, Vector3? forward = null)
        {
            SplineSample sample = new SplineSample();
            sample.position = new Vector3(1f, 2f, 3f);
            sample.forward = forward ?? Vector3.forward;
            sample.up = Vector3.up;
            sample.size = 2f;
            sample.percent = percent;
            return sample;
        }

        private ObjectControllerSpiralRule NewSpiralFacing(Vector3 forward, int index, int total)
        {
            ObjectControllerSpiralRule rule = NewRule<ObjectControllerSpiralRule>();
            rule.SetContext(null, Sample(0.5, forward), index, total);
            return rule;
        }

        private ObjectControllerSineRule NewSine(int index, int total, double percent = 0.5)
        {
            ObjectControllerSineRule rule = NewRule<ObjectControllerSineRule>();
            rule.SetContext(null, Sample(percent), index, total);
            return rule;
        }

        private ObjectControllerSpiralRule NewSpiral(int index, int total, double percent = 0.5)
        {
            ObjectControllerSpiralRule rule = NewRule<ObjectControllerSpiralRule>();
            rule.SetContext(null, Sample(percent), index, total);
            return rule;
        }

        // SetContext 0x4a60c0 stores the four values; GetOffset 0x4a6120, GetRotation 0x4a6130 and GetScale 0x4a6140 answer the
        // sample's position, rotation and size (a vector of that size).
        [Test]
        public void BaseRuleAnswersTheSample()
        {
            ObjectControllerCustomRuleBase rule = NewRule<ObjectControllerCustomRuleBase>();
            rule.SetContext(null, Sample(), 3, 7);
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(typeof(ObjectControllerCustomRuleBase).GetField("currentObjectIndex", flags).GetValue(rule), Is.EqualTo(3));
            Assert.That(typeof(ObjectControllerCustomRuleBase).GetField("totalObjects", flags).GetValue(rule), Is.EqualTo(7));
            Near(rule.GetOffset(), new Vector3(1f, 2f, 3f), "offset");
            Assert.That(Quaternion.Angle(rule.GetRotation(), Quaternion.LookRotation(Vector3.forward, Vector3.up)), Is.LessThan(0.01f));
            Near(rule.GetScale(), new Vector3(2f, 2f, 2f), "scale");

            // A sample looking along +x has the rotation (0, 0.70711, 0, 0.70711), 90 degrees from the identity that a stub gives.
            ObjectControllerCustomRuleBase facing = NewRule<ObjectControllerCustomRuleBase>();
            facing.SetContext(null, Sample(0.5, Vector3.right), 0, 1);
            Assert.That(Quaternion.Angle(facing.GetRotation(), Quaternion.LookRotation(Vector3.right, Vector3.up)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(facing.GetRotation(), Quaternion.identity), Is.GreaterThan(80f));
        }

        // GetSine 0x4a6520: sin(percent * pi * frequency + offset * pi) with the object percent index / (total - 1); GetOffset
        // 0x4a63e0 is AngleAxis(angle, forward) * up * sine * amplitude.
        [Test]
        public void SineOffsetRotatesTheWaveByTheAngle()
        {
            ObjectControllerSineRule rule = NewSine(1, 5);
            rule.amplitude = 2f;
            float sine = Mathf.Sin(0.25f * Mathf.PI);
            Near(rule.GetOffset(), new Vector3(0f, sine * 2f, 0f), "object percent 0.25 along up");
            rule.angle = 90f;
            Near(rule.GetOffset(), new Vector3(-sine * 2f, 0f, 0f), "up turned 90 degrees around forward is -x");
            rule.angle = 180f;
            Near(rule.GetOffset(), new Vector3(0f, -sine * 2f, 0f), "180 degrees");
        }

        [Test]
        public void SineFrequencyAndOffsetShiftTheWave()
        {
            ObjectControllerSineRule rule = NewSine(1, 5);
            rule.frequency = 2f;
            Near(rule.GetOffset(), new Vector3(0f, 1f, 0f), "sin(0.5 pi)");
            rule.offset = 0.25f;
            Near(rule.GetOffset(), new Vector3(0f, Mathf.Sin(0.75f * Mathf.PI), 0f), "sin(0.75 pi)");
            Near(NewSine(0, 5).GetOffset(), Vector3.zero, "the first object starts at sin(0)");
        }

        // With useSplinePercent the sample's own percent drives the wave.
        [Test]
        public void SineUsesTheSplinePercentWhenAsked()
        {
            ObjectControllerSineRule rule = NewSine(1, 5, 0.5);
            rule.useSplinePercent = true;
            Near(rule.GetOffset(), new Vector3(0f, 1f, 0f), "sin(0.5 pi) from the sample percent, not the object percent");
            rule.offset = 0.5f;
            Near(rule.GetOffset(), new Vector3(0f, Mathf.Sin(Mathf.PI), 0f), "sin(pi)");
        }

        // GetScale 0x4a6570: Lerp(one * minScale, one * maxScale, sine) with the interpolation clamped, so a negative wave gives
        // the minimum and a full wave the maximum.
        [Test]
        public void SineScaleLerpsBetweenTheLimitsByTheWave()
        {
            ObjectControllerSineRule rule = NewSine(1, 5);
            rule.minScale = 1f;
            rule.maxScale = 3f;
            float sine = Mathf.Sin(0.25f * Mathf.PI);
            Near(rule.GetScale(), Vector3.one * (1f + 2f * sine), "between the limits");
            rule.frequency = 2f;
            Near(rule.GetScale(), Vector3.one * 3f, "sin(0.5 pi) = 1 gives the maximum");
            ObjectControllerSineRule negative = NewSine(3, 5);
            negative.frequency = 2f;
            negative.minScale = 1f;
            negative.maxScale = 3f;
            Near(negative.GetOffset(), new Vector3(0f, -1f, 0f), "sin(1.5 pi) = -1");
            Near(negative.GetScale(), Vector3.one, "a negative wave clamps to the minimum");
        }

        // The offset setters 0x4a6280 and 0x4a6740 wrap a value above 1 by its whole units and a value below 0 by the whole units
        // of its magnitude, so a negative value above -1 stays negative.
        [Test]
        public void OffsetSettersWrapIntoTheUnitRange()
        {
            ObjectControllerSineRule sine = NewRule<ObjectControllerSineRule>();
            ObjectControllerSpiralRule spiral = NewRule<ObjectControllerSpiralRule>();
            float[,] cases =
            {
                { 0.5f, 0.5f },
                { 1f, 1f },
                { 1.25f, 0.25f },
                { 2.5f, 0.5f },
                { 0f, 0f },
                { -0.25f, -0.25f },
                { -1.25f, -0.25f },
                { -2.5f, -0.5f }
            };
            for (int i = 0; i < cases.GetLength(0); i++)
            {
                sine.offset = cases[i, 0];
                spiral.offset = cases[i, 0];
                Assert.That(sine.offset, Is.EqualTo(cases[i, 1]).Within(1e-6f), "sine " + cases[i, 0]);
                Assert.That(spiral.offset, Is.EqualTo(cases[i, 1]).Within(1e-6f), "spiral " + cases[i, 0]);
            }
        }

        // GetOffset 0x4a68a0: AngleAxis(revolve * percent, forward) * up, its x and y stretched by Lerp(startSize, endSize, percent).
        [Test]
        public void SpiralOffsetTurnsTheUpVectorAndStretchesBySize()
        {
            ObjectControllerSpiralRule rule = NewSpiral(1, 5);
            Near(rule.GetOffset(), new Vector3(-1f, 0f, 0f), "a quarter turn of the unit up vector");
            rule.startSize = new Vector2(1f, 2f);
            rule.endSize = new Vector2(3f, 4f);
            Near(rule.GetOffset(), new Vector3(-1.5f, 0f, 0f), "size 1.5 along x");
            rule.revolve = 0f;
            Near(rule.GetOffset(), new Vector3(0f, 2.5f, 0f), "no turn keeps up, size 2.5 along y");
            ObjectControllerSpiralRule first = NewSpiral(0, 5);
            first.startSize = new Vector2(1f, 2f);
            first.endSize = new Vector2(3f, 4f);
            Near(first.GetOffset(), new Vector3(0f, 2f, 0f), "the first object is at the start size");
            ObjectControllerSpiralRule last = NewSpiral(4, 5);
            last.startSize = new Vector2(1f, 2f);
            last.endSize = new Vector2(3f, 4f);
            Near(last.GetOffset(), new Vector3(0f, 4f, 0f), "a full turn at the last object, at the end size");
        }

        // GetPercent 0x4a69c0: the object percent gets the rule's offset added and wraps; the spline percent is used as is (also
        // wrapped) and never gets the offset; a negative percent above -1 stays negative.
        [Test]
        public void SpiralPercentWrapsAndTheSplinePercentIgnoresTheOffset()
        {
            ObjectControllerSpiralRule rule = NewSpiral(4, 5);
            rule.offset = 0.25f;
            Near(rule.GetOffset(), new Vector3(-1f, 0f, 0f), "1 + 0.25 wraps to 0.25: a quarter turn");

            ObjectControllerSpiralRule spline = NewSpiral(0, 5, 0.5);
            spline.useSplinePercent = true;
            spline.offset = 0.25f;
            Near(spline.GetOffset(), new Vector3(0f, -1f, 0f), "the spline percent 0.5 is a half turn, the offset is ignored");
            ObjectControllerSpiralRule wrapped = NewSpiral(0, 5, 1.25);
            wrapped.useSplinePercent = true;
            Near(wrapped.GetOffset(), new Vector3(-1f, 0f, 0f), "1.25 wraps to 0.25");
            ObjectControllerSpiralRule negative = NewSpiral(0, 5, -0.25);
            negative.useSplinePercent = true;
            Near(negative.GetOffset(), new Vector3(1f, 0f, 0f), "-0.25 stays and turns the other way");
        }

        // GetRotation 0x4a6b30: the sample rotation times AngleAxis(-revolve * percent, forward).
        [Test]
        public void SpiralRotationTurnsTheSampleBackByTheSameAngle()
        {
            ObjectControllerSpiralRule rule = NewSpiral(1, 5);
            Quaternion expected = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.forward);
            Assert.That(Quaternion.Angle(rule.GetRotation(), expected), Is.LessThan(0.01f));
            rule.revolve = 180f;
            expected = Quaternion.AngleAxis(-45f, Vector3.forward);
            Assert.That(Quaternion.Angle(rule.GetRotation(), expected), Is.LessThan(0.01f));

            // The sample is the left factor (0x4a6bba..0x4a6c5c): with a sample looking along +x the product is
            // (-0.5, 0.5, -0.5, 0.5), 120 degrees from the reversed product (0.5, 0.5, -0.5, 0.5).
            ObjectControllerSpiralRule facing = NewSpiralFacing(Vector3.right, 1, 5);
            Quaternion actual = facing.GetRotation();
            Assert.That(Quaternion.Angle(actual, new Quaternion(-0.5f, 0.5f, -0.5f, 0.5f)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(actual, new Quaternion(0.5f, 0.5f, -0.5f, 0.5f)), Is.GreaterThan(100f));
        }

        // The ctors 0x4a6690 and 0x4a6c70 and the serialized layouts.
        [Test]
        public void DefaultsAndSerializedLayoutMatchTheShippedRules()
        {
            ObjectControllerSineRule sine = NewRule<ObjectControllerSineRule>();
            Assert.That(sine.useSplinePercent, Is.False);
            Assert.That(sine.frequency, Is.EqualTo(1f));
            Assert.That(sine.amplitude, Is.EqualTo(1f));
            Assert.That(sine.angle, Is.EqualTo(0f));
            Assert.That(sine.minScale, Is.EqualTo(1f));
            Assert.That(sine.maxScale, Is.EqualTo(1f));
            Assert.That(sine.offset, Is.EqualTo(0f));
            ObjectControllerSpiralRule spiral = NewRule<ObjectControllerSpiralRule>();
            Assert.That(spiral.useSplinePercent, Is.False);
            Assert.That(spiral.revolve, Is.EqualTo(360f));
            Assert.That(spiral.startSize, Is.EqualTo(Vector2.one));
            Assert.That(spiral.endSize, Is.EqualTo(Vector2.one));
            Assert.That(spiral.offset, Is.EqualTo(0f));

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(ObjectControllerCustomRuleBase).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "currentController", "currentSample", "currentObjectIndex", "totalObjects" }));
            Assert.That(typeof(ObjectControllerSineRule).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "_useSplinePercent", "_frequency", "_amplitude", "_angle", "_minScale", "_maxScale", "_offset" }));
            Assert.That(typeof(ObjectControllerSpiralRule).GetFields(flags).Select(f => f.Name).ToArray(),
                Is.EqualTo(new[] { "_useSplinePercent", "_revolve", "_startSize", "_endSize", "_offset" }));
            UnityEngine.RangeAttribute sineRange = typeof(ObjectControllerSineRule).GetField("_offset", flags).GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That(sineRange.min, Is.EqualTo(0f));
            Assert.That(sineRange.max, Is.EqualTo(1f));
            UnityEngine.RangeAttribute spiralRange = typeof(ObjectControllerSpiralRule).GetField("_offset", flags).GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.That(spiralRange.max, Is.EqualTo(1f));
            Assert.That(typeof(ObjectControllerSineRule).GetCustomAttribute<CreateAssetMenuAttribute>().menuName,
                Is.EqualTo("Dreamteck/Splines/Object Controller Rules/Sine Rule"));
            Assert.That(typeof(ObjectControllerSpiralRule).GetCustomAttribute<CreateAssetMenuAttribute>().menuName,
                Is.EqualTo("Dreamteck/Splines/Object Controller Rules/Spiral Rule"));
            Assert.That(typeof(ObjectControllerSineRule).BaseType, Is.SameAs(typeof(ObjectControllerCustomRuleBase)));
            Assert.That(typeof(ObjectControllerCustomRuleBase).BaseType, Is.SameAs(typeof(ScriptableObject)));
        }
    }
}
