using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Dream_Barrier, reconstructed from the shipped Metal programs: the diffuse times its
    // tint (its tiling and offset applied twice), the distortion noise's scroll and amounts, the rotated, tiled and
    // scrolling scanlines stepped by the thickness towards the scanline colour, and the alpha from the diffuse
    // alpha, vertex red and the camera fade.
    public static class GameplayDreamBarrierVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Dream_Barrier.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Dream_Barrier";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "e5071b70cd80f5b48ac389075c4513f1";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D", "e8055f4d7423e2a41a7e7cd68a052b5c" },
            { "_Texture2DAsset_efea1f31ccab439c9e6d1d1c2705abe6_Out_0_Texture2D", "737b0c6c5bbc2af4198c74848846235c" },
        };
        private const string DistortionNoise = "_Texture2DAsset_efea1f31ccab439c9e6d1d1c2705abe6_Out_0_Texture2D";
        private const string Scanlines = "_SampleTexture2D_7ded27fa0d1b4e12b302d5bbab485959_Texture_1_Texture2D";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
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

        public static void Run()
        {
            Shader shader = Load();
            RecoveredShaderProof.RequireImportedTextures(shader, ShaderPath, OriginalTextures);
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                int cases = 0;
                Color black = new Color(0, 0, 0, 1), white = Color.white, red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1),
                      blue = new Color(0, 0, 1, 1), magenta = new Color(1, 0, 1, 1);

                // The pass blends SrcAlpha One (alpha: One One) over a cleared target: a fragment (rgb, a) reads (rgb * a, a).
                Material material = rig.Own(new Material(shader));
                material.SetTexture(DistortionNoise, rig.Texels(black));
                material.SetTexture(Scanlines, rig.Texels(black));
                material.SetTexture("_Diff_Texture", rig.Texels(magenta));
                void Expect(Color c, string label, int x = 16, int y = 16)
                {
                    rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a), label, x: x, y: y);
                    cases++;
                }

                // The diffuse times the tint.
                Expect(magenta, "the diffuse");
                material.SetColor("_Diff_Colour_Tint", new Color(0, 1, 1, 1));
                Expect(blue, "times the tint");
                material.SetColor("_Diff_Colour_Tint", white);

                // Alpha: saturate(saturate(diffuse alpha + channel amount) * vertex red * global amount), times the
                // camera fade 1 - saturate((clip w - fade) / 100) with clip w 1.
                material.SetTexture("_Diff_Texture", rig.Texels(new Color(1, 0, 1, 0.5f)));
                Expect(new Color(1, 0, 1, 0.5f), "the diffuse alpha");
                material.SetFloat("_Diff_Alpha_Channel_Amount", 0.25f);
                Expect(new Color(1, 0, 1, 0.75f), "the alpha channel amount");
                material.SetFloat("_Diff_Alpha_Channel_Amount", 1);
                material.SetFloat("_Diff_Global_Alpha_Amount", 0.5f);
                Expect(new Color(1, 0, 1, 0.5f), "the channel amount saturates before the global amount");
                material.SetFloat("_Diff_Global_Alpha_Amount", 1);
                material.SetFloat("_Diff_Alpha_Channel_Amount", -1);
                Expect(new Color(0, 0, 0, 0), "a negative channel amount clears it");
                material.SetFloat("_Diff_Alpha_Channel_Amount", 0);
                material.SetFloat("_Diff_Global_Alpha_Amount", 2);
                Expect(magenta, "the global amount");
                rig.SetColors(new Color(0.5f, 0, 0, 0));
                Expect(new Color(1, 0, 1, 0.5f), "vertex red (only)");
                rig.SetColors(white);
                material.SetFloat("_Diff_Global_Alpha_Amount", 1);
                material.SetTexture("_Diff_Texture", rig.Texels(magenta));
                material.SetFloat("_Alpha_Camera_Fade", -49);
                Expect(new Color(1, 0, 1, 0.5f), "the camera fade");
                material.SetFloat("_Alpha_Camera_Fade", -99);
                Expect(new Color(0, 0, 0, 0), "faded out");
                material.SetFloat("_Alpha_Camera_Fade", 100);
                Expect(magenta, "inside the fade distance");
                material.SetFloat("_Alpha_Camera_Fade", 1);

                // The diffuse UV on a 4-band strip (pixel 16 at u 0.5156, band 2): the tiling and offset apply twice,
                // so offset 0.25 moves it by 0.5 (band 0), not 0.25 (band 3).
                material.SetTexture("_Diff_Texture", rig.Bands(red, green, blue, white));
                Expect(blue, "band 2");
                material.SetTextureOffset("_Diff_Texture", new Vector2(0.25f, 0));
                Expect(red, "the offset applies twice");
                material.SetTextureOffset("_Diff_Texture", Vector2.zero);

                // The distortion: noise (0.25, 0.5) times the amount offsets the diffuse UV.
                material.SetTexture(DistortionNoise, rig.Texels(new Color(0.25f, 0.5f, 0, 1)));
                Expect(blue, "no distortion at the default amount 0");
                material.SetFloat("_Distortion_Amount", 1);
                Expect(white, "amount 1 moves u by 0.25 (band 3)");
                material.SetFloat("_Distortion_Amount", -1);
                Expect(green, "amount -1 moves u by -0.25 (band 1)");
                // The noise UV with the tiling at 0: u = time * speed x, on a 4-band noise strip (1, 0, 0, 0).
                material.SetFloat("_Distortion_Amount", 0.25f);
                material.SetTexture(DistortionNoise, rig.Bands(red, black, black, black));
                material.SetFloat("_Distortion_UV_Tiling_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Distortion_UV_Speed_Direction_X", 0.125f);
                Expect(white, "noise band 0 moves the diffuse to band 3");
                material.SetFloat("_Distortion_UV_Speed_Direction_X", 0.375f);
                Expect(blue, "the noise scrolls to band 1 (no offset)");
                material.SetFloat("_Distortion_UV_Speed_Direction_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Distortion_UV_Tiling_X", 1);
                material.SetFloat("_Distortion_Amount", 0);

                // The scanlines over a black diffuse with a white scanline colour read back as the stepped scanline
                // texture itself: each channel at or above the thickness (0.5) is 1.
                material.SetTexture("_Diff_Texture", rig.Texels(black));
                material.SetTexture(Scanlines, rig.Texels(new Color(0.75f, 0.25f, 0.5f, 1)));
                Expect(new Color(1, 0, 1, 1), "channels at or above the thickness");
                material.SetFloat("_Scanlines_Thickness", 0.25f);
                Expect(white, "a thinner threshold");
                material.SetFloat("_Scanlines_Thickness", 0.5f);
                material.SetTexture("_Diff_Texture", rig.Texels(new Color(0, 1, 0, 1)));
                material.SetColor("_Scanlines_Colour", new Color(0, 0, 1, 0.5f));
                Expect(new Color(0, 1, 0.5f, 1), "halfway to the scanline colour by its alpha, per channel");
                material.SetColor("_Scanlines_Colour", white);
                material.SetTexture("_Diff_Texture", rig.Texels(black));

                // The scanline UV on a 4-band strip (black, red, green, blue): rotated about the centre, tiled, scrolled.
                material.SetTexture(Scanlines, rig.Bands(black, red, green, blue));
                Expect(green, "pixel 16 at u 0.5156 (band 2)");
                Expect(red, "pixel (12, 24) at u 0.39 (band 1)", 12, 24);
                material.SetFloat("_Scanlines_UV_Rotation", 90);
                Expect(blue, "rotation 90 reads v: u 0.7656 (band 3)", 12, 24);
                material.SetFloat("_Scanlines_UV_Rotation", -90);
                Expect(black, "rotation -90 reads 1 - v: u 0.2344 (band 0)", 12, 24);
                material.SetFloat("_Scanlines_UV_Rotation", 0);
                material.SetFloat("_Scanlines_UV_Tiling", 2);
                Expect(red, "tiling 2 at pixel 4: u 0.281 (band 1)", 4);
                material.SetFloat("_Scanlines_UV_Tiling", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Scanlines_UV_Speed_Direction", 0.875f);
                Expect(blue, "scrolled by game time times the speed (band 3)");
                material.SetFloat("_Scanlines_UV_Speed_Direction", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Scanlines_UV_Tiling", 1);
                material.SetTexture(DistortionNoise, rig.Texels(new Color(0.25f, 0, 0, 1)));
                material.SetFloat("_Distortion_Amount", 1);
                Expect(green, "the diffuse distortion leaves the scanlines");
                material.SetFloat("_Scanlines_Distortion_Amount", 1);
                Expect(blue, "the scanline distortion amount (band 3)");
                material.SetFloat("_Scanlines_Distortion_Amount", 0);
                material.SetFloat("_Distortion_Amount", 0);
                material.SetTexture(DistortionNoise, rig.Texels(black));

                // _GlobalMipBias applies once to the diffuse and to the scanlines: across the quad a 256 texture is read
                // at level 3; bias 1 reads level 4.
                material.SetTexture(Scanlines, rig.Texels(black));
                material.SetTexture("_Diff_Texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : black));
                Expect(red, "the diffuse without bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "the diffuse with bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Diff_Texture", rig.Texels(black));
                material.SetTexture(Scanlines, rig.MipLevels(256, level => level == 4 ? white : black));
                Expect(black, "the scanlines without bias read level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(white, "the scanlines with bias 1 read level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Gameplay_Dream_Barrier GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ".");
            }
        }
    }
}
