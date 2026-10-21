using System;
using System.Collections.Generic;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // The shipped player branch of Dreamteck.Splines.SplineMesh, run in play mode: Application.isPlaying is true, so SplineUser
    // caches _isPlaying in Awake and Rebuild only queues (rebuild = getSamples = true) until Update runs the queued build. The
    // EditMode compile of the same text builds at once instead. Scenes and expectations are those of OriginalDreamteckSplineMeshTests
    // (Linear line (0,0,0) -> (10,0,0), tetrahedron channel). No frame passes inside these synchronous tests, so only the explicit
    // Update calls of the build counter run the queue. TearDown destroys every game object and mesh a test made.
    public sealed class OriginalDreamteckSplineMeshPlayModeTests
    {
        private readonly SplineMeshFixture _fixture = new SplineMeshFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private static Vector3[] Vertices(SplineMesh generator)
        {
            return SplineMeshFixture.Ts(generator).vertices;
        }

        // AddChannel(string) 0x4bc290 only creates and stores the channel; AddChannel(Mesh, string) 0x4bc500 builds it with the mesh
        // as its first mesh, and that constructor (0x4bc5d0) asks the owner for a rebuild.
        [Test]
        public void AddChannelCreatesTheChannelAndOnlyTheMeshOverloadRebuilds()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            SplineMesh.Channel plain = generator.AddChannel("plain");
            Assert.That(plain.name, Is.EqualTo("plain"));
            Assert.That(plain.GetMeshCount(), Is.EqualTo(0));
            Assert.That(generator.GetChannelCount(), Is.EqualTo(1));
            Assert.That(generator.GetChannel(0), Is.SameAs(plain));
            Assert.That(DreamteckGeneratorFixture.Field<SplineMesh>(plain, "owner") == generator, Is.True);
            Assert.That(builds.Update(), Is.EqualTo(0), "no rebuild for a channel without mesh");

            Mesh tetra = _fixture.Tetra();
            SplineMesh.Channel withMesh = generator.AddChannel(tetra, "withMesh");
            Assert.That(withMesh.name, Is.EqualTo("withMesh"));
            Assert.That(withMesh.GetMeshCount(), Is.EqualTo(1));
            Assert.That(withMesh.GetMesh(0).mesh, Is.SameAs(tetra));
            Assert.That(generator.GetChannelCount(), Is.EqualTo(2));
            Assert.That(generator.GetChannel(1), Is.SameAs(withMesh));
            Assert.That(builds.Update(), Is.EqualTo(1), "one rebuild for the mesh channel");

            Assert.That(() => generator.GetChannel(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => generator.GetChannel(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // RemoveChannel 0x4bc360 removes by index and rebuilds; a bad index throws before the rebuild.
        [Test]
        public void RemoveChannelRebuildsAndRejectsABadIndex()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel first = generator.AddChannel("first");
            SplineMesh.Channel second = generator.AddChannel("second");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            Assert.That(() => generator.RemoveChannel(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => generator.RemoveChannel(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(generator.GetChannelCount(), Is.EqualTo(2));
            Assert.That(builds.Update(), Is.EqualTo(0), "a bad index queues nothing");

            generator.RemoveChannel(0);
            Assert.That(generator.GetChannelCount(), Is.EqualTo(1));
            Assert.That(generator.GetChannel(0), Is.SameAs(second));
            Assert.That(builds.Update(), Is.EqualTo(1));
            Assert.That(first.name, Is.EqualTo("first"));
        }

        // SwapChannels 0x4bc3d0 changes nothing for an index outside the list, otherwise exchanges the two channels and rebuilds
        // (also for equal indices).
        [Test]
        public void SwapChannelsExchangesTheChannelsAndIgnoresBadIndices()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel a = generator.AddChannel("a");
            SplineMesh.Channel b = generator.AddChannel("b");
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            foreach (int[] bad in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 3, 0 }, new[] { 0, 3 }, new[] { 5, 7 } })
            {
                generator.SwapChannels(bad[0], bad[1]);
                Assert.That(builds.Update(), Is.EqualTo(0), "(" + bad[0] + ", " + bad[1] + ") queues nothing");
                Assert.That(generator.GetChannel(0), Is.SameAs(a));
                Assert.That(generator.GetChannel(1), Is.SameAs(b));
                Assert.That(generator.GetChannel(2), Is.SameAs(c));
            }

            generator.SwapChannels(0, 2);
            Assert.That(builds.Update(), Is.EqualTo(1));
            Assert.That(generator.GetChannel(0), Is.SameAs(c));
            Assert.That(generator.GetChannel(1), Is.SameAs(b));
            Assert.That(generator.GetChannel(2), Is.SameAs(a));

            generator.SwapChannels(1, 1);
            Assert.That(builds.Update(), Is.EqualTo(1), "equal indices still rebuild");
            Assert.That(generator.GetChannel(1), Is.SameAs(b));
        }

        // The average is not taken for a mesh whose bounds are flat along z (a front spacing of -1 cancels the depth), so the count stays.
        // The flat bounds also make MeshDefinition.Refresh log its small-bounds warning (asserted in the EditMode MeshDefinition
        // tests); in play mode the original HLOutput.OnRuntimeMethodLoad has set Debug.unityLogger.filterLogType to Error, so the
        // warning never reaches the log here (native run63d).
        [Test]
        public void AutoCountKeepsTheCountForAFlatAverage()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            channel.autoCount = true;
            channel.GetMesh(0).spacing = new SplineMesh.Channel.BoundsSpacing { front = -1f, back = 0f };
            Assert.That(channel.GetMesh(0).bounds.size.z, Is.EqualTo(0f).Within(1e-6f));
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(3));
            Assert.That(Vertices(generator).Length, Is.EqualTo(12));
        }

        // Native 0x4bd236 is `ucomiss average, 0.0; jbe skip`: the count is only fitted for an average greater than zero, so a negative
        // depth (front spacing -2 turns the bounds into z 0..-1) keeps the count as well (the small-bounds warning is filtered as above).
        [Test]
        public void AutoCountKeepsTheCountForANegativeAverage()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel channel = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 3);
            channel.autoCount = true;
            channel.GetMesh(0).spacing = new SplineMesh.Channel.BoundsSpacing { front = -2f, back = 0f };
            Assert.That(channel.GetMesh(0).bounds.size.z, Is.EqualTo(-1f).Within(1e-6f));
            generator.RebuildImmediate();
            Assert.That(channel.count, Is.EqualTo(3));
            Assert.That(Vertices(generator).Length, Is.EqualTo(12));
        }

        // Generate 0x4bcee0 moves the combine index only inside the Extrude and Place loops (0x4bd587 reached from the loop exits
        // 0x4bd749 and 0x4bdb60); the default path 0x4bd580 reloads the old index. A channel with an unknown serialized type
        // therefore leaves its slots unwritten and the next channel takes the slot right after the previous copy. The unwritten
        // slots stay null, which the final Combine then trips over; RebuildImmediate (0x4cef50) catches that exception and logs
        // its message instead of throwing.
        [Test]
        public void UnknownChannelTypeDoesNotAdvanceTheCombineIndex()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            SplineMesh.Channel unknown = _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 2);
            _fixture.AddTetra(generator, SplineMesh.Channel.Type.Place, 1);
            typeof(SplineMesh.Channel).GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(unknown, (SplineMesh.Channel.Type)7);

            LogAssert.Expect(LogType.Error, "Object reference not set to an instance of an object");
            generator.RebuildImmediate();

            List<TS_Mesh> combine = DreamteckGeneratorFixture.Field<List<TS_Mesh>>(generator, "_combineMeshes");
            Assert.That(combine.Count, Is.EqualTo(4));
            Assert.That(combine[0], Is.Not.Null, "first channel");
            Assert.That(combine[1], Is.Not.Null, "third channel takes the next slot");
            Assert.That(combine[2], Is.Null);
            Assert.That(combine[3], Is.Null);
        }
    }
}
