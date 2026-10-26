using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_FoliageWind, reconstructed from the shipped Metal programs. The diffuse albedo,
    // its alpha clip and the world-space wind sway are checked through the Meta pass, the normal map through
    // DepthNormals, the clip in the depth pass, and the alpha-to-coverage sharpening through Forward.
    public static class EnvFoliageWindVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_FoliageWind.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_FoliageWind";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "60f6b649330493f43a3fe019913c8b4c";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D", "5cecf516273ba544f8abc9afeafea881" },
        };
        private const int Forward = 0, DepthOnly = 2, DepthNormals = 3, Meta = 4;
        private const string Wind = "_SampleTexture2DLOD_47aee9514d6f42a8ab4f6851147fbbe5_Texture_1_Texture2D";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "_REFLECTION_PROBE_BOX_PROJECTION", "_ADDITIONAL_LIGHT_SHADOWS" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0, 3);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            RecoveredShaderProof.RequireImportedTextures(shader, ShaderPath, OriginalTextures);
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 5, "five shipped passes");
                rig.SaveFloats("unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace", "_HLShaderTimeGame", "_AlphaToMaskAvailable");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), clipped = new Color(0, 0, 0, 0);

                Material material = rig.Own(new Material(shader));
                material.SetFloat("_AlphaClipping", 0);
                material.SetFloat("_WindStrength", 0);
                material.SetTexture(Wind, rig.Texels(Color.white));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass);
                    cases++;
                }

                // A 7-texel strip: the read pixel's u 0.516 lands on texel 3 (green), u 0.266 on texel 1 (red) and
                // u 0.4125 on texel 2 (blue).
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Diffuse", rig.Texels(grey, red, blue, green, grey, grey, grey));
                Expect(green, "the diffuse colour is the albedo");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(new Color(0, 0, 0, 1), "no emission");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // Wind: the wind texture's red at each vertex's world XY times the strength moves the vertex along
                // world X by its vertex red. Strength 0.5 slides the quad to -0.5..1.5.
                material.SetFloat("_WindStrength", 0.5f);
                Expect(red, "the wind strength sways the vertices along X");
                rig.SetColors(new Color(0, 1, 1, 1));
                Expect(green, "only the vertex red sways");
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                material.SetFloat("_WindStrength", 1);
                Expect(red, "the vertex red scales the sway");
                rig.SetColors(Color.white);
                material.SetFloat("_WindStrength", 0.5f);
                // The wind texture: texel 0 (1) and texel 1 (0). With scale 0 every vertex reads u 0 (time scrolls
                // it by the speed); with scale 0.25 the right vertices (x 1, u 0.25) sway and the left ones (u 0.75)
                // stay, stretching the quad to -1..1.5.
                material.SetTexture(Wind, rig.Texels(Color.white, Color.black));
                material.SetFloat("_WindScale", 0);
                Expect(red, "the wind texture at u 0");
                material.SetFloat("_WindSpeed", 0.75f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(green, "game time times the speed scrolls the wind texture");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_WindScale", 0.25f);
                Expect(blue, "the wind scale reads the texture at each vertex's world position");
                material.SetFloat("_WindStrength", 0);
                material.SetTexture(Wind, rig.Texels(Color.white));

                // Alpha clip: the diffuse alpha below the threshold discards the meta, depth and normal passes.
                material.SetTexture("_Diffuse", rig.Texels(new Color(0, 1, 0, 0.25f)));
                material.SetFloat("_AlphaClipping", 0.5f);
                Expect(clipped, "alpha below the threshold clips");
                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth == 0, "the depth pass clips too: " + depth);
                cases++;
                material.SetTexture("_Diffuse", rig.Texels(new Color(0, 1, 0, 0.75f)));
                Expect(green, "alpha above the threshold");
                depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "DepthOnly writes the depth");
                cases++;
                material.SetFloat("_AlphaClipping", 0);

                // _GlobalMipBias applies once: level 3 unbiased, level 4 at bias 1.
                material.SetTexture("_Diffuse", rig.MipLevels(256, level => level == 4 ? red : green));
                Expect(green, "diffuse: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(red, "diffuse: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Diffuse", rig.Texels(green));

                // Normals, read by DepthNormals: the AG-packed normal map at full strength. The quad faces -Z with
                // tangent +X.
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                material.SetTexture("_Normal_Map", rig.Texels(new Color(0.5f, 0.5f, 1, 1)));
                Expect(new Color(0, 0, -1, 0), "flat normal map", DepthNormals);
                material.SetTexture("_Normal_Map", rig.Texels(new Color(1, 0.5f, 1, 1)));
                Expect(new Color(1, 0, 0, 0), "a normal along the tangent", DepthNormals);
                material.SetTexture("_Normal_Map", rig.Texels(new Color(1, 0.5f, 1, 0.5f)));
                Expect(new Color(0, 0, -1, 0), "the AG packing multiplies red by alpha", DepthNormals);
                material.SetTexture("_Diffuse", rig.Texels(new Color(0, 1, 0, 0.25f)));
                material.SetFloat("_AlphaClipping", 0.5f);
                Expect(clipped, "DepthNormals clips", DepthNormals);

                // Forward alpha to coverage: a flat alpha has no width, so the sharpened alpha is
                // saturate((alpha - threshold) / 0.0001 + 1). Alpha 0.5 under threshold 0.50005 reads 0.5 when
                // alpha to coverage is available and clips otherwise.
                material.SetTexture("_Diffuse", rig.Texels(new Color(0, 1, 0, 0.5f)));
                material.SetFloat("_AlphaClipping", 0.50005f);
                float alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, alpha == 0, "without alpha to coverage the bare alpha clips: " + alpha);
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 0.5f) < 0.02f, "alpha to coverage writes the sharpened alpha: " + alpha);
                material.SetFloat("_AlphaClipping", 0);
                alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, alpha == 1, "threshold 0 keeps the sharpened alpha at 1: " + alpha);
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                material.SetFloat("_AlphaClipping", 0.25f);
                alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, alpha == 1, "without alpha to coverage the output alpha is 1: " + alpha);
                material.SetFloat("_AlphaClipping", 0.49995f);
                alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, alpha == 1, "without alpha to coverage alpha 0.5 passes threshold 0.49995: " + alpha);
                cases += 5;

                Debug.Log("Project Lucid HL_Env_FoliageWind GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading and the smoothness texture remain compile-checked only.");
            }
        }
    }
}
