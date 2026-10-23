using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Ghosted, reconstructed from Metal blobs 1/3/5/7: the colour-free depth
    // prepass, then the ForwardLit blend of diffuse, the fresnel-masked dream texture indexed by two
    // scrolling noise taps, the fresnel rim, the fade to colour and _Alpha.
    public static class CharGhostedVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Ghosted.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Ghosted";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "c18e3bc73f2c52c458bbc4569a3d42a1";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), new string[0]);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Material material = rig.Own(new Material(shader));
                Color k = new Color(0, 0, 0, 1), r = new Color(1, 0, 0, 1), g = new Color(0, 1, 0, 1), b = new Color(0, 0, 1, 1);
                Color magenta = new Color(1, 0, 1, 1);
                material.SetTexture("_Diffuse_Texture", rig.Texels(magenta, magenta, magenta, magenta));
                material.SetColor("_Diffuse_Colour_Tint", Color.white);
                material.SetColor("_Fresnel_Colour", Color.white);
                material.SetColor("_Dream_Colour_Tint", Color.white);
                // The shipped defaults raise 0 to the power 0 (NaN); keep exponents positive here.
                material.SetFloat("_Dream_Noise_Fresnel_Amount", 1);
                material.SetFloat("_Dream_Noise_Fresnel_Intensity", 0);
                material.SetFloat("_Alpha", 0.5f);

                // The depth prepass writes no colour.
                rig.Expect(material, new Color(0, 0, 0, 0), "depth prepass has ColorMask 0", pass: 0);

                // ForwardLit blends SrcAlpha OneMinusSrcAlpha over a cleared target: rgb * a, alpha a * a.
                // Facing the camera, the rim is 0, so only diffuse and _Alpha remain.
                rig.Expect(material, new Color(0.5f, 0, 0.5f, 0.25f), "diffuse with _Alpha 0.5", pass: 1);
                material.SetFloat("_Alpha", 1);

                // Normal (0.6, 0, -0.8): N.V 0.8, rim 0.2; intensity 2.5 gives fresnel 0.5, and the
                // colour lerps to 0.5 * fresnel colour by 0.5: (0.75, 0.25, 0.75, 0.75).
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.SetFloat("_Fresnel_Intensity", 2.5f);
                rig.Expect(material, new Color(0.5625f, 0.1875f, 0.5625f, 0.5625f), "fresnel rim", pass: 1);
                material.SetFloat("_Fresnel_Amount", 2);
                rig.Expect(material, new Color(0.8281f, 0.0091f, 0.8281f, 0.8281f), "fresnel amount is the exponent", pass: 1);
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Intensity", 0);

                // Dream noise fully on (rim 0.2 x 5). At the centre the taps land on noise texels 2 and 3
                // (0.3 each), and their sum 0.6 picks dream texel 2; one tap alone would pick texel 1.
                material.SetFloat("_Dream_Noise_Fresnel_Intensity", 5);
                Color n = new Color(0.3f, 0, 0, 1), z = new Color(0, 0, 0, 1);
                material.SetTexture("_Perlin_Noise_Texture", rig.Texels(z, z, n, n));
                material.SetTexture("_Dream_Noise_Texture", rig.Texels(k, r, g, b));
                rig.Expect(material, g, "two noise taps index the dream texture", pass: 1);
                // Time 1 scrolls the first tap by -0.2 onto texel 1 (0) and the second by +0.1 (still 0.3).
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                rig.Expect(material, r, "game time scrolls the taps", pass: 1);
                material.SetFloat("_Dream_Noise_Speed", 0);
                rig.Expect(material, g, "speed 0 holds the taps", pass: 1);
                material.SetFloat("_Dream_Noise_Speed", 1);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Dream_Noise_Fresnel_Intensity", 0);
                rig.SetNormals(Vector3.back);

                // Fade to colour, then the diffuse tiling and offset.
                material.SetFloat("_Fade_To_Colour", 0.5f);
                material.SetColor("_Fade_Colour", b);
                rig.Expect(material, new Color(0.5f, 0, 1, 1), "half fade to blue", pass: 1);
                material.SetFloat("_Fade_To_Colour", 0);
                material.SetTexture("_Diffuse_Texture", rig.Texels(k, k, r, g));
                rig.Expect(material, r, "untransformed diffuse UV", pass: 1);
                material.SetTextureOffset("_Diffuse_Texture", new Vector2(0.25f, 0));
                rig.Expect(material, g, "diffuse offset", pass: 1);
                material.SetTextureOffset("_Diffuse_Texture", Vector2.zero);

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D). Each texture spans its UV
                // 0..1 across the 32-pixel quad, so a 256 texture reads level 3 unbiased and level 4 at bias 1;
                // a second application would read level 5.
                Vector4 bias1 = new Vector4(1, 0, 0, 0);
                material.SetTexture("_Diffuse_Texture", rig.MipLevels(256, level => level <= 3 ? magenta : level == 4 ? g : b));
                rig.Expect(material, magenta, "diffuse: no bias reads level 3", pass: 1);
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                rig.Expect(material, g, "diffuse: bias 1 applies once", pass: 1);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Diffuse_Texture", rig.Texels(magenta, magenta, magenta, magenta));

                // Noise path, with the dream texture fully shown (rim 0.2 x 5): both taps read the same level,
                // whose red (0.1, 0.2, 0.35 at levels 3, 4, 5) sums to the dream lookup 0.2 / 0.4 / 0.7, landing
                // in the dream texture's red / green / blue band.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.SetFloat("_Fresnel_Intensity", 0);
                material.SetFloat("_Dream_Noise_Fresnel_Intensity", 5);
                material.SetTexture("_Dream_Noise_Texture", rig.Bands(r, g, b, k));
                material.SetTexture("_Perlin_Noise_Texture", rig.MipLevels(256, level =>
                    new Color(level <= 3 ? 0.1f : level == 4 ? 0.2f : 0.35f, 0, 0, 1)));
                rig.Expect(material, r, "noise: no bias reads level 3", pass: 1);
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                rig.Expect(material, g, "noise: bias 1 applies once", pass: 1);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                // Dream path: a noise ramp (red = u, no mips) makes the dream lookup 2u + 0.3. At pixel 4 that
                // is 0.58 and moves 2/32 per pixel, so the 256 dream texture reads level 4 unbiased, 5 at bias 1,
                // and would read 6 with the bias applied twice.
                var ramp = new Color[256];
                for (int i = 0; i < ramp.Length; i++) ramp[i] = new Color((i + 0.5f) / 256, 0, 0, 1);
                material.SetTexture("_Perlin_Noise_Texture", rig.Texels(ramp));
                material.SetTexture("_Dream_Noise_Texture", rig.MipLevels(256, level => level <= 4 ? r : level == 5 ? g : b));
                rig.Expect(material, r, "dream: no bias reads level 4", pass: 1, x: 4);
                Shader.SetGlobalVector("_GlobalMipBias", bias1);
                rig.Expect(material, g, "dream: bias 1 applies once", pass: 1, x: 4);

                Debug.Log("Project Lucid HL_Char_Ghosted GPU verification passed: 16 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
