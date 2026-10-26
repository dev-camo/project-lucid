using UnityEngine;

namespace ProjectLucid
{
    // Hidden/HardLight/UI/HL_UI_Core_Loop_Transition, reconstructed from the shipped Metal programs: the background's
    // polar coordinates about the centre point (radius and angle scales, UI-time rotation), its flowmap offset and
    // two-phase blend, the centre colour fade, the foreground's rotated, scaled and centre-offset screen UV and its
    // overlay, and the alpha from the background, the polar noise and its radial falloff.
    public static class UICoreLoopTransitionVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_UI_Core_Loop_Transition.shader";
        private const string ShaderName = "Hidden/HardLight/UI/HL_UI_Core_Loop_Transition";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "a95546cc3cafd984991d36b57cd01ce7";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_HLSubSgFlowMapInput_acd9b3a5411347599d1a42d501175ad2_FlowMapTexture_4128534274_Texture2D", "1061d6e61c336e44b9dd1103fb90a4f1" },
            { "_HLSubSgTex2DGrad_5da7d4cba8b4452091965912b7f28090_Texture2D_4287008460_Texture2D", "d2f552a503a952c48b4f9b39c32be0d7" },
            { "_HLSubSgTex2DGrad_6a07e9d27201494eaa0ba86ed1f50c3a_Texture2D_4287008460_Texture2D", "d2f552a503a952c48b4f9b39c32be0d7" },
            { "_SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D", "e8055f4d7423e2a41a7e7cd68a052b5c" },
            { "_Texture2DAsset_bade7436e4bd476da86479d094bcf974_Out_0_Texture2D", "e8129d5371ff1e04d8070064644689a1" },
        };
        private const string FlowMap = "_HLSubSgFlowMapInput_acd9b3a5411347599d1a42d501175ad2_FlowMapTexture_4128534274_Texture2D";
        private const string BackgroundA = "_HLSubSgTex2DGrad_6a07e9d27201494eaa0ba86ed1f50c3a_Texture2D_4287008460_Texture2D";
        private const string BackgroundB = "_HLSubSgTex2DGrad_5da7d4cba8b4452091965912b7f28090_Texture2D_4287008460_Texture2D";
        private const string Foreground = "_SampleTexture2D_ffc956bac9fb4289b903d92cc5924e93_Texture_1_Texture2D";
        private const string Noise = "_Texture2DAsset_bade7436e4bd476da86479d094bcf974_Out_0_Texture2D";

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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveVectors("_ScaledScreenParams", "_TimeParameters");
                rig.SaveFloats("_HLShaderTimeUI");
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                Shader.SetGlobalFloat("_HLShaderTimeUI", 0);
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 1, 0, 0));
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color clear = new Color(0, 0, 0, 0);
                Color Grey(float v) => new Color(v, v, v, 1);

                // 4x4 bands of 16 texels (the graph's bilinear inline sampler) whose colour names the band:
                // (column / 3, row / 3, 0).
                var bands = rig.Own(new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Repeat });
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        pixels[y * 64 + x] = new Color(x / 16 / 3f, y / 16 / 3f, 0, 1);
                bands.SetPixels(pixels);
                bands.Apply(false, false);
                Color Band(int column, int row) => new Color(column / 3f, row / 3f, 0, 1);
                // Bands along v only (1 x 64), for the noise's v lookups at u 0.
                Texture2D VBands(params Color[] colors)
                {
                    var texture = rig.Own(new Texture2D(1, colors.Length * 16, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Repeat });
                    var column = new Color[colors.Length * 16];
                    for (int i = 0; i < column.Length; i++) column[i] = colors[i / 16];
                    texture.SetPixels(column);
                    texture.Apply(false, false);
                    return texture;
                }

                // Neutral start: no flow, the foreground fully transparent, the centre fade out of range, no radial
                // noise falloff (alpha 1). The pixel (16, 16) is screen (0.516, 0.516); a centre 0.25 to its left puts
                // it at polar radius 0.25, angle pi / 2.
                Material material = rig.Own(new Material(shader));
                material.SetTexture(FlowMap, rig.Texels(new Color(0.5f, 0.5f, 0, 0)));
                material.SetTexture(BackgroundA, bands);
                material.SetTexture(BackgroundB, rig.Texels(blue));
                material.SetTexture(Noise, rig.Texels(clear));
                material.SetColor("_Foreground_Colour_A", clear);
                material.SetColor("__Foreground_Colour_B", clear);
                material.SetColor("_Center_Colour", new Color(0, 0, 0, 0));
                material.SetFloat("_Center_Fade_Min", 2);
                material.SetFloat("_Center_Fade_Max", 3);
                material.SetFloat("_Noise_Radial_Amount", 0);
                const float pixel = 16.5f / 32f;
                material.SetFloat("_Center_Point_X", pixel - 0.25f);
                material.SetFloat("_Center_Point_Y", pixel);
                void Expect(Color expected, string label, int x = 16, int y = 16)
                {
                    rig.Expect(material, expected, label, x: x, y: y);
                    cases++;
                }

                // Polar background: u = radius * 2 * radial amount, v = angle / 6.28 * length amount.
                material.SetFloat("_Background_Flowmap_Radial_Amount", 0.75f);
                material.SetFloat("_Background_Flowmap_Length_Amount", 1.5f);
                Expect(Band(1, 1), "polar radius 0.375, angle 0.375");
                material.SetFloat("_Background_Flowmap_Radial_Amount", 1.25f);
                Expect(Band(2, 1), "the radial amount");
                material.SetFloat("_Background_Flowmap_Radial_Amount", 0.75f);
                material.SetFloat("_Background_Flowmap_Length_Amount", 2.5f);
                Expect(Band(1, 2), "the length amount");
                material.SetFloat("_Background_Flowmap_Length_Amount", 1.5f);
                // A quarter turn's half (pi / 4 at UI time 1) takes the angle from pi / 2 to 3 pi / 4.
                material.SetFloat("_Background_Flowmap_UV_Rotation_Speed", Mathf.PI / 4);
                Shader.SetGlobalFloat("_HLShaderTimeUI", 1);
                Expect(Band(1, 2), "UI time rotates the polar coordinates");
                Shader.SetGlobalFloat("_HLShaderTimeUI", 0);
                material.SetFloat("_Background_Flowmap_UV_Rotation_Speed", 0);
                // Screen y is up: at pixel (16, 24) the centre (0.266, 0.5) is below-left, angle atan2(0.25, 0.266).
                material.SetFloat("_Center_Point_Y", 0.5f);
                Vector2 up = new Vector2(0.25f, 24.5f / 32f - 0.5f);
                float upU = up.magnitude * 2 * 0.75f, upV = Mathf.Atan2(up.x, up.y) / 6.28f * 1.5f;
                Expect(Band(Mathf.FloorToInt(upU * 4), Mathf.FloorToInt(upV * 4)), "the screen UV runs bottom to top", y: 24);
                material.SetFloat("_Center_Point_Y", pixel);

                // Flowmap: (flow - 0.5) * amount * -2, scaled by phase A (0.5 at time 0) for background A.
                material.SetFloat("_Background_Flowmap_Radial_Amount", 1.25f);
                material.SetTexture(FlowMap, rig.Texels(new Color(1, 0.5f, 0, 0)));
                Expect(Band(1, 1), "the flowmap offsets the polar UV");
                material.SetFloat("_Background_Flowmap_Amount", 1);
                Expect(Band(0, 1), "the flowmap amount");
                material.SetFloat("_Background_Flowmap_Amount", 0.5f);
                material.SetTexture(FlowMap, rig.Texels(new Color(0.5f, 0.5f, 0, 0)));
                // The phases: B = frac(time * speed), A half a cycle on; A weighs |2B - 1|.
                material.SetTexture(BackgroundA, rig.Texels(red));
                Shader.SetGlobalVector("_TimeParameters", new Vector4(0.25f, 0, 0, 0));
                Expect(new Color(0.5f, 0, 0.5f, 1), "phase B 0.75 blends the backgrounds equally");
                Shader.SetGlobalVector("_TimeParameters", new Vector4(0.1f, 0, 0, 0));
                Expect(new Color(0.8f, 0, 0.2f, 1), "phase B 0.9");
                material.SetFloat("_Background_Flowmap_Speed", 2);
                Expect(new Color(0.6f, 0, 0.4f, 1), "the flowmap speed");
                material.SetFloat("_Background_Flowmap_Speed", -1);
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);

                // Centre colour: smoothstep(min, max, 1 - 2 * radius) = smoothstep(min, max, 0.5) here.
                material.SetColor("_Center_Colour", green);
                material.SetFloat("_Center_Fade_Min", 0);
                material.SetFloat("_Center_Fade_Max", 1);
                Expect(new Color(0.5f, 0.5f, 0, 1), "the centre fade at 0.5");
                material.SetFloat("_Center_Fade_Max", 0.5f);
                Expect(green, "the fade max");
                material.SetFloat("_Center_Fade_Min", 0.4f);
                material.SetFloat("_Center_Fade_Max", 0.6f);
                Expect(new Color(0.5f, 0.5f, 0, 1), "the fade min");
                material.SetColor("_Center_Colour", new Color(0, 0, 0, 0));
                material.SetFloat("_Center_Fade_Min", 2);
                material.SetFloat("_Center_Fade_Max", 3);

                // Foreground: colours A (white, clear) to B (white, opaque) by a point strip whose texel 2 (white)
                // is under the pixel; the white overlay at the foreground alpha turns the red background white.
                material.SetFloat("_Center_Point_X", 0.5f);
                material.SetFloat("_Center_Point_Y", 0.5f);
                material.SetColor("_Foreground_Colour_A", new Color(1, 1, 1, 0));
                material.SetColor("__Foreground_Colour_B", white);
                material.SetTexture(Foreground, rig.Texels(clear, clear, white, clear));
                Expect(white, "the foreground texel under the pixel");
                material.SetFloat("_Foreground_UV_Rotation", Mathf.PI);
                Expect(red, "the foreground rotation turns the screen UV about 0.5");
                material.SetFloat("_Foreground_UV_Rotation", 0);
                material.SetFloat("_Foreground_UV_Scale", 16);
                Expect(red, "the foreground scale");
                material.SetFloat("_Foreground_UV_Scale", 1);
                material.SetFloat("_Center_Point_X", 0.75f);
                Expect(red, "the centre point offsets the foreground");
                material.SetFloat("_Center_Point_X", 0.5f);
                material.SetTexture(Foreground, rig.Texels(Grey(0.5f) * new Color(1, 1, 1, 0.5f)));
                Expect(new Color(1, 0.5f, 0.5f, 1), "the foreground alpha is the overlay opacity");
                // Overlay on a 0.25 grey: a dark foreground (0.25) multiplies, a light one (0.75) screens.
                material.SetTexture(BackgroundA, rig.Texels(Grey(0.25f)));
                material.SetColor("_Foreground_Colour_A", new Color(0, 0, 0, 1));
                material.SetTexture(Foreground, rig.Texels(Grey(0.25f)));
                Expect(Grey(0.125f), "overlay with a dark foreground");
                material.SetTexture(Foreground, rig.Texels(Grey(0.75f)));
                Expect(Grey(0.625f), "overlay with a light foreground");
                material.SetColor("_Foreground_Colour_A", clear);
                material.SetColor("__Foreground_Colour_B", clear);
                material.SetTexture(BackgroundA, rig.Texels(red));

                // Alpha: saturate(background alpha + noise) * alpha, the noise being the fixed-angle noise's green
                // and blue, the length-scaled noise's green, and (1 - 2 * radius * radial amount) * 1.52.
                material.SetFloat("_Center_Point_X", pixel - 0.25f);
                material.SetFloat("_Center_Point_Y", pixel);
                float Alpha() => rig.Draw(material).a;
                void ExpectAlpha(float expected, string label)
                {
                    float alpha = Alpha();
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - expected) < 2e-3f, label + ": " + alpha + " (expected " + expected + ")");
                    cases++;
                }
                material.SetTexture(BackgroundA, rig.Texels(new Color(1, 0, 0, 0.5f)));
                ExpectAlpha(1, "no radial falloff saturates the alpha");
                material.SetFloat("_Noise_Radial_Amount", 2);
                ExpectAlpha(0.5f, "radial amount 2 cancels the falloff at radius 0.25");
                material.SetFloat("_Noise_Radial_Amount", 1.5f);
                ExpectAlpha(0.5f + 0.25f * 1.52f, "the radial falloff");
                material.SetFloat("_Noise_Radial_Amount", 2);
                material.SetFloat("_Alpha", 0.5f);
                ExpectAlpha(0.25f, "the alpha");
                material.SetFloat("_Alpha", 1);
                // Noise bands along v: the fixed lookup (v = pi / 2 * 0.0209) reads band 0, the length one
                // (v = 0.25 * length amount) band 1 or 2.
                material.SetTexture(BackgroundA, rig.Texels(new Color(1, 0, 0, 0)));
                material.SetTexture(Noise, VBands(new Color(0, 0.1f, 0.05f, 0), new Color(0, 0.2f, 0, 0), new Color(0, 0.3f, 0, 0), clear));
                material.SetFloat("_Noise_Length_Amount", 1.5f);
                ExpectAlpha(0.35f, "the fixed noise's green and blue plus the length noise");
                material.SetFloat("_Noise_Length_Amount", 2.5f);
                ExpectAlpha(0.45f, "the noise length amount");

                // Mip selection. The noise samples with UV0's derivatives times _GlobalMipBias.y (URP's
                // SAMPLE_TEXTURE2D_GRAD): level 3 of a 256 texture at y 1, level 4 at y 2.
                material.SetTexture(Noise, rig.MipLevels(256, level => level == 3 ? new Color(0, 0.25f, 0, 0) : clear));
                ExpectAlpha(0.5f, "noise: unscaled derivatives read level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 2, 0, 0));
                ExpectAlpha(0, "noise: _GlobalMipBias.y scales the derivatives");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 1, 0, 0));
                material.SetTexture(Noise, rig.Texels(clear));
                material.SetFloat("_Noise_Radial_Amount", 0);
                // The backgrounds take the raw screen derivatives (level 3), whatever the global bias.
                material.SetTexture(BackgroundA, rig.MipLevels(256, level => level == 3 ? red : green));
                Expect(red, "background: screen derivatives read level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 2, 0, 0));
                Expect(red, "background: the global bias does not apply");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 1, 0, 0));
                material.SetTexture(BackgroundA, rig.Texels(red));
                // The foreground samples with the bias: level 3 (clear) unbiased, level 4 (white) at bias 1.
                material.SetFloat("_Center_Point_X", 0.5f);
                material.SetFloat("_Center_Point_Y", 0.5f);
                material.SetColor("_Foreground_Colour_A", new Color(1, 1, 1, 0));
                material.SetColor("__Foreground_Colour_B", white);
                material.SetTexture(Foreground, rig.MipLevels(256, level => level == 4 ? white : clear));
                Expect(red, "foreground: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 1, 0, 0));
                Expect(white, "foreground: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_UI_Core_Loop_Transition GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The flowmap's own mip bias remains compile-checked only.");
            }
        }
    }
}
