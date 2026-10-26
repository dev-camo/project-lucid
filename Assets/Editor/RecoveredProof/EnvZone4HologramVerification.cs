using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone4_Hologram, reconstructed from the shipped Metal programs: the pattern texture
    // (its tiling and offset applied twice), the tint, the alpha times the scan lines (the positive half of a sine
    // along v, scrolled by game time and lifted by the line alpha), the clip at the material threshold and its
    // alpha-to-coverage sharpening.
    public static class EnvZone4HologramVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone4_Hologram.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone4_Hologram";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "8311a68f3e699dc47ab7b4af3cee94be";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_WRITE_RENDERING_LAYERS", "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target.
        private static Color Blended(float r, float g, float b, float a) => new Color(r * a, g * a, b * a, a);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                rig.SaveFloats("_HLShaderTimeGame", "_AlphaToMaskAvailable");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color clear = new Color(0, 0, 0, 0);

                // The default line alpha (1) keeps the lines at 1. The 4-texel strip reads texel 2 (blue).
                material.SetTexture("_RGB_Pattern_Texture", rig.Texels(red, green, blue, white));
                Expect(Blended(0, 0, 1, 1), "the pattern under the pixel");
                // Offset 0.25 applied twice: u 1.016 (texel 0), not 0.766 (texel 3).
                material.SetTextureOffset("_RGB_Pattern_Texture", new Vector2(0.25f, 0));
                Expect(Blended(1, 0, 0, 1), "the tiling and offset apply twice");
                material.SetTextureOffset("_RGB_Pattern_Texture", Vector2.zero);
                // Tiling 0.6 applied twice: u 0.186 (texel 0), not 0.309 (texel 1).
                material.SetTextureScale("_RGB_Pattern_Texture", new Vector2(0.6f, 1));
                Expect(Blended(1, 0, 0, 1), "the tiling applies twice");
                material.SetTextureScale("_RGB_Pattern_Texture", Vector2.one);
                material.SetTexture("_RGB_Pattern_Texture", rig.Texels(white));
                material.SetColor("_ColorTint", new Color(0, 1, 1, 1));
                Expect(Blended(0, 1, 1, 1), "the tint");
                material.SetColor("_ColorTint", white);

                // Alpha: the pattern alpha times the alpha, clipped at the threshold.
                Color Pattern(float a) => new Color(1, 1, 1, a);
                material.SetTexture("_RGB_Pattern_Texture", rig.Texels(Pattern(0.8f)));
                Expect(Blended(1, 1, 1, 0.8f), "the pattern alpha");
                material.SetFloat("_Alpha", 0.5f);
                Expect(clear, "0.4 is clipped at 0.5");
                material.SetFloat("_Alpha_Clip", 0.3f);
                Expect(Blended(1, 1, 1, 0.4f), "the clip threshold");
                material.SetFloat("_Alpha", 1);
                material.SetFloat("_Alpha_Clip", 0.1f);
                material.SetTexture("_RGB_Pattern_Texture", rig.Texels(white));

                // Scan lines at v 0.516: amount (pi / 6) / v gives sin 0.5, lifted by the line alpha 0.25.
                const float v = 16.5f / 32f;
                material.SetFloat("_Line_Alpha", 0.25f);
                material.SetFloat("_Line_Amount", Mathf.PI / 6 / v);
                Expect(Blended(1, 1, 1, 0.75f), "the scan line sine plus the line alpha");
                material.SetFloat("_Line_Amount", Mathf.PI * 1.5f / v);
                Expect(Blended(1, 1, 1, 0.25f), "the negative half of the sine is cut to 0");
                // Game time scrolls the lines: (time * speed + v) * amount, here pi / 2 at time 0 and pi at time 1.
                material.SetFloat("_Line_Amount", Mathf.PI / 2 / v);
                material.SetFloat("_Line_Speed", v);
                Expect(white, "the line crest");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Blended(1, 1, 1, 0.25f), "game time scrolls the lines");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Line_Speed", 0);
                material.SetFloat("_Line_Alpha", 1);
                material.SetFloat("_Line_Amount", 100);

                // Alpha-to-coverage sharpens the alpha: just under the 0.5 threshold it is 0.5 rather than clipped.
                material.SetFloat("_Alpha_Clip", 0.5f);
                material.SetTexture("_RGB_Pattern_Texture", rig.Texels(Pattern(0.49995f)));
                Expect(clear, "just under the threshold is clipped");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                float sharpened = rig.Draw(material).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(sharpened - 0.5f) < 0.02f, "alpha-to-coverage sharpens it to 0.5: " + sharpened);
                cases++;
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);

                // _GlobalMipBias applies once: over UV0 a 256 texture reads level 3 unbiased and level 4 at bias 1.
                material.SetTexture("_RGB_Pattern_Texture", rig.MipLevels(256, level => level == 4 ? green : red));
                Expect(Blended(1, 0, 0, 1), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(Blended(0, 1, 0, 1), "bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Env_Zone4_Hologram GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
