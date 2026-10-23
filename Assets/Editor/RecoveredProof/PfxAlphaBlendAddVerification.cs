using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Particle/HL_PFX_AlphaBlendAdd, reconstructed from Metal blobs 1/3: the red-weighted additive
    // tint, the alpha channel switch, the vertex colour applied twice, and the camera-distance fade that
    // always scales alpha and, when toggled, scales all four channels again.
    public static class PfxAlphaBlendAddVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_PFX_AlphaBlendAdd.shader";
        private const string ShaderName = "HardLight/Particle/HL_PFX_AlphaBlendAdd";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "eb77dff1bca503f43903d55ab1540df0";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), new string[0]);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                Material material = rig.Own(new Material(shader));
                material.SetFloat("_BlendSrc", 1);
                material.SetFloat("_BlendDst", 0);
                material.SetColor("_emissive_colour", Color.white);
                Color texel = new Color(0.5f, 1f, 0f, 0.25f);
                material.SetTexture("_MainTex", rig.Texels(texel, texel, texel, texel));
                // Near 0 and far 0 divide (w - z) by zero; give the fade a range it has fully passed.
                material.SetFloat("_distance_fade_near", -2);
                material.SetFloat("_distance_fade_far", -1);

                // lerp(tex + vc, tex, 1 - red) = tex + 0.5 at red 0.5; alpha is red by default.
                rig.Expect(material, new Color(1f, 1.5f, 0.5f, 0.5f), "red-weighted add, red alpha");
                material.SetFloat("_alpha_channel_switch", 0);
                rig.Expect(material, new Color(1f, 1.5f, 0.5f, 0.25f), "texture alpha");
                material.SetFloat("_alpha_channel_switch", 1);
                material.SetFloat("_alpha_blend_add_toggle", 1);
                rig.Expect(material, new Color(0.5f, 1f, 0f, 0.5f), "toggle keeps the plain texture colour");
                material.SetFloat("_alpha_blend_add_toggle", 0);
                material.SetColor("_emissive_colour", new Color(0, 1, 1, 1));
                rig.Expect(material, new Color(0f, 1.5f, 0.5f, 0.5f), "emissive colour scales rgb");
                material.SetColor("_emissive_colour", Color.white);

                // Vertex red 0.5: (0.5 + 0.5 * 0.5) * 0.5 * 0.5 = 0.1875; one multiply would give 0.375.
                rig.SetColors(new Color(0.5f, 1, 1, 0.5f));
                rig.Expect(material, new Color(0.1875f, 1.5f, 0.5f, 0.25f), "vertex colour applied twice, vertex alpha");
                rig.SetColors(Color.white);

                // A range centred on this quad's w - z gives fade 0.5.
                float distance = rig.ClipDistance(Vector3.zero);
                material.SetFloat("_distance_fade_near", distance - 0.1f);
                material.SetFloat("_distance_fade_far", distance + 0.1f);
                rig.Expect(material, new Color(1f, 1.5f, 0.5f, 0.25f), "fade scales alpha even with the toggle off");
                material.SetFloat("_distance_fade_toggle", 1);
                rig.Expect(material, new Color(0.5f, 0.75f, 0.25f, 0.125f), "toggle applies the fade to all channels again");

                // Custom1.xy from UV0.zw replaces the material range.
                material.SetFloat("_distance_fade_near", 100);
                material.SetFloat("_distance_fade_far", 200);
                rig.Expect(material, new Color(0f, 0f, 0f, 0f), "material range not yet reached");
                material.SetFloat("_distance_fade_custom_particle_data_toggle", 1);
                rig.SetUv0(new Vector4(0, 0, distance - 0.1f, distance + 0.1f));
                rig.Expect(material, new Color(0.5f, 0.75f, 0.25f, 0.125f), "custom stream range");
                rig.SetUv0(Vector4.zero);
                material.SetFloat("_distance_fade_custom_particle_data_toggle", 0);
                material.SetFloat("_distance_fade_toggle", 0);
                material.SetFloat("_distance_fade_near", -2);
                material.SetFloat("_distance_fade_far", -1);

                // _GlobalMipBias applies once (URP's SAMPLE_TEXTURE2D): spanning UV 0..1, the 256 texture reads
                // level 3; bias 1 moves it to level 4 (red: tex + vc = (2, 1, 1)); applying it twice would reach
                // level 5 (green: alpha 0).
                material.SetTexture("_MainTex", rig.MipLevels(256, level => level <= 3 ? texel : level == 4 ? Color.red : Color.green));
                rig.Expect(material, new Color(1f, 1.5f, 0.5f, 0.5f), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                rig.Expect(material, new Color(2f, 1f, 1f, 1f), "bias 1 applies once (level 4)");

                Debug.Log("Project Lucid HL_PFX_AlphaBlendAdd GPU verification passed: 11 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
