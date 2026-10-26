using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Eye, reconstructed from the shipped Metal programs. The surface is checked
    // unlit through the Meta pass (the four eye-mask lookups, the vertex tint, the emission and the erosion
    // clip and edge glow), the added eye normals through DepthNormals, and the erosion clip in Forward.
    public static class CharEyeVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Eye.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Eye";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "269ce84e7a03e7b4a8c80eb1ecdf93ed";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D", "e8129d5371ff1e04d8070064644689a1" },
        };
        private const int Forward = 0, DepthOnly = 1, DepthNormals = 2, Meta = 3;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_REFLECTION_PROBE_BLENDING", "_SHADOWS_SOFT" },
            new[] { "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_WRITE_RENDERING_LAYERS", "DOTS_INSTANCING_ON" },
            new[] { "EDITOR_VISUALIZATION", "_FORWARD_PLUS", "_DBUFFER_MRT3" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0, 2);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            RecoveredShaderProof.RequireImportedTextures(shader, ShaderPath, OriginalTextures);
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 4, "four shipped passes");
                rig.SaveFloats("unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace", "_AlphaToMaskAvailable");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                void Expect(Material m, Color expected, string label) => rig.Expect(m, expected, label, pass: Meta);

                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), white = Color.white, clipped = new Color(0, 0, 0, 0);
                Material material = rig.Own(new Material(shader));
                material.SetColor("_EyeColor", green);
                material.SetColor("_GlintColour", red);
                rig.SetColors(white);

                // A uniform mask (iris r, background g, glint b): iris = r * r, glint = b + b, background = 1 - (g + g),
                // and the background lerps to the bare iris.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0.5f, 0, 1)));
                Expect(material, green, "full iris, no glint, no background");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(0.5f, 0.5f, 0, 1)));
                Expect(material, new Color(0, 0.25f, 0, 1), "the two irises multiply");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0.5f, 0.25f, 1)));
                Expect(material, new Color(0.5f, 0.5f, 0, 1), "the two glints add, then lerp to the glint colour");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0, 0, 1)));
                Expect(material, white, "no background mask shows the bare iris");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0.25f, 0, 1)));
                Expect(material, new Color(0.5f, 1, 0.5f, 1), "half background");

                // The vertex colour tints by _Vertex_Colour.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0, 0, 1)));
                rig.SetColors(new Color(1, 1, 0.5f, 1));
                Expect(material, white, "_Vertex_Colour 0 ignores the vertex colour");
                material.SetFloat("_Vertex_Colour", 1);
                Expect(material, new Color(1, 1, 0.5f, 1), "_Vertex_Colour 1 tints by the vertex colour");
                material.SetFloat("_Vertex_Colour", 0.5f);
                Expect(material, new Color(1, 1, 0.75f, 1), "_Vertex_Colour lerps the tint");

                // Emission: the untinted colour at the emission strength, plus the erosion edge.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetFloat("_Vertex_Colour", 1);
                material.SetFloat("_EmissionStrength", 0.5f);
                Expect(material, new Color(0.5f, 0.5f, 0.5f, 1), "emission strength, without the vertex tint");
                material.SetFloat("_Vertex_Colour", 0);
                rig.SetColors(white);
                material.SetFloat("_EmissionStrength", 0);
                material.SetColor("_ErosionColor", new Color(0, 0, 1, 1));
                material.SetTexture("_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D", rig.Texels(new Color(1, 1, 0.5f, 1)));
                material.SetFloat("_Erosion_Progress", 0.5f);
                Expect(material, new Color(0, 0, 0.5f, 1), "erosion 0.5 glows half the erosion colour");
                material.SetFloat("_Erosion_Progress", 0.55f);
                Expect(material, clipped, "erosion past 0.5 clips the meta pass");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                // DepthOnly writes the depth to red (0 where nothing is drawn); Forward writes alpha 1 where it draws.
                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth == 0, "eroded depth fragment clipped: " + depth);
                float forwardAlpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, forwardAlpha == 0, "eroded forward fragment clipped: " + forwardAlpha);
                material.SetFloat("_Erosion_Progress", 0.5f);
                depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "depth fragment at erosion 0.5 drawn");
                forwardAlpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(forwardAlpha - 1) < 0.01f, "forward alpha 1 without alpha to coverage: " + forwardAlpha);
                // The erosion tiling scales the vertex UV: on a two-texel erosion mask (1, then 0.5), tiling 1 reads
                // the second texel (u 0.52) and tiling 2 the first (u 1.03), which clips.
                material.SetTexture("_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D",
                    rig.Texels(new Color(1, 1, 1, 1), new Color(1, 1, 0.5f, 1)));
                Expect(material, white, "erosion tiling 1 reads the second texel");
                material.SetFloat("_ErosionTiling", 2);
                Expect(material, clipped, "erosion tiling 2 reads the first texel");
                material.SetFloat("_ErosionTiling", 1);
                material.SetFloat("_Erosion_Progress", 0);

                // Offsets and scales, on a strip of seven texels: no offset repeats onto texel 0 (or 6, if V is
                // flipped), a plain iris; an offset of -0.25 reaches texel 1 (no iris) and -0.5, like a scale of 0,
                // the centre texel 3 (a half glint). No case lands near a texel boundary.
                Color iris = new Color(1, 0.5f, 0, 1);
                Color[] strip = { iris, new Color(0, 0.5f, 0, 1), iris, new Color(1, 0.5f, 0.5f, 1), iris, iris, iris };
                Color black = new Color(0, 0, 0, 1), halfGlint = new Color(0.5f, 0.5f, 0, 1);
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(strip));
                foreach (string eye in new[] { "Right", "Left" })
                {
                    material.SetFloat("_" + eye + "EyeHorizontal", -0.25f);
                    Expect(material, black, eye + " eye offset moves its iris and glint");
                    material.SetFloat("_" + eye + "EyeHorizontal", 0);
                    material.SetFloat("_" + eye + "GlintHorizontal", -0.5f);
                    Expect(material, halfGlint, eye + " glint offset moves only its glint");
                    material.SetFloat("_" + eye + "GlintHorizontal", 0);
                    material.SetFloat("_" + eye + "_Eye_UV_Scale", 0);
                    Expect(material, halfGlint, eye + " eye scale 0 reads the centre for its eye and glint");
                    material.SetFloat("_" + eye + "_Eye_UV_Scale", 1);
                }
                material.SetFloat("_Global_UV_Horizontal", 0);
                Expect(material, red, "global horizontal 0 reads the centre for all four lookups");
                material.SetFloat("_Global_UV_Horizontal", 1);
                material.SetFloat("_Global_UV_Scale", 0);
                Expect(material, red, "global scale 0 reads the centre for all four lookups");
                material.SetFloat("_Global_UV_Scale", 1);

                // _GlobalMipBias applies once per lookup. Level 3 is a plain iris; level 4 gives (0.75, 0.75, 0.5),
                // and a second bias on any one lookup would read level 5 and change it.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.MipLevels(256, level =>
                    level <= 3 ? iris : level == 4 ? new Color(1, 0.25f, 0.25f, 1) : new Color(0.5f, 0.5f, 0, 1)));
                Expect(material, green, "eye mask: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, new Color(0.75f, 0.75f, 0.5f, 1), "eye mask: bias 1 applies once to every lookup");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0, 0, 1)));
                // The erosion mask: level 4 glows, level 5 would clip.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetFloat("_Erosion_Progress", 0.5f);
                material.SetTexture("_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D", rig.MipLevels(256, level =>
                    new Color(1, 1, level <= 3 ? 0 : level == 4 ? 0.5f : 1, 1)));
                Expect(material, black, "erosion: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, new Color(0, 0, 0.5f, 1), "erosion: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetFloat("_Erosion_Progress", 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // Normals, read by DepthNormals: each eye's normal (at its own UV) is unpacked and the two are
                // added, then Normal Strength scales xy and lerps z from 1. The quad faces -Z with tangent +X,
                // so its bitangent is -Y.
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                Color flat = new Color(0.5f, 0.5f, 1, 1), along = new Color(1, 0.5f, 0.5f, 1);
                void Normal(Color expected, string label) => rig.Expect(material, expected, label, pass: DepthNormals);
                material.SetTexture("_EyeTextureNormal", rig.Texels(flat));
                Normal(new Color(0, 0, -1, 0), "flat normals");
                material.SetTexture("_EyeTextureNormal", rig.Texels(along));
                Normal(new Color(1, 0, 0, 0), "both eyes along the tangent");
                material.SetFloat("_NormalStrength", 0.5f);
                Normal(new Color(0.894f, 0, -0.447f, 0), "half strength halves xy and lerps z halfway from 1");
                material.SetFloat("_NormalStrength", 0);
                Normal(new Color(0, 0, -1, 0), "strength 0 is flat");
                material.SetFloat("_NormalStrength", 1);
                material.SetTexture("_EyeTextureNormal", rig.Texels(flat, along, flat, flat, flat, flat, flat));
                material.SetFloat("_RightEyeHorizontal", -0.25f);
                Normal(new Color(0.707f, 0, -0.707f, 0), "the right eye's normal at its own UV adds to the left's");
                material.SetFloat("_RightEyeHorizontal", 0);
                material.SetFloat("_LeftEyeHorizontal", -0.25f);
                Normal(new Color(0.707f, 0, -0.707f, 0), "the left eye's normal at its own UV adds to the right's");
                material.SetFloat("_LeftEyeHorizontal", 0);
                material.SetTexture("_EyeTextureNormal", rig.MipLevels(256, level => level == 4 ? along : flat));
                Normal(new Color(0, 0, -1, 0), "normal: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Normal(new Color(1, 0, 0, 0), "normal: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetFloat("_Erosion_Progress", 0.55f);
                material.SetTexture("_SampleTexture2D_b6ebbf4e151d47d491f3933b8ce35177_Texture_1_Texture2D", rig.Texels(new Color(1, 1, 0.5f, 1)));
                Normal(clipped, "DepthNormals clips the eroded fragment");

                Debug.Log("Project Lucid HL_Char_Eye GPU verification passed: 38 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading and smoothness remain compile-checked only.");
            }
        }
    }
}
