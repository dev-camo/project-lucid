using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Standard_TriPlanar, reconstructed from the shipped Metal programs. The
    // surface is checked unlit through the Meta pass: the triplanar projections (X: zy, Y: xz, Z: xy) and
    // their |normal|^blend weights, the tiling, tints, the triplanar detail, fresnel and emission, which
    // the shipped variants enable one at a time.
    public static class EnvStandardTriPlanarVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Standard_TriPlanar.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Standard_TriPlanar";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "af6da4f6308a6cd45b8544e8d821d998";
        private const int Forward = 0, DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_REFLECTION_PROBE_BLENDING", "_SHADOWS_SOFT", "_USE_FRESNEL" },
            new[] { "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON", "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "FOG_LINEAR", "_USE_TRIPLANAR_DETAIL", "INSTANCING_ON" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_WRITE_RENDERING_LAYERS", "_USE_EMISSION", "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "EDITOR_VISUALIZATION", "_FORWARD_PLUS", "_USE_FRESNEL", "_USE_TRIPLANAR_DETAIL", "_USE_EMISSION" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "_USE_FRESNEL", "_USE_TRIPLANAR_DETAIL", "_USE_EMISSION" },
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
                rig.SaveFloats("unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // The graph samples with a bilinear inline sampler, so the diffuse is 4x4 bands of 16 texels
                // whose colour names the band: (column / 3, row / 3, 0).
                var bands = rig.Own(new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Repeat });
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        pixels[y * 64 + x] = new Color(x / 16 / 3f, y / 16 / 3f, 0, 1);
                bands.SetPixels(pixels);
                bands.Apply(false, false);
                Color Band(int column, int row) => new Color(column / 3f, row / 3f, 0, 1);

                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Diff_Tex", bands);
                material.SetColor("_Diff_Colour_Tint", Color.white);
                material.SetFloat("_Tri_Planar_UV_Tiling", 10);
                material.SetFloat("_Tri_Planar_Blend", 2);
                // Pushed back to z 0.4, pixel (18, 18) sits at world (0.156, 0.156, 0.4): with tiling 10 the
                // UV is the world position, so Z reads band (0, 0), Y (xz) band (0, 1) and X (zy) band (1, 0).
                Matrix4x4 back = Matrix4x4.Translate(new Vector3(0, 0, 0.4f));
                void Expect(Color expected, string label) => rig.Expect(material, expected, label, pass: Meta, model: back, x: 18, y: 18);

                Expect(Band(0, 0), "a -Z normal projects onto xy");
                rig.SetNormals(Vector3.up);
                Expect(Band(0, 1), "a Y normal projects onto xz");
                rig.SetNormals(Vector3.right);
                Expect(Band(1, 0), "an X normal projects onto zy");
                // Normal (0.6, 0, -0.8): blend 2 weighs X 0.36 and Z 0.64; blend 1 weighs them 0.6 / 1.4.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                Expect(new Color(0.12f, 0, 0, 1), "blend 2 weights");
                material.SetFloat("_Tri_Planar_Blend", 1);
                Expect(new Color(0.6f / 1.4f / 3, 0, 0, 1), "blend 1 weights");
                material.SetFloat("_Tri_Planar_Blend", 2);
                rig.SetNormals(Vector3.back);
                // Tiling 20 doubles the UV: (0.31, 0.31) is band (1, 1).
                material.SetFloat("_Tri_Planar_UV_Tiling", 20);
                Expect(Band(1, 1), "tiling scales the world position");
                material.SetFloat("_Tri_Planar_UV_Tiling", 10);

                // Tints on band (1, 1) colour (1/3, 1/3, 0) at tiling 20 are easier to see; use (1, 0) instead.
                rig.SetNormals(Vector3.right);
                material.SetColor("_Diff_Colour_Tint", new Color(0, 1, 1, 1));
                Expect(new Color(0, 0, 0, 1), "diffuse tint");
                material.SetColor("_Diff_Colour_Tint", Color.white);
                rig.SetColors(new Color(0, 1, 1, 1));
                Expect(Band(1, 0), "vertex colour ignored by default");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 1);
                Expect(new Color(0, 0, 0, 1), "vertex colour tint");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 0);
                rig.SetColors(Color.white);

                // Detail: a uniform 0.5 detail texture times vertex alpha 0.5 and opacity 1 pulls the albedo a
                // quarter of the way to the blue detail tint.
                material.EnableKeyword("_USE_TRIPLANAR_DETAIL");
                Color half = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Detail_Diff_Tex", rig.Texels(half, half, half, half));
                material.SetColor("_Detail_Diff_Colour_Tint", Color.blue);
                rig.SetColors(new Color(1, 1, 1, 0.5f));
                Expect(new Color(0.25f, 0, 0.25f, 1), "detail tint by detail times vertex alpha");
                material.SetFloat("_Detail_Diff_Opacity", 0.5f);
                Expect(new Color(0.875f / 3, 0, 0.125f, 1), "detail opacity");
                material.SetFloat("_Detail_Diff_Opacity", 1);
                rig.SetColors(Color.white);
                material.DisableKeyword("_USE_TRIPLANAR_DETAIL");

                // Fresnel: normal (0.6, 0, -0.8) has rim 0.2; amount 1, intensity 2.5 and opacity 1 make the
                // keep factor 0.5 between the blue fresnel colour and the albedo (0.12, 0, 0).
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.EnableKeyword("_USE_FRESNEL");
                material.SetColor("_Fresnel_Colour", Color.blue);
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Intensity", 2.5f);
                material.SetFloat("_Fresnel_Opacity", 1);
                Expect(new Color(0.06f, 0, 0.5f, 1), "fresnel colour at the rim");
                material.SetFloat("_Fresnel_Opacity", 0.5f);
                Expect(new Color(0.09f, 0, 0.25f, 1), "fresnel opacity");
                material.DisableKeyword("_USE_FRESNEL");
                rig.SetNormals(Vector3.back);

                // Emission (the Meta pass's emission output): black without the keyword, else the triplanar
                // emissive texture times the emissive colour.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetTexture("_Emissive_Tex", rig.Texels(half, half, half, half));
                material.SetColor("_Emissive_Colour", new Color(1, 0, 1, 1));
                Expect(new Color(0, 0, 0, 1), "no emission without its keyword");
                material.EnableKeyword("_USE_EMISSION");
                Expect(new Color(0.5f, 0, 0.5f, 1), "triplanar emission times colour");
                material.DisableKeyword("_USE_EMISSION");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original. At tiling 10
                // each projection moves 1/16 of UV per pixel, so a 256 texture with a colour per level reads level 4
                // unbiased and level 5 at bias 1; applied twice the bias would reach level 6.
                void BiasCases(Color unbiased, Color once, string path, int pass = Meta)
                {
                    rig.Expect(material, unbiased, path + ": no bias reads level 4", pass: pass, model: back, x: 18, y: 18);
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    rig.Expect(material, once, path + ": bias 1 applies once", pass: pass, model: back, x: 18, y: 18);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                Texture2D Levels(Color unbiased, Color once, Color twice) => rig.MipLevels(256, level => level <= 4 ? unbiased : level == 5 ? once : twice);
                Color green = new Color(0, 1, 0, 1);
                material.SetTexture("_Diff_Tex", Levels(half, Color.red, green));
                BiasCases(half, Color.red, "diffuse");
                material.SetTexture("_Diff_Tex", rig.Texels(half, half, half, half));
                material.EnableKeyword("_USE_TRIPLANAR_DETAIL");
                material.SetFloat("_Detail_Diff_Tri_Planar_UV_Tiling", 10);
                material.SetTexture("_Detail_Diff_Tex", Levels(half, Color.white, Color.black));
                BiasCases(new Color(0.25f, 0.25f, 0.75f, 1), Color.blue, "detail");
                material.DisableKeyword("_USE_TRIPLANAR_DETAIL");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.EnableKeyword("_USE_EMISSION");
                material.SetTexture("_Emissive_Tex", Levels(Color.white, Color.red, green));
                BiasCases(new Color(1, 0, 1, 1), Color.red, "emission");
                material.DisableKeyword("_USE_EMISSION");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // Normal map, read by DepthNormals, one projection at a time. Level 5 unpacks to (1, 0), which the
                // graph's Normal Triplanar adds to the projection's two world axes (X: z, Y: x, Z: x).
                Color flat = new Color(1, 0.5f, 0.5f, 0.5f), along = new Color(1, 0.5f, 0.5f, 1);
                material.SetTexture("_Normal_Tex", Levels(flat, along, flat));
                float r2 = Mathf.Sqrt(0.5f);
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                BiasCases(new Color(0, 0, -1, 0), new Color(r2, 0, -r2, 0), "normal, Z projection", DepthNormals);
                rig.SetNormals(Vector3.right);
                rig.Quad.tangents = new[] { new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 1, 1) };
                BiasCases(new Color(1, 0, 0, 0), new Color(r2, 0, r2, 0), "normal, X projection", DepthNormals);
                rig.SetNormals(Vector3.up);
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                BiasCases(new Color(0, 1, 0, 0), new Color(r2, r2, 0, 0), "normal, Y projection", DepthNormals);
                rig.SetNormals(Vector3.back);
                material.SetTexture("_Normal_Tex", rig.Texels(flat, flat, flat, flat));

                // The forward pass is opaque: alpha 1.
                float alpha = rig.Draw(material, Forward, model: back, x: 18, y: 18).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 1) < 0.01f, "opaque forward alpha: " + alpha);


                Debug.Log("Project Lucid HL_Env_Standard_TriPlanar GPU verification passed: 28 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Normal output is checked only through DepthNormals; the forward lit RGB, material response (metal/smoothness) and in-game presentation remain unverified.");
            }
        }
    }
}
