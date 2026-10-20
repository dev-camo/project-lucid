using System;
using System.Linq;
using System.Reflection;
using Dreamteck;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineRenderer on the Linear line (0,0,0) -> (10,0,0): two samples with forward (1,0,0), up (0,1,0), size 1,
    // white, at percents 0 and 1. The ribbon row of a sample runs along Cross(forward, direction), where the direction is the
    // vertex direction handed to GenerateVertices (orthographic) or the normalised vector from the sample to it. Expected values
    // are hand arithmetic from the native bodies cited per test (x86-64 addresses in the 0x4cc... / 0x4cd... range).
    public sealed class OriginalDreamteckSplineRendererTests
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

        private SplineRenderer NewRenderer(SplineComputer computer = null)
        {
            return _fixture.NewGenerator<SplineRenderer>(computer ?? _fixture.NewLine());
        }

        private static TS_Mesh Ts(MeshGenerator generator)
        {
            return DreamteckGeneratorFixture.TsMeshOf(generator);
        }

        private Camera NewCamera(Vector3 position, bool orthographic)
        {
            Camera camera = _fixture.NewObject("camera").AddComponent<Camera>();
            camera.transform.position = position;
            camera.orthographic = orthographic;
            return camera;
        }

        // Field initialisers of the ctor 0x4cd390: autoOrient on, one slice, the vertex direction is world up.
        [Test]
        public void FreshRendererKeepsTheShippedDefaults()
        {
            SplineRenderer renderer = _fixture.NewBare<SplineRenderer>();
            Assert.That(renderer.autoOrient, Is.True);
            Assert.That(renderer.updateFrameInterval, Is.EqualTo(0));
            Assert.That(renderer.slices, Is.EqualTo(1));
            Assert.That(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), Is.EqualTo(Vector3.up));
            Assert.That(DreamteckGeneratorFixture.Field<int>(renderer, "_currentFrame"), Is.EqualTo(0));
            Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.False);
            Assert.That(renderer.GetComponent<MeshFilter>() == null, Is.False);
            Assert.That(renderer.GetComponent<MeshRenderer>() == null, Is.False);
        }

        // set_slices 0x4cc600 is the same clamp-and-rebuild setter as the path generator.
        [Test]
        public void SlicesClampAtOneAndRebuildOnAnyDifferentValue()
        {
            SplineRenderer renderer = NewRenderer();
            DreamteckPlayState.Cache(renderer); // the setter queues (play mode) instead of the editor's immediate rebuild
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

        // LateRun 0x4cc720 replaces the base late run: with an interval above 0 the frame counter goes up and wraps to 0 once it is
        // above the interval (cmovle keeps it while it is <= the interval); with 0 it stays untouched.
        [Test]
        public void LateRunCountsFramesAndWrapsAboveTheInterval()
        {
            SplineRenderer renderer = NewRenderer();
            DreamteckGeneratorFixture.Invoke(renderer, "LateRun");
            Assert.That(DreamteckGeneratorFixture.Field<int>(renderer, "_currentFrame"), Is.EqualTo(0), "interval 0");

            renderer.updateFrameInterval = 2;
            int[] expected = { 1, 2, 0, 1, 2, 0 };
            foreach (int frame in expected)
            {
                DreamteckGeneratorFixture.Invoke(renderer, "LateRun");
                Assert.That(DreamteckGeneratorFixture.Field<int>(renderer, "_currentFrame"), Is.EqualTo(frame));
            }

            renderer.updateFrameInterval = 1;
            DreamteckGeneratorFixture.SetField(renderer, "_currentFrame", 0);
            DreamteckGeneratorFixture.Invoke(renderer, "LateRun");
            Assert.That(DreamteckGeneratorFixture.Field<int>(renderer, "_currentFrame"), Is.EqualTo(1), "equal to the interval stays");
            DreamteckGeneratorFixture.Invoke(renderer, "LateRun");
            Assert.That(DreamteckGeneratorFixture.Field<int>(renderer, "_currentFrame"), Is.EqualTo(0));
        }

        // LateRun 0x4cc720 never calls MeshGenerator.LateRun 0x49d730: a pending collider update stays pending.
        [Test]
        public void LateRunDoesNotUpdateTheCollider()
        {
            GameObject owner = _fixture.NewObject("generator");
            owner.AddComponent<MeshFilter>();
            owner.AddComponent<MeshRenderer>();
            MeshCollider collider = owner.AddComponent<MeshCollider>();
            SplineRenderer renderer = owner.AddComponent<SplineRenderer>();
            renderer.calculateTangents = false;
            renderer.spline = _fixture.NewLine();
            renderer.RebuildImmediate();
            Assert.That(owner.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, "precondition: a mesh to hand over");

            DreamteckGeneratorFixture.SetField(renderer, "_updateCollider", true);
            renderer.colliderUpdateRate = 0f;
            DreamteckGeneratorFixture.Invoke(renderer, "LateRun");
            Assert.That(collider.sharedMesh, Is.Null);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_updateCollider"), Is.True);
        }

        // BuildMesh 0x4cc750 with the defaults (perspective, direction (0,1,0)): the direction of a sample is the normalised vector
        // from it to (0,1,0), the row runs along Cross(forward, direction), and the normal is the direction itself.
        [Test]
        public void DefaultBuildFacesTheRowTowardsTheVertexDirection()
        {
            SplineRenderer renderer = NewRenderer();
            TS_Mesh ts = Ts(renderer);
            Assert.That(ts.vertices.Length, Is.EqualTo(4));
            Assert.That(ts.triangles, Is.EqualTo(MeshUtility.GeneratePlaneTriangles(1, 2, false)));
            Near(ts.vertices[0], new Vector3(0f, 0f, -0.5f), "first sample, slice percent 0");
            Near(ts.vertices[1], new Vector3(0f, 0f, 0.5f), "first sample, slice percent 1");
            Near(ts.vertices[2], new Vector3(10f, 0f, -0.5f), "second sample");
            Near(ts.vertices[3], new Vector3(10f, 0f, 0.5f), "second sample");
            Near(ts.normals[0], Vector3.up, "direction from the origin to (0,1,0)");
            Near(ts.normals[2], new Vector3(-10f, 1f, 0f).normalized, "direction from (10,0,0) to (0,1,0)");
            // CalculateUVs(percent, slicePercent) in Clip mode and the 180 degree turn leave (u, v) = (slicePercent, percent).
            DreamteckGeneratorFixture.Near(ts.uv[0], new Vector2(0f, 0f), "uv 0");
            DreamteckGeneratorFixture.Near(ts.uv[1], new Vector2(1f, 0f), "uv 1");
            DreamteckGeneratorFixture.Near(ts.uv[2], new Vector2(0f, 1f), "uv 2");
            DreamteckGeneratorFixture.Near(ts.uv[3], new Vector2(1f, 1f), "uv 3");
        }

        // GenerateVertices 0x4cc800 called directly: an orthographic direction is used as given, a perspective one is the
        // normalised vector from the sample; the vertex normal is that direction either way.
        [Test]
        public void GenerateVerticesUsesTheDirectionAsGivenWhenOrthographic()
        {
            SplineRenderer renderer = NewRenderer();
            renderer.GenerateVertices(new Vector3(0f, 0f, 1f), true);
            TS_Mesh ts = Ts(renderer);
            // Cross(forward (1,0,0), direction (0,0,1)) = (0,-1,0): the row vector is (0,-1,0): slice percent 0 is at
            // -0.5 times it (y = +0.5) and slice percent 1 at +0.5 times it (y = -0.5)
            Near(ts.vertices[0], new Vector3(0f, 0.5f, 0f), "slice percent 0 is -0.5 times the row vector");
            Near(ts.vertices[1], new Vector3(0f, -0.5f, 0f), "slice percent 1 is +0.5 times the row vector");
            for (int i = 0; i < 4; i++)
            {
                Near(ts.normals[i], new Vector3(0f, 0f, 1f), "orthographic normal " + i);
            }

            renderer.GenerateVertices(new Vector3(0f, 0f, 1f), false);
            ts = Ts(renderer);
            Near(ts.normals[0], new Vector3(0f, 0f, 1f), "perspective from the origin");
            Near(ts.normals[2], new Vector3(-10f, 0f, 1f).normalized, "perspective from (10,0,0)");
        }

        // The slice rows: three vertices per sample spread by the slice percent, normals and UVs per vertex, and the sample size and
        // colour are used without looking at the use-spline flags (no useSplineSize / useSplineColor test in 0x4cc800).
        [Test]
        public void SampleSizeAndColourIgnoreTheUseSplineFlags()
        {
            SplineComputer sized = _fixture.NewComputer(
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 2f, new Color(0f, 1f, 0f, 1f)),
                new SplinePoint(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.up, 2f, new Color(0f, 1f, 0f, 1f)));
            SplineRenderer renderer = NewRenderer(sized);
            renderer.useSplineSize = false;
            renderer.useSplineColor = false;
            renderer.color = new Color(1f, 0.5f, 1f, 1f);
            renderer.slices = 2;
            renderer.RebuildImmediate();
            TS_Mesh ts = Ts(renderer);
            Assert.That(ts.vertices.Length, Is.EqualTo(6));
            Assert.That(ts.triangles.Length, Is.EqualTo(12));
            Near(ts.vertices[0], new Vector3(0f, 0f, -1f), "sample size 2 gives a width of 2");
            Near(ts.vertices[1], new Vector3(0f, 0f, 0f), "middle");
            Near(ts.vertices[2], new Vector3(0f, 0f, 1f), "end");
            DreamteckGeneratorFixture.Near(ts.colors[0], new Color(0f, 0.5f, 0f, 1f), "generator colour times the sample colour");
            DreamteckGeneratorFixture.Near(ts.colors[5], new Color(0f, 0.5f, 0f, 1f), "all vertices");
            DreamteckGeneratorFixture.Near(ts.uv[1], new Vector2(0.5f, 0f), "middle uv");

            renderer.size = 0.5f;
            renderer.slices = 1;
            renderer.RebuildImmediate();
            Near(Ts(renderer).vertices[0], new Vector3(0f, 0f, -0.5f), "generator size times sample size 2 times 0.5");
        }

        // The offset (0x4cc9d3 ff.): up * y - Cross(forward, up) * x + forward * z, not scaled by the sample size.
        [Test]
        public void OffsetMovesTheCentreAlongUpRightAndForward()
        {
            SplineRenderer renderer = NewRenderer();
            renderer.offset = new Vector3(2f, 3f, 4f);
            renderer.GenerateVertices(Vector3.up, true);
            // right term -Cross((1,0,0),(0,1,0)) * 2 = -(0,0,1) * 2; up term (0,3,0); forward term (4,0,0)
            Near(Ts(renderer).vertices[0], new Vector3(4f, 3f, -2f) + new Vector3(0f, 0f, -0.5f), "vertex 0");
            Near(Ts(renderer).vertices[3], new Vector3(14f, 3f, -2f) + new Vector3(0f, 0f, 0.5f), "vertex 3");
        }

        // The offset is not scaled by the sample size (0x4cc9d3-0x4cca91 never read [r14+0x194]), unlike PathGenerator; only the
        // half width follows the size.
        [Test]
        public void OffsetIsNotScaledBySampleSize()
        {
            SplineComputer sized = _fixture.NewComputer(
                new SplinePoint(Vector3.zero, Vector3.zero, Vector3.up, 2f, Color.white),
                new SplinePoint(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f), Vector3.up, 2f, Color.white));
            SplineRenderer renderer = NewRenderer(sized);
            renderer.offset = new Vector3(2f, 3f, 4f);
            renderer.GenerateVertices(Vector3.up, true);
            // (4,3,-2) from the unscaled offset, half width 2 * 0.5 = 1 along the row
            Near(Ts(renderer).vertices[0], new Vector3(4f, 3f, -3f), "vertex 0");
            Near(Ts(renderer).vertices[3], new Vector3(14f, 3f, -1f), "vertex 3");
        }

        // RenderWithCamera 0x4cd0b0: the projection is remembered; the direction is the camera position (perspective) or the
        // reversed camera forward (orthographic); the mesh is rebuilt and written at once.
        [Test]
        public void RenderWithCameraOrientsTheMeshForTheCamera()
        {
            SplineRenderer renderer = NewRenderer();
            Camera camera = NewCamera(new Vector3(0f, 5f, 0f), false);
            renderer.RenderWithCamera(camera);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.False);
            Near(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), new Vector3(0f, 5f, 0f), "camera position");
            Near(Ts(renderer).normals[0], Vector3.up, "(0,5,0) from the origin");
            Near(Ts(renderer).normals[2], new Vector3(-10f, 5f, 0f).normalized, "(0,5,0) from (10,0,0)");
            Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(4), "written to the mesh");

            camera.orthographic = true;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            renderer.RenderWithCamera(camera);
            Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.True);
            Near(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), -camera.transform.forward, "reversed camera forward");
            Near(Ts(renderer).normals[2], Vector3.up, "orthographic: the same direction for every sample");

            Assert.Throws<NullReferenceException>(() => renderer.RenderWithCamera(null));
        }

        // Runs the action with an owned perspective camera at (3,4,5) as the current camera, then with no current camera and an
        // owned orthographic main camera looking along its forward; the caller tells which of the two the renderer took.
        private void WithCurrentThenMain(Action<Camera> whenCurrent, Action<Camera> whenMain)
        {
            Camera current = NewCamera(new Vector3(3f, 4f, 5f), false);
            Camera main = NewCamera(new Vector3(-7f, 0f, 0f), true);
            main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            main.tag = "MainCamera";
            Camera previous = Camera.current;

            // A batch run may already hold an enabled camera tagged MainCamera (the default scene's); Camera.main returns the first
            // enabled one, so those cameras are switched off for the main phase and restored afterwards.
            System.Collections.Generic.List<Camera> foreignMains = new System.Collections.Generic.List<Camera>();
            foreach (Camera other in UnityEngine.Object.FindObjectsOfType<Camera>())
            {
                if (other != main && other != current && other.enabled && other.CompareTag("MainCamera"))
                {
                    foreignMains.Add(other);
                }
            }

            try
            {
                Camera.SetupCurrent(current);
                Assert.That(Camera.current, Is.SameAs(current), "precondition: owned current camera");
                whenCurrent(current);
                Camera.SetupCurrent(null);
                Assume.That(Camera.current == null, Is.True, "precondition: no current camera");
                foreach (Camera other in foreignMains)
                {
                    other.enabled = false;
                }

                Assert.That(Camera.main, Is.SameAs(main), "precondition: the owned camera is the main camera");
                whenMain(main);
            }
            finally
            {
                foreach (Camera other in foreignMains)
                {
                    other.enabled = true;
                }

                Camera.SetupCurrent(previous);
            }
        }

        // OnWillRenderObject 0x4cd160: nothing without autoOrient or while the frame counter is not at 0 for an interval above 0;
        // otherwise it runs Awake once outside play mode and renders for the current camera, else the main camera.
        [Test]
        public void OnWillRenderObjectRendersForTheCurrentCameraElseTheMainCamera()
        {
            SplineRenderer renderer = NewRenderer();
            renderer.autoOrient = false;
            DreamteckGeneratorFixture.Invoke(renderer, "OnWillRenderObject");
            Near(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), Vector3.up, "autoOrient off");
            Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_init"), Is.False);

            renderer.autoOrient = true;
            renderer.updateFrameInterval = 3;
            DreamteckGeneratorFixture.SetField(renderer, "_currentFrame", 1);
            DreamteckGeneratorFixture.Invoke(renderer, "OnWillRenderObject");
            Near(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), Vector3.up, "not the frame");

            WithCurrentThenMain(
                current =>
                {
                    DreamteckGeneratorFixture.SetField(renderer, "_currentFrame", 0);
                    DreamteckGeneratorFixture.Invoke(renderer, "OnWillRenderObject");
                    Near(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), new Vector3(3f, 4f, 5f), "the current camera position");
                    Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.False);
                    Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_init"), Is.True, "Awake ran once outside play mode");
                },
                main =>
                {
                    DreamteckGeneratorFixture.SetField(renderer, "_currentFrame", 0);
                    DreamteckGeneratorFixture.Invoke(renderer, "OnWillRenderObject");
                    // Rotation (90,0,0) looks along -y, so the reversed forward is +y.
                    Near(DreamteckGeneratorFixture.Field<Vector3>(renderer, "_vertexDirection"), Vector3.up, "the main camera, reversed forward");
                    Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.True);
                });
        }

        // Start 0x4cc640 remembers the projection of the current, else the main, camera and creates the meshes again.
        [Test]
        public void StartTakesTheProjectionOfTheCurrentCameraElseTheMainCameraAndCreatesNewMeshes()
        {
            SplineRenderer renderer = NewRenderer();
            WithCurrentThenMain(
                current =>
                {
                    DreamteckGeneratorFixture.Invoke(renderer, "Start");
                    Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.False, "the perspective current camera");
                },
                main =>
                {
                    TS_Mesh before = Ts(renderer);
                    Mesh oldMesh = DreamteckGeneratorFixture.MeshOf(renderer);
                    DreamteckGeneratorFixture.Invoke(renderer, "Start");
                    UnityEngine.Object.DestroyImmediate(oldMesh);
                    Assert.That(DreamteckGeneratorFixture.Field<bool>(renderer, "_orthographic"), Is.True, "the orthographic main camera");
                    Assert.That(Ts(renderer), Is.Not.SameAs(before));
                    Assert.That(DreamteckGeneratorFixture.MeshOf(renderer).name, Is.EqualTo("Mesh"));
                });
        }

        [Test]
        public void SerializedLayoutKeepsTheFieldOrderAttributesAndMenu()
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(SplineRenderer).GetFields(flags).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "autoOrient", "updateFrameInterval", "_slices", "_currentFrame", "_vertexDirection", "_orthographic", "_init"
            }));
            Assert.That(typeof(SplineRenderer).GetField("autoOrient").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            Assert.That(typeof(SplineRenderer).GetField("updateFrameInterval").GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            FieldInfo slices = typeof(SplineRenderer).GetField("_slices", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(slices.GetCustomAttributes(typeof(SerializeField), false).Length, Is.EqualTo(1));
            Assert.That(slices.GetCustomAttributes(typeof(HideInInspector), false).Length, Is.EqualTo(1));
            AddComponentMenu menu = (AddComponentMenu)typeof(SplineRenderer).GetCustomAttributes(typeof(AddComponentMenu), false).Single();
            Assert.That(menu.componentMenu, Is.EqualTo("Dreamteck/Splines/Users/Spline Renderer"));
            Assert.That(typeof(SplineRenderer).GetCustomAttributes(typeof(ExecuteInEditMode), false).Length, Is.EqualTo(1));
            Assert.That(typeof(SplineRenderer).GetCustomAttributes(typeof(RequireComponent), false).Cast<RequireComponent>().Select(r => r.m_Type0).ToArray(),
                Is.EquivalentTo(new[] { typeof(MeshFilter), typeof(MeshRenderer) }));
            Assert.That(typeof(SplineRenderer).BaseType, Is.EqualTo(typeof(MeshGenerator)));
        }
    }
}
