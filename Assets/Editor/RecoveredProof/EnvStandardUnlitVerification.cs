using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Standard_Unlit, reconstructed from the shipped Metal programs: the tinted
    // Diff with the vertex-colour toggle, scrolling that tiles the UV twice, the graph's fog capped by
    // _Fog_Max_Amount, and the transparent alpha with camera-distance fade and alpha modulate.
    public static class EnvStandardUnlitVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Standard_Unlit.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Standard_Unlit";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "ccfc70e40a7e6a74385421abe4e85ad8";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_USE_SCROLLING_UVS", "_WRITE_RENDERING_LAYERS" },
            new[] { "FOG_EXP2", "_ALPHAMODULATE_ON", "_SURFACE_TYPE_TRANSPARENT", "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "_ALPHATEST_ON", "_USE_SCROLLING_UVS" },
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
            bool fogWasOn = Shader.IsKeywordEnabled("FOG_LINEAR");
            try
            {
                using (var rig = new RecoveredShaderRig(shader))
                {
                    RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                    rig.SaveFloats("_HLShaderTimeGame");
                    rig.SaveVectors("unity_FogParams", "unity_FogColor", "_ProjectionParams");
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                    Shader.DisableKeyword("FOG_LINEAR");

                    // The graph's sampler filters bilinearly: the Diff is four 16-texel bands, red, green,
                    // blue, black. The centre column reads band 2.
                    Color r = Color.red, g = Color.green, b = Color.blue, k = new Color(0, 0, 0, 1);
                    Material material = rig.Own(new Material(shader));
                    material.SetFloat("_SrcBlend", 1);
                    material.SetFloat("_DstBlend", 0);
                    material.SetColor("_Diff_Tint", Color.white);
                    material.SetTexture("_Diff_Tex", rig.Bands(r, g, b, k));

                    rig.Expect(material, b, "opaque Diff, alpha 1");
                    material.SetColor("_Diff_Tint", new Color(1, 1, 0, 1));
                    rig.Expect(material, k, "Diff tint");
                    material.SetColor("_Diff_Tint", Color.white);
                    rig.SetColors(new Color(1, 1, 0, 1));
                    material.SetFloat("_USE_VERTEX_COLOUR_TINT", 1);
                    rig.Expect(material, k, "vertex colour tint");
                    material.SetFloat("_USE_VERTEX_COLOUR_TINT", 0);
                    rig.SetColors(Color.white);

                    // Offset 0.25 reads band 3. Scrolling 0.25 at time 1 adds 0.25 and tiles again, so the
                    // offset counts twice: 0.516 + 0.75 wraps to band 1.
                    material.SetTextureOffset("_Diff_Tex", new Vector2(0.25f, 0));
                    material.SetFloat("_UV_Scrolling_Horizontal", 0.25f);
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                    rig.Expect(material, k, "scrolling is off without its keyword");
                    material.EnableKeyword("_USE_SCROLLING_UVS");
                    rig.Expect(material, g, "scrolled UV tiled twice");
                    material.DisableKeyword("_USE_SCROLLING_UVS");
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                    material.SetFloat("_UV_Scrolling_Horizontal", 0);
                    material.SetTextureOffset("_Diff_Tex", Vector2.zero);

                    // Fog factor 0.25 is density 0.75, capped to 0.375 by a max amount of 0.5, towards red.
                    Shader.EnableKeyword("FOG_LINEAR");
                    Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                    Shader.SetGlobalVector("unity_FogParams", new Vector4(0, 0, 0, 0.25f));
                    Shader.SetGlobalVector("unity_FogColor", new Vector4(1, 0, 0, 1));
                    material.SetFloat("_Fog_Max_Amount", 0.5f);
                    rig.Expect(material, new Color(0.375f, 0, 0.625f, 1), "fog capped by its max amount");
                    material.SetFloat("_Fog_Max_Amount", 1);
                    Shader.DisableKeyword("FOG_LINEAR");

                    // Transparent: Diff alpha times tint alpha, faded by camera distance when enabled.
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    Color half = new Color(0, 0, 1, 0.5f);
                    material.SetTexture("_Diff_Tex", rig.Bands(r, g, half, k));
                    rig.Expect(material, half, "transparent Diff alpha");
                    float distance = rig.ClipDistance(Vector3.zero);
                    material.SetFloat("_Camera_Distance_Fade_Near", distance - 0.1f);
                    material.SetFloat("_Camera_Distance_Fade_Far", distance + 0.1f);
                    rig.Expect(material, half, "camera distance fade is off by default");
                    material.SetFloat("_Use_Camera_Distance_Fade", 1);
                    rig.Expect(material, new Color(0, 0, 1, 0.25f), "camera distance fade halves the alpha");
                    material.EnableKeyword("_ALPHAMODULATE_ON");
                    rig.Expect(material, new Color(0.75f, 0.75f, 1, 0.25f), "alpha modulate lerps from white");
                    material.DisableKeyword("_ALPHAMODULATE_ON");
                    material.SetFloat("_Use_Camera_Distance_Fade", 0);
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    rig.Expect(material, b, "opaque ignores the Diff alpha");

                    // _GlobalMipBias applies once (URP's SAMPLE_TEXTURE2D), as in the original: over UV0 a 256 texture
                    // reads level 3 unbiased and level 4 at bias 1; applied twice it would reach level 5.
                    Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                    material.SetTexture("_Diff_Tex", rig.MipLevels(256, level => level <= 3 ? grey : level == 4 ? g : b));
                    rig.Expect(material, grey, "no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    rig.Expect(material, g, "bias 1 applies once");

                    Debug.Log("Project Lucid HL_Env_Standard_Unlit GPU verification passed: 13 Metal-derived cases on " +
                              SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
                }
            }
            finally
            {
                if (fogWasOn) Shader.EnableKeyword("FOG_LINEAR");
                else Shader.DisableKeyword("FOG_LINEAR");
            }
        }
    }
}
