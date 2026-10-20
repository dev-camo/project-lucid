using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineSampleModifier and its keyed subclasses plus ObjectSequence<T>. Expected values are worked out
    // from the x86-64 bodies (addresses in each comment) with hand arithmetic, not read back from the restored source. The
    // base Key constructor is internal in the original, so the keys are built through the public subclass constructors.
    // Nothing here touches managers or registries; the one GameObject (a look target) is destroyed by its test.
    public sealed class OriginalDreamteckSplineModifierTests
    {
        private const float Tolerance = 1e-4f;
        private const double DoubleTolerance = 1e-9;

        private static readonly BindingFlags DeclaredMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F6") + ")");
        }

        private static void Near(Color actual, Color expected, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(Tolerance), message + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(Tolerance), message + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(Tolerance), message + " b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(Tolerance), message + " a");
        }

        // Up (0,1,0), forward (0,0,1): the sample rotation is the identity and right is (1,0,0).
        private static SplineSample Sample(double percent)
        {
            return new SplineSample(Vector3.zero, Vector3.up, Vector3.forward, Color.black, 1f, percent);
        }

        // Key ctor 0x4b1570: start/end through the clamping setters, centre range 0.25..0.75 and blend 1 from the field
        // initialisers (constant 0x29a7340 holds the pair 0.25, 0.75), interpolation AnimationCurve.Linear(0, 0, 1, 1).
        [Test]
        public void KeyStartsWithTheShippedDefaultsAndConstructorClampsTheRange()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.2, 0.9);
            Assert.That(key.start, Is.EqualTo(0.2));
            Assert.That(key.end, Is.EqualTo(0.9));
            Assert.That(key.centerStart, Is.EqualTo(0.25));
            Assert.That(key.centerEnd, Is.EqualTo(0.75));
            Assert.That(key.blend, Is.EqualTo(1f));
            Assert.That(key.size, Is.EqualTo(0f));
            Assert.That(key.interpolation.Evaluate(0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(key.interpolation.Evaluate(0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(key.interpolation.Evaluate(1f), Is.EqualTo(1f).Within(1e-6f));

            SizeModifier.SizeKey clamped = new SizeModifier.SizeKey(2.0, -1.0);
            Assert.That(clamped.start, Is.EqualTo(1.0));
            Assert.That(clamped.end, Is.EqualTo(0.0));
            Assert.That(new SplineSampleModifier().blend, Is.EqualTo(1f));
            Assert.That(new SplineSampleModifier().useClippedPercent, Is.False);
        }

        // Setters 0x4b1bc0..0x4b1c90: each clamps to 0..1; centerStart is capped at centerEnd and centerEnd is raised to centerStart.
        // The feather setters do not touch the centre range.
        [Test]
        public void KeyRangeSettersClampAndKeepTheCentreRangeOrdered()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.1, 0.9);
            key.centerStart = 0.9;
            Assert.That(key.centerStart, Is.EqualTo(0.75), "centerStart capped at centerEnd");
            key.centerEnd = 0.1;
            Assert.That(key.centerEnd, Is.EqualTo(0.75), "centerEnd raised back to centerStart");
            key.centerStart = -1.0;
            Assert.That(key.centerStart, Is.EqualTo(0.0));
            key.centerEnd = 2.0;
            Assert.That(key.centerEnd, Is.EqualTo(1.0));
            key.start = 5.0;
            key.end = -5.0;
            Assert.That(key.start, Is.EqualTo(1.0));
            Assert.That(key.end, Is.EqualTo(0.0));
            Assert.That(key.centerStart, Is.EqualTo(0.0));
            Assert.That(key.centerEnd, Is.EqualTo(1.0));
        }

        // get_globalCenterStart/End 0x4b1cd0/0x4b1e90: Lerp(start, end, centre); a wrapped key (start > end) lerps to end + 1 and
        // folds results above 1 back by -1. The setters 0x4b1d70/0x4b1ee0 invert that with InverseLerp and clamp (0x4b1e20):
        // above start the tail segment, below end the head segment (from -(1 - start)), otherwise 0.
        [Test]
        public void KeyGlobalCenterMapsThroughTheFeatherRangeAndWraps()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.2, 0.6);
            Assert.That(key.globalCenterStart, Is.EqualTo(0.3).Within(DoubleTolerance));
            Assert.That(key.globalCenterEnd, Is.EqualTo(0.5).Within(DoubleTolerance));
            key.globalCenterStart = 0.4;
            Assert.That(key.centerStart, Is.EqualTo(0.5).Within(DoubleTolerance));
            key.globalCenterEnd = 0.9;
            Assert.That(key.centerEnd, Is.EqualTo(1.0).Within(DoubleTolerance), "beyond end clamps to 1");

            // 0.55 maps to local InverseLerp(0.2, 0.6, 0.55) = 0.875, which the centerStart setter caps at centerEnd (0.75).
            SizeModifier.SizeKey capped = new SizeModifier.SizeKey(0.2, 0.6);
            capped.globalCenterStart = 0.55;
            Assert.That(capped.centerStart, Is.EqualTo(0.75).Within(DoubleTolerance), "goes through the ordering setter");

            SizeModifier.SizeKey wrapped = new SizeModifier.SizeKey(0.8, 0.2);
            Assert.That(wrapped.globalCenterStart, Is.EqualTo(0.9).Within(DoubleTolerance));
            Assert.That(wrapped.globalCenterEnd, Is.EqualTo(0.1).Within(DoubleTolerance), "1.1 folds back by 1");
            wrapped.globalCenterStart = 0.95;
            Assert.That(wrapped.centerStart, Is.EqualTo(0.375).Within(DoubleTolerance), "tail: (0.95 - 0.8) / 0.4");
            wrapped.globalCenterEnd = 0.05;
            Assert.That(wrapped.centerEnd, Is.EqualTo(0.625).Within(DoubleTolerance), "head: (0.05 + 0.2) / 0.4");
            wrapped.globalCenterEnd = 0.5;
            Assert.That(wrapped.centerEnd, Is.EqualTo(wrapped.centerStart).Within(DoubleTolerance), "outside both segments maps to 0, then centerEnd rises to centerStart");
        }

        // get_position 0x4b1f90 / set_position 0x4b2030: the mid point of the centre range mapped into the feather range
        // (the wrapped form is mid * (1 - start + end) + start, folded by -1 above 1); the setter shifts start and end by the difference.
        [Test]
        public void KeyPositionMovesTheWholeFeatherRange()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.2, 0.6);
            Assert.That(key.position, Is.EqualTo(0.4).Within(DoubleTolerance));
            key.position = 0.5;
            Assert.That(key.start, Is.EqualTo(0.3).Within(DoubleTolerance));
            Assert.That(key.end, Is.EqualTo(0.7).Within(DoubleTolerance));

            SizeModifier.SizeKey wrapped = new SizeModifier.SizeKey(0.9, 0.3);
            Assert.That(wrapped.position, Is.EqualTo(0.1).Within(DoubleTolerance), "0.5 * 0.4 + 0.9 = 1.1 folds to 0.1");
            wrapped.position = 0.2;
            Assert.That(wrapped.start, Is.EqualTo(1.0).Within(DoubleTolerance));
            Assert.That(wrapped.end, Is.EqualTo(0.4).Within(DoubleTolerance));

            SizeModifier.SizeKey nearEdge = new SizeModifier.SizeKey(0.5, 0.9);
            nearEdge.position = 2.0;
            Assert.That(nearEdge.start, Is.EqualTo(1.0), "shifted start clamps");
            Assert.That(nearEdge.end, Is.EqualTo(1.0), "shifted end clamps");

            // The centre range 0..0.5 has mid 0.25 (Lerp(centerStart, centerEnd, 0.5), constant 0x29a6e58), so the lerp
            // parameter is 0.25 and not the default range's 0.5.
            SizeModifier.SizeKey offCentre = new SizeModifier.SizeKey(0.2, 0.6);
            offCentre.centerStart = 0.0;
            offCentre.centerEnd = 0.5;
            Assert.That(offCentre.position, Is.EqualTo(0.3).Within(DoubleTolerance), "Lerp(0.2, 0.6, 0.25)");
            SizeModifier.SizeKey wrappedOffCentre = new SizeModifier.SizeKey(0.9, 0.3);
            wrappedOffCentre.centerEnd = 1.0;
            wrappedOffCentre.centerStart = 0.5;
            Assert.That(wrappedOffCentre.position, Is.EqualTo(0.2).Within(DoubleTolerance), "0.75 * 0.4 + 0.9 = 1.2 folds to 0.2");
        }

        // Evaluate 0x4b1470: the local percent (float) below centerStart divides by centerStart, above centerEnd runs
        // 1 - InverseLerp(centerEnd, 1, percent), otherwise 1; the curve is evaluated at that value and scaled by key.blend.
        [Test]
        public void KeyEvaluateRampsHoldsAndFalls()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.2, 0.8);
            Assert.That(key.Evaluate(0.2), Is.EqualTo(0f).Within(1e-5f), "start");
            Assert.That(key.Evaluate(0.275), Is.EqualTo(0.5f).Within(1e-5f), "local 0.125 of 0.25");
            Assert.That(key.Evaluate(0.5), Is.EqualTo(1f).Within(1e-5f), "hold");
            Assert.That(key.Evaluate(0.725), Is.EqualTo(0.5f).Within(1e-5f), "local 0.875 is halfway down");
            Assert.That(key.Evaluate(0.8), Is.EqualTo(0f).Within(1e-5f), "end");
            Assert.That(key.Evaluate(0.0), Is.EqualTo(0f).Within(1e-5f), "before start clamps to local 0");
            Assert.That(key.Evaluate(1.0), Is.EqualTo(0f).Within(1e-5f), "after end clamps to local 1");

            key.blend = 0.5f;
            Assert.That(key.Evaluate(0.5), Is.EqualTo(0.5f).Within(1e-5f), "blend scales the curve value");
            key.blend = 1f;
            key.interpolation = AnimationCurve.Linear(0f, 0f, 1f, 2f);
            Assert.That(key.Evaluate(0.5), Is.EqualTo(2f).Within(1e-5f), "the key's own curve is evaluated");
            Assert.That(key.Evaluate(0.275), Is.EqualTo(1f).Within(1e-5f));
            key.interpolation = null;
            Assert.Throws<NullReferenceException>(() => key.Evaluate(0.5));
        }

        // GlobalToLocalPercent 0x4b1e20 with start > end: t above start runs InverseLerp(start, end + 1, t), t below end runs
        // InverseLerp(-(1 - start), end, t), anything in between is local 0.
        [Test]
        public void KeyEvaluateOfAWrappedRangeUsesTheTailSegments()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.8, 0.2);
            Assert.That(key.Evaluate(0.85), Is.EqualTo(0.5f).Within(1e-5f), "tail local 0.125");
            Assert.That(key.Evaluate(1.0), Is.EqualTo(1f).Within(1e-5f), "tail local 0.5 holds");
            Assert.That(key.Evaluate(0.05), Is.EqualTo(1f).Within(1e-5f), "head local 0.625 holds");
            Assert.That(key.Evaluate(0.15), Is.EqualTo(0.5f).Within(1e-5f), "head local 0.875 falls");
            Assert.That(key.Evaluate(0.5), Is.EqualTo(0f).Within(1e-5f), "between end and start is local 0");
        }

        // Duplicate 0x4b2130 builds a plain Key (derived keys do not override it) from start/end, copies the raw centre
        // fields and blend, and replaces the fresh Linear curve with DuplicateUtility.DuplicateCurve of the original.
        [Test]
        public void KeyDuplicateYieldsAPlainKeyWithAClonedCurve()
        {
            SizeModifier.SizeKey key = new SizeModifier.SizeKey(0.1, 0.9);
            key.size = 7f;
            key.centerStart = 0.3;
            key.centerEnd = 0.6;
            key.blend = 0.4f;
            key.interpolation = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 0.75f));

            SplineSampleModifier.Key copy = key.Duplicate();
            Assert.That(copy.GetType(), Is.EqualTo(typeof(SplineSampleModifier.Key)));
            Assert.That(copy.start, Is.EqualTo(0.1));
            Assert.That(copy.end, Is.EqualTo(0.9));
            Assert.That(copy.centerStart, Is.EqualTo(0.3));
            Assert.That(copy.centerEnd, Is.EqualTo(0.6));
            Assert.That(copy.blend, Is.EqualTo(0.4f));
            Assert.That(copy.interpolation, Is.Not.SameAs(key.interpolation));
            Assert.That(copy.interpolation.keys.Length, Is.EqualTo(2));
            Assert.That(copy.interpolation.Evaluate(0f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(copy.interpolation.Evaluate(1f), Is.EqualTo(0.75f).Within(1e-6f));
            copy.interpolation.AddKey(0.5f, 9f);
            Assert.That(key.interpolation.keys.Length, Is.EqualTo(2), "the copy owns its curve");

            // The centre fields are copied raw: a centre range above the fresh key's default centerEnd of 0.75 would be
            // capped by the setters (centerStart 0.75), so only the field copy keeps 0.8..0.9.
            SizeModifier.SizeKey high = new SizeModifier.SizeKey(0.1, 0.9);
            high.centerEnd = 0.9;
            high.centerStart = 0.8;
            SplineSampleModifier.Key highCopy = high.Duplicate();
            Assert.That(highCopy.centerStart, Is.EqualTo(0.8));
            Assert.That(highCopy.centerEnd, Is.EqualTo(0.9));
        }

        // Base GetKeys 0x4b1b10 answers a fresh empty list; SetKeys 0x4b0e10 and Apply 0x4b1460 are empty; Apply(source,
        // destination) 0x4b1b70 copies the sample then calls the virtual Apply(ref destination) (vtable slot 6).
        [Test]
        public void BaseModifierIsAnEmptyPassThroughAndTheTwoSampleApplyDispatches()
        {
            SplineSampleModifier modifier = new SplineSampleModifier();
            List<SplineSampleModifier.Key> keys = modifier.GetKeys();
            Assert.That(keys, Is.Empty);
            Assert.That(modifier.GetKeys(), Is.Not.SameAs(keys));
            modifier.SetKeys(new List<SplineSampleModifier.Key> { new SizeModifier.SizeKey(0.0, 1.0) });
            Assert.That(modifier.GetKeys(), Is.Empty);

            SplineSample source = new SplineSample(new Vector3(1f, 2f, 3f), Vector3.up, Vector3.forward, Color.red, 4f, 0.5);
            SplineSample inPlace = source;
            modifier.Apply(ref inPlace);
            Assert.That(inPlace.size, Is.EqualTo(4f));

            SplineSample destination = new SplineSample();
            modifier.Apply(ref source, ref destination);
            Assert.That(destination.position, Is.EqualTo(source.position));
            Assert.That(destination.color, Is.EqualTo(Color.red));
            Assert.That(destination.size, Is.EqualTo(4f));
            Assert.That(destination.percent, Is.EqualTo(0.5));

            SizeModifier sizer = new SizeModifier();
            sizer.AddKey(0.0, 1.0);
            sizer.keys[0].size = 2f;
            sizer.Apply(ref source, ref destination);
            Assert.That(destination.size, Is.EqualTo(6f), "the override ran on the copy");
            Assert.That(source.size, Is.EqualTo(4f), "the source is untouched");
        }

        // ColorKey.Blend 0x4af430 (jump table at 0x4af51c): Lerp = Color.Lerp(input, color, percent * blend); Multiply =
        // Color.Lerp(input, input * color, percent * blend); Add = input + color * blend * percent; Subtract = input - color * blend * percent;
        // an out-of-range mode returns input. Lerp clamps t to 0..1, Add and Subtract do not.
        [Test]
        public void ColorKeyBlendModesFollowTheNativeTruthTable()
        {
            ColorModifier.ColorKey key = new ColorModifier.ColorKey(0.0, 1.0);
            Assert.That(key.color, Is.EqualTo(Color.white));
            Assert.That(key.blendMode, Is.EqualTo(ColorModifier.ColorKey.BlendMode.Lerp));
            key.color = new Color(1f, 0.5f, 0f, 0.5f);
            key.blend = 0.5f;
            Color input = new Color(0.2f, 0.4f, 0.6f, 1f);

            key.blendMode = ColorModifier.ColorKey.BlendMode.Lerp;
            Near(key.Blend(input, 0.8f), new Color(0.52f, 0.44f, 0.36f, 0.8f), "Lerp t=0.4");
            Near(key.Blend(input, 3f), key.color, "Lerp clamps t=1.5 to the key colour");

            key.blendMode = ColorModifier.ColorKey.BlendMode.Multiply;
            Near(key.Blend(input, 0.8f), new Color(0.2f, 0.32f, 0.36f, 0.8f), "Multiply lerps toward input * color");

            key.blendMode = ColorModifier.ColorKey.BlendMode.Add;
            Near(key.Blend(input, 0.8f), new Color(0.6f, 0.6f, 0.6f, 1.2f), "Add input + color * 0.5 * 0.8");
            Near(key.Blend(input, 3f), new Color(1.7f, 1.15f, 0.6f, 1.75f), "Add does not clamp");

            key.blendMode = ColorModifier.ColorKey.BlendMode.Subtract;
            Near(key.Blend(input, 0.8f), new Color(-0.2f, 0.2f, 0.6f, 0.8f), "Subtract input - color * 0.5 * 0.8");

            key.blendMode = (ColorModifier.ColorKey.BlendMode)7;
            Near(key.Blend(input, 0.8f), input, "unknown mode");
        }

        // Constructor 0x4af020, GetKeys 0x4af0a0 (a List built from the array), SetKeys 0x4af110 (array of input.Count,
        // castclass ColorKey per element, then the base SetKeys), AddKey 0x4af2c0 (ArrayUtility.Add of a new ColorKey),
        // Apply 0x4af380 (return on no keys, base Apply, then each key blends the colour with Evaluate(percent) * blend).
        [Test]
        public void ColorModifierKeysRoundTripAndApplyBlendsInOrder()
        {
            ColorModifier modifier = new ColorModifier();
            Assert.That(modifier.keys, Is.Not.Null);
            Assert.That(modifier.keys.Length, Is.EqualTo(0));
            SplineSample untouched = new SplineSample(Vector3.zero, Vector3.up, Vector3.forward, Color.blue, 1f, 0.5);
            modifier.Apply(ref untouched);
            Assert.That(untouched.color, Is.EqualTo(Color.blue));

            modifier.AddKey(0.0, 1.0);
            modifier.AddKey(0.0, 1.0);
            modifier.keys[0].color = Color.white;
            modifier.keys[1].color = new Color(0.2f, 0.2f, 0.2f, 0f);
            modifier.keys[1].blendMode = ColorModifier.ColorKey.BlendMode.Add;
            List<SplineSampleModifier.Key> listed = modifier.GetKeys();
            Assert.That(listed.Count, Is.EqualTo(2));
            Assert.That(listed[0], Is.SameAs(modifier.keys[0]));
            listed.Clear();
            Assert.That(modifier.keys.Length, Is.EqualTo(2), "the list is a copy");

            modifier.blend = 0.5f;
            SplineSample sample = new SplineSample(Vector3.zero, Vector3.up, Vector3.forward, new Color(0f, 0f, 0f, 1f), 1f, 0.5);
            modifier.Apply(ref sample);
            Near(sample.color, new Color(0.6f, 0.6f, 0.6f, 1f), "Lerp 0.5 toward white, then Add 0.2 * 0.5");

            // The key weight Evaluate(percent) scales the blend (0x4af3ec): percent 0.125 is half way up the ramp.
            ColorModifier ramped = new ColorModifier();
            ramped.AddKey(0.0, 1.0);
            ramped.keys[0].color = Color.white;
            SplineSample dim = new SplineSample(Vector3.zero, Vector3.up, Vector3.forward, new Color(0f, 0f, 0f, 1f), 1f, 0.125);
            ramped.Apply(ref dim);
            Near(dim.color, new Color(0.5f, 0.5f, 0.5f, 1f), "weight 0.5 lerps half way to white");

            ColorModifier replaced = new ColorModifier();
            ColorModifier.ColorKey a = new ColorModifier.ColorKey(0.1, 0.2);
            ColorModifier.ColorKey b = new ColorModifier.ColorKey(0.3, 0.4);
            replaced.SetKeys(new List<SplineSampleModifier.Key> { a, b });
            Assert.That(replaced.keys, Is.EqualTo(new[] { a, b }));
            Assert.Throws<InvalidCastException>(() => replaced.SetKeys(new List<SplineSampleModifier.Key> { new SizeModifier.SizeKey(0.0, 1.0) }));
            Assert.Throws<NullReferenceException>(() => replaced.SetKeys(null));
        }

        // OffsetKey ctor 0x4b0830 stores the offset after the Key ctor; Evaluate 0x4b0a50 sums offset * weight and scales by blend;
        // Apply 0x4b08b0 returns on no keys, runs the base Apply, then position += right * x + up * y.
        [Test]
        public void OffsetModifierShiftsAlongRightAndUpByTheWeightedOffsets()
        {
            OffsetModifier modifier = new OffsetModifier();
            Assert.That(modifier.keys.Length, Is.EqualTo(0));
            SplineSample none = Sample(0.5);
            none.position = new Vector3(1f, 1f, 1f);
            modifier.Apply(ref none);
            Near(none.position, new Vector3(1f, 1f, 1f), "no keys");
            Assert.That(new OffsetModifier.OffsetKey(Vector2.zero, 0.0, 1.0).offset, Is.EqualTo(Vector2.zero));

            modifier.AddKey(new Vector2(2f, 3f), 0.0, 1.0);
            Assert.That(modifier.keys[0].offset, Is.EqualTo(new Vector2(2f, 3f)));
            modifier.blend = 0.5f;
            SplineSample sample = Sample(0.5);
            sample.position = new Vector3(1f, 1f, 1f);
            modifier.Apply(ref sample);
            Near(sample.position, new Vector3(2f, 2.5f, 1f), "(2,3) * 0.5 along right (1,0,0) and up (0,1,0)");

            modifier.AddKey(new Vector2(-2f, 1f), 0.0, 1.0);
            SplineSample two = Sample(0.5);
            modifier.Apply(ref two);
            Near(two.position, new Vector3(0f, 2f, 0f), "offsets add before the blend: (0,4) * 0.5");

            SplineSample ramp = Sample(0.125);
            modifier.Apply(ref ramp);
            Near(ramp.position, new Vector3(0f, 1f, 0f), "weight 0.5 on both keys: (0,2) * 0.5");

            // The offsets follow the sample's own frame: SplineSample.right = Cross(up, forward) = (0,-1,0) here (0x4b09f6).
            OffsetModifier turned = new OffsetModifier();
            turned.AddKey(new Vector2(2f, 3f), 0.0, 1.0);
            SplineSample framed = new SplineSample(Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f), Color.black, 1f, 0.5);
            turned.Apply(ref framed);
            Near(framed.position, new Vector3(3f, -2f, 0f), "right * 2 + up * 3 with right (0,-1,0) and up (1,0,0)");

            List<SplineSampleModifier.Key> keys = modifier.GetKeys();
            OffsetModifier replaced = new OffsetModifier();
            replaced.SetKeys(keys);
            Assert.That(replaced.keys.Length, Is.EqualTo(2));
            Assert.Throws<InvalidCastException>(() => replaced.SetKeys(new List<SplineSampleModifier.Key> { new SizeModifier.SizeKey(0.0, 1.0) }));
        }

        // RotationModifier.Apply 0x4b0fc0: the accumulated rotation q = Slerp(q, q * Euler(key), weight * blend), the sample rotation
        // is steered only by look-target keys, and the result is (sample * q): forward = result * forward, up = result * (inverse(sample) * up).
        // The product order matters: a yaw sample with a pitch key answers a down forward, not the yaw-rotated forward.
        [Test]
        public void RotationModifierAppliesKeyRotationsOnTopOfTheSampleRotation()
        {
            RotationModifier modifier = new RotationModifier();
            Assert.That(modifier.keys.Length, Is.EqualTo(0));
            SplineSample none = Sample(0.5);
            modifier.Apply(ref none);
            Near(none.forward, Vector3.forward, "no keys");

            modifier.AddKey(new Vector3(0f, 90f, 0f), 0.0, 1.0);
            Assert.That(modifier.keys[0].rotation, Is.EqualTo(new Vector3(0f, 90f, 0f)));
            Assert.That(modifier.keys[0].useLookTarget, Is.False);
            Assert.That(modifier.keys[0].target == null, Is.True);
            SplineSample full = Sample(0.5);
            modifier.Apply(ref full);
            Near(full.forward, Vector3.right, "90 degree yaw");
            Near(full.up, Vector3.up, "yaw keeps up");

            modifier.blend = 0.5f;
            SplineSample half = Sample(0.5);
            modifier.Apply(ref half);
            float root = Mathf.Sqrt(0.5f);
            Near(half.forward, new Vector3(root, 0f, root), "blend 0.5 slerps to 45 degrees");

            RotationModifier pitch = new RotationModifier();
            pitch.AddKey(new Vector3(90f, 0f, 0f), 0.0, 1.0);
            SplineSample yawed = new SplineSample(Vector3.zero, Vector3.up, Vector3.right, Color.black, 1f, 0.5);
            pitch.Apply(ref yawed);
            Near(yawed.forward, Vector3.down, "sample * key: pitch the yaw-rotated frame down");
            Near(yawed.up, Vector3.right, "key up (0,0,1) yawed to (1,0,0)");

            // The sample's up is kept relative to its rotation (Inverse(rotation) * up, 0x4b13b0): the rotation here is the
            // identity, so a tilted up must survive instead of being replaced by (0,1,0).
            RotationModifier tilted = new RotationModifier();
            tilted.AddKey(Vector3.zero, 0.0, 1.0);
            SplineSample slanted = new SplineSample(Vector3.zero, new Vector3(0f, 0.6f, 0.8f), Vector3.forward, Color.black, 1f, 0.5);
            tilted.Apply(ref slanted);
            Near(slanted.up, new Vector3(0f, 0.6f, 0.8f), "a tilted up is kept");
            Near(slanted.forward, Vector3.forward, "forward is untouched by a zero rotation");

            RotationModifier two = new RotationModifier();
            two.AddKey(new Vector3(0f, 90f, 0f), 0.0, 1.0);
            two.AddKey(new Vector3(90f, 0f, 0f), 0.0, 1.0);
            SplineSample both = Sample(0.5);
            two.Apply(ref both);
            // q = (yaw 90) * (pitch 90): forward (0,0,1) -> pitch -> (0,-1,0) -> yaw leaves it.
            Near(both.forward, Vector3.down, "keys accumulate on the right");

            Assert.Throws<InvalidCastException>(() => two.SetKeys(new List<SplineSampleModifier.Key> { new SizeModifier.SizeKey(0.0, 1.0) }));
            two.SetKeys(new List<SplineSampleModifier.Key> { modifier.keys[0] });
            Assert.That(two.keys.Length, Is.EqualTo(1));
            Assert.That(two.GetKeys()[0], Is.SameAs(modifier.keys[0]));
        }

        // The look branch needs useLookTarget AND a live target (UnityEngine.Object inequality, 0x4b10c5); otherwise the euler branch runs.
        [Test]
        public void RotationModifierSteersTowardAnAliveLookTargetOnly()
        {
            GameObject owner = new GameObject("Lucid rotation modifier look target");
            try
            {
                owner.transform.position = new Vector3(5f, 0f, 0f);
                RotationModifier modifier = new RotationModifier();
                modifier.AddKey(new Vector3(0f, 0f, 0f), 0.0, 1.0);
                modifier.keys[0].useLookTarget = true;
                modifier.keys[0].target = owner.transform;
                SplineSample look = Sample(0.5);
                modifier.Apply(ref look);
                Near(look.forward, Vector3.right, "steered toward the target at +x");
                Near(look.up, Vector3.up, "up is kept");

                // The look rotation steers the sample's own rotation (0x4b1173), not the accumulated key rotation:
                // pitch 90 key first, then the look key: yaw90 * (pitch90 * forward) = (0,-1,0).
                RotationModifier mixed = new RotationModifier();
                mixed.AddKey(new Vector3(90f, 0f, 0f), 0.0, 1.0);
                mixed.AddKey(Vector3.zero, 0.0, 1.0);
                mixed.keys[1].useLookTarget = true;
                mixed.keys[1].target = owner.transform;
                SplineSample both = Sample(0.5);
                mixed.Apply(ref both);
                Near(both.forward, Vector3.down, "look steers the sample rotation, the euler key stays separate");

                // The look rotation is slerped by weight * blend, not assigned.
                RotationModifier halved = new RotationModifier();
                halved.AddKey(Vector3.zero, 0.0, 1.0);
                halved.keys[0].useLookTarget = true;
                halved.keys[0].target = owner.transform;
                halved.blend = 0.5f;
                SplineSample half = Sample(0.5);
                halved.Apply(ref half);
                float root = Mathf.Sqrt(0.5f);
                Near(half.forward, new Vector3(root, 0f, root), "blend 0.5 slerps halfway to the target");

                modifier.keys[0].useLookTarget = false;
                modifier.keys[0].rotation = new Vector3(0f, 90f, 0f);
                SplineSample ignored = Sample(0.5);
                modifier.Apply(ref ignored);
                Near(ignored.forward, Vector3.right, "a target without useLookTarget runs the euler branch (yaw 90)");

                modifier.keys[0].useLookTarget = true;
                modifier.keys[0].rotation = new Vector3(0f, -90f, 0f);
                UnityEngine.Object.DestroyImmediate(owner);
                SplineSample dead = Sample(0.5);
                modifier.Apply(ref dead);
                Near(dead.forward, Vector3.left, "a destroyed target falls back to the euler branch (yaw -90)");

                modifier.keys[0].target = null;
                SplineSample missing = Sample(0.5);
                modifier.Apply(ref missing);
                Near(missing.forward, Vector3.left, "no target runs the euler branch");
            }
            finally
            {
                if (owner != null)
                {
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }
        }

        // SizeModifier.Apply 0x4b1a70 returns when there are no keys, then adds Evaluate(percent) * key.size * blend per key.
        [Test]
        public void SizeModifierAddsWeightedKeySizes()
        {
            SizeModifier modifier = new SizeModifier();
            Assert.That(modifier.keys.Length, Is.EqualTo(0));
            SplineSample none = Sample(0.5);
            modifier.Apply(ref none);
            Assert.That(none.size, Is.EqualTo(1f));

            modifier.AddKey(0.0, 1.0);
            modifier.AddKey(0.0, 1.0);
            Assert.That(modifier.keys[0].size, Is.EqualTo(0f), "keys start at size 0");
            modifier.keys[0].size = 2f;
            modifier.keys[1].size = 3f;
            modifier.blend = 0.5f;
            SplineSample hold = Sample(0.5);
            modifier.Apply(ref hold);
            Assert.That(hold.size, Is.EqualTo(3.5f).Within(1e-5f), "1 + (2 + 3) * 0.5");

            SplineSample ramp = Sample(0.125);
            modifier.Apply(ref ramp);
            Assert.That(ramp.size, Is.EqualTo(2.25f).Within(1e-5f), "weight 0.5: 1 + (2 + 3) * 0.5 * 0.5");

            Assert.That(modifier.GetKeys().Count, Is.EqualTo(2));
            SizeModifier replaced = new SizeModifier();
            replaced.SetKeys(modifier.GetKeys());
            Assert.That(replaced.keys, Is.EqualTo(modifier.keys));
            Assert.Throws<InvalidCastException>(() => replaced.SetKeys(new List<SplineSampleModifier.Key> { new ColorModifier.ColorKey(0.0, 1.0) }));
        }

        // GetSpeed 0x4afa10: Add keys add weight * speed; every other mode multiplies by Mathf.Lerp(1, speed, weight), which clamps
        // the weight to 0..1 (the Add branch does not). Keys apply in list order.
        [Test]
        public void FollowerSpeedModifierAddsAndMultipliesWithTheNativeClamps()
        {
            FollowerSpeedModifier modifier = new FollowerSpeedModifier();
            Assert.That(modifier.GetSpeed(2f, 0.5), Is.EqualTo(2f), "no keys");
            modifier.AddKey(0.0, 1.0);
            FollowerSpeedModifier.SpeedKey key = modifier.keys[0];
            Assert.That(key.mode, Is.EqualTo(FollowerSpeedModifier.SpeedKey.Mode.Add));
            key.speed = 3f;
            Assert.That(modifier.GetSpeed(2f, 0.5), Is.EqualTo(5f).Within(1e-5f), "Add 1 * 3");
            Assert.That(modifier.GetSpeed(2f, 0.125), Is.EqualTo(3.5f).Within(1e-5f), "Add 0.5 * 3");

            key.mode = FollowerSpeedModifier.SpeedKey.Mode.Multiply;
            Assert.That(modifier.GetSpeed(2f, 0.5), Is.EqualTo(6f).Within(1e-5f), "Lerp(1, 3, 1) = 3");
            Assert.That(modifier.GetSpeed(2f, 0.125), Is.EqualTo(4f).Within(1e-5f), "Lerp(1, 3, 0.5) = 2");

            key.blend = 2f;
            Assert.That(modifier.GetSpeed(2f, 0.5), Is.EqualTo(6f).Within(1e-5f), "Multiply clamps the weight 2 to 1 (unclamped would be 10)");
            key.mode = FollowerSpeedModifier.SpeedKey.Mode.Add;
            Assert.That(modifier.GetSpeed(2f, 0.5), Is.EqualTo(8f).Within(1e-5f), "Add keeps the weight 2");

            FollowerSpeedModifier ordered = new FollowerSpeedModifier();
            ordered.AddKey(0.0, 1.0);
            ordered.AddKey(0.0, 1.0);
            ordered.keys[0].speed = 1f;
            ordered.keys[1].speed = 3f;
            ordered.keys[1].mode = FollowerSpeedModifier.SpeedKey.Mode.Multiply;
            Assert.That(ordered.GetSpeed(2f, 0.5), Is.EqualTo(9f).Within(1e-5f), "(2 + 1) * 3");
            ordered.keys.Reverse();
            Assert.That(ordered.GetSpeed(2f, 0.5), Is.EqualTo(7f).Within(1e-5f), "2 * 3 + 1");

            // Only mode 0 adds (0x4afad7: cmp [mode], 0; je add); any other value multiplies.
            FollowerSpeedModifier unknown = new FollowerSpeedModifier();
            unknown.AddKey(0.0, 1.0);
            unknown.keys[0].mode = (FollowerSpeedModifier.SpeedKey.Mode)5;
            unknown.keys[0].speed = 3f;
            Assert.That(unknown.GetSpeed(2f, 0.5), Is.EqualTo(6f).Within(1e-5f), "2 * Lerp(1, 3, 1)");
        }

        // Constructor 0x4af530, GetKeys 0x4af5f0 (copies the list into a List<Key>), SetKeys 0x4af760 (a new list with a cast per
        // key), AddKey 0x4af910, Apply 0x4afa00 (empty).
        [Test]
        public void FollowerSpeedModifierKeysRoundTripThroughTheKeyList()
        {
            FollowerSpeedModifier modifier = new FollowerSpeedModifier();
            Assert.That(modifier.keys, Is.Not.Null);
            Assert.That(modifier.keys, Is.Empty);
            Assert.That(modifier.GetKeys(), Is.Empty);

            FollowerSpeedModifier.SpeedKey a = new FollowerSpeedModifier.SpeedKey(0.1, 0.2);
            FollowerSpeedModifier.SpeedKey b = new FollowerSpeedModifier.SpeedKey(0.3, 0.4);
            List<SplineSampleModifier.Key> input = new List<SplineSampleModifier.Key> { a, b };
            List<FollowerSpeedModifier.SpeedKey> before = modifier.keys;
            modifier.SetKeys(input);
            Assert.That(modifier.keys, Is.Not.SameAs(before), "SetKeys allocates a new list");
            Assert.That(modifier.keys, Is.EqualTo(new[] { a, b }));
            List<SplineSampleModifier.Key> listed = modifier.GetKeys();
            Assert.That(listed, Is.Not.SameAs(input));
            Assert.That(listed, Is.EqualTo(input));

            modifier.AddKey(0.5, 0.6);
            Assert.That(modifier.keys.Count, Is.EqualTo(3));
            Assert.That(modifier.keys[2].start, Is.EqualTo(0.5));
            Assert.That(modifier.keys[2].end, Is.EqualTo(0.6));

            SplineSample sample = Sample(0.5);
            modifier.Apply(ref sample);
            Assert.That(sample.size, Is.EqualTo(1f));
            Assert.That(sample.position, Is.EqualTo(Vector3.zero));
            Assert.Throws<InvalidCastException>(() => modifier.SetKeys(new List<SplineSampleModifier.Key> { new MeshScaleModifier.ScaleKey(0.0, 1.0) }));
        }

        // GetScale 0x4b0290: the product of Vector3.Lerp(one, key.scale, weight) over the keys, then Vector3.Lerp(one, product, blend);
        // Vector3.Lerp clamps its parameter, so a weight or blend above 1 saturates.
        [Test]
        public void MeshScaleModifierBlendsScalesMultiplicatively()
        {
            MeshScaleModifier modifier = new MeshScaleModifier();
            Assert.That(modifier.keys, Is.Empty);
            Near(modifier.GetScale(Sample(0.5)), Vector3.one, "no keys");

            modifier.AddKey(0.0, 1.0);
            Near(modifier.keys[0].scale, Vector3.one, "ScaleKey starts at one");
            modifier.keys[0].scale = new Vector3(2f, 4f, 6f);
            Near(modifier.GetScale(Sample(0.5)), new Vector3(2f, 4f, 6f), "full weight");
            Near(modifier.GetScale(Sample(0.125)), new Vector3(1.5f, 2.5f, 3.5f), "weight 0.5");

            modifier.AddKey(0.0, 1.0);
            modifier.keys[1].scale = new Vector3(3f, 3f, 3f);
            Near(modifier.GetScale(Sample(0.5)), new Vector3(6f, 12f, 18f), "component-wise product");
            modifier.blend = 0.5f;
            Near(modifier.GetScale(Sample(0.5)), new Vector3(3.5f, 6.5f, 9.5f), "Lerp(one, product, 0.5)");
            modifier.blend = 3f;
            Near(modifier.GetScale(Sample(0.5)), new Vector3(6f, 12f, 18f), "blend above 1 clamps to 1");

            MeshScaleModifier clamped = new MeshScaleModifier();
            clamped.AddKey(0.0, 1.0);
            clamped.keys[0].scale = new Vector3(2f, 4f, 6f);
            clamped.keys[0].blend = 2f;
            Near(clamped.GetScale(Sample(0.5)), new Vector3(2f, 4f, 6f), "weight 2 clamps to 1 (unclamped would be 3,7,11)");
        }

        // Apply 0x4b0110 adds Evaluate(percent) * key.scale.magnitude * blend to the size, per key, without a base Apply; the key list
        // round trips like the speed modifier's.
        [Test]
        public void MeshScaleModifierApplyAddsTheScaleMagnitudeToTheSize()
        {
            MeshScaleModifier modifier = new MeshScaleModifier();
            modifier.AddKey(0.0, 1.0);
            modifier.keys[0].scale = new Vector3(2f, 3f, 6f);
            modifier.blend = 0.5f;
            SplineSample sample = Sample(0.5);
            modifier.Apply(ref sample);
            Assert.That(sample.size, Is.EqualTo(4.5f).Within(1e-5f), "1 + 1 * 7 * 0.5");

            List<SplineSampleModifier.Key> listed = modifier.GetKeys();
            Assert.That(listed.Count, Is.EqualTo(1));
            MeshScaleModifier replaced = new MeshScaleModifier();
            List<MeshScaleModifier.ScaleKey> before = replaced.keys;
            replaced.SetKeys(listed);
            Assert.That(replaced.keys, Is.Not.SameAs(before));
            Assert.That(replaced.keys[0], Is.SameAs(modifier.keys[0]));
            Assert.Throws<InvalidCastException>(() => replaced.SetKeys(new List<SplineSampleModifier.Key> { new SizeModifier.SizeKey(0.0, 1.0) }));
        }

        // Next 0x18bdf60 (iteration == Ordered): wrap index to 0 when index >= objects.Length, then return objects[index++].
        [Test]
        public void ObjectSequenceOrderedWalksAndWraps()
        {
            ObjectSequence<string> sequence = new ObjectSequence<string>();
            Assert.That(sequence.randomSeed, Is.EqualTo(1), "constructor 0x18bdc10 seeds with 1");
            Assert.That(sequence.iteration, Is.EqualTo(ObjectSequence<string>.Iteration.Ordered));
            sequence.startObject = "start";
            sequence.endObject = "end";
            Assert.That(sequence.GetFirst(), Is.EqualTo("start"));
            Assert.That(sequence.GetLast(), Is.EqualTo("end"));

            sequence.objects = new[] { "a", "b", "c" };
            Assert.That(new[] { sequence.Next(), sequence.Next(), sequence.Next(), sequence.Next(), sequence.Next() }, Is.EqualTo(new[] { "a", "b", "c", "a", "b" }));
            sequence.objects = new[] { "x" };
            Assert.That(sequence.Next(), Is.EqualTo("x"), "index 2 >= 1 resets to 0 before reading");
            Assert.That(sequence.Next(), Is.EqualTo("x"));

            sequence.objects = null;
            Assert.Throws<NullReferenceException>(() => sequence.Next());
        }

        // GetFirst 0x18bdd30 and GetLast 0x18bde40 answer the start or end object when it is not null (0x18bddae and 0x18bdec2
        // test it with would_box_to_non_null) and otherwise take the next object. A value type is never null, so it answers its
        // own default instead.
        [Test]
        public void ObjectSequenceFirstAndLastFallBackToNextForAnUnsetReferenceObject()
        {
            ObjectSequence<string> sequence = new ObjectSequence<string>();
            sequence.objects = new[] { "a", "b", "c" };
            Assert.That(sequence.GetFirst(), Is.EqualTo("a"), "no start object: the first Next");
            Assert.That(sequence.GetLast(), Is.EqualTo("b"), "no end object: the following Next");
            sequence.endObject = "end";
            Assert.That(sequence.GetLast(), Is.EqualTo("end"));
            sequence.startObject = "start";
            Assert.That(sequence.GetFirst(), Is.EqualTo("start"));
            Assert.That(sequence.Next(), Is.EqualTo("c"), "set objects never advance the walk");

            ObjectSequence<int> values = new ObjectSequence<int>();
            values.objects = new[] { 5, 6 };
            Assert.That(values.GetFirst(), Is.EqualTo(0), "a value type is never null");
            Assert.That(values.GetLast(), Is.EqualTo(0));
            Assert.That(values.Next(), Is.EqualTo(5), "and does not advance the walk either");
        }

        // Random mode asks System.Random.Next(objects.Length - 1) (vtable slot 7), so the last element is unreachable and a
        // length-1 or length-2 array always answers element 0. set_randomSeed 0x18bdac0 reseeds only when the value changes.
        [Test]
        public void ObjectSequenceRandomNeverPicksTheLastElementAndReseedsOnlyOnChange()
        {
            ObjectSequence<int> sequence = new ObjectSequence<int>();
            sequence.iteration = ObjectSequence<int>.Iteration.Random;
            sequence.objects = new[] { 7 };
            Assert.That(sequence.Next(), Is.EqualTo(7));
            sequence.objects = new[] { 7, 8 };
            for (int i = 0; i < 20; i++)
            {
                Assert.That(sequence.Next(), Is.EqualTo(7), "Next(1) is always 0");
            }

            sequence.objects = new[] { 10, 11, 12, 13 };
            sequence.randomSeed = 5;
            System.Random reference = new System.Random(5);
            for (int i = 0; i < 10; i++)
            {
                int picked = sequence.Next();
                Assert.That(picked, Is.EqualTo(sequence.objects[reference.Next(3)]));
                Assert.That(picked, Is.Not.EqualTo(13));
            }
            sequence.randomSeed = 5;
            int continued = sequence.Next();
            Assert.That(continued, Is.EqualTo(sequence.objects[reference.Next(3)]), "an unchanged seed keeps the running generator");
            sequence.randomSeed = 9;
            System.Random reseeded = new System.Random(9);
            Assert.That(sequence.Next(), Is.EqualTo(sequence.objects[reseeded.Next(3)]), "a new seed restarts the generator");

            sequence.objects = null;
            Assert.Throws<NullReferenceException>(() => sequence.Next());
        }

        // Field order, serialization attributes and constructor access as shipped (DummyDll metadata).
        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            Assert.That(typeof(SplineSampleModifier).IsSerializable, Is.True);
            Assert.That(typeof(SplineSampleModifier.Key).IsSerializable, Is.True);
            Assert.That(FieldNames(typeof(SplineSampleModifier)), Is.EqualTo(new[] { "blend", "useClippedPercent" }));
            Assert.That(typeof(SplineSampleModifier).GetField("blend").GetCustomAttribute<UnityEngine.RangeAttribute>().max, Is.EqualTo(1f));
            Assert.That(FieldNames(typeof(SplineSampleModifier.Key)), Is.EqualTo(new[] { "_featherStart", "_featherEnd", "_centerStart", "_centerEnd", "interpolation", "blend" }));
            foreach (string name in new[] { "_featherStart", "_featherEnd", "_centerStart", "_centerEnd" })
            {
                FieldInfo field = typeof(SplineSampleModifier.Key).GetField(name, DeclaredMembers);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null, name);
            }
            ConstructorInfo keyConstructor = typeof(SplineSampleModifier.Key).GetConstructor(DeclaredMembers, null, new[] { typeof(double), typeof(double) }, null);
            Assert.That(keyConstructor.IsAssembly, Is.True, "the base Key constructor is internal");

            Assert.That(FieldNames(typeof(ColorModifier)), Is.EqualTo(new[] { "keys" }));
            Assert.That(FieldNames(typeof(ColorModifier.ColorKey)), Is.EqualTo(new[] { "color", "blendMode" }));
            Assert.That(Enum.GetNames(typeof(ColorModifier.ColorKey.BlendMode)), Is.EqualTo(new[] { "Lerp", "Multiply", "Add", "Subtract" }));
            Assert.That(Enum.GetNames(typeof(ObjectSequence<int>.Iteration)), Is.EqualTo(new[] { "Ordered", "Random" }));
            Assert.That(FieldNames(typeof(OffsetModifier.OffsetKey)), Is.EqualTo(new[] { "offset" }));
            Assert.That(FieldNames(typeof(RotationModifier.RotationKey)), Is.EqualTo(new[] { "useLookTarget", "target", "rotation" }));
            Assert.That(FieldNames(typeof(SizeModifier.SizeKey)), Is.EqualTo(new[] { "size" }));
            Assert.That(FieldNames(typeof(FollowerSpeedModifier.SpeedKey)), Is.EqualTo(new[] { "speed", "mode" }));
            Assert.That(Enum.GetNames(typeof(FollowerSpeedModifier.SpeedKey.Mode)), Is.EqualTo(new[] { "Add", "Multiply" }));
            Assert.That(FieldNames(typeof(MeshScaleModifier.ScaleKey)), Is.EqualTo(new[] { "scale" }));
            Assert.That(typeof(OffsetModifier).GetField("keys").FieldType, Is.EqualTo(typeof(OffsetModifier.OffsetKey[])));
            Assert.That(typeof(RotationModifier).GetField("keys").FieldType, Is.EqualTo(typeof(RotationModifier.RotationKey[])));
            Assert.That(typeof(SizeModifier).GetField("keys").FieldType, Is.EqualTo(typeof(SizeModifier.SizeKey[])));
            Assert.That(typeof(FollowerSpeedModifier).GetField("keys").FieldType, Is.EqualTo(typeof(List<FollowerSpeedModifier.SpeedKey>)));
            Assert.That(typeof(MeshScaleModifier).GetField("keys").FieldType, Is.EqualTo(typeof(List<MeshScaleModifier.ScaleKey>)));

            Assert.That(FieldNames(typeof(ObjectSequence<int>)), Is.EqualTo(new[] { "startObject", "endObject", "objects", "iteration", "_randomSeed", "index", "randomizer" }));
            foreach (string name in new[] { "_randomSeed", "index", "randomizer" })
            {
                FieldInfo field = typeof(ObjectSequence<int>).GetField(name, DeclaredMembers);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null, name);
                Assert.That(field.GetCustomAttribute<HideInInspector>(), Is.Not.Null, name);
            }
        }

        private static string[] FieldNames(Type type)
        {
            return type.GetFields(DeclaredMembers).OrderBy(field => field.MetadataToken).Select(field => field.Name).ToArray();
        }
    }
}
