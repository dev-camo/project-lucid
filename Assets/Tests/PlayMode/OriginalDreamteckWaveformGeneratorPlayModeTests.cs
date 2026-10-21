using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.WaveformGenerator setters on the Linear line (0,0,0) -> (10,0,0) lifted to y = 2. The shipped build
    // queues the rebuild of a changed setter for the next Update (SplineUser.Rebuild sets rebuild and getSamples, RunUpdate builds
    // and then raises onPostBuild). The editor compile rebuilds at once while Application.isPlaying is false, so this runs in play
    // mode, where the engine caches the play state itself.
    public sealed class OriginalDreamteckWaveformGeneratorPlayModeTests
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

        private WaveformGenerator NewWaveform(float height = 2f)
        {
            SplineComputer line = _fixture.NewComputer(new SplinePoint(new Vector3(0f, height, 0f)), new SplinePoint(new Vector3(10f, height, 0f)));
            WaveformGenerator waveform = _fixture.NewGenerator<WaveformGenerator>(line);
            SplineSample first = new SplineSample();
            waveform.GetSample(0, ref first);
            Near(first.forward, Vector3.right, "line forward");
            Near(first.up, Vector3.up, "line up");
            Assert.That(waveform.sampleCount, Is.EqualTo(2));
            return waveform;
        }

        // set_axis 0x4d9580, set_symmetry 0x4d95c0 and set_uvWrapMode 0x4d9600 change and rebuild; set_slices 0x4d9640 raises the
        // count to one (cmp 2 / cmovge) and rebuilds for any value that differs from the stored one.
        [Test]
        public void SettersRebuildOnChangeAndSlicesClampAtOne()
        {
            WaveformGenerator waveform = NewWaveform();
            int builds = 0;
            waveform.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(waveform);

            waveform.axis = WaveformGenerator.Axis.Y;
            waveform.symmetry = false;
            waveform.uvWrapMode = WaveformGenerator.UVWrapMode.Clamp;
            waveform.slices = 1;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(0), "equal values");

            waveform.axis = WaveformGenerator.Axis.Z;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(1));
            waveform.symmetry = true;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(2));
            waveform.uvWrapMode = WaveformGenerator.UVWrapMode.Uniform;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(3));
            waveform.slices = 0;
            Assert.That(waveform.slices, Is.EqualTo(1), "clamped");
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(4), "0 differs from the stored 1, so it still rebuilds");
            waveform.slices = -3;
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(5));
            waveform.slices = 4;
            Assert.That(waveform.slices, Is.EqualTo(4));
            DreamteckGeneratorFixture.Update(waveform);
            Assert.That(builds, Is.EqualTo(6));
            Assert.That(waveform.axis, Is.EqualTo(WaveformGenerator.Axis.Z));
            Assert.That(waveform.symmetry, Is.True);
            Assert.That(waveform.uvWrapMode, Is.EqualTo(WaveformGenerator.UVWrapMode.Uniform));
        }
    }
}
