using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Boss_01_Shockwave, reconstructed from the shipped Metal programs. Through the Meta
    // pass: the tinted diffuse (its tiling and offset) as the albedo and the emissive-tinted diffuse as the emission,
    // and the push along the normals (also in DepthOnly). DepthNormals checks the vertex normal; Forward's alpha is
    // vertex red times the alpha, saturated.
    public static class CharBoss01ShockwaveVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Boss_01_Shockwave.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Boss_01_Shockwave";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "bf757782d180e1849a9a1011c3caff3b";
        private const int Forward = 0, DepthOnly = 1, DepthNormals = 2, Meta = 3;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "_REFLECTION_PROBE_BOX_PROJECTION", "_ADDITIONAL_LIGHT_SHADOWS", "FOG_EXP" },
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
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 4, "four shipped passes");
                rig.SaveFloats("unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                // Meta: object position straight to clip space, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), black = new Color(0, 0, 0, 1);
                Color clear = new Color(0, 0, 0, 0), diffuse = new Color(0.25f, 0.5f, 1, 1);

                Material material = rig.Own(new Material(shader));
                material.SetFloat("_Vertex_Push_Amount", 0);
                material.SetTexture("_Diffuse_Texture", rig.Texels(diffuse));
                void Expect(Color expected, string label, int pass = Meta, int x = 16)
                {
                    rig.Expect(material, expected, label, pass: pass, x: x);
                    cases++;
                }

                Expect(diffuse, "the diffuse is the albedo (white tint)");
                material.SetColor("_Diff_Colour_Tint", new Color(1, 0, 1, 0));
                Expect(new Color(0.25f, 0, 1, 1), "the albedo tint");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(diffuse, "the emission has its own tint (white)");
                material.SetColor("_Diff_Colour_Tint_1", new Color(0, 1, 0, 0));
                Expect(new Color(0, 0.5f, 0, 1), "the emissive tint");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                material.SetColor("_Diff_Colour_Tint", Color.white);

                // The diffuse tiling and offset on a 4-band strip (pixel 16 at u 0.5156, band 2).
                material.SetTexture("_Diffuse_Texture", rig.Bands(black, black, red, blue));
                Expect(red, "band 2");
                material.SetTextureOffset("_Diffuse_Texture", new Vector2(0.25f, 0));
                Expect(blue, "the offset (band 3)");
                material.SetTextureOffset("_Diffuse_Texture", Vector2.zero);
                material.SetTextureScale("_Diffuse_Texture", new Vector2(0.5f, 1));
                Expect(black, "the tiling (band 1)");
                material.SetTextureScale("_Diffuse_Texture", Vector2.one);
                material.SetTexture("_Diffuse_Texture", rig.Texels(diffuse));

                // The push: normals along +x move the quad right by the amount; 0.5 (8 px) uncovers pixel 4, pixel 12
                // stays covered.
                rig.SetNormals(Vector3.right);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, Meta, x: 1).a != 0, "no push keeps the edge covered");
                cases++;
                material.SetFloat("_Vertex_Push_Amount", 0.5f);
                Expect(clear, "the push moves the quad", Meta, 4);
                Expect(diffuse, "pixel 12 stays covered", Meta, 12);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, DepthOnly, x: 4).r == 0, "DepthOnly pushes too");
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, DepthOnly, x: 12).r != 0, "DepthOnly writes depth");
                cases += 2;
                material.SetFloat("_Vertex_Push_Amount", 0);

                // DepthNormals: the interpolated vertex normal.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                Expect(new Color(0.6f, 0, -0.8f, 0), "the normal", DepthNormals);
                rig.SetNormals(Vector3.back);
                Expect(new Color(0, 0, -1, 0), "a facing normal", DepthNormals);

                // Forward alpha, written through the shipped alpha factors (One OneMinusSrcAlpha) over a cleared target.
                void Alpha(float expected, string label)
                {
                    float actual = rig.Draw(material, Forward).a;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(actual - expected) < 1e-3f, label + ": " + actual);
                    cases++;
                }
                Alpha(1, "vertex red 1 at alpha 1");
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                Alpha(0.5f, "vertex red");
                material.SetFloat("_Alpha", 0.5f);
                Alpha(0.25f, "times the alpha");
                material.SetFloat("_Alpha", 3);
                Alpha(1, "saturated");
                material.SetFloat("_Alpha", 1);
                rig.SetColors(Color.white);

                // _GlobalMipBias applies once: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Diffuse_Texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : blue));
                Expect(red, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Char_Boss_01_Shockwave GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The forward lighting remains compile-checked only.");
            }
        }
    }
}
