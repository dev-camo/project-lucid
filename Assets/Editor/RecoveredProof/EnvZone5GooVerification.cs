using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone5_Goo, reconstructed from the shipped Metal programs: the distorted dream
    // texture and its stars, the flow-mapped goo noise (its scroll, double tiling and both flow phases) picking
    // within two colour pairs by vertex alpha, the 1.3 contrast and 1.498 output scale, the rim fade to the fake fog
    // colour plus the green noise, and the DepthOnly depth.
    public static class EnvZone5GooVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone5_Goo.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone5_Goo";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "63ac00dcc87ca994a9ceeed73d1e5e8b";

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

        // The output for a pre-contrast colour at a pixel: contrast 1.3 about 0.2176, faded towards the rim to the
        // fog colour plus green, times 1.498.
        private static Color Output(Vector3 pre, int x = 16, int y = 16, float green = 0, Vector3 fog = default)
        {
            const float k = 0.217637643f;
            Vector2 uv = new Vector2((x + 0.5f) / 32f, (y + 0.5f) / 32f);
            float rim = Mathf.Clamp01(((uv - new Vector2(0.5f, 0.5f)).magnitude - 0.23f) * 4.347826f);
            rim = Mathf.Min(rim * rim * (3 - 2 * rim), 1);
            Vector3 graded = (pre - new Vector3(k, k, k)) * 1.3f + new Vector3(k, k, k);
            Vector3 edge = new Vector3(green, green, green) * rim + fog;
            Vector3 c = Vector3.Lerp(edge, graded, 1 - rim) * 1.49803901f;
            return new Color(c.x, c.y, c.z, 1);
        }

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "_UV_Scrolling_Direction_X", "_UV_Scrolling_Direction_Y");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalFloat("_UV_Scrolling_Direction_X", 0);
                Shader.SetGlobalFloat("_UV_Scrolling_Direction_Y", 0);
                int cases = 0;
                Color Noise(float r, float g = 0) => new Color(r, g, 0, 1);
                Color Dream(float r) => new Color(r, 0, 0, 1);
                Color Stars(float a) => new Color(0.5f, 0, 0, a);
                Color none = new Color(0, 0, 0, 0);

                // Colour A black, B blue, C green, D black; no fake fog; the flow map off. The dream texture is black
                // (0.05 after its lift) and the stars transparent; the goo noise's red 0.3 contrasts to 0.5.
                Material material = rig.Own(new Material(shader));
                material.SetColor("_Color_A", Color.black);
                material.SetColor("_Color_B", Color.blue);
                material.SetColor("_Color_C", Color.green);
                material.SetColor("_Color_D", Color.black);
                material.SetColor("_Fake_Fog_Color", Color.black);
                material.SetColor("_Stars_Emission", Color.red);
                material.SetFloat("_Flow_Map_Strength", 0);
                material.SetTexture("_Distortion_Texture", rig.Texels(Color.black));
                material.SetTexture("_Dream_Texture", rig.Texels(Dream(0)));
                material.SetTexture("_Stars_Texture", rig.Texels(none));
                material.SetTexture("_Goo_Noise_Tex", rig.Texels(Noise(0.3f)));
                material.SetTexture("_Flow_Map_Tex", rig.Texels(new Color(0.25f, 0.5f, 0, 1)));
                void Expect(Color expected, string label, int x = 16, int y = 16)
                {
                    rig.Expect(material, expected, label, x: x, y: y);
                    cases++;
                }

                // The goo: red picks within A..B (vertex alpha 1) or C..D (vertex alpha 0).
                Expect(Output(new Vector3(0.05f, 0.05f, 0.55f)), "red 0.3 contrasted to 0.5 between A and B");
                rig.SetColors(new Color(1, 1, 1, 0));
                Expect(Output(new Vector3(0.05f, 0.55f, 0.05f)), "vertex alpha 0 picks C..D");
                rig.SetColors(new Color(1, 1, 1, 0.5f));
                Expect(Output(new Vector3(0.05f, 0.3f, 0.3f)), "vertex alpha blends the pairs");
                rig.SetColors(Color.white);
                material.SetFloat("_Goo_Noise_Contrast", 1);
                Expect(Output(new Vector3(0.05f, 0.05f, 0.35f)), "the red contrast about 0.1");
                material.SetFloat("_Goo_Noise_Contrast", 2);

                // The dream texture, lifted by 0.05, and its stars: emission * colour + 0.45 - alpha, covering the dream
                // by saturate((dream alpha - 0.24) * star alpha * opacity).
                material.SetTexture("_Dream_Texture", rig.Texels(Dream(0.25f)));
                Expect(Output(new Vector3(0.3f, 0.05f, 0.55f)), "the dream texture");
                material.SetTexture("_Dream_Texture", rig.Texels(Dream(0)));
                material.SetTexture("_Stars_Texture", rig.Texels(Stars(0.25f)));
                float w = 0.76f * 0.25f;
                Expect(Output(new Vector3(0.05f + 0.65f * w, 0.05f + 0.15f * w, 0.5f + 0.05f + 0.15f * w)), "the stars");
                material.SetFloat("_Stars_Opacity", 0.5f);
                w *= 0.5f;
                Expect(Output(new Vector3(0.05f + 0.65f * w, 0.05f + 0.15f * w, 0.5f + 0.05f + 0.15f * w)), "the star opacity");
                material.SetFloat("_Stars_Opacity", 1);
                material.SetTexture("_Stars_Texture", rig.Texels(none));

                // The distortion (pixel 16 at u 0.5156, band 2 of 4): its red and green offset the dream UV by the
                // intensity and the stars UV by theirs; it scrolls by game time times its speed.
                material.SetTexture("_Dream_Texture", rig.Bands(Dream(0), Dream(0.1f), Dream(0.2f), Dream(0.3f)));
                material.SetTexture("_Distortion_Texture", rig.Texels(new Color(0.5f, 0, 0, 1)));
                Expect(Output(new Vector3(0.25f, 0.05f, 0.55f)), "no distortion intensity (band 2)");
                material.SetFloat("_Distortion_Intensity", 0.5f);
                Expect(Output(new Vector3(0.35f, 0.05f, 0.55f)), "the distortion intensity (band 3)");
                material.SetFloat("_Distortion_Intensity", 0);
                material.SetTexture("_Stars_Texture", rig.Bands(none, none, none, Stars(0.25f)));
                w = 0.76f * 0.25f;
                material.SetFloat("_Distortion_Intensity_Stars", 0.5f);
                Expect(Output(new Vector3(0.25f + 0.45f * w, 0.05f + 0.15f * w, 0.55f + 0.15f * w)), "the stars distortion (band 3)");
                material.SetFloat("_Distortion_Intensity_Stars", 0);
                material.SetTexture("_Stars_Texture", rig.Texels(none));
                material.SetTexture("_Distortion_Texture", rig.Bands(Color.black, Color.black, Color.black, new Color(0.5f, 0, 0, 1)));
                material.SetFloat("_Distortion_Intensity", 0.5f);
                Expect(Output(new Vector3(0.25f, 0.05f, 0.55f)), "the distortion's band 2 is still");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Distortion_Speed_X", 0.25f);
                Expect(Output(new Vector3(0.35f, 0.05f, 0.55f)), "the distortion scroll (band 3)");
                material.SetFloat("_Distortion_Speed_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Distortion_Intensity", 0);
                material.SetTexture("_Distortion_Texture", rig.Texels(Color.black));
                material.SetTexture("_Dream_Texture", rig.Texels(Dream(0)));

                // The goo UV on a 4-band strip (red 0.3, 0.3, 0.3, 0.1): the global scroll, the tiling offset applied
                // twice, and the flow map.
                material.SetTexture("_Goo_Noise_Tex", rig.Bands(Noise(0.3f), Noise(0.3f), Noise(0.3f), Noise(0.1f)));
                Color band2 = Output(new Vector3(0.05f, 0.05f, 0.55f)), band3 = Output(new Vector3(0.05f, 0.05f, 0.15f));
                Expect(band2, "goo band 2");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Shader.SetGlobalFloat("_UV_Scrolling_Direction_X", 0.25f);
                Expect(band3, "the global scroll direction (band 3)");
                Shader.SetGlobalFloat("_UV_Scrolling_Direction_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTextureOffset("_Goo_Noise_Tex", new Vector2(0.125f, 0));
                Expect(band3, "the offset applies twice (band 3)");
                material.SetTextureOffset("_Goo_Noise_Tex", Vector2.zero);
                // Flow (0.25 - 0.5) * -2 = 0.5 along u: at time 0 the half-cycle phase (0.5) is fully weighted.
                material.SetFloat("_Flow_Map_Strength", 1);
                Expect(band3, "the flow map at time 0 (u + 0.25)");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2.5f);
                Expect(band3, "the other phase at its peak (u + 0.25)");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1.25f);
                Expect(Output(new Vector3(0.05f, 0.05f, 0.35f)), "both phases blend (red 0.2 contrasts to 0.3)");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Flow_Map_Strength", 0);
                material.SetTexture("_Goo_Noise_Tex", rig.Texels(Noise(0.3f, 0.25f)));

                // The rim fade: towards the UV edge the colour fades to the fog colour plus the green noise.
                Vector3 pre = new Vector3(0.05f, 0.05f, 0.55f);
                Expect(Output(pre, 0, 0, 0.25f), "the corner is all rim", 0, 0);
                Expect(Output(pre, 6, 16, 0.25f), "part rim", 6, 16);
                material.SetFloat("_Green_Goo_Noise_Contrast", 2);
                Expect(Output(pre, 0, 0, 0.5f), "the green contrast", 0, 0);
                material.SetFloat("_Green_Goo_Noise_Contrast", 1);
                material.SetColor("_Fake_Fog_Color", new Color(0.5f, 0, 0, 1));
                Vector3 fog = new Vector3(Mathf.GammaToLinearSpace(0.5f), 0, 0);
                Expect(Output(pre, 0, 0, 0.25f, fog), "the fake fog colour", 0, 0);
                Expect(Output(pre, 6, 16, 0.25f, fog), "the fake fog colour, part rim", 6, 16);

                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Env_Zone5_Goo GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ".");
            }
        }
    }
}
