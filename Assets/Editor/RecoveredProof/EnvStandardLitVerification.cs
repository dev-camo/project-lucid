using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Standard_Lit, reconstructed from the shipped Metal programs.
    // The shipped bundles carry only Metal programs, so this proves the source compiles for
    // Direct3D and checks the graph's surface math on the GPU through the Forward pass's
    // emission (with a black albedo and full metal, so lighting contributes nothing) and the
    // Meta pass's albedo output. Mapped-normal fresnel has separate contract checks.
    public static class EnvStandardLitVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Standard_Lit.shader";
        // The shipped shader asset GUID in the 2026-10-05 baseline export, referenced by all 242 shipped materials.
        private const string OriginalGuid = "bf4c82e70ee177f45be870c94d234981";

        public static void Run()
        {
            string previous = Shader.globalRenderPipeline;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                RunUnderUniversalPipeline();
            }
            finally { Shader.globalRenderPipeline = previous; }
        }

        // Compiles every pass for Direct3D 11 with the keyword sets the shipped variants combine.
        public static void CompileForWindows()
        {
            Compile(ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, "Direct3D");
        }

        public static void CompileForLinux()
        {
            Compile(ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64, "OpenGLCore");
        }

        private static void Compile(ShaderCompilerPlatform platform, BuildTarget target, string label)
        {
            Shader shader = Load();
            ShaderData data = ShaderUtil.GetShaderData(shader);
            ShaderData.Subshader subshader = data.GetSerializedSubshader(0);
            var sets = new List<string[]>
            {
                new string[0],
                new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_REFLECTION_PROBE_BLENDING", "_LIGHT_LAYERS", "EVALUATE_SH_VERTEX" },
                new[] { "LIGHTMAP_ON", "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "_MAIN_LIGHT_SHADOWS", "FOG_LINEAR", "INSTANCING_ON" },
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "_ADDITIONAL_LIGHTS", "_WRITE_RENDERING_LAYERS" },
                new[] { "_ALPHATEST_ON", "_USE_SCROLLING_UVS", "_USE_FRESNEL", "_USE_DETAIL_MAPS", "_RECEIVE_SHADOWS_OFF", "_ADDITIONAL_LIGHTS" },
                new[] { "_FORWARD_PLUS", "_USE_DETAIL_MAPS", "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_DBUFFER_MRT3" },
                new[] { "DOTS_INSTANCING_ON", "_SHADOWS_SOFT_HIGH", "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS" },
                new[] { "_SHADOWS_SOFT_LOW", "_SURFACE_TYPE_TRANSPARENT", "_USE_FRESNEL" },
            };
            int compiled = 0;
            for (int p = 0; p < subshader.PassCount; p++)
            {
                ShaderData.Pass pass = subshader.GetPass(p);
                foreach (string[] keywords in sets)
                {
                    foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, keywords,
                            platform, target);
                        string errors = "";
                        foreach (ShaderMessage message in info.Messages)
                            if (message.severity == ShaderCompilerMessageSeverity.Error) errors += " " + message.message;
                        Require(info.Success && errors.Length == 0,
                            label + " compile failed: pass " + pass.Name + " " + stage + " [" + string.Join(" ", keywords) + "]" + errors);
                        compiled++;
                    }
                }
            }
            Debug.Log("Project Lucid HL_Env_Standard_Lit compiled " + compiled + " " + label + " programs across " + subshader.PassCount + " passes.");
        }

        private static Shader Load()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.name == "HardLight/Environment/HL_Env_Standard_Lit", "shader identity");
            Require(AssetDatabase.AssetPathToGUID(ShaderPath) == OriginalGuid, "original material binding GUID");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            // With prepared assets present, the shipped materials resolve to this source.
            string[] recovered = AssetDatabase.IsValidFolder("Assets/Recovered")
                ? AssetDatabase.FindAssets("t:Material", new[] { "Assets/Recovered" }) : new string[0];
            if (recovered.Length > 0)
            {
                int bound = 0;
                foreach (string guid in recovered)
                {
                    Material shipped = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (shipped != null && shipped.shader == shader) bound++;
                }
                Require(bound > 0, "no prepared material resolves to the recovered shader");
            }
            return shader;
        }

        private static void RunUnderUniversalPipeline()
        {
            Shader shader = Load();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");
            Require(shader.passCount == 5, "five shipped passes");

            var owned = new List<UnityEngine.Object>();
            float priorTime = Shader.GetGlobalFloat("_HLShaderTimeGame");
            Vector4 priorMipBias = Shader.GetGlobalVector("_GlobalMipBias");
            Vector4 priorMetaVertex = Shader.GetGlobalVector("unity_MetaVertexControl");
            Vector4 priorMetaFragment = Shader.GetGlobalVector("unity_MetaFragmentControl");
            float priorBoost = Shader.GetGlobalFloat("unity_OneOverOutputBoost");
            float priorMaxOutput = Shader.GetGlobalFloat("unity_MaxOutputValue");
            float priorLinear = Shader.GetGlobalFloat("unity_UseLinearSpace");
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null;
            Exception primaryFailure = null;
            try
            {
                Texture2D Solid(Color color)
                {
                    var texture = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true);
                    owned.Add(texture);
                    texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Repeat;
                    texture.SetPixels(new[] { color, color, color, color });
                    texture.Apply(false, false);
                    return texture;
                }
                // Texels 0..3 are red, red, green, green: a half-texture offset moves red to green.
                var stripes = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true);
                owned.Add(stripes);
                stripes.filterMode = FilterMode.Point; stripes.wrapMode = TextureWrapMode.Repeat;
                stripes.SetPixels(new[] { Color.red, Color.red, Color.green, Color.green });
                stripes.Apply(false, false);

                Material material = new Material(shader);
                owned.Add(material);
                Mesh mesh = new Mesh();
                owned.Add(mesh);
                mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                mesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                mesh.uv2 = mesh.uv;
                mesh.colors = new[] { Color.blue, Color.blue, Color.blue, Color.blue };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();

                target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                owned.Add(target);
                var sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                owned.Add(sample);
                var cameraObject = new GameObject("ShaderVerificationCamera");
                owned.Add(cameraObject);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 1;
                camera.aspect = 1;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10;
                camera.transform.position = new Vector3(0, 0, -2);

                target.Create();
                // Forward: black albedo and full metal leave only the emission.
                material.SetColor("_Diff_Tint", Color.black);
                material.SetFloat("_Metal_Intensity", 1);
                material.SetTexture("_MSEA_Tex", Solid(Color.white));
                material.SetColor("_Emissive_Colour", new Color(0.5f, 1, 1, 1));
                material.SetTexture("_Emissive_Tex", stripes);
                material.SetTexture("_Diff_Tex", Solid(new Color(0, 0, 0, 1)));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);

                // Pixel 4 of 32 samples uv.x 0.14 (texel 0, red); the emissive colour scales red by 0.5.
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "emission times colour");
                material.SetTextureOffset("_Diff_Tex", new Vector2(0.5f, 0));
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0, 1, 0), "Diff tiling offset moves the main UV");
                material.SetTextureOffset("_Diff_Tex", Vector2.zero);
                material.SetFloat("_UV_Scrolling_Horizontal", 0.5f);
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "scrolling is off without its keyword");
                material.EnableKeyword("_USE_SCROLLING_UVS");
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0, 1, 0), "game time times scroll speed offsets the UV");
                material.DisableKeyword("_USE_SCROLLING_UVS");

                // Alpha test: Diff alpha times contrast against the clip threshold.
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetTexture("_Diff_Tex", Solid(new Color(0, 0, 0, 0.3f)));
                material.SetFloat("_Alpha_Clip", 0.5f);
                material.SetFloat("_Alpha_Contrast", 1);
                Expect(material, 0, mesh, camera, target, sample, 4, Color.black, "0.3 alpha is clipped at 0.5");
                material.SetFloat("_Alpha_Contrast", 2);
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "contrast 2 lifts 0.3 to 0.6, above the clip");
                material.DisableKeyword("_ALPHATEST_ON");

                // Opaque writes alpha 1; transparent writes the saturated graph alpha.
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetFloat("_Alpha_Contrast", 1);
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "opaque alpha is 1", 1);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "transparent alpha is Diff alpha times contrast", 0.3f);
                material.SetFloat("_Alpha_Contrast", 20);
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "contrast 20 saturates the alpha to 1", 1);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_Alpha_Contrast", 1);

                // _GlobalMipBias applies once (URP's SAMPLE_TEXTURE2D), as in the original. Each texture spans its
                // UV 0..1 across the 32-pixel quad, so a 256 texture whose levels are coloured reads level 3
                // unbiased and level 4 at bias 1; applying the bias twice would read level 5.
                Texture2D Levels(Color unbiased, Color once, Color twice)
                {
                    var levels = new Texture2D(256, 256, TextureFormat.RGBAFloat, true, true);
                    owned.Add(levels);
                    levels.filterMode = FilterMode.Bilinear; levels.wrapMode = TextureWrapMode.Repeat;
                    for (int mip = 0; mip < levels.mipmapCount; mip++)
                    {
                        int width = Math.Max(1, 256 >> mip);
                        var pixels = new Color[width * width];
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = mip <= 3 ? unbiased : mip == 4 ? once : twice;
                        levels.SetPixels(pixels, mip);
                    }
                    levels.Apply(false, false);
                    return levels;
                }
                Vector4 bias1 = new Vector4(1, 0, 0, 0);
                material.SetTexture("_Emissive_Tex", Levels(Color.red, Color.green, Color.blue));
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "emission: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0, 1, 0), "emission: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Emissive_Tex", stripes);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetTexture("_Diff_Tex", Levels(new Color(0, 0, 0, 0.3f), new Color(0, 0, 0, 0.6f), new Color(0, 0, 0, 0.9f)));
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "Diff alpha: no bias reads level 3", 0.3f);
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                Expect(material, 0, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "Diff alpha: bias 1 applies once", 0.6f);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

                // Meta: the lightmapper's albedo output is Diff times tint, vertex tinted, times detail.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(1, 0, 0, 0));
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 97);
                material.SetTexture("_Diff_Tex", Solid(new Color(0.5f, 0.5f, 1, 1)));
                // _Diff_Tint is a non-HDR colour, so the linear-space project hands the shader 0.5 as
                // GammaToLinearSpace(0.5) = 0.214.
                material.SetColor("_Diff_Tint", new Color(1, 0.5f, 1, 1));
                float tintG = Mathf.GammaToLinearSpace(0.5f);
                int meta = material.FindPass("Meta");
                Require(meta >= 0, "Meta pass");
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.5f, 0.5f * tintG, 1), "Diff times tint");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 1);
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0, 0, 1), "vertex colour tint (blue)");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 0);
                material.SetTexture("_Detail_Diffuse_Tex", Solid(new Color(0.5f, 1, 0.5f, 1)));
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.5f, 0.5f * tintG, 1), "detail is off without its keyword");
                material.EnableKeyword("_USE_DETAIL_MAPS");
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.25f, 0.5f * tintG, 0.5f), "detail diffuse multiplies the albedo");

                // Single bias on the Meta pass's albedo paths: the detail diffuse, then the Diff itself.
                material.SetTexture("_Detail_Diffuse_Tex", Levels(new Color(0.5f, 1, 0.5f, 1), Color.white, Color.black));
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.25f, 0.5f * tintG, 0.5f), "detail: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.5f, 0.5f * tintG, 1), "detail: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.DisableKeyword("_USE_DETAIL_MAPS");
                material.SetTexture("_Diff_Tex", Levels(new Color(0.5f, 0.5f, 1, 1), Color.red, Color.green));
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.5f, 0.5f * tintG, 1), "Meta Diff: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(1, 0, 0), "Meta Diff: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                // And the Meta pass's emission output (linear space).
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(0, 1, 0, 0));
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                material.SetTexture("_Emissive_Tex", Levels(Color.red, Color.green, Color.blue));
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0.5f, 0, 0), "Meta emission: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                Expect(material, meta, mesh, camera, target, sample, 4, new Color(0, 1, 0), "Meta emission: bias 1 applies once");

                Debug.Log("Project Lucid HL_Env_Standard_Lit GPU verification passed: 23 Metal-derived cases on " + SystemInfo.graphicsDeviceType +
                          ". Fresnel and lit presentation QA remain unverified.");
            }
            catch (Exception failure) { primaryFailure = failure; throw; }
            finally
            {
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                Attempt(() => Shader.SetGlobalFloat("_HLShaderTimeGame", priorTime));
                Attempt(() => Shader.SetGlobalVector("_GlobalMipBias", priorMipBias));
                Attempt(() => Shader.SetGlobalVector("unity_MetaVertexControl", priorMetaVertex));
                Attempt(() => Shader.SetGlobalVector("unity_MetaFragmentControl", priorMetaFragment));
                Attempt(() => Shader.SetGlobalFloat("unity_OneOverOutputBoost", priorBoost));
                Attempt(() => Shader.SetGlobalFloat("unity_MaxOutputValue", priorMaxOutput));
                Attempt(() => Shader.SetGlobalFloat("unity_UseLinearSpace", priorLinear));
                Attempt(() => RenderTexture.active = previous);
                Attempt(() => { if (target != null) target.Release(); });
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index];
                    Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                if (failures.Count != 0)
                {
                    // Keep the triggering assertion alongside any cleanup failures.
                    if (primaryFailure != null) failures.Insert(0, primaryFailure);
                    throw new AggregateException("Environment surface verification cleanup failed.", failures);
                }
            }
        }

        private static void Expect(Material material, int pass, Mesh mesh, Camera camera, RenderTexture target, Texture2D sample,
                                   int x, Color expected, string label, float expectedAlpha = float.NaN)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            try
            {
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                Require(material.SetPass(pass), "pass did not bind: " + label);
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
            finally { GL.PopMatrix(); }
            sample.ReadPixels(new Rect(x, 16, 1, 1), 0, 0, false);
            sample.Apply(false);
            Color actual = sample.GetPixel(0, 0);
            Require(Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
                    Mathf.Abs(actual.b - expected.b) < 0.01f &&
                    (float.IsNaN(expectedAlpha) || Mathf.Abs(actual.a - expectedAlpha) < 0.01f),
                    label + ": expected=" + expected + " alpha " + expectedAlpha + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HL_Env_Standard_Lit verification failed: " + message);
        }
    }
}
