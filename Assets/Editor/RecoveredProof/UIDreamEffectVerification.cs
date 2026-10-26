using UnityEngine;

namespace ProjectLucid
{
    // HardLight/UI/HL_UI_Dream_Effect, reconstructed from the shipped Metal programs: the two counter-scrolling noise
    // lookups (sprite or screen UVs, tiled and offset), the dream gradient they index, the sprite colour mode, the
    // tint, and the noise-eaten alpha. Both shipped copies (bundled and Assets/Shader) run every case.
    public static class UIDreamEffectVerification
    {
        private const string ShaderName = "HardLight/UI/HL_UI_Dream_Effect";
        // The shipped shader asset GUIDs in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private static readonly string[][] Copies =
        {
            new[] { "Assets/Shaders/Recovered/HL_UI_Dream_Effect.shader", "9c34bc750cbdd2047aca5c74c4ace89d" },
            new[] { "Assets/Shaders/Recovered/HL_UI_Dream_Effect_Resources.shader", "e3f415b2336da4b428f724a065145dae" },
        };

        private static readonly string[][] KeywordSets =
        {
            new string[0],
        };

        public static void CompileForWindows()
        {
            foreach (string[] copy in Copies) RecoveredShaderProof.CompileForWindows(RecoveredShaderProof.Load(copy[0], ShaderName, copy[1]), KeywordSets);
        }

        public static void CompileForOpenGL()
        {
            foreach (string[] copy in Copies) RecoveredShaderProof.CompileForOpenGL(RecoveredShaderProof.Load(copy[0], ShaderName, copy[1]), KeywordSets);
        }

        public static void Run()
        {
            int cases = 0;
            foreach (string[] copy in Copies) cases += Run(RecoveredShaderProof.Load(copy[0], ShaderName, copy[1]));
            Debug.Log("Project Lucid HL_UI_Dream_Effect GPU verification passed: " + cases + " Metal-derived cases over both copies on " +
                      SystemInfo.graphicsDeviceType + ". The gradient lookup's mip bias remains compile-checked only (its UV, the noise sum, has no screen derivatives here).");
        }

