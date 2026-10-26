using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectLucid.Tests
{
    // HL_Env_Zone1_Cliffs' bitangent sign under a mirrored world transform (#538). The original Forward and
    // DepthNormals fragments take the strict sign of the interpolated tangent w (w > 0 ? 1 : -1) and multiply it by
    // the world transform's sign separately. Only a real renderer supplies that per-draw sign
    // (unity_WorldTransformParams.w), so a MeshRenderer in an owned preview scene is drawn by an owned pipeline
    // through Camera.Render, DepthNormals only, into a float target that is read back.
    //
    // The edge normal map gives the tangent-space normal (0, 0.6, 0.8), mixing Y and Z, so the output is
    // 0.8 N + 0.6 B with N (0, 0, -1) and B = sign * cross(N, T): T +X unmirrored gives cross (0, -1, 0); under the
    // (-1, 1, 1) mirror T is -X and cross is (0, 1, 0). Zero w under the mirror separates the original order
    // (strict sign -1, times -1: +1) from combining first (sign of 0 * -1 = 0: -1).
    public sealed class CliffsMirroredRendererTests
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone1_Cliffs.shader";
        private static readonly Vector3 Down = new Vector3(0, -0.6f, -0.8f), Up = new Vector3(0, 0.6f, -0.8f);

        [Test]
        public void DepthNormalsTakeTheStrictTangentSignTimesTheRendererTransformSign()
        {
            using (var fixture = new Fixture())
            {
                // Unmirrored controls.
                Check(fixture, 1, false, Down, "unmirrored, w 1");
                Check(fixture, 0.5f, false, Down, "unmirrored, w 0.5");
                Check(fixture, 0, false, Up, "unmirrored, w 0: the strict sign is -1");
                // Mirrored by the renderer's own transform.
                Check(fixture, 1, true, Down, "mirrored, w 1: 1 * -1 with cross (0, 1, 0)");
                Check(fixture, 0.5f, true, Down, "mirrored, w 0.5");
                Check(fixture, 0, true, Up, "mirrored, w 0: (-1) * (-1), not the sign of 0 * -1");
            }
        }

        private static void Check(Fixture fixture, float w, bool mirrored, Vector3 expected, string label)
        {
            Vector4 normal = fixture.Render(w, mirrored);
            Assert.That(new Vector3(normal.x, normal.y, normal.z), Is.EqualTo(expected).Using(new Vector3Within(1e-3f)), label + ": read " + normal);
            Assert.That(normal.w, Is.EqualTo(0).Within(1e-5f), label + ": DepthNormals alpha");
        }

        private sealed class Vector3Within : IEqualityComparer<Vector3>
        {
            private readonly float tolerance;
            public Vector3Within(float tolerance) { this.tolerance = tolerance; }
            public bool Equals(Vector3 a, Vector3 b) => (a - b).magnitude <= tolerance;
            public int GetHashCode(Vector3 v) => 0;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly Scene scene;
            private readonly Camera camera;
            private readonly Mesh mesh;
            private readonly MeshRenderer renderer;
            private readonly RenderTexture color, depth;
            private readonly Texture2D sample;
            private readonly ProbePipelineAsset pipeline;
            private int invocations;
            private Exception renderFailure;

            public Fixture()
            {
                try
                {
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                    Assert.That(shader, Is.Not.Null, ShaderPath);
                    scene = EditorSceneManager.NewPreviewScene();
                    Assert.That(scene.IsValid(), Is.True);
                    color = Own(new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                    depth = Own(new RenderTexture(32, 32, 24, RenderTextureFormat.Depth));
                    Assert.That(color.Create() && depth.Create(), Is.True);
                    sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));

                    var cameraObject = Own(new GameObject("CliffsMirroredCamera"));
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

                    // A quad facing the camera, normal -Z, tangent +X; UV 0 keeps the top blend off.
                    mesh = Own(new Mesh());
                    mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                    mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                    mesh.uv = new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
                    mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                    mesh.RecalculateBounds();

                    // Main normal flat, edge normal (0, 0.6, 0.8) under a full edge mask, top blend pushed off.
                    var material = Own(new Material(shader));
                    material.SetTexture("_Main_Normal_Tex", Texel(new Color(1, 0.5f, 0.5f, 0.5f)));
                    material.SetTexture("_Edge_Normal_Tex", Texel(new Color(1, 0.8f, 0.5f, 0.5f)));
                    material.SetTexture("_Edge_DiffA_Tex", Texel(new Color(0, 0, 1, 1)));
                    material.SetTexture("_Top_Normal_Tex", Texel(new Color(1, 0.5f, 0.5f, 0.5f)));
                    material.SetFloat("_Top_Edge_Blend_Amount", -2);

                    var item = Own(new GameObject("CliffsMirroredQuad"));
                    SceneManager.MoveGameObjectToScene(item, scene);
                    item.layer = 31;
                    item.AddComponent<MeshFilter>().sharedMesh = mesh;
                    renderer = item.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                    pipeline = Own(ScriptableObject.CreateInstance<ProbePipelineAsset>());
                    pipeline.Owner = this;
                }
                catch (Exception failure)
                {
                    try { Dispose(); }
                    catch (Exception cleanup) { failure.Data["FixtureCleanup"] = cleanup; }
                    throw;
                }
            }

            private Texture2D Texel(Color value)
            {
                var texture = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point });
                texture.SetPixel(0, 0, value);
                texture.Apply(false, false);
                return texture;
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }

            // Draws the quad with every vertex tangent (1, 0, 0, w), mirrored or not by its own transform, and
            // returns the DepthNormals output at the screen centre.
            public Vector4 Render(float w, bool mirrored)
            {
                var tangent = new Vector4(1, 0, 0, w);
                mesh.tangents = new[] { tangent, tangent, tangent, tangent };
                renderer.transform.localScale = new Vector3(mirrored ? -0.9f : 0.9f, 0.9f, 1);
                Assert.That(renderer.transform.localToWorldMatrix.determinant < 0, Is.EqualTo(mirrored));
                invocations = 0;
                renderFailure = null;
                RenderPipelineAsset graphics = GraphicsSettings.renderPipelineAsset;
                RenderPipelineAsset quality = QualitySettings.renderPipeline;
                SupportedRenderingFeatures features = SupportedRenderingFeatures.active;
                bool allowScriptableRenderPipeline = Unsupported.useScriptableRenderPipeline;
                string tag = Shader.globalRenderPipeline;
                Vector4 mipBias = Shader.GetGlobalVector("_GlobalMipBias");
                RenderTexture active = RenderTexture.active;
                Exception primary = null;
                try
                {
                    GraphicsSettings.renderPipelineAsset = pipeline;
                    QualitySettings.renderPipeline = pipeline;
                    Unsupported.useScriptableRenderPipeline = true;
                    camera.Render();
                    if (renderFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(renderFailure).Throw();
                    Assert.That(invocations, Is.EqualTo(1), "Camera.Render invokes the owned pipeline once");
                    RenderTexture.active = color;
                    sample.ReadPixels(new Rect(16, 16, 1, 1), 0, 0);
                    sample.Apply(false, false);
                    Color read = sample.GetPixel(0, 0);
                    return new Vector4(read.r, read.g, read.b, read.a);
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
                    Restore(() => Shader.globalRenderPipeline = tag);
                    Restore(() => Shader.SetGlobalVector("_GlobalMipBias", mipBias));
                    Restore(() => RenderTexture.active = active);
                    if (failures.Count != 0)
                    {
                        var restoration = new AggregateException("Owned pipeline restoration failed.", failures);
                        if (primary != null) primary.Data["OwnedPipelineRestoration"] = restoration;
                        else throw restoration;
                    }
                }
            }

            private void RenderOwned(ScriptableRenderContext context, Camera[] cameras)
            {
                try
                {
                    Assert.That(cameras, Has.Length.EqualTo(1));
                    Assert.That(ReferenceEquals(cameras[0], camera), Is.True);
                    invocations++;
                    Assert.That(camera.TryGetCullingParameters(out ScriptableCullingParameters parameters), Is.True);
                    CullingResults culled = context.Cull(ref parameters);
                    context.SetupCameraProperties(camera);
                    using (var commands = new CommandBuffer { name = "CliffsMirrored target" })
                    {
                        commands.SetRenderTarget(color, depth);
                        commands.ClearRenderTarget(true, true, new Color(0, 0, 0, 1));
                        commands.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                        context.ExecuteCommandBuffer(commands);
                    }
                    var drawing = new DrawingSettings(new ShaderTagId("DepthNormals"), new SortingSettings(camera));
                    var filtering = new FilteringSettings(RenderQueueRange.all, 1 << 31);
                    context.DrawRenderers(culled, ref drawing, ref filtering);
                    context.Submit();
                }
                catch (Exception failure) { renderFailure = failure; }
            }

            public void Dispose()
            {
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index];
                    if (item is RenderTexture texture) Attempt(() => texture.Release());
                    Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                if (scene.IsValid()) Attempt(() => EditorSceneManager.ClosePreviewScene(scene));
                if (failures.Count != 0) throw new AggregateException("CliffsMirrored cleanup failed.", failures);
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
