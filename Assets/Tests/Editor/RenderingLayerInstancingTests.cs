using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectLucid.Tests
{
    // Per-instance rendering-layer output (#516). Two MeshRenderers share one instancing material and differ only in
    // renderingLayerMask; Camera.Render drives an owned pipeline that draws them in one DrawRenderers call with GPU
    // instancing on and the SRP Batcher off, into a colour target plus an owned rendering-layer target that is read
    // back. The negative control is a copy of the same shader whose layer-writing fragments skip
    // UNITY_SETUP_INSTANCE_ID, so every instance reads the per-instance array at index 0. Under the instanced draw
    // both renderers must then report the first mask: one instanced draw carried both, and a shared or wrongly
    // indexed mask is detected. Drawn without instancing, each draw has one instance and the copy reports each mask
    // again, so it is otherwise sound. (Removing instancing_options renderinglayer is no control: Unity then splits
    // renderers whose masks differ into separate draws.)
    public sealed class RenderingLayerInstancingTests
    {
        private const uint LeftMask = 0x05, RightMask = 0x0A;
        private const int MaxInt = 0xFF;

        [Test]
        public void EnvUnlitForwardWritesEachInstancesRenderingLayers()
        {
            // The forward pass has no LightMode tag, so it is drawn as SRPDefaultUnlit.
            Check("Assets/Shaders/Recovered/HL_Env_Standard_Unlit.shader", "SRPDefaultUnlit", 1);
        }

        [Test]
        public void TrackDepthNormalsWritesEachInstancesRenderingLayers()
        {
            Check("Assets/Shaders/Recovered/HL_Env_Zone5_Track.shader", "DepthNormals", 2);
        }

        // The fixture sets _GlobalMipBias to zero for its draw. A nondefault entry value is restored after a
        // successful render and after a failure raised once the draw's command buffer has executed.
        [Test]
        public void RenderRestoresANondefaultGlobalMipBias()
        {
            var entry = new Vector4(0.25f, -0.5f, 0.75f, 1);
            Vector4 outer = Shader.GetGlobalVector(MipBias);
            try
            {
                Shader.SetGlobalVector(MipBias, entry);
                WithFixture(() => new Fixture(UnlitPath, 1), fixture =>
                {
                    Vector4? during = null;
                    fixture.AfterDraw = () => during = Shader.GetGlobalVector(MipBias);
                    fixture.Render(fixture.Production, "SRPDefaultUnlit", true, out _, out _);
                    Assert.That(during, Is.EqualTo(Vector4.zero), "the draw ran with the fixture's zero bias");
                    Assert.That(Shader.GetGlobalVector(MipBias), Is.EqualTo(entry), "restored after success");

                    during = null;
                    var injected = new InvalidOperationException("injected after the command buffer executed");
                    fixture.AfterDraw = () => { during = Shader.GetGlobalVector(MipBias); throw injected; };
                    Exception thrown = Assert.Catch(() => fixture.Render(fixture.Production, "SRPDefaultUnlit", true, out _, out _));
                    Assert.That(thrown, Is.SameAs(injected));
                    Assert.That(during, Is.EqualTo(Vector4.zero), "the failure came after the zero bias was set");
                    Assert.That(Shader.GetGlobalVector(MipBias), Is.EqualTo(entry), "restored after failure");
                });
                Assert.That(Shader.GetGlobalVector(MipBias), Is.EqualTo(entry));
            }
            finally
            {
                Shader.SetGlobalVector(MipBias, outer);
            }
        }

        // A cleanup failure never replaces the failure it follows, whether that came from construction or from the
        // body; it is attached to it, every owned object is still released, and with no earlier failure it is thrown.
        [Test]
        public void FixtureCleanupFailureKeepsThePrimaryFailure()
        {
            var cleanup = new InvalidOperationException("injected cleanup failure");
            Action fault = () => throw cleanup;

            Camera camera = null;
            string folder = null;
            var body = new InvalidOperationException("injected body failure");
            Exception thrown = Assert.Catch(() => WithFixture(() => new Fixture(UnlitPath, 1, cleanupFault: fault), fixture =>
            {
                camera = fixture.Camera;
                folder = fixture.Folder;
                throw body;
            }));
            AssertKept(thrown, body, cleanup, camera, folder);

            camera = null;
            folder = null;
            var construction = new InvalidOperationException("injected construction failure");
            thrown = Assert.Catch(() => WithFixture(() => new Fixture(UnlitPath, 1, fixture =>
            {
                camera = fixture.Camera;
                folder = fixture.Folder;
                throw construction;
            }, fault), fixture => Assert.Fail("the body runs only on a constructed fixture")));
            AssertKept(thrown, construction, cleanup, camera, folder);

            camera = null;
            folder = null;
            thrown = Assert.Catch(() => WithFixture(() => new Fixture(UnlitPath, 1, cleanupFault: fault), fixture =>
            {
                camera = fixture.Camera;
                folder = fixture.Folder;
            }));
            Assert.That(thrown, Is.TypeOf<AggregateException>());
            Assert.That(((AggregateException)thrown).InnerExceptions, Does.Contain(cleanup));
            AssertReleased(camera, folder);
        }

        private static void AssertKept(Exception thrown, Exception primary, Exception cleanup, Camera camera, string folder)
        {
            Assert.That(thrown, Is.SameAs(primary));
            Assert.That(thrown.Data[CleanupKey], Is.TypeOf<AggregateException>());
            Assert.That(((AggregateException)thrown.Data[CleanupKey]).InnerExceptions, Does.Contain(cleanup));
            AssertReleased(camera, folder);
        }

        private static void AssertReleased(Camera camera, string folder)
        {
            Assert.That(ReferenceEquals(camera, null), Is.False, "the fixture was observed");
            Assert.That(camera == null, Is.True, "owned objects destroyed despite the cleanup failure");
            Assert.That(AssetDatabase.IsValidFolder(folder), Is.False, "owned control folder removed despite the cleanup failure");
        }

        private const string UnlitPath = "Assets/Shaders/Recovered/HL_Env_Standard_Unlit.shader";
        private const string MipBias = "_GlobalMipBias";
        private const string CleanupKey = "FixtureCleanup";

        // Runs body over a new fixture and disposes it. A disposal failure is thrown only when nothing failed
        // before it; otherwise it is attached to the earlier failure, which propagates unchanged.
        private static void WithFixture(Func<Fixture> create, Action<Fixture> body)
        {
            Fixture fixture = create();
            Exception primary = null;
            try { body(fixture); }
            catch (Exception failure) { primary = failure; throw; }
            finally { DisposeAfter(fixture, primary); }
        }

        private static void DisposeAfter(IDisposable owned, Exception primary)
        {
            if (primary == null)
            {
                owned.Dispose();
                return;
            }
            try { owned.Dispose(); }
            catch (Exception cleanup) { primary.Data[CleanupKey] = cleanup; }
        }

        private static void Check(string path, string lightMode, int fragmentCount)
        {
            WithFixture(() => new Fixture(path, fragmentCount), fixture =>
            {
                // Production shader, instanced: each renderer's own mask.
                fixture.Render(fixture.Production, lightMode, true, out float left, out float right);
                Assert.That(left, Is.EqualTo(LeftMask / (float)MaxInt).Within(1e-5f), "left instance mask");
                Assert.That(right, Is.EqualTo(RightMask / (float)MaxInt).Within(1e-5f), "right instance mask");

                // Pinned to index 0, one instanced draw reports the first renderer's mask for both.
                fixture.Render(fixture.FirstInstance, lightMode, true, out left, out right);
                Assert.That(left, Is.EqualTo(LeftMask / (float)MaxInt).Within(1e-5f), "control left instance mask");
                Assert.That(right, Is.EqualTo(LeftMask / (float)MaxInt).Within(1e-5f),
                    "the control pinned to index 0 shares the first mask, so one instanced draw carried both renderers");

                // The same control drawn per renderer reads each mask.
                fixture.Render(fixture.FirstInstance, lightMode, false, out left, out right);
                Assert.That(left, Is.EqualTo(LeftMask / (float)MaxInt).Within(1e-5f), "left per-draw mask");
                Assert.That(right, Is.EqualTo(RightMask / (float)MaxInt).Within(1e-5f), "right per-draw mask");
            });
        }

        private sealed class Fixture : IDisposable
        {
            public readonly Shader Production;
            public readonly Shader FirstInstance;
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly string folder;
            private readonly Scene scene;
            private readonly Camera camera;
            private readonly Material material;
            private readonly MeshRenderer leftRenderer, rightRenderer;
            private readonly RenderTexture color, layers, depth;
            private readonly Texture2D sample;
            private readonly ProbePipelineAsset pipeline;
            private Shader drawShader;
            private string drawLightMode;
            private bool drawInstanced;
            private int invocations;
            private Exception renderFailure;
            private readonly Action cleanupFault;

            // Test injection: runs after the draw's command buffers have executed and been submitted.
            public Action AfterDraw;
            public Camera Camera => camera;
            public string Folder => folder;

            public Fixture(string path, int fragmentCount, Action<Fixture> constructionFault = null, Action cleanupFault = null)
            {
                this.cleanupFault = cleanupFault;
                try
                {
                    Production = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    Assert.That(Production, Is.Not.Null, path);

                    // Each fragment that writes outRenderingLayers sets up its instance ID first.
                    string source = File.ReadAllText(path).Replace("\r\n", "\n");
                    var setup = new Regex(@"(void Frag\(Varyings input,[^{]*outRenderingLayers[^{]*\{\n)(\s*)UNITY_SETUP_INSTANCE_ID\(input\);\n");
                    Assert.That(setup.Matches(source).Count, Is.EqualTo(fragmentCount), "layer-writing fragments in " + path);
                    string candidate = "Assets/RenderingLayerInstancing_" + Guid.NewGuid().ToString("N");
                    Assert.That(AssetDatabase.IsValidFolder(candidate) || File.Exists(candidate + ".meta"), Is.False);
                    Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(candidate)), Is.Not.Empty);
                    folder = candidate;
                    string guid = Guid.NewGuid().ToString("N");
                    Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Empty);
                    string copy = folder + "/Control.shader", name = "Hidden/ProjectLucid/RenderingLayerInstancing/" + guid;
                    string control = setup.Replace(source, "$1$2// Control: no instance ID setup, so index 0.\n");
                    control = control.Replace("Shader \"" + Production.name + "\"", "Shader \"" + name + "\"");
                    File.WriteAllText(copy, control, new UTF8Encoding(false));
                    File.WriteAllText(copy + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", new UTF8Encoding(false));
                    AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    FirstInstance = AssetDatabase.LoadAssetAtPath<Shader>(copy);
                    Assert.That(FirstInstance, Is.Not.Null);
                    Assert.That(FirstInstance.name, Is.EqualTo(name));
                    foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(FirstInstance))
                        Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);

                    scene = EditorSceneManager.NewPreviewScene();
                    Assert.That(scene.IsValid(), Is.True);
                    color = Own(new RenderTexture(32, 16, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                    layers = Own(new RenderTexture(32, 16, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                    depth = Own(new RenderTexture(32, 16, 24, RenderTextureFormat.Depth));
                    Assert.That(color.Create() && layers.Create() && depth.Create(), Is.True);
                    sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));

                    var cameraObject = Own(new GameObject("RenderingLayerInstancingCamera"));
                    SceneManager.MoveGameObjectToScene(cameraObject, scene);
                    camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.scene = scene;
                    camera.cameraType = CameraType.Preview;
                    camera.targetTexture = color;
                    camera.orthographic = true;
                    camera.orthographicSize = 1;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 10;
                    camera.useOcclusionCulling = false;
                    camera.cullingMask = 1 << 31;
                    camera.transform.position = new Vector3(0, 0, -2);

                    // One quad per screen half: the left half is the left renderer, the right half the right one.
                    var mesh = Own(new Mesh());
                    mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                    mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                    mesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                    mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                    mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                    mesh.RecalculateBounds();
                    material = Own(new Material(Production) { enableInstancing = true });
                    leftRenderer = Quad(mesh, -1, LeftMask);
                    rightRenderer = Quad(mesh, 1, RightMask);

                    pipeline = Own(ScriptableObject.CreateInstance<ProbePipelineAsset>());
                    pipeline.Owner = this;
                    constructionFault?.Invoke(this);
                }
                catch (Exception failure)
                {
                    DisposeAfter(this, failure);
                    throw;
                }
            }

            private MeshRenderer Quad(Mesh mesh, float x, uint mask)
            {
                var item = Own(new GameObject("RenderingLayerInstancingQuad"));
                SceneManager.MoveGameObjectToScene(item, scene);
                item.layer = 31;
                item.transform.position = new Vector3(x, 0, 0);
                item.transform.localScale = new Vector3(0.9f, 0.9f, 1);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderingLayerMask = mask;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Assert.That(renderer.renderingLayerMask, Is.EqualTo(mask));
                return renderer;
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }


            public void Render(Shader shader, string lightMode, bool instanced, out float left, out float right)
            {
                material.shader = shader;
                material.enableInstancing = true;
                Assert.That(leftRenderer.sharedMaterial == material && rightRenderer.sharedMaterial == material, Is.True);
                drawShader = shader;
                drawLightMode = lightMode;
                drawInstanced = instanced;
                invocations = 0;
                renderFailure = null;
                RenderPipelineAsset graphics = GraphicsSettings.renderPipelineAsset;
                RenderPipelineAsset quality = QualitySettings.renderPipeline;
                SupportedRenderingFeatures features = SupportedRenderingFeatures.active;
                bool allowScriptableRenderPipeline = Unsupported.useScriptableRenderPipeline;
                bool batcher = GraphicsSettings.useScriptableRenderPipelineBatching;
                bool writeLayers = Shader.IsKeywordEnabled("_WRITE_RENDERING_LAYERS");
                int maxInt = Shader.GetGlobalInt("_RenderingLayerMaxInt");
                float rcpMaxInt = Shader.GetGlobalFloat("_RenderingLayerRcpMaxInt");
                Vector4 mipBias = Shader.GetGlobalVector(MipBias);
                string tag = Shader.globalRenderPipeline;
                RenderTexture active = RenderTexture.active;
                Exception primary = null;
                try
                {
                    GraphicsSettings.renderPipelineAsset = pipeline;
                    QualitySettings.renderPipeline = pipeline;
                    Unsupported.useScriptableRenderPipeline = true;
                    // The SRP Batcher would take these renderers before GPU instancing could.
                    GraphicsSettings.useScriptableRenderPipelineBatching = false;
                    camera.Render();
                    if (renderFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(renderFailure).Throw();
                    Assert.That(invocations, Is.EqualTo(1), "Camera.Render invokes the owned pipeline once");
                    left = Read(8);
                    right = Read(24);
                }
                catch (Exception failure) { primary = failure; throw; }
                finally
                {
                    var failures = new List<Exception>();
                    void Restore(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                    Restore(() => QualitySettings.renderPipeline = quality);
                    Restore(() => GraphicsSettings.renderPipelineAsset = graphics);
                    Restore(() => pipeline.ReleaseOwnedPipeline());
                    Restore(() => SupportedRenderingFeatures.active = features);
                    Restore(() => Unsupported.useScriptableRenderPipeline = allowScriptableRenderPipeline);
                    Restore(() => GraphicsSettings.useScriptableRenderPipelineBatching = batcher);
                    Restore(() => { if (writeLayers) Shader.EnableKeyword("_WRITE_RENDERING_LAYERS"); else Shader.DisableKeyword("_WRITE_RENDERING_LAYERS"); });
                    Restore(() => Shader.SetGlobalInt("_RenderingLayerMaxInt", maxInt));
                    Restore(() => Shader.SetGlobalFloat("_RenderingLayerRcpMaxInt", rcpMaxInt));
                    Restore(() => Shader.SetGlobalVector(MipBias, mipBias));
                    Restore(() => Shader.globalRenderPipeline = tag);
                    Restore(() => RenderTexture.active = active);
                    if (failures.Count != 0)
                    {
                        var restoration = new AggregateException("Owned pipeline restoration failed.", failures);
                        if (primary != null) primary.Data["OwnedPipelineRestoration"] = restoration;
                        else throw restoration;
                    }
                }
            }

            // The rendering-layer target at the vertical centre of one screen column.
            private float Read(int x)
            {
                RenderTexture.active = layers;
                sample.ReadPixels(new Rect(x, 8, 1, 1), 0, 0);
                sample.Apply(false, false);
                return sample.GetPixel(0, 0).r;
            }

            private void RenderOwned(ScriptableRenderContext context, Camera[] cameras)
            {
                try
                {
                    Assert.That(cameras, Has.Length.EqualTo(1));
                    Assert.That(ReferenceEquals(cameras[0], camera), Is.True);
                    invocations++;
                    Assert.That(material.shader, Is.SameAs(drawShader));
                    Assert.That(camera.TryGetCullingParameters(out ScriptableCullingParameters parameters), Is.True);
                    CullingResults culled = context.Cull(ref parameters);
                    context.SetupCameraProperties(camera);
                    using (var commands = new CommandBuffer { name = "RenderingLayerInstancing targets" })
                    {
                        commands.SetRenderTarget(new RenderTargetIdentifier[] { color, layers }, depth);
                        commands.ClearRenderTarget(true, true, new Color(-1, -1, -1, -1));
                        commands.EnableShaderKeyword("_WRITE_RENDERING_LAYERS");
                        commands.SetGlobalInt("_RenderingLayerMaxInt", MaxInt);
                        commands.SetGlobalFloat("_RenderingLayerRcpMaxInt", 1f / MaxInt);
                        commands.SetGlobalVector(MipBias, Vector4.zero);
                        context.ExecuteCommandBuffer(commands);
                    }
                    var drawing = new DrawingSettings(new ShaderTagId(drawLightMode), new SortingSettings(camera))
                    {
                        enableDynamicBatching = false,
                        enableInstancing = drawInstanced,
                    };
                    var filtering = new FilteringSettings(RenderQueueRange.all, 1 << 31);
                    context.DrawRenderers(culled, ref drawing, ref filtering);
                    context.Submit();
                    AfterDraw?.Invoke();
                }
                catch (Exception failure) { renderFailure = failure; }
            }

            public void Dispose()
            {
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                if (cleanupFault != null) Attempt(cleanupFault);
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index];
                    if (item is RenderTexture texture) Attempt(() => texture.Release());
                    Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                if (scene.IsValid()) Attempt(() => EditorSceneManager.ClosePreviewScene(scene));
                if (folder != null) Attempt(() => Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "owned control folder removed"));
                if (failures.Count != 0) throw new AggregateException("RenderingLayerInstancing cleanup failed.", failures);
            }

            private sealed class ProbePipelineAsset : RenderPipelineAsset
            {
                public Fixture Owner;
                public override string renderPipelineShaderTag => "UniversalPipeline";
                protected override RenderPipeline CreatePipeline() { return new ProbePipeline(Owner); }
                public void ReleaseOwnedPipeline()
                {
                    if (RenderPipelineManager.currentPipeline is ProbePipeline current && ReferenceEquals(current.Owner, Owner))
                        base.OnDisable();
                }
                protected override void OnDisable() { ReleaseOwnedPipeline(); }
            }

            private sealed class ProbePipeline : RenderPipeline
            {
                public readonly Fixture Owner;
                public ProbePipeline(Fixture owner)
                {
                    Owner = owner;
                    Shader.globalRenderPipeline = "UniversalPipeline";
                }
                protected override void Render(ScriptableRenderContext context, Camera[] cameras) => Owner.RenderOwned(context, cameras);
            }
        }
    }
}