        private static int Run(Shader shader)
        {
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                rig.SaveFloats("unity_GUIZTestMode");
                rig.SaveVectors("_Time");
                Shader.SetGlobalFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                Shader.SetGlobalVector("_Time", Vector4.zero);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color N(float x) => new Color(x, x, 0, 1);

                // Unblended output (One Zero); the noise neither moves the gradient nor eats the alpha to start.
                Material material = rig.Own(new Material(shader));
                material.SetFloat("_BlendSrc", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_BlendDst", (float)UnityEngine.Rendering.BlendMode.Zero);
                material.SetFloat("_Dream_Noise_Speed", 0);
                material.SetFloat("_Alpha_Dream_Noise_Amount", 0);
                material.SetFloat("_Alpha_Dream_Noise_Contrast", 1);
                material.SetFloat("_Use_Sprite_RGB_Texture", 0);
                material.SetTexture("_MainTex", rig.Texels(white));
                material.SetTexture("_Dream_Noise", rig.Texels(N(0)));
                // The gradient strip, indexed by the noise sum: 0 red, 0.3 green, 0.6 blue, 0.9 white.
                material.SetTexture("_Dream_Gradient", rig.Texels(red, green, blue, white));
                void Expect(Color expected, string label, Matrix4x4? model = null)
                {
                    rig.Expect(material, expected, label, model: model);
                    cases++;
                }

                Expect(red, "noise 0 reads the gradient start");
                material.SetFloat("_Dream_Gradient_Multiplier", 0.5f);
                Expect(new Color(0.5f, 0, 0, 1), "the gradient multiplier");
                material.SetFloat("_Dream_Gradient_Multiplier", 1);

                // Sprite colour mode: 1 is the per-channel maximum of the gradient and sprite, 0.5 halfway there and
                // then halfway back to the gradient.
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_MainTex", rig.Texels(grey));
                Expect(red, "mode 0 ignores the sprite colour");
                material.SetFloat("_Use_Sprite_RGB_Texture", 1);
                Expect(new Color(1, 0.5f, 0.5f, 1), "mode 1 keeps the brighter of gradient and sprite");
                material.SetColor("_ColourTint", new Color(1, 0, 1, 1));
                Expect(new Color(1, 0, 0.5f, 1), "the tint multiplies the colour");
                material.SetColor("_ColourTint", white);
                material.SetFloat("_Use_Sprite_RGB_Texture", 0.5f);
                Expect(new Color(0.875f, 0.25f, 0.25f, 1), "mode 0.5 lerps twice");
                material.SetFloat("_Use_Sprite_RGB_Texture", 0);
                material.SetTexture("_MainTex", rig.Texels(white));

                // The noise sum: one lookup at the UV, one at minus the UV. On a 2-texel strip the pixel (u 0.516)
                // reads texel 1 and its mirror (u 0.484) texel 0.
                material.SetTexture("_Dream_Noise", rig.Texels(N(0), N(0.3f)));
                Expect(green, "the noise at the UV indexes the gradient");
                material.SetTexture("_Dream_Noise", rig.Texels(N(0.3f), N(0)));
                Expect(green, "the noise at minus the UV adds to it");
                // On an 8-texel strip with 0.3 only in texel 7: the pixel reads texel 4 and its mirror texel 3.
                material.SetTexture("_Dream_Noise", rig.Texels(N(0), N(0), N(0), N(0), N(0), N(0), N(0), N(0.3f)));
                Expect(red, "neither lookup reaches texel 7");
                material.SetTextureOffset("_Dream_Noise", new Vector2(0.375f, 0));
                Expect(green, "the noise offset (u 0.89, texel 7)");
                material.SetTextureOffset("_Dream_Noise", Vector2.zero);
                material.SetTextureScale("_Dream_Noise", new Vector2(1.75f, 1));
                Expect(green, "the noise tiling (u 0.90, texel 7)");
                material.SetTextureScale("_Dream_Noise", Vector2.one);
                material.SetFloat("_Dream_Noise_Speed", 0.375f);
                Shader.SetGlobalVector("_Time", new Vector4(0.05f, 1, 2, 3));
                Expect(green, "the noise scrolls with _Time.y times the speed");
                Shader.SetGlobalVector("_Time", Vector4.zero);
                material.SetFloat("_Dream_Noise_Speed", 0);
                // Screen-space UVs: with the quad shifted right by 0.5 the pixel's sprite u is 0.27 (texel 2) while
                // its screen u stays 0.516 (texel 4).
                material.SetTexture("_Dream_Noise", rig.Texels(N(0), N(0), N(0.3f), N(0), N(0), N(0), N(0), N(0)));
                Matrix4x4 shifted = Matrix4x4.Translate(new Vector3(0.5f, 0, 0));
                Expect(green, "the noise follows the sprite UV", shifted);
                material.SetFloat("_UseScreenSpaceUVs", 1);
                Expect(red, "screen-space UVs follow the screen instead", shifted);
                material.SetFloat("_UseScreenSpaceUVs", 0);

                // Alpha: the sprite alpha minus the noise (sum, pivoted on linear mid grey by the contrast, times the
                // amount, saturated), times the vertex alpha and the saturated sprite alpha. Noise 0.3 + 0.3 = 0.6
                // reads gradient blue.
                material.SetTexture("_Dream_Noise", rig.Texels(N(0.3f)));
                material.SetFloat("_Alpha_Dream_Noise_Amount", 1);
                Expect(new Color(0, 0, 1, 0.4f), "the noise eats the alpha");
                material.SetFloat("_Alpha_Dream_Noise_Amount", 0.5f);
                Expect(new Color(0, 0, 1, 0.7f), "the amount scales the noise");
                material.SetFloat("_Alpha_Dream_Noise_Amount", 1);
                material.SetFloat("_Alpha_Dream_Noise_Contrast", 0.5f);
                const float midGrey = 0.217637643f;
                Expect(new Color(0, 0, 1, 1 - ((0.6f - midGrey) * 0.5f + midGrey)), "the contrast pivots on linear mid grey");
                material.SetFloat("_Alpha_Dream_Noise_Contrast", 1);
                rig.SetColors(new Color(0, 0, 0, 0.5f));
                Expect(new Color(0, 0, 1, 0.2f), "the vertex alpha scales it (its colour is unused)");
                rig.SetColors(white);
                material.SetTexture("_Dream_Noise", rig.Texels(N(0.75f)));
                Expect(new Color(0, 0, 1, 0), "the noise term saturates (1.5 to 1)");
                material.SetTexture("_Dream_Noise", rig.Texels(N(0)));
                material.SetTexture("_MainTex", rig.Texels(new Color(1, 1, 1, 0.5f)));
                Expect(new Color(1, 0, 0, 0.25f), "the sprite alpha applies twice");
                material.SetTexture("_MainTex", rig.Texels(new Color(1, 1, 1, 2)));
                Expect(new Color(1, 0, 0, 2), "only the second sprite alpha is saturated");
                material.SetTexture("_MainTex", rig.Texels(white));

                // _GlobalMipBias applies once per sample: over UV0 a 256 texture with a colour per level reads level 3
                // unbiased and level 4 at bias 1.
                void BiasCases(Color unbiased, Color once, string path)
                {
                    Expect(unbiased, path + ": no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(once, path + ": bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                material.SetFloat("_Alpha_Dream_Noise_Amount", 0);
                material.SetFloat("_Use_Sprite_RGB_Texture", 1);
                material.SetTexture("_Dream_Gradient", rig.Texels(Color.black, Color.black, Color.black, Color.black));
                material.SetTexture("_MainTex", rig.MipLevels(256, level => level == 4 ? green : red));
                BiasCases(red, green, "sprite");
                material.SetTexture("_MainTex", rig.Texels(white));
                material.SetFloat("_Use_Sprite_RGB_Texture", 0);
                material.SetTexture("_Dream_Gradient", rig.Texels(red, green, blue, white));
                // Both noise lookups: level 4 is 0.3 each (sum 0.6, blue); a lookup biased twice would read level 5 (0).
                material.SetTexture("_Dream_Noise", rig.MipLevels(256, level => level == 4 ? N(0.3f) : N(0)));
                BiasCases(red, blue, "noise");

                return cases;
            }
        }
    }
}
