using System;
using System.Linq;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.PathGenerator setters in play mode, where SplineUser caches _isPlaying from Application.isPlaying in
    // Awake and so queues the rebuild a setter asks for (the shipped player text) instead of the editor's immediate rebuild;
    // Update runs the queued build. The EditMode class OriginalDreamteckPathGeneratorTests covers the rest of the generator.
    // Linear line (0,0,0) -> (10,0,0); expected values are from the native bodies cited per test (0x4ac... / 0x4ad...).
    public sealed class OriginalDreamteckPathGeneratorPlayModeTests
    {
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

        // A path generator on the line, built once, with the line frame verified so the arithmetic below holds.
        private PathGenerator NewPath(SplineComputer computer = null)
        {
            computer = computer ?? _fixture.NewLine();
            PathGenerator path = _fixture.NewGenerator<PathGenerator>(computer);
            SplineSample first = new SplineSample();
            path.GetSample(0, ref first);
            Near(first.forward, Vector3.right, "line forward");
            Near(first.up, Vector3.up, "line up");
            Assert.That(path.sampleCount, Is.EqualTo(computer.pointCount));
            return path;
        }

        // set_slices 0x4acb50: a value equal to the stored one is ignored; otherwise the count is raised to at least 1
        // (cmp 2 / cmovge) and a rebuild is queued, even when the clamped count equals the stored one.
        [Test]
        public void SlicesClampAtOneAndRebuildOnAnyDifferentValue()
        {
            PathGenerator path = NewPath();
            int builds = 0;
            path.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(path);

            path.slices = 1;
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(0), "equal value");
            path.slices = 0;
            Assert.That(path.slices, Is.EqualTo(1), "clamped");
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1), "0 differs from the stored 1, so it still rebuilds");
            path.slices = -7;
            Assert.That(path.slices, Is.EqualTo(1));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(2));
            path.slices = 4;
            Assert.That(path.slices, Is.EqualTo(4));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(3));
            path.slices = 2;
            Assert.That(path.slices, Is.EqualTo(2));
        }

        // set_compensateCorners 0x4acd00 is a plain change-and-rebuild setter.
        [Test]
        public void CompensateCornersRebuildsOnChange()
        {
            PathGenerator path = NewPath();
            int builds = 0;
            path.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(path);
            path.compensateCorners = false;
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(0));
            path.compensateCorners = true;
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1));
            Assert.That(path.compensateCorners, Is.True);
        }

        // set_useShapeCurve 0x4acba0: on creates a curve with the keys (0,0) and (1,0), off drops it; both rebuild.
        [Test]
        public void ShapeCurveToggleCreatesAFlatCurveOrDropsIt()
        {
            PathGenerator path = NewPath();
            int builds = 0;
            path.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(path);

            path.useShapeCurve = false;
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(0));

            path.useShapeCurve = true;
            Assert.That(path.shape, Is.Not.Null);
            Assert.That(path.shape.keys.Length, Is.EqualTo(2));
            Assert.That(path.shape.keys[0].time, Is.EqualTo(0f));
            Assert.That(path.shape.keys[0].value, Is.EqualTo(0f));
            Assert.That(path.shape.keys[1].time, Is.EqualTo(1f));
            Assert.That(path.shape.keys[1].value, Is.EqualTo(0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1));

            AnimationCurve first = path.shape;
            path.useShapeCurve = true;
            Assert.That(path.shape, Is.SameAs(first), "an equal flag keeps the curve");

            path.useShapeCurve = false;
            Assert.That(path.shape, Is.Null);
            Assert.That(path.useShapeCurve, Is.False);
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(2));
        }

        // set_shapeExposure 0x4acd40 does nothing without a spline (the value is not stored either), nothing for an equal value,
        // and rebuilds for a different one.
        [Test]
        public void ShapeExposureNeedsASplineAndADifferentValue()
        {
            PathGenerator loose = _fixture.NewBare<PathGenerator>();
            loose.shapeExposure = 3f;
            Assert.That(loose.shapeExposure, Is.EqualTo(1f), "without a spline the value is dropped");

            PathGenerator path = NewPath();
            int builds = 0;
            path.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(path);
            path.shapeExposure = 1f;
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(0));
            path.shapeExposure = 3f;
            Assert.That(path.shapeExposure, Is.EqualTo(3f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1));
        }

        private static AnimationCurve Curve(params Keyframe[] keys)
        {
            return new AnimationCurve(keys);
        }

        // set_shape 0x4acdf0: the new curve is compared with the stored copy key by key (in tangent, out tangent, time, value)
        // and a rebuild is queued for a different key count or at the first difference. The copy is then replaced by blank keys:
        // the CopyTo fills a temporary array of Unity's keys getter, as shipped. The wrap modes are copied.
        [Test]
        public void ShapeSetterComparesKeysWithTheBlankCopy()
        {
            PathGenerator path = NewPath();
            int builds = 0;
            path.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(path);

            AnimationCurve zeroKey = Curve(new Keyframe(0f, 0f));
            path.shape = zeroKey;
            Assert.That(path.shape, Is.SameAs(zeroKey));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1), "one key against the empty copy: different count");

            AnimationCurve lastShape = DreamteckGeneratorFixture.Field<AnimationCurve>(path, "_lastShape");
            Assert.That(lastShape, Is.Not.Null);
            Assert.That(lastShape.keys.Length, Is.EqualTo(1), "the copy has as many keys");
            Assert.That(lastShape.keys[0].time == 0f && lastShape.keys[0].value == 0f && lastShape.keys[0].inTangent == 0f && lastShape.keys[0].outTangent == 0f, Is.True);

            path.shape = Curve(new Keyframe(0f, 0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1), "an all-zero key equals the blank copy: no rebuild");

            path.shape = Curve(new Keyframe(0f, 1f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1), "the value is compared with itself, so a value-only change queues nothing");
            path.shape = Curve(new Keyframe(0f, 0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(1), "the copy is blank again, so back to zero is equal");

            // Unity's AnimationCurve drops a key whose value is NaN, so such a curve has no keys and the compare sees a different
            // key count (the value-against-itself NaN test of 0x4ad06b cannot be reached through the engine's curve).
            AnimationCurve nanCurve = Curve(new Keyframe(0f, float.NaN));
            Assert.That(nanCurve.keys.Length, Is.EqualTo(0), "the engine kept no NaN key");
            path.shape = nanCurve;
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(2), "no keys against the one-key copy: different count");
            path.shape = Curve(new Keyframe(0f, 0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(3), "one key against the keyless copy: different count");

            path.shape = Curve(new Keyframe(2f, 0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(4), "time differs");
            path.shape = Curve(new Keyframe(0f, 0f, 1f, 0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(5), "in tangent differs");
            path.shape = Curve(new Keyframe(0f, 0f, 0f, 1f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(6), "out tangent differs");
            path.shape = Curve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            DreamteckGeneratorFixture.Update(path);
            Assert.That(builds, Is.EqualTo(7), "key count differs");

            AnimationCurve wrapped = Curve(new Keyframe(0f, 0f));
            wrapped.preWrapMode = WrapMode.Loop;
            wrapped.postWrapMode = WrapMode.PingPong;
            path.shape = wrapped;
            Assert.That(lastShape.preWrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(lastShape.postWrapMode, Is.EqualTo(WrapMode.PingPong));
            Assert.Throws<NullReferenceException>(() => path.shape = null);
        }
    }
}
