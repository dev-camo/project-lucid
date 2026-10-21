using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.TubeGenerator setters on the Linear line (0,0,0) -> (10,0,0). The shipped build queues the rebuild of a
    // changed setter for the next Update (SplineUser.Rebuild sets rebuild and getSamples, RunUpdate builds and then raises
    // onPostBuild). The editor compile rebuilds at once while Application.isPlaying is false, so these tests run in play mode,
    // where the engine caches the play state itself. Expected values are the native bodies cited per test.
    public sealed class OriginalDreamteckTubeGeneratorPlayModeTests
    {
        private readonly DreamteckGeneratorFixture _fixture = new DreamteckGeneratorFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        // A tube on the computer with the given number of sides, built once.
        private TubeGenerator NewTube(int sides = 4)
        {
            TubeGenerator tube = _fixture.NewGenerator<TubeGenerator>(_fixture.NewLine());
            tube.sides = sides;
            tube.RebuildImmediate();
            return tube;
        }

        private static int[] CountBuilds(TubeGenerator tube)
        {
            int[] counter = new int[1];
            tube.onPostBuild += delegate { counter[0]++; };
            DreamteckGeneratorFixture.Update(tube);
            return counter;
        }

        // set_sides 0x4d6770 compares the raw value with the stored one, then stores max(value, 3) and queues a rebuild even when
        // the raised value equals the stored one (cmp 4 / cmovge).
        [Test]
        public void SidesClampAtThreeAndRebuildOnAnyDifferentRawValue()
        {
            TubeGenerator tube = NewTube(12);
            int[] builds = CountBuilds(tube);

            tube.sides = 12;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(0), "equal value");
            tube.sides = 2;
            Assert.That(tube.sides, Is.EqualTo(3), "raised to three");
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(1));
            tube.sides = 2;
            Assert.That(tube.sides, Is.EqualTo(3));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2), "2 differs from the stored 3, so it still rebuilds");
            tube.sides = 40;
            Assert.That(tube.sides, Is.EqualTo(40));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(3));
        }

        // set_roundCapLatitude 0x4d6800 raises the count to one (cmp 2 / cmovge), compares the raised value, and rebuilds only
        // for the round cap mode.
        [Test]
        public void RoundCapLatitudeClampsAtOneAndOnlyRoundCapsRebuild()
        {
            TubeGenerator tube = NewTube(4);
            int[] builds = CountBuilds(tube);

            tube.roundCapLatitude = 3;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(3));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(0), "no round cap: stored without a rebuild");
            tube.roundCapLatitude = -5;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(1), "raised to one");
            tube.roundCapLatitude = 0;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(1));

            tube.capMode = TubeGenerator.CapMethod.Round;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(1));
            tube.roundCapLatitude = 4;
            Assert.That(tube.roundCapLatitude, Is.EqualTo(4));
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2), "round caps depend on the latitude");
            tube.roundCapLatitude = 4;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2), "equal value");
        }

        // set_capMode 0x4d67c0, set_revolve 0x4d6850, set_capUVScale 0x4d68a0 and set_uvTwist 0x4d68f0 are plain
        // change-and-rebuild setters.
        [Test]
        public void PlainSettersRebuildOnChangeOnly()
        {
            TubeGenerator tube = NewTube(4);
            int[] builds = CountBuilds(tube);

            tube.capMode = TubeGenerator.CapMethod.None;
            tube.revolve = 360f;
            tube.capUVScale = 1f;
            tube.uvTwist = 0f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(0), "equal values");

            tube.capMode = TubeGenerator.CapMethod.Flat;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(1));
            tube.revolve = 180f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(2));
            tube.capUVScale = 2f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(3));
            tube.uvTwist = 0.5f;
            DreamteckGeneratorFixture.Update(tube);
            Assert.That(builds[0], Is.EqualTo(4));
            Assert.That(tube.capMode, Is.EqualTo(TubeGenerator.CapMethod.Flat));
            Assert.That(tube.revolve, Is.EqualTo(180f));
            Assert.That(tube.capUVScale, Is.EqualTo(2f));
            Assert.That(tube.uvTwist, Is.EqualTo(0.5f));
        }
    }
}
