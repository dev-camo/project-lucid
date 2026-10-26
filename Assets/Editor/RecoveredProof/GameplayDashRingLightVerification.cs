using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Dash_Ring_Light, reconstructed from the shipped Metal programs: the light texture
    // times 1.5 tinted by the idle or active colour, the dream texture in dream mode, both scrolled by game time,
    // opaque alpha, and the DepthOnly depth.
    public static class GameplayDashRingLightVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Dash_Ring_Light.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Dash_Ring_Light";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "4cd63add086047e448053c613c3ea10e";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_Texture2DAsset_df0b622c693b4ce09eb0cef48816c421_Out_0_Texture2D", "b24b778e2484c7e43b7a5a13dbb5de69" },
            { "_Texture2DAsset_3b305af8f02543c1b40ec4cd91ca4592_Out_0_Texture2D", "e332b2fb428a53f4b87bfe9b13356d64" },
        };
        private const string Light = "_Texture2DAsset_df0b622c693b4ce09eb0cef48816c421_Out_0_Texture2D";
        private const string Dream = "_Texture2DAsset_3b305af8f02543c1b40ec4cd91ca4592_Out_0_Texture2D";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                int cases = 0;
                Color black = new Color(0, 0, 0, 1), white = Color.white, gray = new Color(0.5f, 0.5f, 0.5f, 1), blue = new Color(0, 0, 1, 1);
                Material material = rig.Own(new Material(shader));
                material.SetTexture(Light, rig.Texels(gray));
                material.SetTexture(Dream, rig.Texels(blue));
                material.SetColor("_Default_Idle_Colour", new Color(1, 0, 1, 1));
                material.SetColor("_Default_Active_Colour", new Color(0, 1, 0, 1));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }

                Expect(new Color(0.75f, 0, 0.75f, 1), "idle: the light texture times 1.5 times the idle colour, opaque");
                material.SetFloat("_ISACTIVE", 1);
                Expect(new Color(0, 0.75f, 0, 1), "active: times the active colour");
                material.SetFloat("_ISDREAM", 1);
                Expect(blue, "dream mode shows the dream texture");
                material.SetFloat("_ISACTIVE", 0);
                Expect(blue, "dream mode ignores the activation");
                material.SetFloat("_ISDREAM", 0);

                // The scroll on 4-band strips (pixel 16 at u 0.5156, band 2): u + game time * speed.
                material.SetTexture(Light, rig.Bands(black, black, white, black));
                material.SetTexture(Dream, rig.Bands(black, black, black, blue));
                Expect(new Color(1.5f, 0, 1.5f, 1), "band 2 of the light texture");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2);
                material.SetFloat("_UV_Speed", 0.125f);
                Expect(black, "the light texture scrolls to band 3");
                material.SetFloat("_ISDREAM", 1);
                Expect(blue, "the dream texture scrolls with it");
                material.SetFloat("_ISDREAM", 0);
                material.SetFloat("_UV_Speed", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);

                // _GlobalMipBias applies once: across the quad a 256 texture is read at level 3; bias 1 reads level 4.
                material.SetTexture(Light, rig.MipLevels(256, level => level == 3 ? gray : level == 4 ? white : black));
                Expect(new Color(0.75f, 0, 0.75f, 1), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(new Color(1.5f, 0, 1.5f, 1), "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Gameplay_Dash_Ring_Light GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The ShadowCaster pass remains compile-checked only.");
            }
        }
    }
}
