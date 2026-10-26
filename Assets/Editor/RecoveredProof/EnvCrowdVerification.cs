using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Crowd, reconstructed from the shipped Metal programs: the flipbook tile (the
    // game-time frame wrapped by the graph's signed Modulo, split into column and row, offset by the picked row),
    // the clip at the material threshold in both passes, the opaque alpha and its alpha-to-coverage sharpening.
    public static class EnvCrowdVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Crowd.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Crowd";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "8e8f358773ca69749aec9704ca3a5006";

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
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "_AlphaToMaskAvailable");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }

                // A 6 x 6 grid names the tile UV: (UV0 + (0, floor(row)) + (column, tile row)) / (width, height).
                const int size = 6;
                material.SetTexture("_Diff_Tex", rig.Grid(size));
                const float pixel = 16.5f / 32f;
                void Tile(float column, float row, string label, float width = 4, float height = 4) =>
                    Expect(rig.GridColor(size, new Vector2((pixel + column) / width, (pixel + row) / height)), label);
                Tile(0, 3, "time 0 shows the first frame of the picked row (3)");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Tile(1, 3, "one second at 1 FPS is the second frame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2.5f);
                Tile(2, 3, "frames are floored");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 5);
                Tile(1, 3, "the frame wraps at the sheet width");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_FPS", 2);
                Tile(2, 3, "the frame rate");
                material.SetFloat("_FPS", 1);
                material.SetFloat("_Pick_Row", 1.7f);
                Tile(1, 1, "the picked row is floored");
                // Negative time: the signed Modulo gives frame -1, tile -1, which splits into column 3 of tile row -1.
                Shader.SetGlobalFloat("_HLShaderTimeGame", -1);
                Tile(3, 0, "negative time keeps the dividend's sign");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Pick_Row", 0);
                material.SetFloat("_Max_Frames_width", 2);
                material.SetFloat("_Max_Frames_height", 2);
                Tile(1, 0, "a 2 x 2 sheet", 2, 2);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Max_Frames_width", 1);
                material.SetFloat("_Max_Frames_height", 1);

                // Alpha clip at the material threshold; an opaque surface then writes alpha 1.
                Color green = new Color(0, 1, 0, 1), clear = new Color(0, 0, 0, 0);
                Color Green(float a) => new Color(0, 1, 0, a);
                material.SetTexture("_Diff_Tex", rig.Texels(Green(0.6f)));
                Expect(green, "alpha 0.6 survives the default 0.5 clip and writes 1");
                material.SetTexture("_Diff_Tex", rig.Texels(Green(0.4f)));
                Expect(clear, "alpha 0.4 is clipped");
                material.SetFloat("_Alpha_Clip", 0.3f);
                Expect(green, "the clip threshold");
                // With alpha-to-coverage available the alpha is sharpened: (a - clip - fwidth / 2) / max(fwidth,
                // 1e-4) + 1, saturated, and the clip uses that minus 1e-4. Just under the threshold it is 0.5.
                material.SetFloat("_Alpha_Clip", 0.5f);
                material.SetTexture("_Diff_Tex", rig.Texels(Green(0.49995f)));
                Expect(clear, "just under the threshold is clipped without alpha-to-coverage");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                float sharpened = rig.Draw(material).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(sharpened - 0.5f) < 0.02f, "alpha-to-coverage sharpens it to 0.5: " + sharpened);
                material.SetTexture("_Diff_Tex", rig.Texels(Green(0)));
                material.SetFloat("_Alpha_Clip", 0);
                Expect(green, "a zero threshold keeps alpha 1 under alpha-to-coverage");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                cases++;

                // DepthOnly clips at the same threshold.
                material.SetFloat("_Alpha_Clip", 0.5f);
                material.SetTexture("_Diff_Tex", rig.Texels(Green(0.6f)));
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly keeps alpha 0.6");
                material.SetTexture("_Diff_Tex", rig.Texels(Green(0.4f)));
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r == 0, "DepthOnly clips alpha 0.4");
                cases += 2;

                // _GlobalMipBias applies once: over UV0 (a 1 x 1 sheet) a 256 texture reads level 3 unbiased and
                // level 4 at bias 1.
                material.SetTexture("_Diff_Tex", rig.MipLevels(256, level => level == 4 ? green : new Color(1, 0, 0, 1)));
                Expect(new Color(1, 0, 0, 1), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Env_Crowd GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
