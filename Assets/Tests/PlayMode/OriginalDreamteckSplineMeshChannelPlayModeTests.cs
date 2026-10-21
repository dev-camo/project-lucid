using System;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branch of Dreamteck.Splines.SplineMesh.Channel, run in play mode: the setters rebuild the owner through
    // SplineUser.Rebuild, which only queues the build in a player (the EditMode compile builds at once), so the builds a SplineMesh
    // runs when it is updated count the queued rebuilds. No frame passes inside these synchronous tests. TearDown destroys every
    // game object and mesh a test made.
    public sealed class OriginalDreamteckSplineMeshChannelPlayModeTests
    {
        private readonly SplineMeshFixture _fixture = new SplineMeshFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private SplineMesh.Channel.MeshDefinition NewDefinition()
        {
            return new SplineMesh.Channel.MeshDefinition(_fixture.Tetra());
        }

        // Every setter skips a value equal to the stored one (the vector comparisons are the Unity operators, so a 1e-6 change is
        // equal; delegates compare by reference) and otherwise rebuilds the owner through Rebuild 0x4c0bd0.
        [Test]
        public void SettersSkipEqualValuesAndRebuildTheOwner()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            void Check(string name, Action equal, Action changed)
            {
                equal();
                Assert.That(builds.Update(), Is.EqualTo(0), name + ": an equal value queues nothing");
                changed();
                Assert.That(builds.Update(), Is.EqualTo(1), name + ": a new value queues one build");
            }

            SplineMesh.Channel.Vector2Handler offsetA = percent => Vector2.zero;
            SplineMesh.Channel.Vector2Handler offsetB = percent => Vector2.one;
            SplineMesh.Channel.QuaternionHandler placeA = percent => Quaternion.identity;
            SplineMesh.Channel.FloatHandler extrudeA = percent => 0f;
            SplineMesh.Channel.Vector3Handler scaleA = percent => Vector3.one;

            Check("clipFrom", () => c.clipFrom = 0.0, () => c.clipFrom = 0.25);
            Check("clipTo", () => c.clipTo = 1.0, () => c.clipTo = 0.75);
            Check("randomOffset", () => c.randomOffset = false, () => c.randomOffset = true);
            Check("offsetHandler", () => c.offsetHandler = null, () => c.offsetHandler = offsetA);
            Check("offsetHandler same", () => c.offsetHandler = offsetA, () => c.offsetHandler = offsetB);
            Check("overrideMaterialID", () => c.overrideMaterialID = false, () => c.overrideMaterialID = true);
            Check("targetMaterialID", () => c.targetMaterialID = 0, () => c.targetMaterialID = 3);
            Check("randomRotation", () => c.randomRotation = false, () => c.randomRotation = true);
            Check("placeRotationHandler", () => c.placeRotationHandler = null, () => c.placeRotationHandler = placeA);
            Check("extrudeRotationHandler", () => c.extrudeRotationHandler = null, () => c.extrudeRotationHandler = extrudeA);
            Check("randomScale", () => c.randomScale = false, () => c.randomScale = true);
            Check("scaleHandler", () => c.scaleHandler = null, () => c.scaleHandler = scaleA);
            Check("uniformRandomScale", () => c.uniformRandomScale = false, () => c.uniformRandomScale = true);
            Check("offsetSeed", () => c.offsetSeed = 0, () => c.offsetSeed = 5);
            Check("rotationSeed", () => c.rotationSeed = 0, () => c.rotationSeed = 6);
            Check("scaleSeed", () => c.scaleSeed = 0, () => c.scaleSeed = 7);
            Check("spacing", () => c.spacing = 0.0, () => c.spacing = 0.1);
            Check("minOffset", () => c.minOffset = new Vector2(1e-6f, 0f), () => c.minOffset = new Vector2(1f, 2f));
            Check("maxOffset", () => c.maxOffset = new Vector2(1e-6f, 0f), () => c.maxOffset = new Vector2(3f, 4f));
            Check("minRotation", () => c.minRotation = new Vector3(1e-6f, 0f, 0f), () => c.minRotation = new Vector3(1f, 2f, 3f));
            Check("maxRotation", () => c.maxRotation = new Vector3(1e-6f, 0f, 0f), () => c.maxRotation = new Vector3(4f, 5f, 6f));
            Check("minScale", () => c.minScale = new Vector3(1f + 1e-6f, 1f, 1f), () => c.minScale = new Vector3(2f, 2f, 2f));
            Check("maxScale", () => c.maxScale = new Vector3(1f + 1e-6f, 1f, 1f), () => c.maxScale = new Vector3(3f, 3f, 3f));
            Check("type", () => c.type = SplineMesh.Channel.Type.Extrude, () => c.type = SplineMesh.Channel.Type.Place);
            Check("randomOrder", () => c.randomOrder = false, () => c.randomOrder = true);
            Check("count", () => c.count = 1, () => c.count = 4);
            Check("autoCount", () => c.autoCount = false, () => c.autoCount = true);
            Check("overrideUVs", () => c.overrideUVs = SplineMesh.Channel.UVOverride.None, () => c.overrideUVs = SplineMesh.Channel.UVOverride.ClampU);
            Check("uvOffset", () => c.uvOffset = new Vector2(1e-6f, 0f), () => c.uvOffset = new Vector2(0.5f, 0.5f));
            Check("uvScale", () => c.uvScale = new Vector2(1f + 1e-6f, 1f), () => c.uvScale = new Vector2(2f, 2f));
            Check("overrideNormal", () => c.overrideNormal = false, () => c.overrideNormal = true);
            Check("customNormal", () => c.customNormal = new Vector3(1e-6f, 1f, 0f), () => c.customNormal = new Vector3(0f, 0f, 1f));

            Assert.That(c.clipFrom, Is.EqualTo(0.25));
            Assert.That(c.clipTo, Is.EqualTo(0.75));
            Assert.That(c.offsetHandler, Is.SameAs(offsetB));
            Assert.That(c.targetMaterialID, Is.EqualTo(3));
            Assert.That(c.minOffset, Is.EqualTo(new Vector2(1f, 2f)), "the stored value is the new one, not the equal one");
            Assert.That(c.maxScale, Is.EqualTo(new Vector3(3f, 3f, 3f)));
            Assert.That(c.type, Is.EqualTo(SplineMesh.Channel.Type.Place));
            Assert.That(c.count, Is.EqualTo(4));
            Assert.That(c.overrideUVs, Is.EqualTo(SplineMesh.Channel.UVOverride.ClampU));
            Assert.That(c.customNormal, Is.EqualTo(new Vector3(0f, 0f, 1f)));
        }

        // set_count 0x4bdc50 stores at least one but rebuilds whenever the value differs from the stored one; set_randomSeed 0x4c1ce0 stores
        // always and rebuilds only with a random order.
        [Test]
        public void CountIsRaisedToOneAndTheSeedRebuildsOnlyWithRandomOrder()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            c.count = 0;
            Assert.That(c.count, Is.EqualTo(1));
            Assert.That(builds.Update(), Is.EqualTo(1), "0 differs from the stored 1, so it rebuilds although 1 is kept");
            c.count = -7;
            Assert.That(c.count, Is.EqualTo(1));
            Assert.That(builds.Update(), Is.EqualTo(1));
            c.count = 5;
            Assert.That(c.count, Is.EqualTo(5));
            Assert.That(builds.Update(), Is.EqualTo(1));
            c.count = 5;
            Assert.That(builds.Update(), Is.EqualTo(0));

            c.randomSeed = 9;
            Assert.That(c.randomSeed, Is.EqualTo(9));
            Assert.That(builds.Update(), Is.EqualTo(0), "no random order, no rebuild");
            c.randomOrder = true;
            Assert.That(builds.Update(), Is.EqualTo(1));
            c.randomSeed = 10;
            Assert.That(builds.Update(), Is.EqualTo(1));
            c.randomSeed = 10;
            Assert.That(builds.Update(), Is.EqualTo(0));
        }

        // AddMesh 0x4c3300/0x4c3450, GetMesh 0x4bdc00, RemoveMesh 0x4c3590: every change of the list rebuilds the owner; a definition that
        // is already in the list is not added again; bad indices throw.
        [Test]
        public void MeshListChangesRebuildAndRejectBadIndices()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            Mesh first = _fixture.Tetra();
            c.AddMesh(first);
            Assert.That(c.GetMeshCount(), Is.EqualTo(1));
            Assert.That(c.GetMesh(0).mesh, Is.SameAs(first));
            Assert.That(builds.Update(), Is.EqualTo(1));

            SplineMesh.Channel.MeshDefinition second = NewDefinition();
            c.AddMesh(second);
            Assert.That(c.GetMeshCount(), Is.EqualTo(2));
            Assert.That(c.GetMesh(1), Is.SameAs(second));
            Assert.That(builds.Update(), Is.EqualTo(1));

            c.AddMesh(second);
            Assert.That(c.GetMeshCount(), Is.EqualTo(2), "already in the list");
            Assert.That(builds.Update(), Is.EqualTo(0));

            Assert.That(() => c.GetMesh(2), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => c.GetMesh(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => c.RemoveMesh(5), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(builds.Update(), Is.EqualTo(0), "a bad index queues nothing");

            c.RemoveMesh(0);
            Assert.That(c.GetMeshCount(), Is.EqualTo(1));
            Assert.That(c.GetMesh(0), Is.SameAs(second));
            Assert.That(builds.Update(), Is.EqualTo(1));
        }

        // SwapMeshes 0x4c3020 and DuplicateMesh 0x4c3190 ignore indices outside the list; a duplicate is appended as a copy of the
        // definition (Copy 0x4c29a0).
        [Test]
        public void SwapAndDuplicateRespectTheRange()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMesh.Channel.MeshDefinition a = NewDefinition();
            SplineMesh.Channel.MeshDefinition b = NewDefinition();
            SplineMesh.Channel.MeshDefinition d = NewDefinition();
            c.AddMesh(a);
            c.AddMesh(b);
            c.AddMesh(d);
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            foreach (int[] bad in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 3, 0 }, new[] { 0, 3 } })
            {
                c.SwapMeshes(bad[0], bad[1]);
                Assert.That(builds.Update(), Is.EqualTo(0), "(" + bad[0] + ", " + bad[1] + ")");
                Assert.That(c.GetMesh(0), Is.SameAs(a));
                Assert.That(c.GetMesh(2), Is.SameAs(d));
            }

            c.SwapMeshes(0, 2);
            Assert.That(builds.Update(), Is.EqualTo(1));
            Assert.That(c.GetMesh(0), Is.SameAs(d));
            Assert.That(c.GetMesh(1), Is.SameAs(b));
            Assert.That(c.GetMesh(2), Is.SameAs(a));
            c.SwapMeshes(1, 1);
            Assert.That(builds.Update(), Is.EqualTo(1), "equal indices still rebuild");

            c.DuplicateMesh(-1);
            c.DuplicateMesh(3);
            Assert.That(c.GetMeshCount(), Is.EqualTo(3));
            Assert.That(builds.Update(), Is.EqualTo(0));

            c.DuplicateMesh(0);
            Assert.That(c.GetMeshCount(), Is.EqualTo(4));
            Assert.That(c.GetMesh(3), Is.Not.SameAs(c.GetMesh(0)));
            Assert.That(c.GetMesh(3).mesh, Is.SameAs(c.GetMesh(0).mesh));
            Assert.That(c.GetMesh(3).vertices, Is.Not.SameAs(c.GetMesh(0).vertices));
            Assert.That(c.GetMesh(3).vertices, Is.EqualTo(c.GetMesh(0).vertices));
            Assert.That(builds.Update(), Is.EqualTo(1));
        }

        // Refresh 0x4c3650 refreshes every definition from its mesh and rebuilds the owner; Rebuild 0x4c0bd0 only rebuilds.
        [Test]
        public void RefreshReReadsEveryMeshAndRebuilds()
        {
            SplineMesh generator = _fixture.NewSplineMesh();
            SplineMesh.Channel c = generator.AddChannel("c");
            SplineMesh.Channel.MeshDefinition a = NewDefinition();
            SplineMesh.Channel.MeshDefinition b = NewDefinition();
            c.AddMesh(a);
            c.AddMesh(b);
            SplineMeshFixture.BuildCounter builds = new SplineMeshFixture.BuildCounter(generator);

            a.triangles = new[] { 7 };
            b.triangles = new[] { 8 };
            DreamteckGeneratorFixture.Invoke(c, "Refresh");
            Assert.That(a.triangles.Length, Is.EqualTo(12));
            Assert.That(b.triangles.Length, Is.EqualTo(12));
            Assert.That(builds.Update(), Is.EqualTo(1));

            DreamteckGeneratorFixture.Invoke(c, "Rebuild");
            Assert.That(builds.Update(), Is.EqualTo(1));

            SplineMesh.Channel unowned = new SplineMesh.Channel("u", null);
            DreamteckGeneratorFixture.Invoke(unowned, "Rebuild");
            DreamteckGeneratorFixture.Invoke(unowned, "Refresh");
        }
    }
}
