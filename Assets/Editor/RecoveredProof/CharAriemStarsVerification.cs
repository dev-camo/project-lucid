using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Ariem_Stars, reconstructed from the shipped Metal programs: the three star layers
    // of the screen-space stars texture (their colours, alphas, twinkle phases and scrolls), the cloud noise and its
    // colour over the background, the fresnel colour masked by the mirrored cloud read, and the DepthOnly depth.
    public static class CharAriemStarsVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Ariem_Stars.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Ariem_Stars";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "28c8c73cf4147ed4286a3a805ed7ad93";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
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

        // The star layers: blue (channel times colour, twinkle and colour alpha), then green and red covering it by
        // channel times twinkle and colour alpha, with their colour times the twinkle.
        private static Vector3 Stars(Vector3 channels, Color red, Color green, Color blue, float twinkleR = 1, float twinkleG = 1, float twinkleB = 1)
        {
            Vector3 stars = new Vector3(blue.r, blue.g, blue.b) * (channels.z * twinkleB * blue.a);
            stars = Vector3.Lerp(stars, new Vector3(green.r, green.g, green.b) * twinkleG, twinkleG * channels.y * green.a);
            return Vector3.Lerp(stars, new Vector3(red.r, red.g, red.b) * twinkleR, twinkleR * channels.x * red.a);
        }

        // The sum's red picks it over the background.
        private static Color OverBackground(Vector3 stars, Vector3 background)
        {
            Vector3 c = Vector3.LerpUnclamped(background, stars, stars.x);
            return new Color(c.x, c.y, c.z, 1);
        }

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                rig.SaveVectors("_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Pixel 16 sits at screen u 0.5156 (band 2 of 4).
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color none = new Color(0, 0, 0, 1);
                Vector3 background = new Vector3(0, 0, 1);

                // Background blue, white blue stars at alpha 0.5, green and red star colours at alpha 1 and 0.5, a red
                // noise colour; the fresnel off.
                Material material = rig.Own(new Material(shader));
                Color starsBlue = new Color(1, 1, 1, 0.5f), starsGreen = green, starsRed = new Color(1, 0, 0, 0.5f);
                material.SetColor("_Background_Colour", blue);
                material.SetColor("_Stars_Blue_Colour", starsBlue);
                material.SetColor("_Stars_Green_Colour", starsGreen);
                material.SetColor("_Stars_Red_Colour", starsRed);
                material.SetColor("_Noise_Colour", red);
                material.SetColor("_Fresnel_Colour", red);
                material.SetFloat("_Fresnel_Opacity", 0);
                material.SetTexture("_Stars_Texture", rig.Texels(none));
                material.SetTexture("_Cloud_Texture", rig.Texels(none));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }
                Color Layers(Vector3 channels, float twinkleR = 1, float twinkleG = 1, float twinkleB = 1) =>
                    OverBackground(Stars(channels, starsRed, starsGreen, starsBlue, twinkleR, twinkleG, twinkleB), background);

                Expect(blue, "no stars or cloud shows the background");
                material.SetTexture("_Cloud_Texture", rig.Texels(new Color(0.5f, 0, 0, 1)));
                Expect(OverBackground(new Vector3(0.5f, 0, 0), background), "the cloud times the noise colour, its red over the background");
                material.SetTexture("_Cloud_Texture", rig.Texels(none));

                // The star layers at time 0 (every twinkle 1).
                material.SetTexture("_Stars_Texture", rig.Texels(new Color(0, 0, 1, 1)));
                Expect(Layers(new Vector3(0, 0, 1)), "blue stars times their colour alpha");
                material.SetTexture("_Stars_Texture", rig.Texels(new Color(0, 0.5f, 1, 1)));
                Expect(Layers(new Vector3(0, 0.5f, 1)), "green stars cover the blue");
                material.SetTexture("_Stars_Texture", rig.Texels(new Color(1, 0.5f, 1, 1)));
                Expect(Layers(new Vector3(1, 0.5f, 1)), "red stars cover both");

                // Twinkles: max(|frac(time * speed * multiplier) * 2 - 1|, 0.1); the speeds default to 0.1.
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2.5f);
                Expect(Layers(new Vector3(1, 0.5f, 1), 0.5f, 0.5f, 0.5f), "every twinkle at 0.5");
                material.SetFloat("_Stars_Phase_Mask_Speed_2", 0);
                Expect(Layers(new Vector3(1, 0.5f, 1), 0.5f, 0.5f, 1), "the blue phase speed");
                material.SetFloat("_Stars_Phase_Mask_Speed_1", 0);
                Expect(Layers(new Vector3(1, 0.5f, 1), 0.5f, 1, 1), "the green phase speed");
                material.SetFloat("_Stars_Phase_Mask_Speed", 0.2f);
                Expect(Layers(new Vector3(1, 0.5f, 1), 0.1f, 1, 1), "the red phase speed (floored at 0.1)");
                material.SetFloat("_Stars_Phase_Mask_Speed", 0.1f);
                material.SetFloat("_Stars_Phase_Mask_Speed_3", 2);
                Expect(Layers(new Vector3(1, 0.5f, 1), 0.1f, 1, 1), "the phase speed multiplier");
                material.SetFloat("_Stars_Phase_Mask_Speed_3", 1);
                material.SetFloat("_Stars_Phase_Mask_Speed", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);

                // The scrolls in screen space on a 4-band strip (blue stars in band 3 only).
                material.SetTexture("_Stars_Texture", rig.Bands(none, none, none, new Color(0, 0, 1, 1)));
                Color blueStars = Layers(new Vector3(0, 0, 1));
                Expect(blue, "band 2 has no stars");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Stars_Blue_UV_Speed_Direction_X", 0.25f);
                Expect(blueStars, "the blue scroll (band 3)");
                material.SetFloat("_Stars_Blue_UV_Speed_Direction_X", 0.5f);
                material.SetFloat("_Global_UV_Speed_Mult", 0.5f);
                Expect(blueStars, "the global speed multiplier (band 3)");
                material.SetFloat("_Global_UV_Speed_Mult", 1);
                material.SetFloat("_Stars_Blue_UV_Speed_Direction_X", 0);
                material.SetTexture("_Stars_Texture", rig.Bands(none, none, none, new Color(0, 1, 0, 1)));
                material.SetFloat("_Stars_Green_UV_Speed_Direction_X", 0.25f);
                Expect(Layers(new Vector3(0, 1, 0)), "the green scroll (band 3)");
                material.SetFloat("_Stars_Green_UV_Speed_Direction_X", 0);
                material.SetTexture("_Stars_Texture", rig.Bands(none, none, none, new Color(1, 0, 0, 1)));
                material.SetFloat("_Stars_Red_UV_Speed_Direction_X", 0.25f);
                Expect(Layers(new Vector3(1, 0, 0)), "the red scroll (band 3)");
                material.SetFloat("_Stars_Red_UV_Speed_Direction_X", 0);
                material.SetTextureOffset("_Stars_Texture", new Vector2(0.25f, 0));
                Expect(Layers(new Vector3(1, 0, 0)), "the stars offset (band 3)");
                material.SetTextureOffset("_Stars_Texture", Vector2.zero);
                material.SetTexture("_Stars_Texture", rig.Texels(none));
                material.SetTexture("_Cloud_Texture", rig.Bands(none, none, none, new Color(0.5f, 0, 0, 1)));
                material.SetFloat("_Noise_UV_Speed_Direction_X", 0.25f);
                Expect(OverBackground(new Vector3(0.5f, 0, 0), background), "the cloud scroll (band 3)");
                material.SetFloat("_Noise_UV_Speed_Direction_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);

                // The fresnel at n.v 0.8 (amount 1, intensity 1, opacity 1): weight 0.8 from the fresnel colour to the
                // background, shown by the mirrored cloud red halved and lifted by 0.5 (0.5 on a black cloud).
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.SetFloat("_Fresnel_Opacity", 1);
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetTexture("_Cloud_Texture", rig.Texels(none));
                Color Fresnel(float weight, float mask)
                {
                    Vector3 rim = Vector3.Lerp(new Vector3(1, 0, 0), background, weight);
                    Vector3 c = Vector3.Lerp(background, rim, mask);
                    return new Color(c.x, c.y, c.z, 1);
                }
                Expect(Fresnel(0.8f, 0.5f), "the fresnel at mask 0.5");
                material.SetFloat("_Fresnel_Intensity", 2);
                Expect(Fresnel(0.6f, 0.5f), "the fresnel intensity");
                material.SetFloat("_Fresnel_Intensity", 1);
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(Fresnel(0.96f, 0.5f), "the fresnel amount is a power");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Opacity", 0.5f);
                Expect(Fresnel(0.9f, 0.5f), "the fresnel opacity");
                material.SetFloat("_Fresnel_Opacity", 1);
                // The mask reads the cloud at one minus the UV (band 1), through the tiling and offset again.
                material.SetTexture("_Cloud_Texture", rig.Bands(none, new Color(1, 0, 0, 1), none, none));
                Expect(Fresnel(0.8f, 1), "the mirrored cloud read (band 1) unmasks the fresnel");
                material.SetTextureOffset("_Cloud_Texture", new Vector2(0.25f, 0));
                Expect(Fresnel(0.8f, 1), "the mirrored read takes the offset again (band 1)");
                material.SetTextureOffset("_Cloud_Texture", Vector2.zero);
                material.SetTexture("_Cloud_Texture", rig.Texels(none));
                material.SetFloat("_Fresnel_Opacity", 0);
                rig.SetNormals(Vector3.back);

                // _GlobalMipBias applies once to each sample: across the screen a 256 texture is read at level 3.
                material.SetTexture("_Stars_Texture", rig.MipLevels(256, level => level == 4 ? new Color(0, 0, 1, 1) : none));
                Expect(blue, "stars: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(Layers(new Vector3(0, 0, 1)), "stars: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Stars_Texture", rig.Texels(none));
                material.SetTexture("_Cloud_Texture", rig.MipLevels(256, level => level == 4 ? new Color(0.5f, 0, 0, 1) : none));
                Expect(blue, "cloud: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(OverBackground(new Vector3(0.5f, 0, 0), background), "cloud: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Char_Ariem_Stars GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The perspective view direction and the ShadowCaster pass remain " +
                          "compile-checked only.");
            }
        }
    }
}
