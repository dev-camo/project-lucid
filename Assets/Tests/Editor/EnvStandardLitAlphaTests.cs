using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Original Metal fragment 1757, SHA256 0ad19c72103e9b4ed33d4dc20507ec16e118f27029729979c8c39c8f1e105e3d:
    // lines 236–256 sharpen/clip alpha; line 489 exports it when alpha-to-coverage is available.
    public sealed class EnvStandardLitAlphaTests
    {
        [Test]
        public void ConstantAlphaTestOutputMatchesOriginalWithAndWithoutCoverageAvailability()
        {
            Run(1, rig =>
            {
                foreach (float available in new[] { 0f, 1f })
                {
                    Shader.SetGlobalFloat("_AlphaToMaskAvailable", available);
                    rig.SetAlpha(.25f, 0f);
                    AssertColor(rig.Draw(12), new Color(.5f, 0, 0, 1), "zero cutoff, availability " + available);
                    rig.SetAlpha(.75f, .5f);
                    AssertColor(rig.Draw(12), new Color(.5f, 0, 0, 1), "constant above cutoff, availability " + available);
                }
            });
        }

        [Test]
        public void InteriorAlphaGradientKeepsOriginalSharpeningAndRejection()
        {
            Run(1, rig =>
            {
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                rig.SetGradient();
                // The original inline linear-repeat sampler reads between the two texel centers here.
                // Across 32 pixels alpha=2u-.5: x12 gives .28125, fwidth=.0625. With cutoff=.28125,
                // saturate((alpha-cutoff-.5*fwidth)/fwidth+1) is .5; x11 clips and x13 saturates to 1.
                AssertColor(rig.Draw(12), new Color(.5f, 0, 0, .5f), "fractional sharpened alpha");
                AssertColor(rig.Draw(11), Color.clear, "zero sharpened alpha clips");
                AssertColor(rig.Draw(13), new Color(.5f, 0, 0, 1), "upper neighbor saturates");
            });
        }

        [Test]
        public void EnabledMsaaCoverageUsesOriginalCutoffZeroAlpha()
        {
            Run(4, rig =>
            {
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                rig.SetAlpha(.5f, 0);
                rig.Material.DisableKeyword("_ALPHATEST_ON");
                rig.Material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                AssertColor(rig.Draw(12), new Color(.5f, 0, 0, .5f), "unmasked full draw");

                rig.Material.SetFloat("_AlphaToMask", 1);
                Color partial = rig.Draw(12);
                // Replacement blending and a clear target make resolved red exactly the covered
                // sample fraction times emission. The vendor chooses which/count of partial samples.
                float covered = partial.r / .5f * 4;
                int count = Mathf.RoundToInt(covered);
                Assert.That(count, Is.InRange(1, 3), "alpha-to-coverage must actually mask some samples");
                Assert.That(covered, Is.EqualTo(count).Within(.01f), "resolved coverage must be a whole number of four samples");
                AssertColor(partial, new Color(.5f * count / 4, 0, 0, .5f * count / 4), "partial coverage control");

                rig.Material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                rig.Material.EnableKeyword("_ALPHATEST_ON");
                AssertColor(rig.Draw(12), new Color(.5f, 0, 0, 1), "zero cutoff forces all four samples covered");
                rig.SetAlpha(0, .5f);
                AssertColor(rig.Draw(12), Color.clear, "rejected fragment covers no samples");
            });
        }

        private static void Run(int samples, Action<Rig> verify)
        {
            var rig = new Rig(samples);
            Exception primary = null;
            try { verify(rig); }
            catch (Exception failure) { primary = failure; throw; }
            finally { rig.Release(primary); }
        }

        private static void AssertColor(Color actual, Color expected, string label)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.002f), label + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.002f), label + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.002f), label + " blue");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.002f), label + " alpha");
        }

        private sealed class Rig
        {
            private readonly List<Object> owned = new List<Object>();
            private readonly string priorPipeline = Shader.globalRenderPipeline;
            private readonly float priorAvailability = Shader.GetGlobalFloat("_AlphaToMaskAvailable");
            private readonly Vector4 priorMipBias = Shader.GetGlobalVector("_GlobalMipBias");
            private readonly RenderTexture priorTarget = RenderTexture.active;
            private readonly bool priorAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            private RenderTexture target, resolved;
            private Texture2D diffuse, readback;
            private Mesh mesh;
            private Camera camera;
            public Material Material { get; private set; }

            public Rig(int samples)
            {
                try
                {
                    Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "requires a graphics device");
                    Assert.That(SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat), Is.True, "linear float texture/readback support");
                    ShaderUtil.allowAsyncCompilation = false;
                    Shader.globalRenderPipeline = "UniversalPipeline";
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Recovered/HL_Env_Standard_Lit.shader");
                    Assert.That(shader, Is.Not.Null);
                    Assert.That(shader.isSupported, Is.True);
                    Material = Own(new Material(shader));
                    Material.shaderKeywords = new[] { "_ALPHATEST_ON" };
                    Material.SetFloat("_SrcBlend", (float)BlendMode.One);
                    Material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    Material.SetFloat("_AlphaToMask", 0);
                    Material.SetFloat("_Alpha_Contrast", 1);
                    Material.SetFloat("_Cull", (float)CullMode.Off);
                    Material.SetColor("_Diff_Tint", Color.black);
                    Material.SetFloat("_Metal_Intensity", 1);
                    Material.SetTexture("_MSEA_Tex", Texture2D.whiteTexture);
                    Material.SetTexture("_Emissive_Tex", Texture2D.whiteTexture);
                    Material.SetColor("_Emissive_Colour", new Color(.5f, 0, 0, 1));
                    Material.SetTextureScale("_Diff_Tex", Vector2.one);
                    Material.SetTextureOffset("_Diff_Tex", Vector2.zero);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                    diffuse = Own(new Texture2D(2, 1, TextureFormat.RGBAFloat, false, true));
                    Assert.That(SystemInfo.IsFormatSupported(diffuse.graphicsFormat, FormatUsage.Linear), Is.True,
                        "the gradient requires actual linear filtering support");
                    diffuse.filterMode = FilterMode.Bilinear;
                    diffuse.wrapMode = TextureWrapMode.Repeat;
                    Material.SetTexture("_Diff_Tex", diffuse);
                    readback = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));

                    mesh = Own(new Mesh());
                    mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                    mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                    mesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                    mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    mesh.uv2 = mesh.uv;
                    mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                    mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                    mesh.RecalculateBounds();
                    GameObject cameraObject = Own(new GameObject("EnvAlphaVerificationCamera"));
                    cameraObject.SetActive(false);
                    camera = cameraObject.AddComponent<Camera>();
                    camera.enabled = false;
                    camera.orthographic = true;
                    camera.orthographicSize = 1;
                    camera.aspect = 1;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 10;
                    camera.transform.position = new Vector3(0, 0, -2);

                    target = CreateTarget(samples, 24);
                    if (samples > 1) resolved = CreateTarget(1, 0);
                }
                catch (Exception failure) { Release(failure); throw; }
            }

            private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

            private RenderTexture CreateTarget(int samples, int depth)
            {
                const GraphicsFormat format = GraphicsFormat.R16G16B16A16_SFloat;
                Assert.That(SystemInfo.IsFormatSupported(format, FormatUsage.Render), Is.True, "linear floating render format");
                var descriptor = new RenderTextureDescriptor(32, 32, format, depth)
                { msaaSamples = samples, bindMS = samples > 1, useMipMap = false, autoGenerateMips = false };
                Assert.That(SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor), Is.EqualTo(samples), "requested sample count must be supported without fallback");
                RenderTexture result = Own(new RenderTexture(descriptor));
                Assert.That(result.Create(), Is.True);
                Assert.That(result.IsCreated(), Is.True);
                Assert.That(result.antiAliasing, Is.EqualTo(samples), "created sample count");
                Assert.That(result.graphicsFormat, Is.EqualTo(format), "created floating format");
                Assert.That(result.sRGB, Is.False, "linear target");
                Assert.That(result.bindTextureMS, Is.EqualTo(samples > 1), "explicit MSAA resolve ownership");
                return result;
            }

            public void SetAlpha(float alpha, float cutoff)
            {
                diffuse.SetPixels(new[] { new Color(0, 0, 0, alpha), new Color(0, 0, 0, alpha) });
                diffuse.Apply(false, false);
                Material.SetFloat("_Alpha_Clip", cutoff);
            }

            public void SetGradient()
            {
                diffuse.SetPixels(new[] { Color.clear, new Color(0, 0, 0, 1) });
                diffuse.Apply(false, false);
                Material.SetFloat("_Alpha_Clip", .28125f);
            }

            public Color Draw(int x)
            {
                RenderTexture before = RenderTexture.active;
                try
                {
                    Graphics.SetRenderTarget(target);
                    GL.Clear(true, true, Color.clear);
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadProjectionMatrix(camera.projectionMatrix);
                        GL.modelview = camera.worldToCameraMatrix;
                        int pass = Material.FindPass("Universal Forward");
                        Assert.That(pass, Is.GreaterThanOrEqualTo(0));
                        Assert.That(Material.SetPass(pass), Is.True, "production forward pass binding");
                        Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
                    }
                    finally { GL.PopMatrix(); }
                    if (resolved != null)
                    {
                        Graphics.SetRenderTarget(null);
                        target.ResolveAntiAliasedSurface(resolved);
                        RenderTexture.active = resolved;
                    }
                    readback.ReadPixels(new Rect(x, 16, 1, 1), 0, 0, false);
                    readback.Apply(false, false);
                    return readback.GetPixel(0, 0);
                }
                finally { RenderTexture.active = before; }
            }

            public void Release(Exception primary)
            {
                var failures = new List<Exception>();
                void Attempt(Action action)
                {
                    try { action(); }
                    catch (Exception failure) { failures.Add(failure); }
                }
                Attempt(() => Shader.SetGlobalFloat("_AlphaToMaskAvailable", priorAvailability));
                Attempt(() => Shader.SetGlobalVector("_GlobalMipBias", priorMipBias));
                Attempt(() => Shader.globalRenderPipeline = priorPipeline);
                Attempt(() => ShaderUtil.allowAsyncCompilation = priorAsyncCompilation);
                Attempt(() => RenderTexture.active = priorTarget);
                for (int i = owned.Count - 1; i >= 0; i--)
                {
                    Object item = owned[i];
                    if (item is RenderTexture renderTexture) Attempt(() => renderTexture.Release());
                    Attempt(() => { if (item != null) Object.DestroyImmediate(item); });
                }
                owned.Clear();
                if (failures.Count != 0)
                {
                    if (primary != null) failures.Insert(0, primary);
                    throw new AggregateException("Environment alpha fixture cleanup failed.", failures);
                }
            }
        }
    }
}
