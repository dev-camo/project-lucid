using System;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SurfaceGenerator setters on a closed Linear square in the XY plane, corners (0,0,0), (10,0,0), (10,10,0),
    // (0,10,0). The shipped build queues the rebuild of a changed setter for the next Update (SplineUser.Rebuild sets rebuild and
    // getSamples, RunUpdate builds and then raises onPostBuild). The editor compile rebuilds at once while Application.isPlaying
    // is false, so these tests run in play mode, where the engine caches the play state itself.
    public sealed class OriginalDreamteckSurfaceGeneratorPlayModeTests
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

        private static readonly Vector3[] Corners =
        {
            new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 10f, 0f), new Vector3(0f, 10f, 0f)
        };

        // keep in step with OriginalDreamteckSurfaceGeneratorTests.NewSquare
        private SplineComputer NewSquare()
        {
            SplinePoint[] points = new SplinePoint[4];
            for (int i = 0; i < 4; i++)
            {
                points[i] = new SplinePoint(Corners[i], Corners[i], new Vector3(0f, 0f, 1f), 1f, Color.white);
            }

            SplineComputer square = _fixture.NewComputer(points);
            square.linearAverageDirection = false;
            square.Close();
            square.RebuildImmediate();
            return square;
        }

        // A straight Linear spline along +z from the origin: two samples, forward (0,0,1), up (0,1,0), right (1,0,0).
        private SplineComputer NewSweep()
        {
            return _fixture.NewComputer(new SplinePoint(Vector3.zero), new SplinePoint(new Vector3(0f, 0f, 10f)));
        }

        // A surface built once with the final settings, so that no earlier build leaves old values in the arrays.
        private SurfaceGenerator NewSurface(SplineComputer computer, Action<SurfaceGenerator> configure = null, Vector3? firstForward = null)
        {
            SurfaceGenerator surface = _fixture.NewBare<SurfaceGenerator>();
            surface.calculateTangents = false;
            if (configure != null)
            {
                configure(surface);
            }

            surface.spline = computer;
            surface.RebuildImmediate();
            Assert.That(surface.sampleCount, Is.EqualTo(5), "the closed square repeats its first sample");
            SplineSample first = new SplineSample();
            surface.GetSample(0, ref first);
            Near(first.forward, firstForward ?? Vector3.right, "first edge direction");
            Near(first.up, new Vector3(0f, 0f, 1f), "point normal");
            return surface;
        }

        private static int[] CountBuilds(SurfaceGenerator surface)
        {
            int[] counter = new int[1];
            surface.onPostBuild += delegate { counter[0]++; };
            DreamteckGeneratorFixture.Update(surface);
            return counter;
        }

        // set_expand 0x4d2b20, set_extrude 0x4d2b70, set_extrudeClipFrom 0x4d2bc0, set_extrudeClipTo 0x4d2c10,
        // set_extrudeOffset 0x4d2eb0 and set_uniformUvs 0x4d2f40 change and rebuild; an equal value does nothing.
        [Test]
        public void PlainSettersRebuildOnChangeOnly()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            int[] builds = CountBuilds(surface);

            surface.expand = 0f;
            surface.extrude = 0f;
            surface.extrudeClipFrom = 0.0;
            surface.extrudeClipTo = 1.0;
            surface.extrudeOffset = Vector3.zero;
            surface.uniformUvs = false;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(0), "equal values");

            surface.expand = 1f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));
            surface.extrude = 2f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(2));
            surface.extrudeClipFrom = 0.25;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3));
            surface.extrudeClipTo = 0.75;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(4));
            surface.extrudeOffset = new Vector3(1f, 2f, 3f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(5));
            surface.extrudeOffset = new Vector3(1f + 1e-6f, 2f, 3f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(5), "the Vector3 equality threshold ignores 1e-6");
            surface.uniformUvs = true;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(6));
            Assert.That(surface.expand, Is.EqualTo(1f));
            Assert.That(surface.extrude, Is.EqualTo(2f));
            Assert.That(surface.extrudeClipFrom, Is.EqualTo(0.25));
            Assert.That(surface.extrudeClipTo, Is.EqualTo(0.75));
            Assert.That(surface.extrudeOffset, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(surface.uniformUvs, Is.True);
        }

        // set_sideUvScale 0x4d2c60, set_sideUvOffset 0x4d2cc0 and set_sideUvRotation 0x4d2d20 store the value before they compare
        // it with the old one (the store at 0x4d2c6c / 0x4d2ccc / 0x4d2d2f precedes the test), so a difference below the Vector2
        // equality threshold is kept without a rebuild.
        [Test]
        public void SideUvSettersStoreTheValueBeforeTheyCompare()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            int[] builds = CountBuilds(surface);

            surface.sideUvScale = Vector2.one;
            surface.sideUvOffset = Vector2.zero;
            surface.sideUvRotation = 0f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(0), "equal values");

            surface.sideUvScale = new Vector2(1f + 1e-6f, 1f);
            Assert.That(surface.sideUvScale.x, Is.EqualTo(1f + 1e-6f), "stored although equal for the operator");
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(0), "but no rebuild");
            surface.sideUvScale = new Vector2(2f, 3f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));

            surface.sideUvOffset = new Vector2(1e-6f, 0f);
            Assert.That(surface.sideUvOffset.x, Is.EqualTo(1e-6f));
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));
            surface.sideUvOffset = new Vector2(0.5f, 0.25f);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(2));

            surface.sideUvRotation = 45f;
            Assert.That(surface.sideUvRotation, Is.EqualTo(45f));
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3));
            surface.sideUvRotation = 45f;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3), "equal value");
        }

        // set_extrudeSpline 0x4d2d70: a different computer is unsubscribed from the old one, subscribed to the new one (unless
        // null) and a rebuild is queued; the same computer is ignored.
        [Test]
        public void ExtrudeSplineFollowsTheComputerItIsGiven()
        {
            SurfaceGenerator surface = NewSurface(NewSquare());
            SplineComputer first = NewSweep();
            SplineComputer second = NewSweep();
            int[] builds = CountBuilds(surface);

            surface.extrudeSpline = first;
            Assert.That(surface.extrudeSpline == first, Is.True);
            Assert.That(first.IsSubscribed(surface), Is.True);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1));

            surface.extrudeSpline = first;
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(1), "same computer");

            surface.extrudeSpline = second;
            Assert.That(first.IsSubscribed(surface), Is.False, "unsubscribed from the old one");
            Assert.That(second.IsSubscribed(surface), Is.True);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(2));

            surface.extrudeSpline = null;
            Assert.That(second.IsSubscribed(surface), Is.False);
            Assert.That(surface.extrudeSpline == null, Is.True);
            DreamteckGeneratorFixture.Update(surface);
            Assert.That(builds[0], Is.EqualTo(3));
        }
    }
}
