using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Boss_03_Body_Blobs, reconstructed from the shipped Metal programs: the
    // flat colour, the erosion edge clipped at 0.5 (from the erosion texture's blue, progress and tiling),
    // the opaque alpha, and the scrolling displacement along the normal.
    public static class CharBoss03BodyBlobsVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Boss_03_Body_Blobs.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Boss_03_Body_Blobs";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "78c329c1d2e2b1f498a41271fa63ce7e";
        private const string Erosion = "_SampleTexture2D_576d80b255874f098fc57ed07098b701_Texture_1_Texture2D";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), KeywordSets);

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
                Material material = rig.Own(new Material(shader));
                material.SetColor("_Colour", Color.green);
                material.SetFloat("_Displacement_Amount", 0);
                Color Blue(float b) => new Color(0, 0, b, 1);
                Color clear = new Color(0, 0, 0, 0);

                // Erosion blue 0.4 at progress 0.5: edge smoothstep(0.4) = 0.352 is clipped; blue 0.2 gives
                // smoothstep(1) = 1 and the opaque surface writes alpha 1.
                material.SetTexture(Erosion, rig.Texels(Blue(0.2f), Blue(0.2f), Blue(0.2f), Blue(0.2f)));
                material.SetFloat("_Erosion_Progress", 0.5f);
                rig.Expect(material, Color.green, "uneroded colour, alpha 1");
                material.SetTexture(Erosion, rig.Texels(Blue(0.4f), Blue(0.4f), Blue(0.4f), Blue(0.4f)));
                rig.Expect(material, clear, "the eroded edge clips at 0.5");
                // Progress 0.45 shifts the erosion by -0.1 to 0.3: smoothstep(0.8) = 0.896 survives.
                material.SetFloat("_Erosion_Progress", 0.45f);
                rig.Expect(material, Color.green, "progress shifts the erosion");
                material.SetFloat("_Erosion_Progress", 0.5f);

                // Tiling: bands of blue 0.2 and 0.4; tiling 0.5 moves the centre column (u 0.516, band 2: 0.4,
                // clipped) to u 0.258 (band 1: 0.2, kept).
                material.SetTexture(Erosion, rig.Texels(Blue(0.2f), Blue(0.2f), Blue(0.4f), Blue(0.4f)));
                rig.Expect(material, clear, "untiled erosion clips at the centre");
                material.SetFloat("_Erosion_Tiling", 0.5f);
                rig.Expect(material, Color.green, "erosion tiling");
                material.SetFloat("_Erosion_Tiling", 1);

                // Displacement: the white displacement texture at 0.5 pushes the quad along a +X normal, so
                // pixel 5 (x -0.66) falls outside it.
                material.SetTexture(Erosion, rig.Texels(Blue(0.2f), Blue(0.2f), Blue(0.2f), Blue(0.2f)));
                rig.SetNormals(Vector3.right);
                rig.Expect(material, Color.green, "undisplaced edge", x: 5);
                material.SetFloat("_Displacement_Amount", 0.5f);
                rig.Expect(material, clear, "displacement along the normal", x: 5);
                material.SetFloat("_Displacement_Amount", 0);
                rig.SetNormals(Vector3.back);

                // _GlobalMipBias applies once (URP's SAMPLE_TEXTURE2D), as in the original: over UV0 a 256 erosion
                // texture reads level 3 (blue 0.2, kept) unbiased and level 4 (0.4, clipped) at bias 1; applied twice
                // it would read level 5 (0.2, kept).
                material.SetTexture(Erosion, rig.MipLevels(256, level => Blue(level == 4 ? 0.4f : 0.2f)));
                rig.Expect(material, Color.green, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                rig.Expect(material, clear, "bias 1 applies once");

                Debug.Log("Project Lucid HL_Char_Boss_03_Body_Blobs GPU verification passed: 9 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
