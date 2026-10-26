using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone2_Goo_Pipes, reconstructed from the shipped Metal programs. Through the Meta
    // pass: the red noise (its contrast about 0.1, tiling, offset and scroll) picking between colours A and B, the green
    // noise (its (0.3, 0.2) offset, tiling and opacity) blending in its colour, the fresnel colour and its contrast, and
    // the same colour as the emission. DepthNormals checks the unmapped normal.
    public static class EnvZone2GooPipesVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone2_Goo_Pipes.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone2_Goo_Pipes";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "0e1ca3150ccc9fa4c8a3302f1207ff24";
        private const int DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "_REFLECTION_PROBE_BOX_PROJECTION", "_ADDITIONAL_LIGHT_SHADOWS", "FOG_EXP" },
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
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 5, "five shipped passes");
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
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color Noise(float r, float g = 0) => new Color(r, g, 0, 1);
                Color Mix(float t) => new Color(1 - t, 0, t, 1);

                // Colours A red, B blue, the green colour green and the fresnel colour white; no fresnel facing the camera.
                Material material = rig.Own(new Material(shader));
                material.SetColor("_R_Noise_Color_A", red);
                material.SetColor("_R_Noise_Color_B", blue);
                material.SetColor("_G_Noise_Colour", green);
                material.SetColor("_Fresnel_Colour", white);
                material.SetTexture("_Packed_Noise_Tex", rig.Texels(Noise(0.25f)));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass: pass);
                    cases++;
                }

                // Red picks between A and B by saturate((red - 0.1) * contrast + 0.1).
                Expect(Mix(0.25f), "red 0.25 at contrast 1");
                material.SetFloat("_R_Noise_Contrast", 2);
                Expect(Mix(0.4f), "contrast 2 about 0.1");
                material.SetFloat("_R_Noise_Contrast", 0);
                Expect(Mix(0.1f), "contrast 0 holds 0.1");
                material.SetFloat("_R_Noise_Contrast", 10);
                Expect(blue, "saturated");
                material.SetFloat("_R_Noise_Contrast", 1);
                // Green times its opacity (0.4 by default) blends in the green colour.
                material.SetTexture("_Packed_Noise_Tex", rig.Texels(Noise(0.25f, 0.5f)));
                Expect(new Color(0.6f, 0.2f, 0.2f, 1), "green 0.5 at opacity 0.4");
                material.SetFloat("_G_Noise_Opacity", 1);
                Expect(new Color(0.375f, 0.5f, 0.125f, 1), "the green opacity");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(new Color(0.375f, 0.5f, 0.125f, 1), "the emission is the same colour");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // The red UV on a 4-band red strip (0, 1, 0, 0; pixel 16 at u 0.5156, band 2): (uv + time * speed) through
                // the tiling, then the offset.
                material.SetTexture("_Packed_Noise_Tex", rig.Bands(Noise(0), Noise(1), Noise(0), Noise(0)));
                Expect(red, "band 2 has no red");
                material.SetVector("_Tiling_R_Noise_UV", new Vector4(1, 1, -0.25f, 0));
                Expect(blue, "the red offset (band 1)");
                material.SetVector("_Tiling_R_Noise_UV", new Vector4(2, 1, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_R_Noise_UV_Speed_Direction_X", 0.125f);
                Expect(blue, "the scroll goes through the tiling: (0.5156 + 0.125) * 2 (band 1)");
                material.SetFloat("_R_Noise_UV_Speed_Direction_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetVector("_Tiling_R_Noise_UV", new Vector4(1, 1, 0, 0));
                // The green UV on a 4-band green strip (0, 0, 0, 1): uv + (0.3, 0.2) reaches band 3.
                material.SetTexture("_Packed_Noise_Tex", rig.Bands(Noise(0), Noise(0), Noise(0), Noise(0, 1)));
                Expect(green, "the green offset of 0.3 (band 3)");
                material.SetVector("_Tiling_G_Noise_UV", new Vector4(1, 1, 0.25f, 0));
                Expect(red, "the green tiling's offset (band 0)");
                material.SetVector("_Tiling_G_Noise_UV", new Vector4(1, 1, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_G_Noise_UV_Speed_Direction_X", -0.25f);
                Expect(red, "the green scroll (band 2)");
                material.SetFloat("_G_Noise_UV_Speed_Direction_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTexture("_Packed_Noise_Tex", rig.Texels(Noise(0)));

                // The fresnel at n.v 0.8: weight saturate(1 - 0.2^amount * intensity * opacity) lerps from the fresnel
                // colour to the goo (red) and from the contrast to 1.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Intensity", 1);
                material.SetFloat("_Fresnel_Opacity", 1);
                Color Fresnel(float w, float contrast)
                {
                    float k = Mathf.Lerp(contrast, 1, w);
                    return new Color(k, (1 - w) * k, (1 - w) * k, 1);
                }
                Expect(Fresnel(0.8f, 0), "the fresnel weight at contrast 0");
                material.SetFloat("_Fresnel_Colour_Contrast", 1);
                Expect(Fresnel(0.8f, 1), "contrast 1 keeps the blend");
                material.SetFloat("_Fresnel_Colour_Contrast", 0.5f);
                Expect(Fresnel(0.8f, 0.5f), "the fresnel colour contrast");
                material.SetFloat("_Fresnel_Colour_Contrast", 0);
                material.SetFloat("_Fresnel_Intensity", 2);
                Expect(Fresnel(0.6f, 0), "the fresnel intensity");
                material.SetFloat("_Fresnel_Intensity", 1);
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(Fresnel(0.96f, 0), "the fresnel amount is a power");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Opacity", 0.5f);
                Expect(Fresnel(0.9f, 0), "the fresnel opacity");
                material.SetFloat("_Fresnel_Opacity", 0);

                // DepthNormals: the interpolated normal, unmapped.
                Expect(new Color(0.6f, 0, -0.8f, 0), "the normal", DepthNormals);
                rig.SetNormals(Vector3.back);
                Expect(new Color(0, 0, -1, 0), "a facing normal", DepthNormals);

                // _GlobalMipBias applies once to each noise sample: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Packed_Noise_Tex", rig.MipLevels(256, level => level == 3 ? Noise(0.25f) : level == 4 ? Noise(0.5f) : Noise(0)));
                Expect(Mix(0.25f), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(Mix(0.5f), "bias 1 reads level 4");
                material.SetTexture("_Packed_Noise_Tex", rig.MipLevels(256, level => level == 4 ? Noise(0, 1) : Noise(0)));
                Expect(green, "the green sample's bias too");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Env_Zone2_Goo_Pipes GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The forward lighting (dielectric, flat smoothness) remains compile-checked only.");
            }
        }
    }
}
