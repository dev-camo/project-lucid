using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone5_Track, reconstructed from the shipped Metal programs. The surface
    // is checked unlit through the Meta pass: the Diff tiling, the game-time scrolling that applies only
    // where the Diff alpha (read at the static UV tiled a second time) is at least 0.001, the tints and
    // the emission.
    public static class EnvZone5TrackVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone5_Track.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone5_Track";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "aea03fbf55edc744f86f594b52b8de6d";
        private const int Forward = 0, DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_REFLECTION_PROBE_BLENDING", "_SHADOWS_SOFT" },
            new[] { "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_WRITE_RENDERING_LAYERS", "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "EDITOR_VISUALIZATION", "_FORWARD_PLUS", "_DBUFFER_MRT3" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, Forward, DepthNormals);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 5, "five shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                void Expect(Material m, Color expected, string label) => rig.Expect(m, expected, label, pass: Meta);

                // The colour samplers filter bilinearly, so the Diff is four 16-texel bands: red, green, blue,
                // black. The centre column (u 0.516) is in band 2.
                Color r = Color.red, g = Color.green, b = Color.blue, k = new Color(0, 0, 0, 1);
                Color Transparent(Color c) => new Color(c.r, c.g, c.b, 0);
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Diff_Tex", rig.Bands(r, g, b, k));
                material.SetColor("_Diff_Tint", Color.white);
                material.SetFloat("_UV_Scrolling_Horizontal", 0);

                Expect(material, b, "static UV");
                material.SetTextureOffset("_Diff_Tex", new Vector2(0.25f, 0));
                Expect(material, k, "Diff offset moves the UV");
                material.SetTextureOffset("_Diff_Tex", Vector2.zero);

                // Scrolling: game time 1 at 0.25 moves to band 3 where the alpha mask passes.
                material.SetFloat("_UV_Scrolling_Horizontal", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(material, k, "opaque Diff alpha scrolls");
                material.SetTexture("_Diff_Tex", rig.Bands(r, g, Transparent(b), k));
                Expect(material, b, "Diff alpha below 0.001 keeps the static UV");
                // The mask reads the static UV tiled again: with offset 0.25 the colour reads band 3 but the
                // mask reads band 0 (u 1.02). A transparent band 0 holds the static UV; a single tiling would
                // read band 3's opaque alpha and scroll to band 0 (red).
                material.SetTexture("_Diff_Tex", rig.Bands(Transparent(r), g, b, k));
                material.SetTextureOffset("_Diff_Tex", new Vector2(0.25f, 0));
                Expect(material, k, "the mask reads the twice-tiled static UV");
                material.SetTextureOffset("_Diff_Tex", Vector2.zero);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_UV_Scrolling_Horizontal", 0);
                material.SetTexture("_Diff_Tex", rig.Bands(r, g, b, k));

                // Tints.
                material.SetColor("_Diff_Tint", new Color(1, 1, 0, 1));
                Expect(material, k, "Diff tint");
                material.SetColor("_Diff_Tint", Color.white);
                rig.SetColors(new Color(1, 1, 0, 1));
                Expect(material, b, "vertex colour ignored by default");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 1);
                Expect(material, k, "vertex colour tint");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 0);
                rig.SetColors(Color.white);

                // Emission: the emissive texture at the same (switched) UV times the emissive colour.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetTexture("_Emissive_Tex", rig.Bands(r, g, new Color(0.5f, 0.5f, 0.5f, 1), k));
                material.SetColor("_Emissive_Colour", new Color(1, 0, 1, 1));
                Expect(material, new Color(0.5f, 0, 0.5f, 1), "emission times colour");
                material.SetFloat("_UV_Scrolling_Horizontal", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(material, k, "emission scrolls with the Diff mask");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_UV_Scrolling_Horizontal", 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original. A 256
                // texture spanning UV 0..1 across the 32-pixel quad reads level 3 unbiased and level 4 at bias 1;
                // applied twice the bias would reach level 5. Each level has its own colour.
                void BiasCases(Color unbiased, Color once, string path, int pass = Meta)
                {
                    rig.Expect(material, unbiased, path + ": no bias reads level 3", pass: pass);
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    rig.Expect(material, once, path + ": bias 1 applies once", pass: pass);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Diff_Tex", rig.MipLevels(256, level => level <= 3 ? grey : level == 4 ? r : g));
                BiasCases(grey, r, "Diff colour");
                // The scroll mask (Diff alpha, through the texture's own sampler): level 4 is transparent, so bias 1
                // keeps the static UV (emissive band 2, blue) where levels 3 and 5 scroll it onto band 3 (black).
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetColor("_Emissive_Colour", Color.white);
                material.SetTexture("_Emissive_Tex", rig.Bands(r, g, b, k));
                material.SetTexture("_Diff_Tex", rig.MipLevels(256, level => new Color(1, 1, 1, level == 4 ? 0 : 1)));
                material.SetFloat("_UV_Scrolling_Horizontal", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                BiasCases(k, b, "scroll mask");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_UV_Scrolling_Horizontal", 0);
                material.SetTexture("_Emissive_Tex", rig.MipLevels(256, level => level <= 3 ? Color.white : level == 4 ? r : g));
                BiasCases(Color.white, r, "emissive");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                // Normal map, read by DepthNormals: level 4 lies along the tangent (+X).
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                Color flat = new Color(0.5f, 0.5f, 1, 1), along = new Color(1, 0.5f, 0.5f, 1);
                material.SetTexture("_Normal_Tex", rig.MipLevels(256, level => level == 4 ? along : flat));
                BiasCases(new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), "normal", DepthNormals);

                float alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 1) < 0.01f, "opaque forward alpha: " + alpha);


                Debug.Log("Project Lucid HL_Env_Zone5_Track GPU verification passed: 19 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Normal output is checked only through DepthNormals; the forward lit RGB, material response (metal/smoothness) and in-game presentation remain unverified.");
            }
        }
    }
}
