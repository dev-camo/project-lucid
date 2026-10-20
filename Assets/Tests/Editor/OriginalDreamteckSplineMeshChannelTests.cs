using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineMesh.Channel (x86-64 addresses in the 0x4bc... / 0x4bd... / 0x4bf... / 0x4c0... / 0x4c2... /
    // 0x4c3... ranges). The setters rebuild the owner, which is observed through the builds a SplineMesh runs when it is
    // updated; the random sequences are the System.Random sequences of the seeds, drawn in the order the native bodies draw
    // them. TearDown destroys every game object and mesh a test made.
    public sealed class OriginalDreamteckSplineMeshChannelTests
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

        private static string Format(double percent)
        {
            return percent.ToString(CultureInfo.InvariantCulture);
        }

        // The constructors 0x4bca90 and 0x4bc5d0: the field initialisers, then Init 0x4c21f0 (scale limits one, offset and rotation
        // limits zero). The mesh constructor adds the mesh as the first definition.
        [Test]
        public void FreshChannelKeepsTheShippedDefaults()
        {
            Mesh tetra = _fixture.Tetra();
            foreach (SplineMesh.Channel channel in new[] { new SplineMesh.Channel("n", null), new SplineMesh.Channel("n", tetra, null) })
            {
                Assert.That(channel.name, Is.EqualTo("n"));
                Assert.That(channel.clipFrom, Is.EqualTo(0.0));
                Assert.That(channel.clipTo, Is.EqualTo(1.0));
                Assert.That(channel.randomOffset, Is.False);
                Assert.That(channel.offsetHandler, Is.Null);
                Assert.That(channel.overrideMaterialID, Is.False);
                Assert.That(channel.targetMaterialID, Is.EqualTo(0));
                Assert.That(channel.randomRotation, Is.False);
                Assert.That(channel.placeRotationHandler, Is.Null);
                Assert.That(channel.extrudeRotationHandler, Is.Null);
                Assert.That(channel.randomScale, Is.False);
                Assert.That(channel.scaleHandler, Is.Null);
                Assert.That(channel.uniformRandomScale, Is.False);
                Assert.That(channel.offsetSeed, Is.EqualTo(0));
                Assert.That(channel.rotationSeed, Is.EqualTo(0));
                Assert.That(channel.scaleSeed, Is.EqualTo(0));
                Assert.That(channel.spacing, Is.EqualTo(0.0));
                Assert.That(channel.minOffset, Is.EqualTo(Vector2.zero));
                Assert.That(channel.maxOffset, Is.EqualTo(Vector2.zero));
                Assert.That(channel.minRotation, Is.EqualTo(Vector3.zero));
                Assert.That(channel.maxRotation, Is.EqualTo(Vector3.zero));
                Assert.That(channel.minScale, Is.EqualTo(Vector3.one));
                Assert.That(channel.maxScale, Is.EqualTo(Vector3.one));
                Assert.That(channel.type, Is.EqualTo(SplineMesh.Channel.Type.Extrude));
                Assert.That(channel.randomOrder, Is.False);
                Assert.That(channel.randomSeed, Is.EqualTo(0));
                Assert.That(channel.count, Is.EqualTo(1));
                Assert.That(channel.autoCount, Is.False);
                Assert.That(channel.overrideUVs, Is.EqualTo(SplineMesh.Channel.UVOverride.None));
                Assert.That(channel.uvOffset, Is.EqualTo(Vector2.zero));
                Assert.That(channel.uvScale, Is.EqualTo(Vector2.one));
                Assert.That(channel.overrideNormal, Is.False);
                Assert.That(channel.customNormal, Is.EqualTo(Vector3.up));
                Assert.That(channel.scaleModifier, Is.Not.Null);
                Assert.That(channel.scaleModifier.keys, Is.Empty);
                Assert.That(DreamteckGeneratorFixture.Field<SplineMesh>(channel, "owner") == null, Is.True);
            }

            Assert.That(new SplineMesh.Channel("n", null).GetMeshCount(), Is.EqualTo(0));
            SplineMesh.Channel withMesh = new SplineMesh.Channel("n", tetra, null);
            Assert.That(withMesh.GetMeshCount(), Is.EqualTo(1));
            Assert.That(withMesh.GetMesh(0).mesh, Is.SameAs(tetra));
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

        // A channel without an owner just stores the values.
        [Test]
        public void ChannelWithoutAnOwnerStoresWithoutRebuilding()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            c.clipFrom = 0.3;
            c.count = 9;
            c.type = SplineMesh.Channel.Type.Place;
            c.minScale = Vector3.zero;
            c.AddMesh(_fixture.Tetra());
            c.RemoveMesh(0);
            Assert.That(c.clipFrom, Is.EqualTo(0.3));
            Assert.That(c.count, Is.EqualTo(9));
            Assert.That(c.type, Is.EqualTo(SplineMesh.Channel.Type.Place));
            Assert.That(c.minScale, Is.EqualTo(Vector3.zero));
            Assert.That(c.GetMeshCount(), Is.EqualTo(0));
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

        // CopyTo 0x4c2680 replaces the meshes by copies and copies the settings listed in the native body. The random order, the auto
        // count, the uniform scale flag, the uv transform, the name and the owner are not copied.
        [Test]
        public void CopyToCopiesTheSettingsAndTheMeshesOnly()
        {
            SplineMesh.Channel source = new SplineMesh.Channel("source", null);
            SplineMesh.Channel.MeshDefinition definition = NewDefinition();
            source.AddMesh(definition);
            SplineMesh.Channel.Vector2Handler offsetHandler = percent => Vector2.one;
            SplineMesh.Channel.QuaternionHandler placeHandler = percent => Quaternion.identity;
            SplineMesh.Channel.FloatHandler extrudeHandler = percent => 1f;
            SplineMesh.Channel.Vector3Handler scaleHandler = percent => Vector3.one;
            source.clipFrom = 0.2;
            source.clipTo = 0.8;
            source.customNormal = new Vector3(0f, 0f, 1f);
            source.randomSeed = 11;
            source.minOffset = new Vector2(1f, 2f);
            source.maxOffset = new Vector2(3f, 4f);
            source.minRotation = new Vector3(1f, 2f, 3f);
            source.maxRotation = new Vector3(4f, 5f, 6f);
            source.minScale = new Vector3(2f, 2f, 2f);
            source.maxScale = new Vector3(3f, 3f, 3f);
            source.randomOffset = true;
            source.randomRotation = true;
            source.randomScale = true;
            source.offsetSeed = 12;
            source.offsetHandler = offsetHandler;
            source.rotationSeed = 13;
            source.placeRotationHandler = placeHandler;
            source.extrudeRotationHandler = extrudeHandler;
            source.scaleSeed = 14;
            source.scaleHandler = scaleHandler;
            source.spacing = 0.3;
            source.overrideUVs = SplineMesh.Channel.UVOverride.UniformV;
            source.type = SplineMesh.Channel.Type.Place;
            source.count = 7;
            source.overrideMaterialID = true;
            source.targetMaterialID = 2;
            source.overrideNormal = true;
            source.randomOrder = true;
            source.autoCount = true;
            source.uniformRandomScale = true;
            source.uvScale = new Vector2(5f, 5f);
            source.uvOffset = new Vector2(6f, 6f);

            SplineMesh.Channel target = new SplineMesh.Channel("target", _fixture.Tetra(), null);
            target.AddMesh(_fixture.Tetra());
            source.CopyTo(target);

            Assert.That(target.name, Is.EqualTo("target"));
            Assert.That(target.clipFrom, Is.EqualTo(0.2));
            Assert.That(target.clipTo, Is.EqualTo(0.8));
            Assert.That(target.customNormal, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(target.randomSeed, Is.EqualTo(11));
            Assert.That(target.minOffset, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(target.maxOffset, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(target.minRotation, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(target.maxRotation, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(target.minScale, Is.EqualTo(new Vector3(2f, 2f, 2f)));
            Assert.That(target.maxScale, Is.EqualTo(new Vector3(3f, 3f, 3f)));
            Assert.That(target.randomOffset, Is.True);
            Assert.That(target.randomRotation, Is.True);
            Assert.That(target.randomScale, Is.True);
            Assert.That(target.offsetSeed, Is.EqualTo(12));
            Assert.That(target.offsetHandler, Is.SameAs(offsetHandler));
            Assert.That(target.rotationSeed, Is.EqualTo(13));
            Assert.That(target.placeRotationHandler, Is.SameAs(placeHandler));
            Assert.That(target.extrudeRotationHandler, Is.SameAs(extrudeHandler));
            Assert.That(target.scaleSeed, Is.EqualTo(14));
            Assert.That(target.scaleHandler, Is.SameAs(scaleHandler));
            Assert.That(target.spacing, Is.EqualTo(0.3));
            Assert.That(target.overrideUVs, Is.EqualTo(SplineMesh.Channel.UVOverride.UniformV));
            Assert.That(target.type, Is.EqualTo(SplineMesh.Channel.Type.Place));
            Assert.That(target.count, Is.EqualTo(7));
            Assert.That(target.overrideMaterialID, Is.True);
            Assert.That(target.targetMaterialID, Is.EqualTo(2));
            Assert.That(target.overrideNormal, Is.True);

            Assert.That(target.randomOrder, Is.False, "not copied");
            Assert.That(target.autoCount, Is.False, "not copied");
            Assert.That(target.uniformRandomScale, Is.False, "not copied");
            Assert.That(target.uvScale, Is.EqualTo(Vector2.one), "not copied");
            Assert.That(target.uvOffset, Is.EqualTo(Vector2.zero), "not copied");

            Assert.That(target.GetMeshCount(), Is.EqualTo(1), "the old meshes are cleared");
            Assert.That(target.GetMesh(0), Is.Not.SameAs(definition));
            Assert.That(target.GetMesh(0).mesh, Is.SameAs(definition.mesh));
            Assert.That(target.GetMesh(0).vertices, Is.EqualTo(definition.vertices));
            Assert.That(target.GetMesh(0).vertices, Is.Not.SameAs(definition.vertices));
        }

        // NextMesh 0x4bf600 walks the list in turn and wraps; ResetIteration 0x4bdcf0 restarts the walk.
        [Test]
        public void NextMeshWalksTheMeshesAndWraps()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            SplineMesh.Channel.MeshDefinition a = NewDefinition();
            SplineMesh.Channel.MeshDefinition b = NewDefinition();
            SplineMesh.Channel.MeshDefinition d = NewDefinition();
            c.AddMesh(a);
            c.AddMesh(b);
            c.AddMesh(d);

            Assert.That(new[] { c.NextMesh(), c.NextMesh(), c.NextMesh(), c.NextMesh(), c.NextMesh() }, Is.EqualTo(new[] { a, b, d, a, b }));
            c.ResetIteration();
            Assert.That(c.NextMesh(), Is.SameAs(a), "ResetIteration restarts the walk");
            Assert.That(c.NextMesh(), Is.SameAs(b));
            Assert.That(c.NextMesh(), Is.SameAs(d));
            c.RemoveMesh(2);
            Assert.That(c.NextMesh(), Is.SameAs(a), "an iterator past the end wraps to the first mesh");
            Assert.That(c.NextMesh(), Is.SameAs(b));
            Assert.That(c.NextMesh(), Is.SameAs(a));
        }

        // A random order picks Next(count) of the sequence of the seed and leaves the walk position alone.
        [Test]
        public void RandomOrderPicksFromTheSeededSequence()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            SplineMesh.Channel.MeshDefinition[] definitions = { NewDefinition(), NewDefinition(), NewDefinition() };
            foreach (SplineMesh.Channel.MeshDefinition definition in definitions)
            {
                c.AddMesh(definition);
            }

            c.randomOrder = true;
            c.randomSeed = 4;
            Assert.That(() => c.NextMesh(), Throws.TypeOf<NullReferenceException>(), "the sequence exists after ResetIteration only");
            c.ResetIteration();
            System.Random oracle = new System.Random(4);
            List<SplineMesh.Channel.MeshDefinition> actual = new List<SplineMesh.Channel.MeshDefinition>();
            List<SplineMesh.Channel.MeshDefinition> expected = new List<SplineMesh.Channel.MeshDefinition>();
            for (int i = 0; i < 8; i++)
            {
                actual.Add(c.NextMesh());
                expected.Add(definitions[oracle.Next(3)]);
            }

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.Distinct().Count(), Is.GreaterThan(1), "the seed does not pin one mesh");

            c.randomOrder = false;
            Assert.That(c.NextMesh(), Is.SameAs(definitions[0]), "the walk position was not moved by the random picks");
        }

        // ResetIteration builds the sequence of every enabled random option from its seed; an option enabled afterwards has no sequence.
        [Test]
        public void ResetIterationRestartsOnlyTheEnabledSequences()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            c.randomOffset = true;
            c.offsetSeed = 5;
            c.minOffset = Vector2.zero;
            c.maxOffset = new Vector2(10f, 10f);
            c.ResetIteration();
            Vector2 first = c.NextRandomOffset();
            Vector2 second = c.NextRandomOffset();
            Assert.That(second, Is.Not.EqualTo(first), "the sequence advances");
            c.ResetIteration();
            Assert.That(c.NextRandomOffset(), Is.EqualTo(first), "a reset restarts it from the seed");

            c.randomRotation = true;
            Assert.That(() => c.NextRandomQuaternion(), Throws.TypeOf<NullReferenceException>());
            Assert.That(() => c.NextRandomAngle(), Throws.TypeOf<NullReferenceException>());
            c.randomScale = true;
            Assert.That(() => c.NextPlaceScale(), Throws.TypeOf<NullReferenceException>());
            Assert.That(() => c.NextRandomScale(), Throws.TypeOf<NullReferenceException>());
            c.ResetIteration();
            Assert.That(() => c.NextRandomQuaternion(), Throws.Nothing);
            Assert.That(() => c.NextPlaceScale(), Throws.Nothing);
        }

        // NextRandomOffset 0x4bffe0: x lerps with the first draw of the offset sequence, y with the second; without random offsets it is
        // the minimum.
        [Test]
        public void NextRandomOffsetLerpsEachAxisWithItsOwnDraw()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            c.minOffset = new Vector2(1f, 2f);
            c.maxOffset = new Vector2(3f, 6f);
            Assert.That(c.NextRandomOffset(), Is.EqualTo(new Vector2(1f, 2f)), "no random offset: the minimum");

            c.randomOffset = true;
            c.offsetSeed = 42;
            c.ResetIteration();
            System.Random oracle = new System.Random(42);
            for (int i = 0; i < 3; i++)
            {
                float x = (float)oracle.NextDouble();
                float y = (float)oracle.NextDouble();
                Vector2 expected = new Vector2(Mathf.Lerp(1f, 3f, x), Mathf.Lerp(2f, 6f, y));
                SplineMeshFixture.Near(c.NextRandomOffset(), expected, "draw " + i);
            }
        }

        // NextRandomQuaternion 0x4c00c0 lerps the three euler angles with three successive draws; NextRandomAngle 0x4c0800 lerps the z
        // angle with one draw. Without random rotation both read the minimum.
        [Test]
        public void NextRandomRotationDrawsOncePerAxis()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            c.minRotation = new Vector3(10f, 20f, 30f);
            c.maxRotation = new Vector3(50f, 60f, 70f);
            Assert.That(Quaternion.Angle(c.NextRandomQuaternion(), Quaternion.Euler(10f, 20f, 30f)), Is.LessThan(1e-3f));
            Assert.That(c.NextRandomAngle(), Is.EqualTo(30f));

            c.randomRotation = true;
            c.rotationSeed = 7;
            c.ResetIteration();
            System.Random oracle = new System.Random(7);
            float x = (float)oracle.NextDouble();
            float y = (float)oracle.NextDouble();
            float z = (float)oracle.NextDouble();
            Quaternion expected = Quaternion.Euler(Mathf.Lerp(10f, 50f, x), Mathf.Lerp(20f, 60f, y), Mathf.Lerp(30f, 70f, z));
            Assert.That(Quaternion.Angle(c.NextRandomQuaternion(), expected), Is.LessThan(1e-3f));

            c.ResetIteration();
            Assert.That(c.NextRandomAngle(), Is.EqualTo(Mathf.Lerp(30f, 70f, x)).Within(1e-4f), "one draw: the first of the sequence");
            Assert.That(c.NextRandomAngle(), Is.EqualTo(Mathf.Lerp(30f, 70f, y)).Within(1e-4f), "the next angle takes the next draw");
        }

        // NextRandomScale 0x4c06a0 is flat: x and y only, z is one; a uniform scale uses one draw for both axes. NextPlaceScale 0x4c0470
        // draws for all three axes, or lerps the vectors with one draw when uniform.
        [Test]
        public void RandomScalesAreFlatForExtrudeAndFullForPlace()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            c.minScale = new Vector3(1f, 2f, 3f);
            c.maxScale = new Vector3(5f, 6f, 7f);
            Assert.That(c.NextRandomScale(), Is.EqualTo(new Vector3(1f, 2f, 1f)), "no random scale: minimum x and y, z one");
            Assert.That(c.NextPlaceScale(), Is.EqualTo(new Vector3(1f, 2f, 3f)), "no random scale: the minimum");

            c.randomScale = true;
            c.scaleSeed = 11;
            c.ResetIteration();
            System.Random oracle = new System.Random(11);
            float d1 = (float)oracle.NextDouble();
            float d2 = (float)oracle.NextDouble();
            float d3 = (float)oracle.NextDouble();
            SplineMeshFixture.Near(c.NextRandomScale(), new Vector3(Mathf.Lerp(1f, 5f, d1), Mathf.Lerp(2f, 6f, d2), 1f), "flat, one draw per axis");
            c.ResetIteration();
            SplineMeshFixture.Near(c.NextPlaceScale(), new Vector3(Mathf.Lerp(1f, 5f, d1), Mathf.Lerp(2f, 6f, d2), Mathf.Lerp(3f, 7f, d3)), "place, one draw per axis");

            c.uniformRandomScale = true;
            c.ResetIteration();
            SplineMeshFixture.Near(c.NextRandomScale(), new Vector3(Mathf.Lerp(1f, 5f, d1), Mathf.Lerp(2f, 6f, d1), 1f), "flat, uniform: one draw");
            c.ResetIteration();
            SplineMeshFixture.Near(c.NextPlaceScale(), Vector3.Lerp(c.minScale, c.maxScale, d1), "place, uniform: one draw for all axes");
        }

        // GetCustomPlaceValues 0x4c0260 and GetCustomExtrudeValues 0x4c08a0: neutral values without handlers, otherwise the handler
        // results; the offset handler runs first, the rotation handler of the mode second and the scale handler last.
        [Test]
        public void CustomValuesRunTheHandlersInOrder()
        {
            SplineMesh.Channel c = new SplineMesh.Channel("c", null);
            (Vector2, Quaternion, Vector3) neutralPlace = c.GetCustomPlaceValues(0.3);
            Assert.That(neutralPlace.Item1, Is.EqualTo(Vector2.zero));
            Assert.That(neutralPlace.Item2, Is.EqualTo(Quaternion.identity));
            Assert.That(neutralPlace.Item3, Is.EqualTo(Vector3.one));
            (Vector2, float, Vector3) neutralExtrude = c.GetCustomExtrudeValues(0.3);
            Assert.That(neutralExtrude.Item1, Is.EqualTo(Vector2.zero));
            Assert.That(neutralExtrude.Item2, Is.EqualTo(0f));
            Assert.That(neutralExtrude.Item3, Is.EqualTo(Vector3.one));

            List<string> calls = new List<string>();
            c.scaleHandler = percent => { calls.Add("scale " + Format(percent)); return new Vector3(1f, 2f, 3f); };
            c.extrudeRotationHandler = percent => { calls.Add("extrude " + Format(percent)); return 33f; };
            c.placeRotationHandler = percent => { calls.Add("place " + Format(percent)); return Quaternion.Euler(0f, 90f, 0f); };
            c.offsetHandler = percent => { calls.Add("offset " + Format(percent)); return new Vector2(4f, 5f); };

            (Vector2, Quaternion, Vector3) place = c.GetCustomPlaceValues(0.25);
            Assert.That(calls, Is.EqualTo(new[] { "offset 0.25", "place 0.25", "scale 0.25" }));
            Assert.That(place.Item1, Is.EqualTo(new Vector2(4f, 5f)));
            Assert.That(Quaternion.Angle(place.Item2, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-3f));
            Assert.That(place.Item3, Is.EqualTo(new Vector3(1f, 2f, 3f)));

            calls.Clear();
            (Vector2, float, Vector3) extrude = c.GetCustomExtrudeValues(0.75);
            Assert.That(calls, Is.EqualTo(new[] { "offset 0.75", "extrude 0.75", "scale 0.75" }));
            Assert.That(extrude.Item1, Is.EqualTo(new Vector2(4f, 5f)));
            Assert.That(extrude.Item2, Is.EqualTo(33f));
            Assert.That(extrude.Item3, Is.EqualTo(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAndAttributes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string[] channelFields =
            {
                "name", "iterationRandom", "_iterationSeed", "_offsetSeed", "_offsetRandom", "_offsetHandler", "_rotationSeed",
                "_rotationRandom", "_placeRotationHandler", "_extrudeRotationHandler", "_scaleSeed", "_scaleRandom", "_scaleHandler",
                "owner", "meshes", "_clipFrom", "_clipTo", "_randomOrder", "_overrideUVs", "_uvScale", "_uvOffset", "_overrideNormal",
                "_customNormal", "_type", "_count", "_autoCount", "_spacing", "_randomRotation", "_minRotation", "_maxRotation",
                "_randomOffset", "_minOffset", "_maxOffset", "_randomScale", "_uniformRandomScale", "_minScale", "_maxScale", "iterator",
                "_overrideMaterialID", "_targetMaterialID", "_scaleModifier"
            };
            Assert.That(typeof(SplineMesh.Channel).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(channelFields));

            string[] plain = { "name", "iterationRandom", "_offsetRandom", "_offsetHandler", "_rotationRandom", "_placeRotationHandler", "_extrudeRotationHandler", "_scaleRandom", "_scaleHandler", "iterator" };
            foreach (string name in channelFields)
            {
                FieldInfo field = typeof(SplineMesh.Channel).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int serialize = field.GetCustomAttributes(typeof(SerializeField), false).Length;
                int hide = field.GetCustomAttributes(typeof(HideInInspector), false).Length;
                if (plain.Contains(name))
                {
                    Assert.That(serialize + hide, Is.EqualTo(0), name);
                }
                else if (name == "owner")
                {
                    Assert.That(serialize, Is.EqualTo(1), name);
                    Assert.That(hide, Is.EqualTo(0), name);
                    Assert.That(field.IsAssembly, Is.True, "internal");
                }
                else
                {
                    Assert.That(serialize, Is.EqualTo(1), name);
                    Assert.That(hide, Is.EqualTo(1), name);
                }
            }

            Assert.That(typeof(SplineMesh.Channel).GetField("name").IsPublic, Is.True);
            Assert.That(typeof(SplineMesh.Channel).GetField("_scaleModifier", BindingFlags.Instance | BindingFlags.NonPublic).IsFamily, Is.True, "protected");
            Assert.That(typeof(SplineMesh.Channel).GetMethod("Rebuild", BindingFlags.Instance | BindingFlags.NonPublic).IsAssembly, Is.True, "internal");
            Assert.That(typeof(SplineMesh.Channel).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic).IsPrivate, Is.True);
            Assert.That(typeof(SplineMesh.Channel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).IsPrivate, Is.True);
        }
    }
}
