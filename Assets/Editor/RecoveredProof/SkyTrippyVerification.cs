using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Sky/HL_Sky_Trippy, reconstructed from the shipped Metal programs: two scrolling noise lookups
    // into the top and bottom gradient ramps, the height-driven ramp blend and fogs with their opacities, and
    // the turned, blurred cubemap comp composited by its alpha.
    public static class SkyTrippyVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Sky_Trippy.shader";
        private const string ShaderName = "HardLight/Sky/HL_Sky_Trippy";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "029018aa5570911489282f3efd2e81cf";
        private const int Forward = 0, DepthOnly = 1;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_USE_CUBEMAP_COMP", "_WRITE_RENDERING_LAYERS" },
            new[] { "_USE_PATTERN_MASK", "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
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
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                void Expect(Material m, Color c, string label) => rig.Expect(m, c, label, pass: Forward);
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), black = new Color(0, 0, 0, 1);
                Color Mix(Color a, Color b, float t) { Color c = Color.LerpUnclamped(a, b, t); c.a = 1; return c; }

                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Bottom_Gradient_Ramp_Tex", rig.Texels(red));
                material.SetTexture("_Top_Gradient_Ramp_Tex", rig.Texels(green));
                // Fogs off (opacity 0 lifts each weight to 1) while the ramp blend is checked.
                material.SetFloat("_Bottom_Fog_Opacity", 0);
                material.SetFloat("_Top_Fog_Opacity", 0);

                // The read pixel's object height is +-1/32 (the quad spans -1..1 over 32 rows). Ramp weight
                // 0.5 + 8 * height reads it back; it must be exactly a half row from the centre.
                material.SetFloat("_Gradient_Ramp_Blend_Opacity", 1);
                material.SetFloat("_Gradient_Ramp_Blend_Amount", 8);
                material.SetFloat("_Gradient_Ramp_Blend_Position", 0.5f);
                float probe = rig.Draw(material, Forward).g;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(Mathf.Abs(probe - 0.5f) - 0.25f) < 0.01f, "height-driven ramp weight: " + probe);
                float height = (probe - 0.5f) / 8;

                material.SetFloat("_Gradient_Ramp_Blend_Amount", 0);
                material.SetFloat("_Gradient_Ramp_Blend_Position", 0.25f);
                Expect(material, Mix(red, green, 0.25f), "ramp blend position");
                material.SetFloat("_Gradient_Ramp_Blend_Opacity", 0.5f);
                Expect(material, Mix(red, green, 0.75f), "ramp blend opacity lifts the weight");
                material.SetFloat("_Gradient_Ramp_Blend_Opacity", 0);
                Expect(material, green, "ramp blend opacity 0 shows the top ramp");
                material.SetFloat("_Gradient_Ramp_Blend_Opacity", 1);
                material.SetFloat("_Gradient_Ramp_Blend_Position", 2);
                Expect(material, green, "the inner ramp weight saturates");

                // Fogs over a red sky.
                material.SetTexture("_Top_Gradient_Ramp_Tex", rig.Texels(red));
                material.SetColor("_Bottom_Fog_Colour", blue);
                material.SetFloat("_Bottom_Fog_Opacity", 1);
                material.SetFloat("_Bottom_Fog_Blend", 0);
                material.SetFloat("_Bottom_Fog_Position", 0.25f);
                Expect(material, Mix(blue, red, 0.25f), "bottom fog position");
                material.SetFloat("_Bottom_Fog_Blend", 8);
                material.SetFloat("_Bottom_Fog_Position", 0.5f);
                Expect(material, Mix(blue, red, 0.5f + 8 * height), "bottom fog rises with height");
                material.SetFloat("_Bottom_Fog_Opacity", 0.75f);
                Expect(material, Mix(blue, red, Mathf.Clamp01(0.75f + 8 * height)), "bottom fog opacity");
                material.SetFloat("_Bottom_Fog_Opacity", 0);
                material.SetColor("_Top_Fog_Colour", green);
                material.SetFloat("_Top_Fog_Opacity", 1);
                Expect(material, Mix(green, red, 1 - height), "top fog follows one minus the height");
                material.SetFloat("_Top_Fog_Blend_Amount", 0);
                material.SetFloat("_Top_Fog_Position", 0.5f);
                Expect(material, Mix(green, red, 0.5f), "top fog position");
                material.SetFloat("_Top_Fog_Opacity", 0.25f);
                Expect(material, Mix(green, red, 1), "top fog opacity lifts the weight");
                material.SetFloat("_Top_Fog_Opacity", 0);

                // Noise: four 16-texel bands (the inline sampler is linear). The plain lookup at u 0.516 reads band 2
                // (0.1) and the (0.3, 0.2) offset lookup band 3 (0.35); their sum 0.45 times the ramp offset 1.25
                // reads the bottom ramp's band 2 (blue). One lookup alone would read band 0 or 1.
                material.SetFloat("_Gradient_Ramp_Blend_Position", 0);
                material.SetFloat("_Gradient_Ramp_Blend_Amount", 0);
                material.SetTexture("_Bottom_Gradient_Ramp_Tex", rig.Bands(red, green, blue, black));
                material.SetFloat("_Bottom_Gradient_Ramp_Offset", 1.25f);
                Color N(float x) => new Color(x, 0, 0, 1);
                material.SetTexture("_Noise_Texture", rig.Bands(N(0), N(0.1f), N(0.1f), N(0.35f)));
                Expect(material, blue, "both noise lookups add");
                material.SetFloat("_Bottom_Gradient_Ramp_Offset", 0.5f);
                Expect(material, red, "the ramp offset scales the noise");
                material.SetFloat("_Bottom_Gradient_Ramp_Offset", 1.25f);
                // Scrolling by 0.1 * (1 - speed) * time: 0.5 moves the lookups to bands 0 and 1 (0.1 in all, red).
                material.SetFloat("_Noise_UV_Speed", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 5);
                Expect(material, red, "noise scrolls with game time");
                material.SetFloat("_Noise_UV_Speed", 0.5f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 10);
                Expect(material, red, "noise speed slows the scroll");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 5);
                Expect(material, blue, "half speed at time 5 only swaps the two lookups");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Noise_UV_Speed", 0);
                material.SetTextureOffset("_Noise_Texture", new Vector2(0.5f, 0));
                Expect(material, red, "the noise offset moves both lookups");
                material.SetTextureOffset("_Noise_Texture", Vector2.zero);
                // The top ramp reads the same noise times its own offset.
                material.SetFloat("_Gradient_Ramp_Blend_Opacity", 0);
                material.SetTexture("_Top_Gradient_Ramp_Tex", rig.Bands(red, green, blue, black));
                material.SetFloat("_Top_Gradient_Ramp_Offset", 0.5f);
                Expect(material, red, "top ramp offset");
                material.SetFloat("_Top_Gradient_Ramp_Offset", 1.25f);
                Expect(material, blue, "top ramp reads the added noise");
                material.SetFloat("_Gradient_Ramp_Blend_Opacity", 1);

                // _GlobalMipBias applies once to each noise lookup: both read level 3 (0.225 each, blue) unbiased and
                // level 4 (0.05 each, red) at bias 1; level 5 (0.175 each) would read green.
                material.SetTexture("_Noise_Texture", rig.MipLevels(256, level => N(level <= 3 ? 0.225f : level == 4 ? 0.05f : 0.175f)));
                Expect(material, blue, "noise: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, red, "noise: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Bottom_Gradient_Ramp_Tex", rig.Texels(red));

                // Cubemap comp: the orthographic view direction toward the camera is -Z. Turning 90 degrees reads -X,
                // the alpha composites, and the blur amount picks the mip level.
                var cubemap = rig.Own(new Cubemap(2, TextureFormat.RGBAFloat, true));
                foreach (CubemapFace face in System.Enum.GetValues(typeof(CubemapFace)))
                {
                    if (face == CubemapFace.Unknown) continue;
                    Color colour = face == CubemapFace.NegativeZ ? green : face == CubemapFace.NegativeX ? blue : black;
                    cubemap.SetPixels(new[] { colour, colour, colour, colour }, face, 0);
                    cubemap.SetPixels(new[] { new Color(1, 1, 1, 1) }, face, 1);
                }
                cubemap.Apply(false);
                material.SetTexture("_Cubemap_Texture", cubemap);
                Expect(material, red, "the cubemap is off without _USE_CUBEMAP_COMP");
                material.EnableKeyword("_USE_CUBEMAP_COMP");
                Expect(material, green, "the cubemap reads the direction toward the camera");
                material.SetFloat("_Cubemap_Rotation_Amount", 90);
                Expect(material, blue, "the rotation turns the direction about Y");
                material.SetFloat("_Cubemap_Rotation_Amount", 0);
                material.SetFloat("_Cubemap_Blur_Amount", 1);
                Expect(material, Color.white, "the blur amount is the mip level");
                material.SetFloat("_Cubemap_Blur_Amount", 0);
                foreach (CubemapFace face in new[] { CubemapFace.NegativeZ })
                    cubemap.SetPixels(new[] { new Color(0, 1, 0, 0.5f), new Color(0, 1, 0, 0.5f), new Color(0, 1, 0, 0.5f), new Color(0, 1, 0, 0.5f) }, face, 0);
                cubemap.Apply(false);
                Expect(material, Mix(red, green, 0.5f), "the cubemap alpha composites it");

                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "DepthOnly writes the depth");

                Debug.Log("Project Lucid HL_Sky_Trippy GPU verification passed: 27 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The cubemap Y flip remains compile-checked only (the orthographic view has no Y).");
            }
        }
    }
}
