using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineRenderer setter in play mode, where SplineUser caches _isPlaying from Application.isPlaying in Awake
    // and so queues the rebuild (the shipped player text) instead of the editor's immediate rebuild; Update runs the queued
    // build. The EditMode class OriginalDreamteckSplineRendererTests covers the rest of the renderer. Native 0x4cc... range.
    public sealed class OriginalDreamteckSplineRendererPlayModeTests
    {
        private readonly DreamteckGeneratorFixture _fixture = new DreamteckGeneratorFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private SplineRenderer NewRenderer(SplineComputer computer = null)
        {
            return _fixture.NewGenerator<SplineRenderer>(computer ?? _fixture.NewLine());
        }

        // set_slices 0x4cc600 is the same clamp-and-rebuild setter as the path generator.
        [Test]
        public void SlicesClampAtOneAndRebuildOnAnyDifferentValue()
        {
            SplineRenderer renderer = NewRenderer();
            int builds = 0;
            renderer.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(renderer);
            renderer.slices = 1;
            DreamteckGeneratorFixture.Update(renderer);
            Assert.That(builds, Is.EqualTo(0));
            renderer.slices = -3;
            Assert.That(renderer.slices, Is.EqualTo(1));
            DreamteckGeneratorFixture.Update(renderer);
            Assert.That(builds, Is.EqualTo(1));
            renderer.slices = 3;
            Assert.That(renderer.slices, Is.EqualTo(3));
        }
    }
}
