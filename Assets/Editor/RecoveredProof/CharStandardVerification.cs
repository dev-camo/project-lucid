using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Standard, reconstructed from the shipped Metal programs. The shipped
    // bundles carry only Metal programs, so this proves the source compiles for Direct3D and checks the
    // graph's surface math on the GPU: the forward emission (a black colour and full metal leave only
    // emission) and the meta pass's albedo.
    public static class CharStandardVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Standard.shader";
        private const string Erosion = "_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D";
        // The erosion texture the original importer binds immutably (2026-10-05 baseline GUID).
        private const string ErosionTextureGuid = "e8129d5371ff1e04d8070064644689a1";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "fb495c01c5fd7534590903b8e77f3281";
        private static Action<List<UnityEngine.Object>, string> resourceFaultProbe;

        public static void VerifyResourceRestorationAfterFailure(string phase)
        {
            Require(resourceFaultProbe == null, "another resource fault control is active");
            Require(phase == "setup" || phase == "body", "known resource fault phase");
            string pipeline = Shader.globalRenderPipeline;
            RenderTexture activeTarget = RenderTexture.active;
            string[] vectorNames = { "_MainLightPosition", "_WorldSpaceCameraPos", "unity_OrthoParams", "unity_MetaVertexControl", "unity_MetaFragmentControl", "_GlobalMipBias" };
            string[] floatNames = { "unity_OneOverOutputBoost", "unity_MaxOutputValue", "_AlphaToMaskAvailable" };
            var vectors = new Dictionary<string, Vector4>();
            var floats = new Dictionary<string, float>();
            foreach (string name in vectorNames) vectors.Add(name, Shader.GetGlobalVector(name));
            foreach (string name in floatNames) floats.Add(name, Shader.GetGlobalFloat(name));
            UnityEngine.Object[] observed = null;
            var expected = new InvalidOperationException("Owned character " + phase + " failure.");
            Exception primary = null;
            try
            {
                resourceFaultProbe = (objects, at) =>
                {
                    if (at != phase) return;
                    observed = objects.ToArray();
                    throw expected;
                };
                bool reached = false;
                try { Run(); }
                catch (Exception error)
                {
                    Require(ReferenceEquals(error, expected), "resource cleanup preserves the primary failure identity");
                    reached = true;
                }
                Require(reached && observed != null && observed.Length > 0, "the genuine Run reaches the requested resource fault");
                foreach (UnityEngine.Object item in observed) Require(item == null, "Run destroys every allocated owned object after " + phase);
                Require(Shader.globalRenderPipeline == pipeline && RenderTexture.active == activeTarget, "pipeline and active target restored after " + phase);
                foreach (var item in vectors) Require(Shader.GetGlobalVector(item.Key).Equals(item.Value), item.Key + " restored after " + phase);
                foreach (var item in floats) Require(Shader.GetGlobalFloat(item.Key).Equals(item.Value), item.Key + " restored after " + phase);
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                resourceFaultProbe = null;
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                // A failing negative control must not leave the allocation it just detected behind.
                if (observed != null)
                    foreach (UnityEngine.Object item in observed)
                        Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                foreach (var item in vectors) Attempt(() => Shader.SetGlobalVector(item.Key, item.Value));
                foreach (var item in floats) Attempt(() => Shader.SetGlobalFloat(item.Key, item.Value));
                Attempt(() => RenderTexture.active = activeTarget);
                Attempt(() => Shader.globalRenderPipeline = pipeline);
                if (failures.Count != 0)
                {
                    if (primary != null) failures.Insert(0, primary);
                    throw new AggregateException("Character resource fault-control cleanup failed.", failures);
                }
            }
        }

        public static void Run()
        {
            string previous = Shader.globalRenderPipeline;
            Exception primary = null;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                RunUnderUniversalPipeline();
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                try { Shader.globalRenderPipeline = previous; }
                catch (Exception cleanup)
                {
                    if (primary != null) throw new AggregateException("Character pipeline restoration failed.", primary, cleanup);
                    throw;
                }
            }
        }

        // Compiles every pass for Direct3D 11 with the keyword sets the shipped variants combine.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            var sets = new List<string[]>
            {
                new string[0],
                new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_REFLECTION_PROBE_BLENDING", "_LIGHT_LAYERS", "EVALUATE_SH_VERTEX" },
                new[] { "_ALPHATEST_ON", "_USE_MATCAP", "_USE_SUBSURFACE_SCATTERING", "_USE_BALLMODE", "_ADDITIONAL_LIGHTS", "_WRITE_RENDERING_LAYERS" },
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "FOG_LINEAR", "INSTANCING_ON", "_MAIN_LIGHT_SHADOWS" },
                new[] { "_FORWARD_PLUS", "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
                new[] { "DOTS_INSTANCING_ON", "_SHADOWS_SOFT_MEDIUM", "_SURFACE_TYPE_TRANSPARENT", "_USE_MATCAP" },
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
                            ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                        string errors = "";
                        foreach (ShaderMessage message in info.Messages)
                            if (message.severity == ShaderCompilerMessageSeverity.Error) errors += " " + message.message;
                        Require(info.Success && errors.Length == 0,
                            "D3D compile failed: pass " + pass.Name + " " + stage + " [" + string.Join(" ", keywords) + "]" + errors);
                        compiled++;
                    }
                }
            }
            Debug.Log("Project Lucid HL_Char_Standard compiled " + compiled + " Direct3D programs across " + subshader.PassCount + " passes.");
        }

        private static Shader Load()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.name == "HardLight/Character/HL_Char_Standard", "shader identity");
            Require(AssetDatabase.AssetPathToGUID(ShaderPath) == OriginalGuid, "original material binding GUID");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            return shader;
        }

        private static void RunUnderUniversalPipeline()
        {
            Shader shader = Load();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");
            Require(shader.passCount == 5, "five shipped passes");

            var owned = new List<UnityEngine.Object>();
            Texture2D Solid(Color color)
            {
                var texture = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true);
                owned.Add(texture);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply(false, false);
                return texture;
            }

            string[] vectors = { "_MainLightPosition", "_WorldSpaceCameraPos", "unity_OrthoParams", "unity_MetaVertexControl", "unity_MetaFragmentControl", "_GlobalMipBias" };
            string[] floats = { "unity_OneOverOutputBoost", "unity_MaxOutputValue", "_AlphaToMaskAvailable" };
            var prior = new Dictionary<string, Vector4>();
            var priorFloats = new Dictionary<string, float>();
            foreach (string name in vectors) prior[name] = Shader.GetGlobalVector(name);
            foreach (string name in floats) priorFloats[name] = Shader.GetGlobalFloat(name);
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null;
            Exception primary = null;
            try
            {
                Material material = new Material(shader);
                owned.Add(material);
                resourceFaultProbe?.Invoke(owned, "setup");
                Mesh mesh = new Mesh();
                owned.Add(mesh);
                mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                mesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                mesh.uv2 = mesh.uv;
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
                // A distant camera makes the view head-on everywhere; the main light points away from
                // the quad, so the half-Lambert term is 0 unless a case turns it toward the normal.
                Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0, 0, -10000, 0));
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, 1, 0));
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                // Forward: a black colour and full metal leave only the emission.
                material.SetColor("_Diff_Colour", new Color(0, 0, 0, 1));
                material.SetTexture("_MS_Texture", Solid(Color.white));
                material.SetTexture("_Diff_Texture", Solid(new Color(0.5f, 0.25f, 1, 1)));
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));
                material.SetTexture("_Emissive_Texture", Solid(Color.white));
                material.SetColor("_Emissive_Colour", new Color(0.2f, 0.4f, 0.6f, 1));
                material.SetTexture("_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D", Solid(Color.black));
                material.SetTexture("_Fresnel_Texture", Solid(Color.white));
                material.SetTexture("__Matcap_Texture__USE_MATCAP", Solid(Color.green));
                material.SetFloat("_Global_Emission_Intensity", 1);
                material.SetFloat("_Rim_Opacity", 0);
                material.SetFloat("_Erosion_Progress", 0);
                material.SetFloat("_SSS_Emissive_Intensity", 1);
                material.SetFloat("_Matcap_Emissive_Intensity", 1);

                resourceFaultProbe?.Invoke(owned, "body");

                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "head-on fresnel selects the emissive texture times colour", 1);
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0.6f, 0)));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "transparent alpha is the opacity mask", 0.6f);
                material.SetFloat("_Erosion_Progress", 0.6f);
                material.SetTexture("_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D", Solid(new Color(0, 0, 0.1f, 1)));
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "erosion 0.3 is subtracted from the opacity", 0.3f);
                material.SetFloat("_Erosion_Progress", 0);
                material.SetTexture("_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D", Solid(Color.black));
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));
                material.SetFloat("_Global_Emission_Intensity", 0.5f);
                Expect(material, 0, mesh, camera, target, sample, new Color(0.1f, 0.2f, 0.3f), "global emission intensity");
                material.SetFloat("_Global_Emission_Intensity", 1);

                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, -1, 0));
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "no subsurface emission without its keyword");
                // The subsurface emission weight is the skin mask times the SSS opacity and emissive intensity
                // (Forward 309/315 and Meta 735/737 overwrite the half-Lambert temporary with ID.r * opacity
                // before the blend). With the skin mask at 0, a fully lit half-Lambert leaves the emission
                // unchanged at either intensity.
                material.EnableKeyword("_USE_SUBSURFACE_SCATTERING");
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "skin mask 0 keeps the emission under a lit half-Lambert");
                material.SetFloat("_SSS_Emissive_Intensity", 0.5f);
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "skin mask 0 keeps it at half intensity too");
                material.SetFloat("_SSS_Emissive_Intensity", 1);
                // Separating controls: skin 1, opacity 1, intensity 1 and offset 0 (ramp 0), with the light behind
                // the quad (half-Lambert 0), still select the full subsurface colour, the diffuse.
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, 1, 0));
                material.SetTexture("_ID_Mask", Solid(new Color(1, 0, 0, 0)));
                Expect(material, 0, mesh, camera, target, sample, new Color(0.5f, 0.25f, 1), "full skin weight selects the subsurface colour at half-Lambert 0");
                // Skin 0.5 and opacity 0.5 weigh it 0.25; intensity 0.5 then halves that.
                material.SetTexture("_ID_Mask", Solid(new Color(0.5f, 0, 0, 0)));
                material.SetFloat("_SSS_Opacity", 0.5f);
                Expect(material, 0, mesh, camera, target, sample, new Color(0.275f, 0.3625f, 0.7f), "skin 0.5 x opacity 0.5 weighs the subsurface colour 0.25");
                material.SetFloat("_SSS_Emissive_Intensity", 0.5f);
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2375f, 0.38125f, 0.65f), "and intensity 0.5 halves the weight");
                material.SetFloat("_SSS_Emissive_Intensity", 1);
                material.SetFloat("_SSS_Opacity", 1);
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));
                material.DisableKeyword("_USE_SUBSURFACE_SCATTERING");

                material.SetTexture("_ID_Mask", Solid(new Color(0, 1, 0, 0)));
                Expect(material, 0, mesh, camera, target, sample, Color.black, "boots emit nothing without the matcap");
                material.EnableKeyword("_USE_MATCAP");
                Expect(material, 0, mesh, camera, target, sample, new Color(0, 1, 0), "the boots mask emits the matcap");
                material.DisableKeyword("_USE_MATCAP");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));

                material.SetFloat("_Erosion_Progress", 0.75f);
                material.SetColor("_ErosionColor", new Color(0, 0, 1, 1));
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 1.1f), "erosion 0.5 adds half the edge colour");
                material.SetFloat("_Erosion_Progress", 0);

                // Skin rim with the light position at zero: smoothstep(-0.25, 1.75, 0) = 0.04296875.
                material.SetTexture("_ID_Mask", Solid(new Color(1, 0, 0, 0)));
                material.SetFloat("_Rim_Opacity", 1);
                material.SetFloat("_Rim_Amount", 0.75f);
                material.SetFloat("_Rim_Softness", 1);
                material.SetColor("_Rim_Colour", Color.white);
                float rim = 0.04296875f;
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f + rim * 0.8f, 0.4f + rim * 0.6f, 0.6f + rim * 0.4f), "skin rim toward the fresnel texture");
                material.SetFloat("_Rim_Opacity", 0);
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));

                // A normal map pointing along the tangent makes the fresnel normal perpendicular to the view.
                material.SetTexture("_Norm_Texture", Solid(new Color(1, 0.5f, 0.5f, 1)));
                material.SetColor("_Fresnel_Boost_Colour", new Color(0.3f, 0.3f, 0.3f, 1));
                Expect(material, 0, mesh, camera, target, sample, new Color(0.3f, 0.3f, 0.3f), "a grazing fresnel selects the boost colour");

                // Meta: the lightmapper's albedo output.
                material.SetTexture("_Norm_Texture", Solid(new Color(0.5f, 0.5f, 1, 1)));
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(1, 0, 0, 0));
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 97);
                int meta = material.FindPass("Meta");
                Require(meta >= 0, "Meta pass");
                material.SetColor("_Diff_Colour", Color.white);
                Expect(material, meta, mesh, camera, target, sample, new Color(0.5f, 0.25f, 1), "albedo is the diffuse");
                material.SetColor("_Diff_Colour", new Color(1, 0, 1, 1));
                Expect(material, meta, mesh, camera, target, sample, new Color(0.5f, 0, 1), "colour tint");
                material.SetColor("_Diff_Colour", Color.white);

                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, -1, 0));
                material.SetTexture("_ID_Mask", Solid(new Color(1, 0, 0, 0)));
                material.SetFloat("_SSS_Opacity", 1);
                material.SetFloat("_SSS_Offset", 1);
                material.SetTexture("_SSS_Gradient_Ramp_Texture", Solid(new Color(0.5f, 1, 1, 1)));
                Expect(material, meta, mesh, camera, target, sample, new Color(0.5f, 0.25f, 1), "no subsurface ramp without its keyword");
                material.EnableKeyword("_USE_SUBSURFACE_SCATTERING");
                Expect(material, meta, mesh, camera, target, sample, new Color(0.25f, 0.25f, 1), "skin subsurface ramp at 1");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));
                material.DisableKeyword("_USE_SUBSURFACE_SCATTERING");
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, 1, 0));

                material.SetTexture("_ID_Mask", Solid(new Color(0, 1, 0, 0)));
                material.SetFloat("_Matcap_Blend_Amount", 0);
                material.EnableKeyword("_USE_MATCAP");
                Expect(material, meta, mesh, camera, target, sample, new Color(0, 1, 0), "boots take the matcap");
                material.DisableKeyword("_USE_MATCAP");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 1)));
                material.SetFloat("_Ball_Noise_Opacity", 1);
                material.SetFloat("_Ball_Noise_Intensity", 0);
                material.SetTexture("_Ball_Noise_Texture", Solid(Color.black));
                material.SetColor("_Ball_Noise_Colour", new Color(1, 0, 1, 1));
                material.EnableKeyword("_USE_BALLMODE");
                Expect(material, meta, mesh, camera, target, sample, new Color(0.5f, 0, 1), "ball mask applies the noise colour");

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original. A 256
                // texture spanning UV 0..1 across the 32-pixel quad, with a colour per level, reads level 3
                // unbiased and level 4 at bias 1; applying the bias twice would read level 5.
                Texture2D Levels(Color unbiased, Color once, Color twice)
                {
                    var levels = new Texture2D(256, 256, TextureFormat.RGBAFloat, true, true);
                    owned.Add(levels);
                    levels.filterMode = FilterMode.Bilinear;
                    levels.wrapMode = TextureWrapMode.Repeat;
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
                void BiasCases(int pass, Color unbiased, Color once, string path, float alphaUnbiased = float.NaN, float alphaOnce = float.NaN)
                {
                    Expect(material, pass, mesh, camera, target, sample, unbiased, path + ": no bias reads level 3", alphaUnbiased);
                    Shader.SetGlobalVector("_GlobalMipBias", bias1);
                    Expect(material, pass, mesh, camera, target, sample, once, path + ": bias 1 applies once", alphaOnce);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }

                // Meta: ball noise (added to the colour, times the noise colour) and the diffuse.
                material.SetTexture("_Ball_Noise_Texture", Levels(Color.black, new Color(0.5f, 0.5f, 0.5f, 1), Color.white));
                BiasCases(meta, new Color(0.5f, 0, 1), new Color(1, 0, 1.5f), "ball noise");
                material.DisableKeyword("_USE_BALLMODE");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));
                material.SetTexture("_Diff_Texture", Levels(new Color(0.5f, 0.25f, 1, 1), Color.red, Color.green));
                BiasCases(meta, new Color(0.5f, 0.25f, 1), Color.red, "diffuse");
                material.SetTexture("_Diff_Texture", Solid(new Color(0.5f, 0.25f, 1, 1)));

                // Meta: the boots matcap. Splayed normals give the lookup about 2.4 texels of a 256 texture per
                // pixel (level 1.3, read as 1); bias 2 reaches level 3 once and would reach 5 twice.
                mesh.normals = new[] { new Vector3(-0.3f, -0.3f, -1), new Vector3(0.3f, -0.3f, -1), new Vector3(0.3f, 0.3f, -1), new Vector3(-0.3f, 0.3f, -1) };
                material.SetTexture("_ID_Mask", Solid(new Color(0, 1, 0, 0)));
                material.EnableKeyword("_USE_MATCAP");
                var matcapLevels = new Texture2D(256, 256, TextureFormat.RGBAFloat, true, true);
                owned.Add(matcapLevels);
                matcapLevels.filterMode = FilterMode.Bilinear;
                matcapLevels.wrapMode = TextureWrapMode.Repeat;
                for (int mip = 0; mip < matcapLevels.mipmapCount; mip++)
                {
                    int width = Math.Max(1, 256 >> mip);
                    var pixels = new Color[width * width];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = mip <= 2 ? Color.white : mip <= 4 ? Color.green : Color.blue;
                    matcapLevels.SetPixels(pixels, mip);
                }
                matcapLevels.Apply(false, false);
                material.SetTexture("__Matcap_Texture__USE_MATCAP", matcapLevels);
                Expect(material, meta, mesh, camera, target, sample, Color.white, "matcap: no bias reads level 1");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(2, 0, 0, 0));
                Expect(material, meta, mesh, camera, target, sample, Color.green, "matcap: bias 2 applies once (level 3)");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.DisableKeyword("_USE_MATCAP");
                material.SetTexture("__Matcap_Texture__USE_MATCAP", Solid(Color.green));
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };

                // Forward (black colour, full metal: emission only): the emissive texture, then the fresnel texture
                // and the normal map through the grazing boost.
                material.SetColor("_Diff_Colour", new Color(0, 0, 0, 1));
                material.SetColor("_Fresnel_Boost_Colour", Color.white);
                material.SetTexture("_Emissive_Texture", Levels(Color.white, Color.red, Color.green));
                BiasCases(0, new Color(0.2f, 0.4f, 0.6f), new Color(0.2f, 0, 0), "emissive");
                material.SetTexture("_Emissive_Texture", Solid(Color.white));
                material.SetTexture("_Norm_Texture", Solid(new Color(1, 0.5f, 0.5f, 1)));
                material.SetTexture("_Fresnel_Texture", Levels(Color.white, Color.red, Color.green));
                BiasCases(0, Color.white, Color.red, "fresnel texture");
                material.SetTexture("_Fresnel_Texture", Solid(Color.white));
                // Normal map levels: flat, then along the tangent (grazing: the boost), then flat again.
                Color flat = new Color(0.5f, 0.5f, 1, 1), along = new Color(1, 0.5f, 0.5f, 1);
                material.SetTexture("_Norm_Texture", Levels(flat, along, flat));
                BiasCases(0, new Color(0.2f, 0.4f, 0.6f), Color.white, "normal map");
                material.SetTexture("_Norm_Texture", Solid(flat));

                // Transparent alpha: the opacity mask, then the erosion subtracted from it.
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetTexture("_ID_Mask", Levels(new Color(0, 0, 0.6f, 0), new Color(0, 0, 0.3f, 0), new Color(0, 0, 0.9f, 0)));
                BiasCases(0, new Color(0.2f, 0.4f, 0.6f), new Color(0.2f, 0.4f, 0.6f), "opacity mask", 0.6f, 0.3f);
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0.6f, 0)));
                material.SetFloat("_Erosion_Progress", 0.5f);
                material.SetTexture(Erosion, Levels(Color.black, new Color(0, 0, 0.3f, 1), new Color(0, 0, 0.6f, 1)));
                BiasCases(0, new Color(0.2f, 0.4f, 0.6f), new Color(0.2f, 0.4f, 0.6f), "erosion", 0.6f, 0.3f);
                material.SetTexture(Erosion, Solid(Color.black));
                material.SetFloat("_Erosion_Progress", 0);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0, 0)));

                // The tangent basis keeps the original's raw interpolants: left vertices carry the frame
                // (N -Z, T +X) and right ones (N -X, T -Z). Between them the interpolated normal and tangent are
                // shorter than 1 and the original builds the bitangent from them before one normalisation; a
                // tangent-space normal (0, 0.6, 0.8) then lands differently than with each vector normalised first.
                var frames = new Mesh();
                owned.Add(frames);
                frames.vertices = mesh.vertices;
                Vector3 nLeft = new Vector3(0, 0, -1), nRight = new Vector3(-1, 0, 0);
                Vector4 tLeft = new Vector4(1, 0, 0, 1), tRight = new Vector4(0, 0, -1, 1);
                frames.normals = new[] { nLeft, nRight, nRight, nLeft };
                frames.tangents = new[] { tLeft, tRight, tRight, tLeft };
                frames.uv = mesh.uv;
                frames.uv2 = mesh.uv;
                frames.triangles = mesh.triangles;
                frames.RecalculateBounds();
                float u = 16.5f / 32;
                Vector3 rawN = Vector3.Lerp(nLeft, nRight, u), rawT = Vector3.Lerp(tLeft, tRight, u);
                Vector3 original = (Vector3.Cross(rawN, rawT) * 0.6f + rawN * 0.8f).normalized;
                Vector3 n1 = rawN.normalized, t1 = rawT.normalized;
                Vector3 separately = (Vector3.Cross(n1, t1) * 0.6f + n1 * 0.8f).normalized;
                Require((original - separately).magnitude > 0.05f, "the frames separate the raw basis from a normalised one");
                material.SetTexture("_Norm_Texture", Solid(new Color(1, 0.8f, 0.5f, 0.5f)));   // unpacks to (0, 0.6, 0.8)
                int depthNormals = material.FindPass("DepthNormals");
                Require(depthNormals >= 0, "DepthNormals pass");
                Expect(material, depthNormals, frames, camera, target, sample, new Color(original.x, original.y, original.z), "DepthNormals keeps the raw basis");
                // Forward fresnel: with the boost black, the emission is fresnel = N.V times the emissive colour.
                Vector3 view = (new Vector3(0, 0, -10000) - new Vector3(u * 2 - 1, u * 2 - 1, 0)).normalized;
                float fresnel = Mathf.Clamp01(Vector3.Dot(original, view));
                material.SetColor("_Fresnel_Boost_Colour", Color.black);
                Expect(material, 0, frames, camera, target, sample, new Color(0.2f, 0.4f, 0.6f) * fresnel, "the fresnel normal keeps the raw basis");
                material.SetColor("_Fresnel_Boost_Colour", new Color(0.3f, 0.3f, 0.3f, 1));
                material.SetTexture("_Norm_Texture", Solid(flat));

                // Clipping: an opacity ramp gives alpha 0.49 here and 0.59 one pixel right (derivative width 0.1).
                // Forward sharpens for alpha to coverage when it is available: (0.49 - 0.5 - 0.05) / 0.1 + 1 = 0.4
                // survives. Meta 736 clips the bare 0.49.
                var opacityRamp = new Texture2D(32, 1, TextureFormat.RGBAFloat, false, true);
                owned.Add(opacityRamp);
                opacityRamp.filterMode = FilterMode.Point;
                opacityRamp.wrapMode = TextureWrapMode.Clamp;
                var ramp = new Color[32];
                for (int i = 0; i < ramp.Length; i++) ramp[i] = new Color(0, 0, 0.49f + 0.1f * (i - 16), 0);
                opacityRamp.SetPixels(ramp);
                opacityRamp.Apply(false, false);
                material.SetTexture("_ID_Mask", opacityRamp);
                material.EnableKeyword("_ALPHATEST_ON");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                Expect(material, 0, mesh, camera, target, sample, Color.black, "Forward clips 0.49 without alpha to coverage");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                Expect(material, 0, mesh, camera, target, sample, new Color(0.2f, 0.4f, 0.6f), "Forward's sharpened 0.4 survives with alpha to coverage");
                material.SetColor("_Diff_Colour", Color.white);
                Expect(material, meta, mesh, camera, target, sample, Color.black, "Meta clips the bare 0.49");
                material.SetTexture("_ID_Mask", Solid(new Color(0, 0, 0.6f, 0)));
                Expect(material, meta, mesh, camera, target, sample, new Color(0.5f, 0.25f, 1), "Meta keeps 0.6");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                material.DisableKeyword("_ALPHATEST_ON");

                // The erosion texture keeps its original immutable importer binding.
                var importer = (ShaderImporter)AssetImporter.GetAtPath(ShaderPath);
                Require(importer != null, "shader importer");
                int erosionIndex = shader.FindPropertyIndex(Erosion);
                Require(erosionIndex >= 0 && (shader.GetPropertyFlags(erosionIndex) & ShaderPropertyFlags.NonModifiableTextureData) != 0,
                        "the erosion texture is NonModifiableTextureData");
                Require(File.ReadAllText(ShaderPath + ".meta").Contains(ErosionTextureGuid), "the importer binds the original erosion texture");
                string erosionPath = AssetDatabase.GUIDToAssetPath(ErosionTextureGuid);
                if (!string.IsNullOrEmpty(erosionPath) && AssetDatabase.LoadAssetAtPath<Texture>(erosionPath) != null)
                {
                    var fresh = new Material(shader);
                    owned.Add(fresh);
                    Require(fresh.GetTexture(Erosion) == AssetDatabase.LoadAssetAtPath<Texture>(erosionPath), "a new material starts with the original erosion texture");
                }

                Debug.Log("Project Lucid HL_Char_Standard GPU verification passed: 43 Metal-derived cases on " + SystemInfo.graphicsDeviceType +
                          ". Lit presentation QA remains unverified.");
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                foreach (string name in vectors) Attempt(() => Shader.SetGlobalVector(name, prior[name]));
                foreach (string name in floats) Attempt(() => Shader.SetGlobalFloat(name, priorFloats[name]));
                Attempt(() => RenderTexture.active = previous);
                Attempt(() => { if (target != null) target.Release(); });
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index];
                    Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                if (failures.Count != 0)
                {
                    if (primary != null) failures.Insert(0, primary);
                    throw new AggregateException("Character verification cleanup failed.", failures);
                }
            }
        }

        private static void Expect(Material material, int pass, Mesh mesh, Camera camera, RenderTexture target, Texture2D sample,
                                   Color expected, string label, float expectedAlpha = float.NaN)
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
            sample.ReadPixels(new Rect(16, 16, 1, 1), 0, 0, false);
            sample.Apply(false);
            Color actual = sample.GetPixel(0, 0);
            Require(Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
                    Mathf.Abs(actual.b - expected.b) < 0.01f &&
                    (float.IsNaN(expectedAlpha) || Mathf.Abs(actual.a - expectedAlpha) < 0.01f),
                    label + ": expected=" + expected + " alpha " + expectedAlpha + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HL_Char_Standard verification failed: " + message);
        }
    }
}
