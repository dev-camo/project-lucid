using System;
using System.Linq;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.MeshGenerator in play mode, where SplineUser caches _isPlaying from Application.isPlaying in Awake and so
    // takes the shipped player text: a setter queues the rebuild that Update runs, Awake builds at once (buildOnAwake), and
    // nothing builds in Reset. The EditMode class OriginalDreamteckMeshGeneratorTests covers the editor branches of the same
    // members. The computer holds the Linear line (0,0,0) -> (10,0,0). Expected values are from the native bodies cited per
    // test (x86-64 addresses in the 0x49d... range). TearDown destroys every game object and mesh a test made.
    public sealed class OriginalDreamteckMeshGeneratorPlayModeTests
    {
        private readonly DreamteckGeneratorFixture _fixture = new DreamteckGeneratorFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            return DreamteckGeneratorFixture.Invoke(target, name, arguments);
        }

        // The filter, the renderer and the generator on an inactive object: Awake has not run, so the field initialisers are
        // the only state and no build has happened. Activating it runs the shipped Awake.
        private MeshGenerator NewInactive(out GameObject owner)
        {
            owner = _fixture.NewObject("generator");
            owner.SetActive(false);
            owner.AddComponent<MeshFilter>();
            owner.AddComponent<MeshRenderer>();
            return owner.AddComponent<MeshGenerator>();
        }

        // The ctor 0x49e3b0 field initialisers and the cctor 0x49e4c0 (__uvs starts at zero), read before any build. In play mode
        // Awake builds at once (buildOnAwake is on and Application.isPlaying is true: SplineUser.Awake ends in RebuildImmediate), so the pre-build values are
        // observed on an inactive object, which gets no Awake until it is activated; activating then builds and arms the
        // collider update. The EditMode test of the same name covers the editor branch (Reset and Awake build on AddComponent).
        [Test]
        public void FreshGeneratorKeepsTheShippedDefaults()
        {
            GameObject owner;
            MeshGenerator generator = NewInactive(out owner);
            Assert.That(generator.markDynamic, Is.True);
            Assert.That(generator.size, Is.EqualTo(1f));
            Assert.That(generator.color, Is.EqualTo(Color.white));
            Assert.That(generator.offset, Is.EqualTo(Vector3.zero));
            Assert.That(generator.normalMethod, Is.EqualTo(MeshGenerator.NormalMethod.SplineNormals));
            Assert.That(generator.calculateTangents, Is.True);
            Assert.That(generator.useSplineSize, Is.True);
            Assert.That(generator.useSplineColor, Is.True);
            Assert.That(generator.rotation, Is.EqualTo(0f));
            Assert.That(generator.flipFaces, Is.False);
            Assert.That(generator.doubleSided, Is.False);
            Assert.That(generator.uvMode, Is.EqualTo(MeshGenerator.UVMode.Clip));
            Assert.That(generator.uvScale, Is.EqualTo(Vector2.one));
            Assert.That(generator.uvOffset, Is.EqualTo(Vector2.zero));
            Assert.That(generator.uvRotation, Is.EqualTo(0f));
            Assert.That(generator.meshIndexFormat, Is.EqualTo(UnityEngine.Rendering.IndexFormat.UInt16));
            Assert.That(generator.colliderUpdateRate, Is.EqualTo(0.2f));
            Assert.That(generator.baked, Is.False);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.False);
            Assert.That(DreamteckGeneratorFixture.Field<float>(generator, "_lastUpdateTime"), Is.EqualTo(0f));
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Null, "no mesh before the first build");
            Assert.That(DreamteckGeneratorFixture.MeshOf(generator) == null, Is.True);
            Assert.That(typeof(MeshGenerator).GetField("__uvs", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null), Is.EqualTo(Vector2.zero));
            Assert.That(Invoke(generator, "get_meshName"), Is.EqualTo("Mesh"));

            owner.SetActive(true);
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Not.Null, "Awake builds when playing");
            Assert.That(DreamteckGeneratorFixture.MeshOf(generator) == null, Is.False);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.True, "WriteMesh armed the collider update");
        }

        // Each setter skips a value equal to the stored one (Color and Vector comparisons are the Unity operators: squared
        // distance under 1e-10, so a 1e-6 change is equal) and otherwise queues a rebuild through Rebuild 0x49d4c0.
        [Test]
        public void ConfigurationSettersSkipEqualValuesAndRebuildOtherwise()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            int builds = 0;
            generator.onPostBuild += delegate { builds++; };
            DreamteckGeneratorFixture.Update(generator);
            Assert.That(builds, Is.EqualTo(0), "nothing queued yet");

            void Check(string name, Action equal, Action changed)
            {
                int before = builds;
                equal();
                DreamteckGeneratorFixture.Update(generator);
                Assert.That(builds, Is.EqualTo(before), name + ": an equal value queues nothing");
                changed();
                DreamteckGeneratorFixture.Update(generator);
                Assert.That(builds, Is.EqualTo(before + 1), name + ": a new value queues one build");
            }

            Check("size", () => generator.size = 1f, () => generator.size = 2f);
            Check("color", () => generator.color = new Color(1f, 1f, 1f, 1f + 1e-6f), () => generator.color = Color.red);
            Check("offset", () => generator.offset = new Vector3(1e-6f, 0f, 0f), () => generator.offset = new Vector3(1f, 0f, 0f));
            Check("normalMethod", () => generator.normalMethod = MeshGenerator.NormalMethod.SplineNormals, () => generator.normalMethod = MeshGenerator.NormalMethod.Recalculate);
            Check("useSplineSize", () => generator.useSplineSize = true, () => generator.useSplineSize = false);
            Check("useSplineColor", () => generator.useSplineColor = true, () => generator.useSplineColor = false);
            Check("calculateTangents", () => generator.calculateTangents = false, () => generator.calculateTangents = true);
            generator.calculateTangents = false;
            DreamteckGeneratorFixture.Update(generator);
            builds = 0;
            Check("rotation", () => generator.rotation = 0f, () => generator.rotation = 45f);
            Check("flipFaces", () => generator.flipFaces = false, () => generator.flipFaces = true);
            Check("doubleSided", () => generator.doubleSided = false, () => generator.doubleSided = true);
            Check("uvMode", () => generator.uvMode = MeshGenerator.UVMode.Clip, () => generator.uvMode = MeshGenerator.UVMode.Clamp);
            Check("uvScale", () => generator.uvScale = new Vector2(1f + 1e-6f, 1f), () => generator.uvScale = new Vector2(2f, 2f));
            Check("uvOffset", () => generator.uvOffset = new Vector2(1e-6f, 0f), () => generator.uvOffset = new Vector2(0.5f, 0f));
            Check("uvRotation", () => generator.uvRotation = 0f, () => generator.uvRotation = 30f);

            Assert.That(generator.size, Is.EqualTo(2f));
            Assert.That(generator.color, Is.EqualTo(Color.red));
            Assert.That(generator.offset, Is.EqualTo(new Vector3(1f, 0f, 0f)), "the stored value is the new one, not the equal one");
            Assert.That(generator.uvScale, Is.EqualTo(new Vector2(2f, 2f)));
        }

        // set_size 0x49cd00 stores the value before the compare decides on the rebuild (the other setters store after it): an
        // equal value with different bits, -0f over 0f, is kept and queues nothing.
        [Test]
        public void SizeStoresAnEqualValueWithoutRebuilding()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            int builds = 0;
            generator.onPostBuild += delegate { builds++; };
            generator.size = 0f;
            DreamteckGeneratorFixture.Update(generator);
            Assert.That(builds, Is.EqualTo(1));
            generator.size = -0f;
            DreamteckGeneratorFixture.Update(generator);
            Assert.That(builds, Is.EqualTo(1), "an equal value queues nothing");
            Assert.That(1f / generator.size, Is.EqualTo(float.NegativeInfinity), "-0f replaced +0f");
        }

        // Rebuild 0x49d4c0 and RebuildImmediate 0x49d4e0 return at once for a baked generator. The baked flag has no setter; the
        // serialized field is filled the way a saved scene would, on an inactive object so that the shipped Awake (which builds
        // in play mode) runs afterwards and is covered too. The EditMode test of the same name covers the editor branch.
        [Test]
        public void BakedGeneratorNeverRebuilds()
        {
            GameObject owner;
            MeshGenerator generator = NewInactive(out owner);
            JsonUtility.FromJsonOverwrite("{\"_baked\":true}", generator);
            Assert.That(generator.baked, Is.True);
            owner.SetActive(true);
            generator.spline = _fixture.NewLine();
            int builds = 0;
            generator.onPostBuild += delegate { builds++; };
            generator.Rebuild();
            DreamteckGeneratorFixture.Update(generator);
            generator.RebuildImmediate();
            Assert.That(builds, Is.EqualTo(0), "no queued build");
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Null, "no immediate build, and Awake did not build either");
            Assert.That(generator.sampleCount, Is.EqualTo(0));

            JsonUtility.FromJsonOverwrite("{\"_baked\":false}", generator);
            generator.RebuildImmediate();
            Assert.That(generator.sampleCount, Is.EqualTo(2));
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Not.Null);
        }

        private static void Fill(TS_Mesh ts, Vector3[] vertices, int[] triangles)
        {
            ts.vertices = vertices;
            ts.normals = vertices.Select(v => Vector3.up).ToArray();
            ts.triangles = triangles;
        }

        // WriteMesh 0x49d940: the vertices go through the worldToLocal matrix of the transform, then (if doubleSided,
        // else if flipFaces) the second half is mirrored or the winding flipped, then the mesh is written, the normals recalculated
        // for NormalMethod.Recalculate, the filter gets the mesh and the collider update is armed.
        [Test]
        public void WriteMeshMovesTheMeshIntoLocalSpaceAndAppliesTheFaceOptions()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            generator.transform.position = new Vector3(5f, 0f, 0f);
            TS_Mesh ts = DreamteckGeneratorFixture.TsMeshOf(generator);
            Fill(ts, new[] { new Vector3(5f, 0f, 0f), new Vector3(6f, 0f, 0f), new Vector3(5f, 1f, 0f) }, new[] { 0, 1, 2 });
            Invoke(generator, "WriteMesh");
            DreamteckGeneratorFixture.Near(ts.vertices[0], Vector3.zero, "world to local");
            DreamteckGeneratorFixture.Near(ts.vertices[1], Vector3.right, "world to local");
            DreamteckGeneratorFixture.Near(ts.vertices[2], Vector3.up, "world to local");
            Mesh mesh = DreamteckGeneratorFixture.MeshOf(generator);
            Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 1, 2 }));
            DreamteckGeneratorFixture.Near(mesh.vertices[1], Vector3.right, "written mesh");
            DreamteckGeneratorFixture.Near(mesh.normals[0], Vector3.up, "SplineNormals keeps the given normals");
            Assert.That(generator.GetComponent<MeshFilter>().sharedMesh == mesh, Is.True);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.True);

            generator.transform.position = Vector3.zero;
            generator.flipFaces = true;
            Fill(ts, new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            Invoke(generator, "WriteMesh");
            Assert.That(ts.triangles, Is.EqualTo(new[] { 2, 1, 0 }), "flipFaces reverses the winding");
            DreamteckGeneratorFixture.Near(ts.normals[0], Vector3.down, "and negates the normals");

            generator.doubleSided = true;
            Fill(ts, new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.zero, Vector3.zero, Vector3.zero }, new[] { 0, 1, 2, 0, 0, 0 });
            Invoke(generator, "WriteMesh");
            Assert.That(ts.triangles, Is.EqualTo(new[] { 0, 1, 2, 5, 4, 3 }), "doubleSided mirrors the first half and wins over flipFaces");
            DreamteckGeneratorFixture.Near(ts.vertices[4], Vector3.right, "mirrored vertex");
            DreamteckGeneratorFixture.Near(ts.normals[3], Vector3.down, "mirrored normal is negated");

            generator.doubleSided = false;
            generator.flipFaces = false;
            generator.normalMethod = MeshGenerator.NormalMethod.Recalculate;
            Fill(ts, new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            Invoke(generator, "WriteMesh");
            Assert.That(Mathf.Abs(mesh.normals[0].z), Is.EqualTo(1f).Within(1e-4f), "Recalculate replaces the given up normals with the face normal");
        }
    }
}
