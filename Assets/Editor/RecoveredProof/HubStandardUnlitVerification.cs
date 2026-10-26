using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Hub/HL_Hub_Standard_Unlit, reconstructed from the shipped Metal programs: the diffuse UV
    // pushed by the scrolling distortion, scrolled and tiled; the hue-cycled glitter dotted with
    // (1 - view direction); and the alpha from the diffuse alpha times vertex red plus the glitter.
    public static class HubStandardUnlitVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Hub_Standard_Unlit.shader";
        private const string ShaderName = "HardLight/Hub/HL_Hub_Standard_Unlit";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "959ce1e53b9a11e449afa3188de59610";

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

        // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target.
        private static Color Blended(Color c, float a) => new Color(c.r * a, c.g * a, c.b * a, a);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);

                // Bilinear graph sampler: the diffuse is four 16-texel bands; the centre column reads band 2.
                Color r = Color.red, g = Color.green, b = Color.blue, k = new Color(0, 0, 0, 1);
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Diff_Texture", rig.Bands(r, g, b, k));
                material.SetFloat("_Alpha", 1);
                material.SetFloat("_Glitter_Opacity", 0);
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Distortion_Texture", rig.Texels(grey, grey, grey, grey));

                rig.Expect(material, Blended(b, 1), "diffuse without glitter");
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                rig.Expect(material, Blended(b, 0.5f), "alpha takes the vertex colour's red");
                rig.SetColors(Color.white);
                material.SetFloat("_Alpha", 0.5f);
                rig.Expect(material, Blended(b, 0.5f), "_Alpha");
                material.SetFloat("_Alpha", 1);

                // UV: distortion (grey 0.5 at intensity 0.5 adds 0.25), scrolling, then the diffuse tiling.
                material.SetFloat("_Distortion_Intensity", 0.5f);
                rig.Expect(material, Blended(k, 1), "distortion pushes the UV");
                material.SetFloat("_Distortion_Intensity", 0);
                material.SetFloat("_Diff_UV_Speed_X", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                rig.Expect(material, Blended(k, 1), "game time scrolls the UV");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Diff_UV_Speed_X", 0);
                material.SetTextureScale("_Diff_Texture", new Vector2(2, 1));
                rig.Expect(material, Blended(r, 1), "diffuse tiling");
                material.SetTextureScale("_Diff_Texture", Vector2.one);

                // Glitter: yellow less 0.3 normalises to (0.677, 0.677, -0.290); against 1 - view = (1, 1, 2)
                // that is 0.774. Over a transparent black diffuse, colour = glitter * glitter colour by the
                // glitter mask (0.774), and alpha = the mask.
                Color clear = new Color(0, 0, 0, 0);
                material.SetTexture("_Diff_Texture", rig.Texels(clear, clear, clear, clear));
                Color yellow = new Color(1, 1, 0, 1);
                material.SetTexture("_Glitter_Texture", rig.Texels(yellow, yellow, yellow, yellow));
                material.SetFloat("_Gliter_Amount", 0.3f);
                material.SetColor("_Glitter_Colour", Color.white);
                material.SetFloat("_Glitter_Opacity", 1);
                float glitter = 0.8f / Mathf.Sqrt(1.07f);
                rig.Expect(material, Blended(new Color(glitter * glitter, glitter * glitter, glitter * glitter), glitter), "glitter against the view");
                material.SetFloat("_Glitter_Opacity", 0.5f);
                float mask = glitter * 0.5f;
                rig.Expect(material, Blended(new Color(glitter * mask, glitter * mask, glitter * mask), mask), "glitter opacity");
                // Speed 1 at time 1/3 turns yellow (hue 1/6) to cyan, which saturates the glitter to 1.
                material.SetFloat("_Glitter_Opacity", 1);
                material.SetFloat("_Glitter_Speed", 1);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1 / 3f);
                rig.Expect(material, Blended(Color.white, 1), "hue-cycled glitter");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Glitter_Speed", 0);
                material.SetFloat("_Glitter_Opacity", 0);

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original. A 256
                // texture spanning UV 0..1 across the 32-pixel quad reads level 3 unbiased and level 4 at bias 1;
                // applied twice the bias would reach level 5. Each level has its own colour.
                void BiasCases(Color unbiased, Color once, string path)
                {
                    rig.Expect(material, unbiased, path + ": no bias reads the unbiased level");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    rig.Expect(material, once, path + ": bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                material.SetTexture("_Diff_Texture", rig.MipLevels(256, level => level <= 3 ? grey : level == 4 ? g : b));
                BiasCases(Blended(grey, 1), Blended(g, 1), "diffuse");
                // Distortion (intensity 0.5): level 4 (0.5) pushes the diffuse from band 2 (blue) to band 3 (black).
                material.SetTexture("_Diff_Texture", rig.Bands(r, g, b, k));
                material.SetFloat("_Distortion_Intensity", 0.5f);
                material.SetTexture("_Distortion_Texture", rig.MipLevels(256, level => level <= 3 ? Color.black : level == 4 ? new Color(0.5f, 0, 0, 1) : Color.red));
                BiasCases(Blended(b, 1), Blended(k, 1), "distortion");
                material.SetFloat("_Distortion_Intensity", 0);
                // Glitter at tiling 4 moves 32 texels of a 256 texture per pixel (level 5): bias 1 reads level 6
                // (cyan, glitter 1) once and would read level 7 (black, glitter 0) twice; level 5 is yellow (0.773).
                material.SetTexture("_Diff_Texture", rig.Texels(clear, clear, clear, clear));
                material.SetFloat("_Glitter_UV_Tiling", 4);
                material.SetFloat("_Glitter_Opacity", 1);
                material.SetTexture("_Glitter_Texture", rig.MipLevels(256, level => level <= 5 ? yellow : level == 6 ? Color.cyan : Color.black));
                BiasCases(Blended(new Color(glitter * glitter, glitter * glitter, glitter * glitter), glitter), Blended(Color.white, 1), "glitter");

                Debug.Log("Project Lucid HL_Hub_Standard_Unlit GPU verification passed: 15 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
