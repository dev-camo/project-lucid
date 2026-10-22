using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.MeshGenerator, used as the plain base component (its BuildMesh is empty). The computer holds the Linear
    // line (0,0,0) -> (10,0,0): two samples at percents 0 and 1. Expected values are hand arithmetic from the native bodies
    // cited per test (x86-64 addresses in the 0x49d... range). TearDown destroys every game object and mesh a test made.
    public sealed class OriginalDreamteckMeshGeneratorTests
    {
        private readonly DreamteckGeneratorFixture _fixture = new DreamteckGeneratorFixture();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _fixture.Dispose();
        }

        // The ctor 0x49e3b0 field initialisers (the cctor's zero is asserted by TypeInitializerStartsTheUvScratchAtZero). The editor compile of the original
        // text runs Reset (which calls Awake) when the component is added, and SplineUser.Awake ends with RebuildImmediate while
        // not playing, so the first build has already happened here: the meshes exist and WriteMesh armed _updateCollider. This
        // test covers the editor branch only. The shipped player (no UNITY_EDITOR code) builds in Awake only when playing and
        // keeps the pre-build values (null, null, false); they are asserted by
        // OriginalDreamteckMeshGeneratorPlayModeTests.FreshGeneratorKeepsTheShippedDefaults. _lastUpdateTime is only written by
        // LateRun and stays 0.
        [Test]
        public void FreshGeneratorHasBuiltOnceInEditMode()
        {
            MeshGenerator generator = _fixture.NewBare<MeshGenerator>();
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
            Assert.That(generator.meshIndexFormat, Is.EqualTo(IndexFormat.UInt16));
            Assert.That(generator.colliderUpdateRate, Is.EqualTo(0.2f));
            Assert.That(generator.baked, Is.False);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.True, "armed by the editor Awake build");
            Assert.That(DreamteckGeneratorFixture.Field<float>(generator, "_lastUpdateTime"), Is.EqualTo(0f));
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Not.Null, "created by the editor Awake build");
            Assert.That(DreamteckGeneratorFixture.MeshOf(generator) == null, Is.False);
            Assert.That(Uvs(), Is.EqualTo(_fixture.UvsBaseline), "the build of a generator without a spline writes no UVs; the baseline is the zero of the cctor unless a fixture-less caller left one");
            Assert.That(Invoke(generator, "get_meshName"), Is.EqualTo("Mesh"));
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            return DreamteckGeneratorFixture.Invoke(target, name, arguments);
        }

        // Awake 0x49d3c0 and Reset 0x49d4a0 fetch the three components through GetComponents 0x49d3e0; Awake runs it before the
        // base Awake, Reset after the base Reset.
        [Test]
        public void ComponentsAreFetchedByAwakeAndReset()
        {
            GameObject owner = _fixture.NewObject("holder");
            MeshFilter filter = owner.AddComponent<MeshFilter>();
            MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
            MeshGenerator generator = owner.AddComponent<MeshGenerator>();
            Assert.That(DreamteckGeneratorFixture.Field<MeshFilter>(generator, "filter") == filter, Is.True);
            Assert.That(DreamteckGeneratorFixture.Field<MeshRenderer>(generator, "meshRenderer") == renderer, Is.True);
            Assert.That(DreamteckGeneratorFixture.Field<MeshCollider>(generator, "meshCollider") == null, Is.True);

            MeshCollider collider = owner.AddComponent<MeshCollider>();
            Invoke(generator, "Reset");
            Assert.That(DreamteckGeneratorFixture.Field<MeshCollider>(generator, "meshCollider") == collider, Is.True, "Reset fetches again");
            UnityEngine.Object.DestroyImmediate(collider);
            Invoke(generator, "Awake");
            Assert.That(DreamteckGeneratorFixture.Field<MeshCollider>(generator, "meshCollider") == null, Is.True, "Awake fetches again");
        }

        // set_meshIndexFormat 0x49d1a0 and set_markDynamic 0x49d310 call RefreshMesh 0x49d1e0 before the rebuild: the Unity mesh
        // is destroyed and both meshes are created again (CreateMesh 0x49e280 hands the index format to both).
        [Test]
        public void IndexFormatAndMarkDynamicReplaceTheMeshes()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            TS_Mesh firstTs = DreamteckGeneratorFixture.TsMeshOf(generator);
            Mesh firstMesh = DreamteckGeneratorFixture.MeshOf(generator);
            Assert.That(firstTs, Is.Not.Null);
            Assert.That(firstMesh.name, Is.EqualTo("Mesh"));
            Assert.That(firstMesh.indexFormat, Is.EqualTo(IndexFormat.UInt16));

            generator.meshIndexFormat = IndexFormat.UInt16;
            generator.markDynamic = true;
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.SameAs(firstTs), "equal values keep the meshes");
            Assert.That(DreamteckGeneratorFixture.MeshOf(generator) == firstMesh, Is.True);

            generator.meshIndexFormat = IndexFormat.UInt32;
            TS_Mesh secondTs = DreamteckGeneratorFixture.TsMeshOf(generator);
            Mesh secondMesh = DreamteckGeneratorFixture.MeshOf(generator);
            Assert.That(secondTs, Is.Not.SameAs(firstTs));
            Assert.That(secondMesh == firstMesh, Is.False);
            Assert.That(firstMesh == null, Is.True, "the old mesh is destroyed");
            Assert.That(secondMesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(secondTs.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(secondMesh.name, Is.EqualTo("Mesh"));

            generator.markDynamic = false;
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Not.SameAs(secondTs));
            Assert.That(secondMesh == null, Is.True);
            Assert.That(generator.markDynamic, Is.False);
        }

        // Rebuild 0x49d4c0 and RebuildImmediate 0x49d4e0 return at once for a baked generator. The baked flag has no setter; the
        // serialized field is filled the way a saved scene would. The editor Awake build (see the defaults test) has already
        // created the meshes before the flag is set, so the test checks that nothing is built or sampled afterwards. This covers the
        // editor branch (immediate rebuild); the queued player path and the unbuilt Awake are asserted by
        // OriginalDreamteckMeshGeneratorPlayModeTests.BakedGeneratorNeverRebuilds.
        [Test]
        public void BakedGeneratorNeverRebuilds()
        {
            MeshGenerator generator = _fixture.NewBare<MeshGenerator>();
            JsonUtility.FromJsonOverwrite("{\"_baked\":true}", generator);
            Assert.That(generator.baked, Is.True);
            TS_Mesh before = DreamteckGeneratorFixture.TsMeshOf(generator);
            Assert.That(before, Is.Not.Null, "built by the editor Awake before the flag was set");
            generator.spline = _fixture.NewLine();
            int builds = 0;
            generator.onPostBuild += delegate { builds++; };
            generator.Rebuild();
            DreamteckGeneratorFixture.Update(generator);
            generator.RebuildImmediate();
            Assert.That(builds, Is.EqualTo(0), "no queued build");
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.SameAs(before), "no immediate build");
            Assert.That(generator.sampleCount, Is.EqualTo(0));

            JsonUtility.FromJsonOverwrite("{\"_baked\":false}", generator);
            generator.RebuildImmediate();
            Assert.That(generator.sampleCount, Is.EqualTo(2));
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.Not.Null);
        }

        // Build 0x49d810 creates the meshes when either is missing and builds with two or more samples (BuildMesh) or clears them
        // (ClearMesh 0x49d8f0); PostBuild 0x49d8c0 writes the mesh and hands it to the filter.
        [Test]
        public void BuildCreatesTheMeshesAndClearsThemBelowTwoSamples()
        {
            MeshGenerator generator = _fixture.NewBare<MeshGenerator>();
            generator.calculateTangents = false;
            generator.RebuildImmediate();
            TS_Mesh ts = DreamteckGeneratorFixture.TsMeshOf(generator);
            Mesh mesh = DreamteckGeneratorFixture.MeshOf(generator);
            Assert.That(ts, Is.Not.Null, "no spline: the meshes are still created");
            Assert.That(mesh == null, Is.False);
            Assert.That(generator.GetComponent<MeshFilter>().sharedMesh == mesh, Is.True, "PostBuild gives the mesh to the filter");

            generator.spline = _fixture.NewLine();
            generator.RebuildImmediate();
            Assert.That(DreamteckGeneratorFixture.TsMeshOf(generator), Is.SameAs(ts), "existing meshes are kept");
            Assert.That(DreamteckGeneratorFixture.MeshOf(generator) == mesh, Is.True);
            Assert.That(generator.sampleCount, Is.EqualTo(2));
            Assert.That(ts.vertices.Length, Is.EqualTo(0), "the base BuildMesh adds nothing");

            Fill(ts, new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 2, 1 });
            mesh.vertices = ts.vertices;
            Assert.That(mesh.vertexCount, Is.EqualTo(3));
            Invoke(generator, "ClearMesh");
            Assert.That(ts.vertexCount, Is.EqualTo(0));
            Assert.That(ts.triangles.Length, Is.EqualTo(0));
            Assert.That(mesh.vertexCount, Is.EqualTo(0));

            Fill(ts, new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 2, 1 });
            mesh.vertices = ts.vertices;
            generator.spline = _fixture.NewComputer(new SplinePoint(Vector3.zero));
            generator.RebuildImmediate();
            Assert.That(generator.sampleCount, Is.LessThan(2));
            Assert.That(ts.vertexCount, Is.EqualTo(0), "fewer than two samples clear the TS_Mesh");
            Assert.That(mesh.vertexCount, Is.EqualTo(0), "and the Unity mesh");
        }

        private static void Fill(TS_Mesh ts, Vector3[] vertices, int[] triangles)
        {
            ts.vertices = vertices;
            ts.normals = vertices.Select(v => Vector3.up).ToArray();
            ts.triangles = triangles;
        }

        // The 16-bit limit check of WriteMesh 0x49d940 (cmp 0x10000 at 0x49dacc): a UInt16 mesh of 65536 vertices logs the error
        // with the object name and 65535, one of 65535 vertices and a UInt32 mesh of 65536 do not.
        [Test]
        public void WriteMeshReportsTheSixteenBitVertexLimit()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            string expected = "WARNING: The generated mesh for " + generator.name + " exceeds the maximum vertex count for standard meshes in Unity (65535). To create bigger meshes, set the Index Format inside the Vertices foldout to 32.";
            TS_Mesh ts = DreamteckGeneratorFixture.TsMeshOf(generator);
            List<string> errors = new List<string>();
            Application.LogCallback record = (condition, stackTrace, type) =>
            {
                if (type == LogType.Error)
                {
                    errors.Add(condition);
                }
            };
            Application.logMessageReceived += record;
            bool ignoreFailing = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                ts.vertices = new Vector3[65535];
                ts.normals = new Vector3[65535];
                Invoke(generator, "WriteMesh");
                Assert.That(errors.Contains(expected), Is.False, "65535 vertices fit");

                ts.vertices = new Vector3[65536];
                ts.normals = new Vector3[65536];
                Invoke(generator, "WriteMesh");
                Assert.That(errors.Count(e => e == expected), Is.EqualTo(1), "65536 vertices exceed the 16 bit format");

                errors.Clear();
                generator.meshIndexFormat = IndexFormat.UInt32;
                ts = DreamteckGeneratorFixture.TsMeshOf(generator);
                ts.vertices = new Vector3[65536];
                ts.normals = new Vector3[65536];
                Invoke(generator, "WriteMesh");
                Assert.That(errors.Contains(expected), Is.False, "the 32 bit format has no limit");
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = ignoreFailing;
                Application.logMessageReceived -= record;
            }
        }

        // AllocateMesh 0x49dcb0: negative counts become 0, a double sided mesh doubles both, the five vertex arrays are
        // replaced only when the vertex count differs (the others are not touched even if they disagree) and the triangle array
        // only when its length differs.
        [Test]
        public void AllocateMeshSizesTheArraysLikeTheNativeBody()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            TS_Mesh ts = DreamteckGeneratorFixture.TsMeshOf(generator);
            Invoke(generator, "AllocateMesh", 4, 6);
            Assert.That(ts.vertices.Length, Is.EqualTo(4));
            Assert.That(ts.normals.Length, Is.EqualTo(4));
            Assert.That(ts.tangents.Length, Is.EqualTo(4));
            Assert.That(ts.colors.Length, Is.EqualTo(4));
            Assert.That(ts.uv.Length, Is.EqualTo(4));
            Assert.That(ts.uv2.Length, Is.EqualTo(0), "the other UV sets are left alone");
            Assert.That(ts.triangles.Length, Is.EqualTo(6));

            Vector3[] vertices = ts.vertices;
            int[] triangles = ts.triangles;
            ts.normals = new Vector3[1];
            Invoke(generator, "AllocateMesh", 4, 6);
            Assert.That(ts.vertices, Is.SameAs(vertices), "equal counts keep the arrays");
            Assert.That(ts.triangles, Is.SameAs(triangles));
            Assert.That(ts.normals.Length, Is.EqualTo(1), "an array that disagrees is not repaired");

            Invoke(generator, "AllocateMesh", 5, 6);
            Assert.That(ts.vertices.Length, Is.EqualTo(5));
            Assert.That(ts.normals.Length, Is.EqualTo(5), "a new vertex count replaces all five");
            Assert.That(ts.triangles, Is.SameAs(triangles), "the triangle array is judged on its own length");
            Invoke(generator, "AllocateMesh", 5, 3);
            Assert.That(ts.triangles.Length, Is.EqualTo(3));

            Invoke(generator, "AllocateMesh", -2, -9);
            Assert.That(ts.vertices.Length, Is.EqualTo(0));
            Assert.That(ts.triangles.Length, Is.EqualTo(0));

            generator.doubleSided = true;
            Invoke(generator, "AllocateMesh", 4, 6);
            Assert.That(ts.vertices.Length, Is.EqualTo(8));
            Assert.That(ts.triangles.Length, Is.EqualTo(12));
            Invoke(generator, "AllocateMesh", -1, 3);
            Assert.That(ts.vertices.Length, Is.EqualTo(0), "the clamp comes before the doubling");
            Assert.That(ts.triangles.Length, Is.EqualTo(6));
        }

        private static Vector2 Uvs()
        {
            return (Vector2)typeof(MeshGenerator).GetField("__uvs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        // The cctor 0x49e4c0 stores Vector2.get_zero in __uvs (the only static MeshGenerator declares, so re-running it changes
        // nothing else). The incoming value is put back afterwards.
        [Test]
        public void TypeInitializerStartsTheUvScratchAtZero()
        {
            Vector2 incoming = Uvs();
            try
            {
                SetUvs(new Vector2(3f, -4f));
                typeof(MeshGenerator).TypeInitializer.Invoke(null, null);
                Assert.That(Uvs(), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                SetUvs(incoming);
            }
        }

        // The fixture hands the scratch back after a test that wrote it, so the defaults test cannot depend on the order. A nonzero
        // incoming value is planted first so that a restore to zero or a no-op restore both fail.
        [Test]
        public void DisposeRestoresTheUvScratchValueFoundBeforeTheTest()
        {
            DreamteckGeneratorFixture probe = new DreamteckGeneratorFixture();
            Vector2 original = Uvs();
            Vector2 incoming = new Vector2(0.75f, -1.5f);
            Vector2 written = new Vector2(-0.25f, 2.5f);
            try
            {
                SetUvs(incoming);
                probe.NewObject("probe");
                SetUvs(written);
                probe.Dispose();
                Assert.That(Uvs(), Is.EqualTo(incoming));
            }
            finally
            {
                probe.Dispose();
                SetUvs(original);
            }
        }

        // The generator's mesh is reached through the fixture even when the component is destroyed before the teardown.
        [Test]
        public void DisposeDestroysTheMeshOfAnAlreadyDestroyedGenerator()
        {
            DreamteckGeneratorFixture probe = new DreamteckGeneratorFixture();
            Mesh mesh = null;
            try
            {
                MeshGenerator generator = probe.NewBare<MeshGenerator>();
                mesh = DreamteckGeneratorFixture.MeshOf(generator);
                Assert.That(mesh == null, Is.False);
                UnityEngine.Object.DestroyImmediate(generator);
                probe.Dispose();
                Assert.That(mesh == null, Is.True);
            }
            finally
            {
                probe.Dispose();
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        private static void SetUvs(Vector2 value)
        {
            typeof(MeshGenerator).GetField("__uvs", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        private MeshGenerator ClippedGenerator()
        {
            MeshGenerator generator = _fixture.NewGenerator<MeshGenerator>(_fixture.NewLine());
            generator.clipFrom = 0.25;
            generator.clipTo = 0.75;
            generator.uvScale = new Vector2(2f, 3f);
            generator.uvOffset = new Vector2(0.25f, 0.5f);
            generator.RebuildImmediate();
            return generator;
        }

        // CalculateUVs 0x49e0b0: U is u * scale.x - offset.x in every mode. V is percent * scale.y - offset.y (Clip),
        // _vDist * scale.y - offset.y (UniformClip, also the fallback), InverseLerp(clipFrom, clipTo, percent) * scale.y -
        // offset.y (Clamp) or _vDist * scale.y / span - offset.y (UniformClamp). The clip range is 0.25..0.75 (span 0.5).
        [Test]
        public void UvCoordinatesFollowTheModeTruthTable()
        {
            MeshGenerator generator = ClippedGenerator();
            Assert.That(generator.sampleCount, Is.EqualTo(2));
            Invoke(generator, "AddUVDistance", 1);

            void Expect(MeshGenerator.UVMode mode, float expectedV)
            {
                generator.uvMode = mode;
                Invoke(generator, "CalculateUVs", 0.625, 0.5f);
                Assert.That(Uvs().x, Is.EqualTo(0.75f).Within(1e-5f), mode + " U");
                Assert.That(Uvs().y, Is.EqualTo(expectedV).Within(1e-4f), mode + " V");
            }

            Expect(MeshGenerator.UVMode.Clip, 0.625f * 3f - 0.5f);
            Expect(MeshGenerator.UVMode.UniformClip, 5f * 3f - 0.5f);
            Expect(MeshGenerator.UVMode.Clamp, 0.75f * 3f - 0.5f);
            Expect(MeshGenerator.UVMode.UniformClamp, 5f * 3f / 0.5f - 0.5f);
            Expect((MeshGenerator.UVMode)7, 5f * 3f - 0.5f);

            generator.uvMode = MeshGenerator.UVMode.Clamp;
            Invoke(generator, "CalculateUVs", 0.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(0f * 3f - 0.5f).Within(1e-5f), "Clamp pins the percent to the clip range");
            Invoke(generator, "CalculateUVs", 1.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(1f * 3f - 0.5f).Within(1e-5f));
        }

        // ResetUVDistance 0x49dec0 sets the distance to the spline length up to the first sample for UniformClip and to 0
        // otherwise; AddUVDistance 0x49df20 adds the distance between a sample and its predecessor and ignores sample 0.
        [Test]
        public void UniformUvDistanceStartsAtTheClipAndGrowsBySampleDistance()
        {
            MeshGenerator generator = ClippedGenerator();
            generator.uvScale = Vector2.one;
            generator.uvOffset = Vector2.zero;
            generator.uvMode = MeshGenerator.UVMode.UniformClip;

            Invoke(generator, "AddUVDistance", 0);
            Invoke(generator, "CalculateUVs", 0.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(0f).Within(1e-5f), "sample 0 adds nothing");

            Invoke(generator, "AddUVDistance", 1);
            Invoke(generator, "CalculateUVs", 0.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(5f).Within(1e-4f), "the samples at 0.25 and 0.75 are 5 apart");
            Invoke(generator, "AddUVDistance", 1);
            Invoke(generator, "CalculateUVs", 0.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(10f).Within(1e-4f), "it accumulates");

            Invoke(generator, "ResetUVDistance");
            Invoke(generator, "CalculateUVs", 0.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(2.5f).Within(1e-4f), "UniformClip starts at the length before the clip (0.25 of 10)");

            generator.uvMode = MeshGenerator.UVMode.Clamp;
            Invoke(generator, "ResetUVDistance");
            generator.uvMode = MeshGenerator.UVMode.UniformClip;
            Invoke(generator, "CalculateUVs", 0.0, 0f);
            Assert.That(Uvs().y, Is.EqualTo(0f).Within(1e-5f), "other modes start at 0");
        }

        // GetBaseSize 0x49e230 and GetBaseColor 0x49e250: the sample value with the use-spline flag, otherwise 1 and white.
        [Test]
        public void BaseSizeAndColorHonourTheSplineFlags()
        {
            MeshGenerator generator = _fixture.NewBare<MeshGenerator>();
            SplineSample sample = new SplineSample(Vector3.zero, Vector3.up, Vector3.forward, new Color(0.2f, 0.4f, 0.6f, 0.8f), 3f, 0.5);
            Assert.That(Invoke(generator, "GetBaseSize", sample), Is.EqualTo(3f));
            Assert.That(Invoke(generator, "GetBaseColor", sample), Is.EqualTo(new Color(0.2f, 0.4f, 0.6f, 0.8f)));
            generator.useSplineSize = false;
            Assert.That(Invoke(generator, "GetBaseSize", sample), Is.EqualTo(1f));
            Assert.That(Invoke(generator, "GetBaseColor", sample), Is.EqualTo(new Color(0.2f, 0.4f, 0.6f, 0.8f)), "the flags are independent");
            generator.useSplineColor = false;
            Assert.That(Invoke(generator, "GetBaseColor", sample), Is.EqualTo(Color.white));
            generator.useSplineSize = true;
            Assert.That(Invoke(generator, "GetBaseSize", sample), Is.EqualTo(3f));
        }

        // UpdateCollider 0x49d620 adds a MeshCollider when there is none and gives it the mesh of the filter; LateRun 0x49d730 does
        // so at most every colliderUpdateRate seconds (Time.time - last >= rate, so rate 0 always) once WriteMesh armed it, and
        // never for a baked generator.
        [Test]
        public void ColliderFollowsTheMeshAtTheUpdateRate()
        {
            GameObject owner = _fixture.NewObject("collider holder");
            owner.AddComponent<MeshFilter>();
            owner.AddComponent<MeshRenderer>();
            MeshCollider collider = owner.AddComponent<MeshCollider>();
            MeshGenerator generator = owner.AddComponent<MeshGenerator>();
            generator.calculateTangents = false;
            generator.spline = _fixture.NewLine();
            generator.RebuildImmediate();
            TS_Mesh ts = DreamteckGeneratorFixture.TsMeshOf(generator);
            Fill(ts, new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            Invoke(generator, "WriteMesh");
            Mesh mesh = DreamteckGeneratorFixture.MeshOf(generator);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.True);

            generator.colliderUpdateRate = 1000000f;
            Invoke(generator, "LateRun");
            Assert.That(collider.sharedMesh == null, Is.True, "the rate has not elapsed");
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.True);

            generator.colliderUpdateRate = 0f;
            Invoke(generator, "LateRun");
            Assert.That(collider.sharedMesh == mesh, Is.True, "rate 0 updates at once");
            Assert.That(DreamteckGeneratorFixture.Field<bool>(generator, "_updateCollider"), Is.False);
            Assert.That(DreamteckGeneratorFixture.Field<float>(generator, "_lastUpdateTime"), Is.EqualTo(Time.time).Within(0.5f));

            collider.sharedMesh = null;
            Invoke(generator, "LateRun");
            Assert.That(collider.sharedMesh == null, Is.True, "not armed again until the next WriteMesh");

            UnityEngine.Object.DestroyImmediate(collider);
            GameObject bare = _fixture.NewObject("no collider");
            bare.AddComponent<MeshFilter>();
            bare.AddComponent<MeshRenderer>();
            MeshGenerator other = bare.AddComponent<MeshGenerator>();
            other.calculateTangents = false;
            other.spline = _fixture.NewLine();
            other.RebuildImmediate();
            Fill(DreamteckGeneratorFixture.TsMeshOf(other), new[] { Vector3.zero, Vector3.right, Vector3.up }, new[] { 0, 1, 2 });
            Invoke(other, "WriteMesh");
            Assert.That(bare.GetComponent<MeshCollider>() == null, Is.True);
            other.UpdateCollider();
            MeshCollider added = bare.GetComponent<MeshCollider>();
            Assert.That(added == null, Is.False, "UpdateCollider adds the collider");
            Assert.That(added.sharedMesh == DreamteckGeneratorFixture.MeshOf(other), Is.True);
            Assert.That(DreamteckGeneratorFixture.Field<MeshCollider>(other, "meshCollider") == added, Is.True);

            JsonUtility.FromJsonOverwrite("{\"_baked\":true}", other);
            added.sharedMesh = null;
            other.colliderUpdateRate = 0f;
            DreamteckGeneratorFixture.SetField(other, "_updateCollider", true);
            Invoke(other, "LateRun");
            Assert.That(added.sharedMesh == null, Is.True, "a baked generator skips the late run");
        }

        // OnDestroy 0x49d520 makes the filter and the renderer visible in the inspector again.
        [Test]
        public void DestroyingTheGeneratorRestoresTheHideFlagsOfFilterAndRenderer()
        {
            MeshGenerator generator = _fixture.NewBare<MeshGenerator>();
            MeshFilter filter = generator.GetComponent<MeshFilter>();
            MeshRenderer renderer = generator.GetComponent<MeshRenderer>();
            filter.hideFlags = HideFlags.HideInInspector;
            renderer.hideFlags = HideFlags.NotEditable;
            UnityEngine.Object.DestroyImmediate(generator);
            Assert.That(filter.hideFlags, Is.EqualTo(HideFlags.None));
            Assert.That(renderer.hideFlags, Is.EqualTo(HideFlags.None));
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(MeshGenerator).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "UNITY_16_VERTEX_LIMIT", "_baked", "_markDynamic", "_size", "_color", "_offset", "_normalMethod", "_calculateTangents",
                "_useSplineSize", "_useSplineColor", "_rotation", "_flipFaces", "_doubleSided", "_uvMode", "_uvScale", "_uvOffset",
                "_uvRotation", "_meshIndexFormat", "_bakedMesh", "colliderUpdateRate", "_updateCollider", "_lastUpdateTime", "_vDist",
                "__uvs", "<_tsMesh>k__BackingField", "_mesh", "filter", "meshRenderer", "meshCollider"
            }));
            Assert.That(typeof(MeshGenerator).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Select(t => t.Name).ToArray(),
                Is.EqualTo(new[] { "UVMode", "NormalMethod" }));
            Assert.That(Enum.GetNames(typeof(MeshGenerator.UVMode)), Is.EqualTo(new[] { "Clip", "UniformClip", "Clamp", "UniformClamp" }));
            Assert.That(Enum.GetNames(typeof(MeshGenerator.NormalMethod)), Is.EqualTo(new[] { "Recalculate", "SplineNormals" }));
            Assert.That(Enum.GetValues(typeof(MeshGenerator.UVMode)).Cast<int>().ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            FieldInfo limit = typeof(MeshGenerator).GetField("UNITY_16_VERTEX_LIMIT", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(limit.IsLiteral && limit.IsFamily && (int)limit.GetRawConstantValue() == 65535, Is.True);

            foreach (string name in new[] { "_baked", "_markDynamic", "_size", "_color", "_offset", "_normalMethod", "_calculateTangents", "_useSplineSize", "_useSplineColor", "_rotation", "_flipFaces", "_doubleSided", "_uvMode", "_uvScale", "_uvOffset", "_uvRotation", "_meshIndexFormat", "_bakedMesh" })
            {
                FieldInfo field = typeof(MeshGenerator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.IsPrivate, Is.True, name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1), name + " serialized");
                Assert.That(field.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1), name + " hidden");
            }

            UnityEngine.RangeAttribute range = (UnityEngine.RangeAttribute)typeof(MeshGenerator).GetField("_rotation", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false).Single();
            Assert.That(range.min, Is.EqualTo(-360f));
            Assert.That(range.max, Is.EqualTo(360f));
            Assert.That(typeof(MeshGenerator).GetField("colliderUpdateRate").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(typeof(MeshGenerator).GetCustomAttributes(false).Length, Is.EqualTo(0), "no class attributes");
            Assert.That(typeof(MeshGenerator).GetProperty("baked").CanWrite, Is.False);
            Assert.That(typeof(MeshGenerator).GetProperty("_tsMesh", BindingFlags.Instance | BindingFlags.NonPublic).GetSetMethod(true).IsPrivate, Is.True);
        }
    }
}
