using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Timed_Platform, reconstructed from the shipped Metal programs: the
    // UV1 clock wipe between the two lightness-lifted pattern colours, the distortion, the rotated and
    // scrolling stripe alpha, the paused flicker time, the glitch, and the graph fog that
    // _Use_Scene_Fog removes.
    public static class GameplayTimedPlatformVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Timed_Platform.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Timed_Platform";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "439e8a81d893fa244a6c338d94b5b25b";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "FOG_EXP2", "DOTS_INSTANCING_ON" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "_FOG_LINEAR" },
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
            bool fogWasOn = Shader.IsKeywordEnabled("FOG_LINEAR");
            try
            {
                using (var rig = new RecoveredShaderRig(shader))
                {
                    RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                    rig.SaveFloats("_HLShaderTimeGame");
                    rig.SaveVectors("unity_FogParams", "unity_FogColor", "_ProjectionParams");
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                    Shader.DisableKeyword("FOG_LINEAR");
                    void Uv1(float u, float v) => rig.Quad.SetUVs(1, new List<Vector4> { new Vector4(u, v), new Vector4(u, v), new Vector4(u, v), new Vector4(u, v) });
                    Uv1(1, 0.5f);

                    // The pattern is sampled bilinearly, so it is four 16-texel bands. Blue (main colour):
                    // 0, 0, 0.5, 1. Green (stripes): 1, 0, 1, 0. The centre column reads band 2.
                    Material material = rig.Own(new Material(shader));
                    material.SetTexture("__RGB_Pattern_Texture", rig.Bands(new Color(0, 1, 0, 1), new Color(0, 0, 0, 1),
                                                                           new Color(0, 1, 0.5f, 1), new Color(0, 0, 1, 1)));
                    Color black = new Color(0, 0, 0, 1);
                    material.SetTexture("_Clock_Mask_Pattern", rig.Texels(black, black, black, black));
                    material.SetTexture("_Glitch_Texture", rig.Texels(black, black, black, black));
                    Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                    material.SetTexture("_Distortion_Texture", rig.Texels(grey, grey, grey, grey));
                    material.SetColor("_Main_Colour_A", Color.red);
                    material.SetColor("_Main_Colour_B", Color.blue);
                    material.SetFloat("_Stripe_Scale", 1);
                    material.SetFloat("_Stripe_Alpha_A", 1);
                    material.SetFloat("_Stripe_Alpha_B", 0.5f);

                    // Pattern blue 0.5 smoothsteps to 0.5: colour B at half; stripe green 1 gives alpha B (0.5).
                    rig.Expect(material, Blended(0, 0, 0.5f, 0.5f), "colour B and stripe alpha B");
                    material.SetFloat("_Main_Colour_B_Lightness", 0.5f);
                    rig.Expect(material, Blended(0, 0, 1, 0.5f), "colour B lightness");
                    material.SetFloat("_Main_Colour_B_Lightness", 0);
                    material.SetFloat("_Use_Stripes", 1);
                    rig.Expect(material, Blended(0, 0, 0.5f, 1), "disabling stripes gives alpha 1");
                    material.SetFloat("_Use_Stripes", 0);

                    // Clock: UV1 (1, 0.5) is at angle 0 and (0.5, 1) at a quarter turn; clock mask 0.3 adds 0.8, so
                    // only the quarter turn reaches 1 and switches to colour A.
                    material.SetFloat("_Clock_Mask", 0.3f);
                    rig.Expect(material, Blended(0, 0, 0.5f, 0.5f), "clock below a full turn keeps colour B");
                    Uv1(0.5f, 1);
                    rig.Expect(material, Blended(0.5f, 0, 0, 0.5f), "clock past a full turn shows colour A");
                    Uv1(1, 0.5f);
                    material.SetFloat("_Clock_Mask", 0);
                    material.SetTexture("_Clock_Mask_Pattern", rig.Texels(Color.white, Color.white, Color.white, Color.white));
                    rig.Expect(material, Blended(0.5f, 0, 0, 0.5f), "the clock pattern adds to the mask");
                    material.SetTexture("_Clock_Mask_Pattern", rig.Texels(black, black, black, black));

                    // Distortion (grey 0.5 at amount 0.5) moves both lookups by 0.25 to band 3: blue 1, green 0.
                    material.SetFloat("_Distortion_Amount", 0.5f);
                    rig.Expect(material, Blended(0, 0, 1, 1), "distortion moves the pattern and stripes");
                    material.SetFloat("_Distortion_Amount", 0);

                    // Stripes scroll with time (0.25 to band 3, alpha A) unless the flicker is paused.
                    material.SetFloat("_Stripe_Speed", 0.25f);
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                    rig.Expect(material, Blended(0, 0, 0.5f, 1), "stripes scroll with time");
                    material.SetFloat("_Pause_Flicker", 1);
                    rig.Expect(material, Blended(0, 0, 0.5f, 0.5f), "paused flicker holds the stripes");
                    material.SetFloat("_Pause_Flicker", 0);
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                    material.SetFloat("_Stripe_Speed", 0);

                    // Stripe rotation: at pixel 8 (u 0.27, band 1, alpha A) a quarter turn (0.5 * 3.145) swaps in
                    // v (0.52, band 2, alpha B). The main colour there reads band 1 (blue 0).
                    rig.Expect(material, Blended(0, 0, 0, 1), "unrotated stripes off-centre", x: 8);
                    material.SetFloat("_Stripes_Rotation", 0.5f);
                    rig.Expect(material, Blended(0, 0, 0, 0.5f), "rotated stripes", x: 8);
                    material.SetFloat("_Stripes_Rotation", 0);

                    // Glitch at time 0: the seed is 0 and the random 0, so the glitch value is _Min (0.5). It adds
                    // the glitch texture (green) and scales the alpha. The object sits off X 0 to keep the
                    // per-object seed finite.
                    Matrix4x4 offset = Matrix4x4.Translate(new Vector3(0.001f, 0, 0.5f));
                    material.SetFloat("_Use_Glitch", 1);
                    material.SetFloat("_Min", 0.5f);
                    material.SetTexture("_Glitch_Texture", rig.Texels(Color.green, Color.green, Color.green, Color.green));
                    rig.Expect(material, Blended(0, 1, 0.5f, 0.25f), "glitch tint and alpha", model: offset);
                    material.SetFloat("_Use_Glitch", 0);
                    material.SetFloat("_Min", 0);

                    // Fog: factor 0.25 is density 0.75 towards red unless _Use_Scene_Fog is set.
                    Shader.EnableKeyword("FOG_LINEAR");
                    Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                    Shader.SetGlobalVector("unity_FogParams", new Vector4(0, 0, 0, 0.25f));
                    Shader.SetGlobalVector("unity_FogColor", new Vector4(1, 0, 0, 1));
                    rig.Expect(material, Blended(0.75f, 0, 0.125f, 0.5f), "graph fog");
                    material.SetFloat("_Use_Scene_Fog", 1);
                    rig.Expect(material, Blended(0, 0, 0.5f, 0.5f), "_Use_Scene_Fog removes it");
                    material.SetFloat("_Use_Scene_Fog", 0);
                    Shader.DisableKeyword("FOG_LINEAR");

                    // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original: over UV0 a 256
                    // texture with a colour per level reads level 3 unbiased and level 4 at bias 1 (level 5 if twice).
                    void BiasCases(Color unbiased, Color once, string path, Matrix4x4? model = null)
                    {
                        rig.Expect(material, unbiased, path + ": no bias reads level 3", model: model);
                        Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                        rig.Expect(material, once, path + ": bias 1 applies once", model: model);
                        Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                    }
                    // The pattern feeds the main colour (blue) and the stripes (green) through separate samples:
                    // level 3 (blue 0.5, green 1), level 4 (blue 1, green 0), level 5 (blue 0, green 1).
                    material.SetTexture("__RGB_Pattern_Texture", rig.MipLevels(256, level => level <= 3 ? new Color(0, 1, 0.5f, 1) : level == 4 ? new Color(0, 0, 1, 1) : new Color(0, 1, 0, 1)));
                    BiasCases(Blended(0, 0, 0.5f, 0.5f), Blended(0, 0, 1, 1), "pattern");
                    Color patternTexel = new Color(0, 1, 0.5f, 1);
                    material.SetTexture("__RGB_Pattern_Texture", rig.Texels(patternTexel, patternTexel, patternTexel, patternTexel));
                    // Clock pattern: level 4 (red 1) switches to colour A.
                    material.SetTexture("_Clock_Mask_Pattern", rig.MipLevels(256, level => level == 4 ? Color.red : black));
                    BiasCases(Blended(0, 0, 0.5f, 0.5f), Blended(0.5f, 0, 0, 0.5f), "clock pattern");
                    material.SetTexture("_Clock_Mask_Pattern", rig.Texels(black, black, black, black));
                    // Glitch texture (glitch value 0.5): level 4 adds green.
                    material.SetFloat("_Use_Glitch", 1);
                    material.SetFloat("_Min", 0.5f);
                    material.SetTexture("_Glitch_Texture", rig.MipLevels(256, level => level == 4 ? Color.green : level <= 3 ? black : Color.blue));
                    BiasCases(Blended(0, 0, 0.5f, 0.25f), Blended(0, 1, 0.5f, 0.25f), "glitch texture", Matrix4x4.Translate(new Vector3(0.001f, 0, 0.5f)));
                    material.SetFloat("_Use_Glitch", 0);
                    material.SetFloat("_Min", 0);
                    // Distortion (amount 0.5) on pattern bands (blue 0, 0, 0.5, 1; green 1, 0, 1, 0): level 4 (0.5)
                    // moves both lookups to band 3 (blue 1, green 0).
                    material.SetTexture("__RGB_Pattern_Texture", rig.Bands(new Color(0, 1, 0, 1), new Color(0, 0, 0, 1),
                                                                           new Color(0, 1, 0.5f, 1), new Color(0, 0, 1, 1)));
                    material.SetFloat("_Distortion_Amount", 0.5f);
                    material.SetTexture("_Distortion_Texture", rig.MipLevels(256, level => level <= 3 ? black : level == 4 ? new Color(0.5f, 0.5f, 0, 1) : new Color(1, 1, 0, 1)));
                    BiasCases(Blended(0, 0, 0.5f, 0.5f), Blended(0, 0, 1, 1), "distortion");

                    Debug.Log("Project Lucid HL_Gameplay_Timed_Platform GPU verification passed: 22 Metal-derived cases on " +
                              SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
                }
            }
            finally
            {
                if (fogWasOn) Shader.EnableKeyword("FOG_LINEAR");
                else Shader.DisableKeyword("FOG_LINEAR");
            }
        }
    }
}
