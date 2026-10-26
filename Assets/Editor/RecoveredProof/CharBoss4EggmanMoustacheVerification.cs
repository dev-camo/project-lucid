using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Boss_4_Eggman_Moustache, reconstructed from the shipped Metal programs. Through the
    // Meta pass: the distorted, scrolling diffuse, the gradient colour screened over it by vertex alpha (inverted or
    // not, times the mask power), the emission strength, and the wind lifting world Y by vertex alpha. DepthNormals
    // checks the normal map and its strength; Forward's alpha checks the opacity terms.
    public static class CharBoss4EggmanMoustacheVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Boss_4_Eggman_Moustache.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Boss_4_Eggman_Moustache";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "f2136b208a7a29b45a6d9d4d729c3927";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D", "5cecf516273ba544f8abc9afeafea881" },
        };
        private const string Wind = "_SampleTexture2DLOD_4ece03de67e24273978bbf408352a95d_Texture_1_Texture2D";
        private const int Forward = 0, DepthNormals = 1, Meta = 2;

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
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0, 1);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            RecoveredShaderProof.RequireImportedTextures(shader, ShaderPath, OriginalTextures);
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Meta: object position straight to clip space, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), black = new Color(0, 0, 0, 1);
                Color flat = new Color(0.5f, 0.5f, 1, 1), tangent = new Color(1, 0.5f, 1, 1);
                Color diffuse = new Color(0.25f, 0.5f, 0, 1);

                // The gradient colour red; no wind; vertex alpha 1 (with the default inverted gradient, no gradient).
                Material material = rig.Own(new Material(shader));
                material.SetColor("_Moustache_Gradient_Colour", red);
                material.SetFloat("_Wind_Strength", 0);
                material.SetTexture(Wind, rig.Texels(Color.white));
                material.SetTexture("_Diff_Texture", rig.Texels(diffuse));
                material.SetTexture("_Distortion_Texture", rig.Texels(new Color(0.5f, 0, 0, 1)));
                material.SetTexture("_Stache_Normal", rig.Texels(flat));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass: pass);
                    cases++;
                }

                Expect(diffuse, "the diffuse is the albedo");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(diffuse, "and the emission at strength 1");
                material.SetFloat("_Emission_Strength", 2);
                Expect(new Color(0.5f, 1, 0, 1), "the emission strength");
                material.SetFloat("_Emission_Strength", 1);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // The gradient: 1 - (1 - colour * strength) * (1 - diffuse), shown by (1 - vertex alpha) when inverted.
                rig.SetColors(new Color(1, 1, 1, 0));
                Expect(new Color(1, 0.5f, 0, 1), "inverted, vertex alpha 0 shows the screened gradient");
                material.SetFloat("_Moustache_Gradient_Strength", 0.5f);
                Expect(new Color(1 - 0.5f * 0.75f, 0.5f, 0, 1), "the gradient strength");
                material.SetFloat("_Moustache_Gradient_Strength", 1);
                material.SetFloat("_Moustache_Gradient_Mask_Power", 0.5f);
                Expect(new Color(0.625f, 0.5f, 0, 1), "the mask power");
                material.SetFloat("_Moustache_Gradient_Mask_Power", 1);
                material.SetFloat("_Invert_Gradient", 0);
                Expect(diffuse, "not inverted, vertex alpha 0 hides it");
                rig.SetColors(new Color(1, 1, 1, 0.5f));
                Expect(new Color(0.625f, 0.5f, 0, 1), "not inverted, vertex alpha shows it");
                material.SetFloat("_Invert_Gradient", 1);
                rig.SetColors(Color.white);

                // The diffuse UV on a 4-band strip (pixel 16 at u 0.5156, band 2): the distortion's red times the
                // intensity, the scroll and the offset each reach band 3.
                material.SetTexture("_Diff_Texture", rig.Bands(black, black, red, blue));
                Expect(red, "band 2");
                material.SetFloat("_Distortion_Intensity", 0.5f);
                Expect(blue, "the distortion intensity (band 3)");
                material.SetTexture("_Distortion_Texture", rig.Bands(black, black, black, new Color(0.5f, 0, 0, 1)));
                Expect(red, "the distortion's band 2 is still");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Distortion_Speed_X", 0.25f);
                Expect(blue, "the distortion scroll (band 3)");
                material.SetFloat("_Distortion_Speed_X", 0);
                material.SetFloat("_Distortion_Intensity", 0);
                material.SetFloat("_Diff_UV_Speed_X", 0.25f);
                Expect(blue, "the diffuse scroll (band 3)");
                material.SetFloat("_Diff_UV_Speed_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTextureOffset("_Diff_Texture", new Vector2(0.25f, 0));
                Expect(blue, "the diffuse offset (band 3)");
                material.SetTextureOffset("_Diff_Texture", Vector2.zero);

                // Wind on an 8-texel grid diffuse (linearly filtered, green = (8 v - 0.5) / 7): the noise red (LOD 0)
                // remapped by 1.5 - 0.5 times the strength lifts world Y (2 units across the quad) by vertex alpha, so
                // the pixel reads v lower by half the lift.
                material.SetTexture("_Diff_Texture", rig.Grid(8));
                float Green() => rig.Draw(material, Meta).g;
                float baseline = Green();
                void Lift(float lift, string label)
                {
                    float expected = baseline - lift / 2 * 8 / 7f, actual = Green();
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(actual - expected) < 0.01f, label + ": " + actual + " vs " + expected);
                    cases++;
                }
                material.SetFloat("_Wind_Strength", 0.5f);
                Lift(0.5f, "white noise at strength 0.5 lifts by 0.5");
                rig.SetColors(new Color(1, 1, 1, 0));
                Lift(0, "vertex alpha 0 holds it");
                rig.SetColors(new Color(1, 1, 1, 0.5f));
                Lift(0.25f, "vertex alpha 0.5 halves it");
                rig.SetColors(Color.white);
                material.SetTexture(Wind, rig.Texels(Color.black));
                Lift(-0.25f, "black noise lowers by 0.5 * strength");
                // A white and a black texel: scale 0 reads u = time * speed / 1000; scale 250 reads each vertex's
                // world X / 4 (the right vertices u 0.25, white; the left u 0.75, black), skewing the lift along X.
                material.SetTexture(Wind, rig.Texels(Color.white, Color.black));
                material.SetFloat("_WindScale", 0);
                Lift(0.5f, "the noise at u 0");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 7.5f);
                material.SetFloat("_WindSpeed", 100);
                Lift(-0.25f, "game time times the speed / 1000 (u 0.75)");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_WindScale", 250);
                Lift(-0.25f + 0.75f * (1 + 1 / 32f) / 2, "the wind scale / 1000 at each vertex's world X");
                material.SetFloat("_Wind_Strength", 0);
                material.SetTexture(Wind, rig.Texels(Color.white));
                material.SetTexture("_Diff_Texture", rig.Texels(diffuse));

                // DepthNormals (the quad faces -Z with tangent +X): the normal map (red times alpha, green) with its XY
                // times the strength and its Z eased from 1 by the saturated strength.
                Expect(new Color(0, 0, -1, 0), "flat normal", DepthNormals);
                material.SetTexture("_Stache_Normal", rig.Texels(tangent));
                Expect(new Color(1, 0, 0, 0), "the normal map", DepthNormals);
                Color tilted = new Color(0.8f, 0.5f, 1, 1);
                material.SetTexture("_Stache_Normal", rig.Texels(tilted));
                Vector3 n = new Vector3(0.3f, 0, 0.9f).normalized;
                material.SetFloat("_Normal_Strength", 0.5f);
                Expect(new Color(n.x, 0, -n.z, 0), "strength 0.5", DepthNormals);
                n = new Vector3(1.2f, 0, 0.8f).normalized;
                material.SetFloat("_Normal_Strength", 2);
                Expect(new Color(n.x, 0, -n.z, 0), "strength 2", DepthNormals);
                material.SetFloat("_Normal_Strength", 0);
                Expect(new Color(0, 0, -1, 0), "strength 0 flattens it", DepthNormals);
                material.SetFloat("_Normal_Strength", 1);
                material.SetTexture("_Stache_Normal", rig.Texels(flat));

                // Forward alpha, written through the shipped alpha factors (One OneMinusSrcAlpha) over a cleared target:
                // saturate(min(diffuse alpha * vertex red + alpha, opacity blue) * ((1 - vertex alpha) - strength + 1)).
                void Alpha(float expected, string label)
                {
                    float actual = rig.Draw(material, Forward).a;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(actual - expected) < 1e-3f, label + ": " + actual);
                    cases++;
                }
                Alpha(0, "the default strength 1 at vertex alpha 1 is clear");
                material.SetFloat("_Vertex_Alpha_Opacity_Strength", 0);
                material.SetFloat("_Alpha", 0);
                material.SetTexture("_Diff_Texture", rig.Texels(new Color(0.25f, 0.5f, 0, 0.5f)));
                Alpha(0.5f, "the diffuse alpha");
                material.SetFloat("_Alpha", 0.25f);
                Alpha(0.75f, "plus the alpha");
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                Alpha(0.5f, "the diffuse alpha times vertex red");
                rig.SetColors(Color.white);
                material.SetTexture("_Stache_Opacity", rig.Texels(new Color(1, 1, 0.5f, 1)));
                Alpha(0.5f, "capped by the opacity blue");
                material.SetFloat("_Vertex_Alpha_Opacity_Strength", 0.5f);
                Alpha(0.25f, "the vertex alpha opacity strength");
                rig.SetColors(new Color(1, 1, 1, 0.5f));
                Alpha(0.5f, "vertex alpha 0.5 at strength 0.5");
                material.SetFloat("_Vertex_Alpha_Opacity_Strength", 0);
                rig.SetColors(new Color(1, 1, 1, 0));
                Alpha(1, "vertex alpha 0 doubles it, saturated");
                rig.SetColors(Color.white);
                material.SetTexture("_Stache_Opacity", rig.Texels(Color.white));
                material.SetTexture("_Diff_Texture", rig.Texels(diffuse));
                material.SetFloat("_Alpha", 1);

                // _GlobalMipBias applies once to each sample: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Diff_Texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : blue));
                Expect(red, "diffuse: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "diffuse: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Diff_Texture", rig.Bands(black, black, red, blue));
                material.SetFloat("_Distortion_Intensity", 0.5f);
                material.SetTexture("_Distortion_Texture", rig.MipLevels(256, level => level == 4 ? new Color(0.5f, 0, 0, 1) : black));
                Expect(red, "distortion: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(blue, "distortion: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetFloat("_Distortion_Intensity", 0);
                material.SetTexture("_Stache_Normal", rig.MipLevels(256, level => level == 4 ? tangent : flat));
                Expect(new Color(0, 0, -1, 0), "normal: no bias reads level 3", DepthNormals);
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(new Color(1, 0, 0, 0), "normal: bias 1 reads level 4", DepthNormals);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Stache_Normal", rig.Texels(flat));
                material.SetTexture("_Stache_Opacity", rig.MipLevels(256, level => level == 4 ? new Color(1, 1, 0.5f, 1) : Color.white));
                Alpha(1, "opacity: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Alpha(0.5f, "opacity: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Char_Boss_4_Eggman_Moustache GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The forward lighting and the wind's object-scale terms remain " +
                          "compile-checked only.");
            }
        }
    }
}
