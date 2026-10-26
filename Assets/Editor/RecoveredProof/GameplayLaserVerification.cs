using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Laser, reconstructed from the shipped Metal programs: the main texture's
    // scrolling UV pulled towards the Simple Noise value, the dissolve towards alpha times noise, the mask (whose red
    // also masks the alpha), the colour and the vertex colour. The sine-hash noise is not reproducible on the CPU,
    // so the cases pin it to exactly 0 (a zero noise UV) or to about 1 (a near-zero levels power).
    public static class GameplayLaserVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Laser.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Laser";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "3be3cd85a9eb4b64884c8372e136710f";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "FOG_EXP2", "DOTS_INSTANCING_ON" },
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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;

                // Noise scale 0 makes every octave read the hash at the origin: noise 0. The 4-texel main strip reads
                // texel 2 (blue) at the pixel.
                material.SetFloat("_Noise_Scale", 0);
                material.SetFloat("_Noise_Amount", 0);
                material.SetVector("_Main_Speed", Vector4.zero);
                material.SetTexture("_Main_Texture", rig.Texels(red, green, blue, white));
                material.SetTexture("_Mask", rig.Texels(white));
                Expect(Blended(0, 0, 1, 1), "the main texture at the UV");
                material.SetVector("_Main_Speed", new Vector4(0.25f, 0, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Blended(1, 1, 1, 1), "the main texture scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetVector("_Main_Speed", Vector4.zero);
                material.SetFloat("_Noise_Amount", 1);
                Expect(Blended(1, 0, 0, 1), "amount 1 replaces the UV with the noise (0)");
                material.SetFloat("_Noise_Amount", 0.5f);
                Expect(Blended(0, 1, 0, 1), "amount 0.5 halves the UV towards it (u 0.26)");
                // A levels power near 0 lifts any positive noise to about 1: u (0.516 + 1) / 2 = 0.76, texel 3.
                material.SetFloat("_Noise_Scale", 25);
                material.SetFloat("_Noise_Levels", 1e-6f);
                Expect(Blended(1, 1, 1, 1), "noise 1 at amount 0.5");
                material.SetFloat("_Noise_Amount", 0);

                // Dissolve: all four channels lerp towards the main alpha times the noise.
                material.SetFloat("_Dissolve_Amount", 1);
                Expect(Blended(1, 1, 1, 1), "full dissolve at noise 1 is the main alpha");
                material.SetFloat("_Dissolve_Amount", 0.5f);
                Expect(Blended(0.5f, 0.5f, 1, 1), "half dissolve");
                material.SetFloat("_Dissolve_Amount", 1);
                material.SetTexture("_Main_Texture", rig.Texels(red, green, new Color(0, 0, 1, 0.5f), white));
                Expect(Blended(0.5f, 0.5f, 0.5f, 0.5f), "the dissolve scales by the main alpha");
                material.SetFloat("_Noise_Scale", 0);
                material.SetFloat("_Noise_Levels", 0.1f);
                Expect(Blended(0, 0, 0, 0), "full dissolve at noise 0 is clear");
                material.SetFloat("_Dissolve_Amount", 0);
                material.SetTexture("_Main_Texture", rig.Texels(red, green, blue, white));

                // The noise scrolls with game time: with every UV at (0.25, 0.25) and the noise speed -0.25 the noise
                // UV reaches the origin (noise 0, u 0.125, red) at time 1, and away from it the lifted noise is about
                // 1 (u 0.625, blue).
                material.SetFloat("_Noise_Amount", 0.5f);
                material.SetFloat("_Noise_Scale", 25);
                material.SetFloat("_Noise_Levels", 1e-6f);
                material.SetVector("_Noise_Speed", new Vector4(-0.25f, -0.25f, 0, 0));
                var quarter = new Vector4(0.25f, 0.25f, 0, 0);
                rig.Quad.SetUVs(0, new List<Vector4> { quarter, quarter, quarter, quarter });
                Expect(Blended(0, 0, 1, 1), "the noise away from the origin");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Blended(1, 0, 0, 1), "game time scrolls the noise to the origin");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                rig.SetUv0(Vector4.zero);
                material.SetFloat("_Noise_Amount", 0);
                material.SetFloat("_Noise_Scale", 0);
                material.SetFloat("_Noise_Levels", 0.1f);

                // Mask, colour and vertex colour.
                material.SetTexture("_Mask", rig.Texels(new Color(0.5f, 1, 1, 1)));
                Expect(Blended(0, 0, 1, 0.5f), "the mask's red masks the alpha too");
                material.SetTexture("_Mask", rig.Texels(new Color(1, 0, 1, 1)));
                material.SetTexture("_Main_Texture", rig.Texels(white, white, white, white));
                Expect(Blended(1, 0, 1, 1), "the mask multiplies the colour");
                material.SetTexture("_Mask", rig.Texels(white));
                material.SetColor("_Color", new Color(0, 1, 1, 0));
                Expect(Blended(0, 1, 1, 1), "the colour tints the RGB only");
                material.SetColor("_Color", Color.white);
                rig.SetColors(new Color(1, 1, 0, 0.5f));
                Expect(Blended(1, 1, 0, 0.5f), "the vertex colour multiplies everything");
                rig.SetColors(Color.white);

                // _GlobalMipBias applies once per sample: over UV0 a 256 texture with a colour per level reads level 3
                // unbiased and level 4 at bias 1 (both red, so the mask keeps the alpha).
                void BiasCases(string path)
                {
                    Color yellow = new Color(1, 1, 0, 1);
                    material.SetTexture(path, rig.MipLevels(256, level => level == 4 ? yellow : red));
                    Expect(Blended(1, 0, 0, 1), path + ": no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(Blended(1, 1, 0, 1), path + ": bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                    material.SetTexture(path, rig.Texels(white));
                }
                BiasCases("_Main_Texture");
                BiasCases("_Mask");

                Debug.Log("Project Lucid HL_Gameplay_Laser GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The noise's hash values and the shadow caster remain compile-checked only.");
            }
        }
    }
}
