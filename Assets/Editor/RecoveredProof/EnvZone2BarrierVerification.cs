using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone2_Barrier, reconstructed from the shipped Metal programs: the pattern
    // colour between colours A and B, the twice-tiled scrolling distortion, the grid overlay and its constant camera
    // fade, and the rotated, scrolling stripe alpha.
    public static class EnvZone2BarrierVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone2_Barrier.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone2_Barrier";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "c6d7e466e18456242a97d63cb54ff5e7";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
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
                void Expect(Color expected, string label, int x = 16, Matrix4x4? model = null)
                {
                    rig.Expect(material, expected, label, x: x, model: model);
                    cases++;
                }

                // The pattern is sampled bilinearly, so it is four 16-texel bands (red: grid, green: stripes, blue:
                // main colour): (0, 1, 0), (0, 0, 0), (0.5, 1, 0.5), (0, 0, 1). The centre column reads band 2.
                Texture2D pattern = rig.Bands(new Color(0, 1, 0, 1), new Color(0, 0, 0, 1), new Color(0.5f, 1, 0.5f, 1), new Color(0, 0, 1, 1));
                material.SetTexture("__RGB_Pattern_Texture", pattern);
                Color black = new Color(0, 0, 0, 1), grey = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Distortion_Texture", rig.Bands(black));
                material.SetColor("_Main_Colour_A", Color.red);
                material.SetColor("_Main_Colour_B", Color.blue);
                material.SetFloat("_Stripe_Scale", 1);
                material.SetFloat("_Stripe_Alpha_A", 1);
                material.SetFloat("_Stripe_Alpha_B", 0.5f);
                material.SetFloat("_Grid_UV_Scale", 1);
                material.SetColor("_Grid_Color", new Color(0, 1, 0, 1));

                // Pattern blue 0.5 smoothsteps to 0.5 between A and B; stripe green 1 gives alpha B (0.5).
                Expect(Blended(0.5f, 0, 0.5f, 0.5f), "colours A to B and stripe alpha B");
                material.SetFloat("_Main_Colour_Edge_1", 0.5f);
                Expect(Blended(1, 0, 0, 0.5f), "the edges smoothstep the pattern");
                material.SetFloat("_Main_Colour_Edge_1", 0);
                material.SetFloat("_Use_Stripes", 1);
                Expect(Blended(0.5f, 0, 0.5f, 1), "disabling stripes gives alpha 1");
                material.SetFloat("_Use_Stripes", 0);

                // Distortion (0.5 at amount 0.5) moves the colour and stripe lookups by 0.25 to band 3.
                material.SetFloat("_Distortion_Amount", 0.5f);
                material.SetTexture("_Distortion_Texture", rig.Bands(grey));
                Expect(Blended(0, 0, 1, 1), "distortion moves the pattern and stripes");
                // On a banded distortion texture (grey only in band 3): time scrolls it, and its tiling and offset
                // apply twice (offset 0.125 reaches band 3).
                material.SetTexture("_Distortion_Texture", rig.Bands(black, black, black, grey));
                Expect(Blended(0.5f, 0, 0.5f, 0.5f), "no distortion from band 2");
                material.SetFloat("_Distortion_UV_Speed_Offset_X", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Blended(0, 0, 1, 1), "time scrolls the distortion");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Distortion_UV_Speed_Offset_X", 0);
                material.SetTextureOffset("_Distortion_Texture", new Vector2(0.125f, 0));
                Expect(Blended(0, 0, 1, 1), "the distortion tiling and offset apply twice");
                material.SetTextureOffset("_Distortion_Texture", Vector2.zero);
                material.SetFloat("_Distortion_Amount", 0);

                // Stripes scroll with time (0.25 to band 3, alpha A).
                material.SetFloat("_Stripe_Speed", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Blended(0.5f, 0, 0.5f, 1), "stripes scroll with time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Stripe_Speed", 0);

                // Stripe rotation: at pixel 8 (u 0.27, band 1, alpha A) a quarter turn (0.5 * 3.145) swaps in v
                // (0.52, band 2, alpha B). The main colour there reads band 1 (blue 0): colour A.
                Expect(Blended(1, 0, 0, 1), "unrotated stripes off-centre", x: 8);
                material.SetFloat("_Stripes_Rotation", 0.5f);
                Expect(Blended(1, 0, 0, 0.5f), "rotated stripes", x: 8);
                material.SetFloat("_Stripes_Rotation", 0);

                // Grid: where the pattern's red reaches the width the grid colour covers the main colour, weighted by
                // the grid colour's alpha; below it the grid shows only with distance.
                Expect(Blended(0.5f, 0, 0.5f, 0.5f), "the grid is off by default");
                material.SetFloat("_Enable_Grid", 1);
                Expect(Blended(0, 1, 0, 0.5f), "red at the width shows the grid colour");
                material.SetColor("_Grid_Color", new Color(0, 1, 0, 0.5f));
                Expect(Blended(0.25f, 0.5f, 0.25f, 0.5f), "the grid colour's alpha weights it");
                material.SetColor("_Grid_Color", new Color(0, 1, 0, 1));
                material.SetFloat("_Grid_UV_Scale", 1.5f);
                Expect(Blended(0.5f, 0, 0.5f, 0.5f), "the grid UV scale (u 0.77, band 3, red 0)");
                material.SetFloat("_Grid_UV_Scale", 1);
                material.SetFloat("_Grid_Pattern_Width", 0.6f);
                Expect(Blended(0.5f, 0, 0.5f, 0.5f), "red below the width hides the grid up close");

                // Camera fade: off the grid lines the grid colour fades in as saturate((20 - w) / 10) falls, so a
                // 90-degree perspective camera with the quad filling the view at clip w 12 shows 0.2 of it and at
                // w 30 all of it.
                float fov = rig.Camera.fieldOfView, far = rig.Camera.farClipPlane;
                try
                {
                    rig.Camera.orthographic = false;
                    rig.Camera.fieldOfView = 90;
                    rig.Camera.farClipPlane = 100;
                    Matrix4x4 AtDepth(float w) => Matrix4x4.TRS(new Vector3(0, 0, w - 2), Quaternion.identity, new Vector3(w, w, 1));
                    Expect(Blended(0.4f, 0.2f, 0.4f, 0.5f), "the camera fade at w 12", model: AtDepth(12));
                    Expect(Blended(0, 1, 0, 0.5f), "the camera fade at w 30", model: AtDepth(30));
                }
                finally
                {
                    rig.Camera.orthographic = true;
                    rig.Camera.fieldOfView = fov;
                    rig.Camera.farClipPlane = far;
                }
                material.SetFloat("_Grid_Pattern_Width", 0.5f);
                material.SetFloat("_Enable_Grid", 0);

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original: over UV0 a 256
                // texture with a colour per level reads level 3 unbiased and level 4 at bias 1 (level 5 if twice).
                void BiasCases(Color unbiased, Color once, string path)
                {
                    Expect(unbiased, path + ": no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(once, path + ": bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                // The main colour (blue) and stripes (green) samples: level 3 (blue 0.5, green 1), level 4 (blue 1,
                // green 0), level 5 (blue 0, green 1).
                material.SetTexture("__RGB_Pattern_Texture", rig.MipLevels(256, level => level <= 3 ? new Color(0, 1, 0.5f, 1) : level == 4 ? new Color(0, 0, 1, 1) : new Color(0, 1, 0, 1)));
                BiasCases(Blended(0.5f, 0, 0.5f, 0.5f), Blended(0, 0, 1, 1), "pattern colour and stripes");
                // The grid sample: red 1 only at level 4.
                material.SetFloat("_Enable_Grid", 1);
                material.SetTexture("__RGB_Pattern_Texture", rig.MipLevels(256, level => new Color(level == 4 ? 1 : 0, 1, 0.5f, 1)));
                BiasCases(Blended(0.5f, 0, 0.5f, 0.5f), Blended(0, 1, 0, 0.5f), "grid");
                material.SetFloat("_Enable_Grid", 0);
                // Distortion (amount 0.5): level 4 (0.5) moves both pattern lookups to band 3.
                material.SetTexture("__RGB_Pattern_Texture", pattern);
                material.SetFloat("_Distortion_Amount", 0.5f);
                material.SetTexture("_Distortion_Texture", rig.MipLevels(256, level => level <= 3 ? black : level == 4 ? new Color(0.5f, 0.5f, 0, 1) : new Color(1, 1, 0, 1)));
                BiasCases(Blended(0.5f, 0, 0.5f, 0.5f), Blended(0, 0, 1, 1), "distortion");

                Debug.Log("Project Lucid HL_Env_Zone2_Barrier GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
