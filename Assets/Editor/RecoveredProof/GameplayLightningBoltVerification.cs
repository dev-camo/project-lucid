using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_LightningBolt, reconstructed from the shipped Metal programs: Shader Graph's
    // Rectangle node (width, height, one-pixel antialiasing) on UV0 rotated by degrees and pulled towards the scrolling,
    // scaled noise by its intensity, written as (coverage * colour, coverage).
    public static class GameplayLightningBoltVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_LightningBolt.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_LightningBolt";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "d357db6b343d23f4a9385855676c9473";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D", "737b0c6c5bbc2af4198c74848846235c" },
        };
        private const string Noise = "_SampleTexture2D_c8f92df2dd054d678562be1997342e27_Texture_1_Texture2D";

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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                material.SetColor("_Color", new Color(1, 0, 0, 1));
                material.SetFloat("_Noise_Intensity", 0);
                material.SetTexture(Noise, rig.Texels(new Color(0.5f, 0.5f, 0, 1)));
                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target: a
                // coverage b writes (b * b * red, b).
                void Expect(float b, string label, int x = 16, int y = 16)
                {
                    rig.Expect(material, new Color(b * b, 0, 0, b), label, x: x, y: y);
                    cases++;
                }
                // Coverage along X at a pixel column: 1 - (|2u - 1| - width) / (1/16), saturated (|2u - 1| moves 1/16
                // per pixel).
                float Edge(int x, float width) => Mathf.Clamp01(1 - (Mathf.Abs(2 * (x + 0.5f) / 32f - 1) - width) * 16);

                Expect(1, "the bolt's centre column at width 0.05");
                Expect(0, "outside the width", 20);
                material.SetFloat("_Width", 0);
                Expect(Edge(16, 0), "width 0 leaves the antialiased half pixel");
                material.SetFloat("_Width", 0.25f);
                Expect(Edge(20, 0.25f), "the width", 20);
                material.SetFloat("_Width", 0.05f);
                material.SetFloat("_Height", 0);
                Expect(Edge(16, 0), "the height clips along V");
                material.SetFloat("_Height", 1);
                material.SetFloat("_Rotation", 90);
                Expect(1, "rotated 90 degrees the bolt runs along U", 20);
                Expect(0, "and leaves V", 16, 20);
                material.SetFloat("_Rotation", 0);

                // The noise pulls UV towards its red and green by the intensity: at 0.5 towards (0.5, 0.5) |2u - 1|
                // halves, so pixel 18 (du 0.078) at width 0.07 is 0.26 of a 1/32 derivative outside: coverage 0.74.
                material.SetFloat("_Width", 0.07f);
                Expect(0, "pixel 18 without noise", 18);
                material.SetFloat("_Noise_Intensity", 0.5f);
                Expect(0.74f, "the noise intensity", 18);
                // The noise on a 4-band strip: band 2 (0.5, 0.5), band 3 (1, 0.5) pushes the bolt away. Pixel 18 reads
                // noise u 0.578 (band 2); the scale or the scroll reaches band 3.
                Color centre = new Color(0.5f, 0.5f, 0, 1), away = new Color(1, 0.5f, 0, 1);
                material.SetTexture(Noise, rig.Bands(centre, centre, centre, away));
                Expect(0.74f, "noise band 2", 18);
                material.SetFloat("_Noise_Scale", 1.5f);
                Expect(0, "the noise scale (band 3)", 18);
                material.SetFloat("_Noise_Scale", 1);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Noise_Speed_X", 0.25f);
                Expect(0, "the noise scroll (band 3)", 18);
                material.SetFloat("_Noise_Speed_X", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);

                // _GlobalMipBias applies once to the noise: across the quad a 256 texture is read at level 3.
                material.SetTexture(Noise, rig.MipLevels(256, level => level == 3 ? centre : away));
                Expect(0.74f, "noise: no bias reads level 3", 18);
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(0, "noise: bias 1 reads level 4", 18);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Gameplay_LightningBolt GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ".");
            }
        }
    }
}
