using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Zone2_Tube_Door, reconstructed from the shipped Metal programs: the hole that
    // opens from the UV centre (twice the distance, capped at 1, against the open amount) clipping Forward and
    // DepthOnly, and the tinted diffuse turned about the centre by twice the open amount.
    public static class GameplayZone2TubeDoorVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Zone2_Tube_Door.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Zone2_Tube_Door";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "7ef43191960ad5943897719c2515fc82";

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
                rig.SaveFloats("_AlphaToMaskAvailable");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), clear = new Color(0, 0, 0, 0);
                Color diffuse = new Color(0.25f, 0.5f, 1, 1);
                Material material = rig.Own(new Material(shader));
                material.SetColor("_Diff_Tint", Color.white);
                material.SetTexture("_Diff_Texture", rig.Texels(diffuse));
                void Expect(Color expected, string label, int x = 16, int y = 16)
                {
                    rig.Expect(material, expected, label, x: x, y: y);
                    cases++;
                }

                Expect(diffuse, "closed: the diffuse, opaque");
                material.SetColor("_Diff_Tint", new Color(1, 0, 1, 0));
                Expect(new Color(0.25f, 0, 1, 1), "tinted (the tint's alpha unused)");
                material.SetColor("_Diff_Tint", Color.white);

                // Opening: pixels where twice the distance from the centre (capped at 1) falls short of the amount
                // clip. Pixel (2, 16) sits 0.42 from the centre (0.84), the corner past 0.5 (capped to 1).
                material.SetTexture("_Diff_Texture", rig.Texels(Color.white));
                material.SetFloat("_Open_Door_Amount", 0.5f);
                Expect(clear, "half open clips the centre");
                Expect(Color.white, "and keeps the edge", 2);
                material.SetFloat("_Open_Door_Amount", 1);
                Expect(clear, "fully open clips the edge", 2);
                Expect(Color.white, "but not the corner (capped at 1)", 0, 0);
                // DepthOnly clips the same way.
                material.SetFloat("_Open_Door_Amount", 0.5f);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r == 0, "DepthOnly clips the centre");
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1, x: 2).r != 0, "DepthOnly keeps the edge");
                cases += 2;
                material.SetFloat("_Open_Door_Amount", 0);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth when closed");
                cases++;

                // The diffuse turns by twice the open amount (radians) about the centre. On a texture four texels tall
                // (point sampled; rows red, green, green, blue) pixel (2, 16) reads a middle row; open pi/4 turns it a
                // quarter, so it reads V 0.5 - (u - 0.5) = 0.92 (blue).
                var rows = rig.Own(new Texture2D(1, 4, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
                rows.SetPixels(new[] { red, green, green, blue });
                rows.Apply(false, false);
                material.SetTexture("_Diff_Texture", rows);
                Expect(green, "unturned: a middle row", 2);
                material.SetFloat("_Open_Door_Amount", Mathf.PI / 4);
                Expect(blue, "open pi/4 turns the diffuse a quarter", 2);
                material.SetFloat("_Open_Door_Amount", 0);

                // _GlobalMipBias applies once: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Diff_Texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : blue));
                Expect(red, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Gameplay_Zone2_Tube_Door GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The alpha-to-coverage sharpening remains compile-checked only.");
            }
        }
    }
}
