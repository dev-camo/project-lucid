using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Prototype/HL_Sky_dream_effect_panning, reconstructed from the shipped Metal programs: the main texture
    // panned along V by game time times 0.5, tinted by the base colour, with its red as the alpha.
    public static class SkyDreamEffectPanningVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Sky_dream_effect_panning.shader";
        private const string ShaderName = "HardLight/Prototype/HL_Sky_dream_effect_panning";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "ba5eacaba0746ef4c95ee4fe05feb050";

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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                material.SetColor("_Base_colour", Color.white);
                material.SetTexture("_Main_texture", rig.Texels(new Color(0.5f, 0.25f, 1, 1)));
                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target: a
                // colour c with alpha a writes (c * a, a).
                void Expect(Color c, float a, string label)
                {
                    rig.Expect(material, new Color(c.r * a, c.g * a, c.b * a, a), label);
                    cases++;
                }

                Expect(new Color(0.5f, 0.25f, 1), 0.5f, "the texture, its red as the alpha");
                material.SetColor("_Base_colour", new Color(1, 0, 1, 0));
                Expect(new Color(0.5f, 0, 1), 0.5f, "tinted by the base colour (its alpha unused)");
                material.SetColor("_Base_colour", Color.black);
                Expect(Color.black, 0.5f, "the default black base colour keeps the alpha");
                material.SetColor("_Base_colour", Color.white);

                // Panning on a texture two texels tall (point sampled, repeating): game time 1 moves V by 0.5 onto the
                // other row, time 2 by a whole period back.
                var rows = rig.Own(new Texture2D(1, 2, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
                Color red = new Color(1, 0, 0, 1), white = Color.white;
                rows.SetPixels(new[] { red, white });
                rows.Apply(false, false);
                material.SetTexture("_Main_texture", rows);
                Color still = rig.Draw(material);
                RecoveredShaderProof.Require(ShaderName, still == red || still == white, "a row at time 0: " + still);
                Color other = still == red ? white : red;
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(other, 1, "time 1 pans V by 0.5");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2);
                Expect(still, 1, "time 2 pans a whole period");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                cases++;

                // _GlobalMipBias applies once: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Main_texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? new Color(0.5f, 0, 0, 1) : white));
                Expect(red, 1, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(new Color(0.5f, 0, 0), 0.5f, "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Sky_dream_effect_panning GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ".");
            }
        }
    }
}
