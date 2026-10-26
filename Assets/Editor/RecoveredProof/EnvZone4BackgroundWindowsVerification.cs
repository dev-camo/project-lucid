using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone4_Background_Windows, reconstructed from the shipped Metal programs: the colour
    // blended towards the fog colour by the scene fog intensity (with or without a fog keyword), opaque alpha, and
    // the DepthOnly depth.
    public static class EnvZone4BackgroundWindowsVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone4_Background_Windows.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone4_Background_Windows";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "5bd8cc986944a52448c57fea5bd41fb5";

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
                rig.SaveVectors("unity_FogColor");
                Shader.SetGlobalVector("unity_FogColor", new Vector4(0, 0, 1, 1));
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }

                material.SetColor("_Diff_Colour", new Color(1, 0, 0, 0));
                Expect(new Color(1, 0, 0, 1), "the colour, opaque");
                material.SetFloat("_Scene_Fog_Intensity", 0.25f);
                Expect(new Color(0.75f, 0, 0.25f, 1), "a quarter of the way to the fog colour");
                material.SetFloat("_Scene_Fog_Intensity", 1);
                Expect(new Color(0, 0, 1, 1), "full fog intensity is the fog colour");
                material.EnableKeyword("FOG_LINEAR");
                Expect(new Color(0, 0, 1, 1), "the fog keyword changes nothing");
                material.DisableKeyword("FOG_LINEAR");

                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Env_Zone4_Background_Windows GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ".");
            }
        }
    }
}
