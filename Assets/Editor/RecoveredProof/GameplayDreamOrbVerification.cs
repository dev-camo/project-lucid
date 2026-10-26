using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Dream_Orb, reconstructed from the shipped Metal programs: the pushed
    // view-space matcap or the tiled diffuse, the dream-noise ramp weighted where the matcap is clear, the
    // detail mask on the second UV, and the matcap alpha.
    public static class GameplayDreamOrbVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Dream_Orb.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Dream_Orb";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "4d86ae3a07402f4438849f05090abbad";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D", "737b0c6c5bbc2af4198c74848846235c" },
            { "_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D", "f172343386ff2dd4aa174ce383a732af" },
        };
        private const int Forward = 0, DepthOnly = 1;
        private const string Noise = "_SampleTexture2D_a7a00c3b42cd409295935933b2dd6395_Texture_1_Texture2D";
        private const string Detail = "_SampleTexture2D_cb50bfe028ef4036ac3206ebb85e8866_Texture_1_Texture2D";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "INSTANCING_ON", "_WRITE_RENDERING_LAYERS" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT2", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION" },
            new[] { "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
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
                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha One OneMinusSrcAlpha) over a cleared target,
                // so a fragment (rgb, a) reads back as (rgb * a, a).
                void Expect(Material m, Color c, string label) => rig.Expect(m, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a), label, pass: Forward);

                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), clear = new Color(0, 0, 0, 0);
                Material material = rig.Own(new Material(shader));
                material.SetTexture(Detail, rig.Texels(clear));
                material.SetFloat("_Use_Matcap_Bool", 1);

                // Matcap: the quad faces the camera, so the view-space normal and view direction meet at the centre;
                // tilting the normal by x (or y) moves the lookup by x (or y) times the push amount. On a 5x5 grid
                // the centre is texel (2, 2).
                material.SetTexture("_Matcap_Tex", rig.Grid(5));
                material.SetFloat("_Matcap_Border_Push_Amount", 0.5f);
                Expect(material, rig.GridColor(5, new Vector2(0.5f, 0.5f)), "facing matcap reads the centre");
                rig.SetNormals(new Vector3(0.8f, 0, -0.6f));
                Expect(material, rig.GridColor(5, new Vector2(0.9f, 0.5f)), "normal x pushes the matcap u");
                material.SetFloat("_Matcap_Border_Push_Amount", 0.25f);
                Expect(material, rig.GridColor(5, new Vector2(0.7f, 0.5f)), "the push amount scales the offset");
                material.SetFloat("_Matcap_Border_Push_Amount", 0.5f);
                rig.SetNormals(new Vector3(0, 0.8f, -0.6f));
                Expect(material, rig.GridColor(5, new Vector2(0.5f, 0.9f)), "normal y pushes the matcap v");
                rig.SetNormals(Vector3.back);

                // Diffuse (tiled by its own scale and offset) when the matcap is off; the alpha stays the matcap's.
                material.SetTexture("_Matcap_Tex", rig.Texels(new Color(1, 0, 0, 0.5f)));
                material.SetFloat("_Dream_Noise_Opacity", 0);
                material.SetTexture("_Diff_Texture", rig.Texels(red, blue));
                Expect(material, new Color(1, 0, 0, 0.5f), "matcap colour and alpha");
                material.SetFloat("_Use_Matcap_Bool", 0);
                Expect(material, new Color(0, 0, 1, 0.5f), "diffuse colour with the matcap alpha");
                material.SetTextureOffset("_Diff_Texture", new Vector2(0.5f, 0));
                Expect(material, new Color(1, 0, 0, 0.5f), "diffuse offset");
                material.SetTextureOffset("_Diff_Texture", Vector2.zero);
                material.SetFloat("_Use_Matcap_Bool", 0.5f);
                Expect(material, new Color(0.5f, 0, 0.5f, 0.5f), "_Use_Matcap_Bool lerps diffuse to matcap");
                material.SetFloat("_Use_Matcap_Bool", 1);
                material.SetFloat("_Alpha_Amount", 0.5f);
                Expect(material, new Color(1, 0, 0, 0.25f), "_Alpha_Amount scales the matcap alpha");
                material.SetFloat("_Alpha_Amount", 1);

                // Dream noise: weight = saturate(noise red * (1 - matcap alpha) * 5 * opacity), and the ramp reads at
                // the noise red and green. Matcap black at alpha 0.5; a two-texel ramp (red, green).
                material.SetTexture("_Matcap_Tex", rig.Texels(new Color(0, 0, 0, 0.5f)));
                material.SetTexture("_Dream_Noise_Colour_Ramp", rig.Texels(red, green));
                material.SetTexture(Noise, rig.Texels(new Color(0.1f, 0.5f, 0, 1)));
                material.SetFloat("_Dream_Noise_Opacity", 1);
                Expect(material, new Color(0.25f, 0, 0, 0.5f), "noise 0.1 weighs the ramp by a quarter");
                material.SetFloat("_Dream_Noise_Opacity", 0.4f);
                Expect(material, new Color(0.1f, 0, 0, 0.5f), "the noise opacity scales the weight");
                material.SetFloat("_Dream_Noise_Opacity", 1);
                material.SetTexture("_Matcap_Tex", rig.Texels(new Color(0, 0, 0, 0.75f)));
                Expect(material, new Color(0.125f, 0, 0, 0.75f), "the matcap alpha holds the noise back");
                material.SetTexture("_Matcap_Tex", rig.Texels(new Color(0, 0, 0, 0.5f)));
                material.SetTexture(Noise, rig.Texels(new Color(0.6f, 0.5f, 0, 1)));
                Expect(material, new Color(0, 1, 0, 0.5f), "the noise red picks the ramp texel and saturates the weight");
                // Tiling and scrolling: noise texel 0 (0.6) and texel 1 (0.1); u 0.516 reads texel 1, tiled by 2
                // (u 1.03) or scrolled by 0.5 it reads texel 0.
                material.SetTexture(Noise, rig.Texels(new Color(0.6f, 0.5f, 0, 1), new Color(0.1f, 0.5f, 0, 1)));
                Expect(material, new Color(0.25f, 0, 0, 0.5f), "untiled noise reads texel 1");
                material.SetFloat("_Dream_Noise_UV_Tiling_X", 2);
                Expect(material, new Color(0, 1, 0, 0.5f), "noise tiling X");
                material.SetFloat("_Dream_Noise_UV_Tiling_X", 1);
                material.SetFloat("_Dream_Noise_UV_Speed_X", 0.5f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(material, new Color(0, 1, 0, 0.5f), "noise scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Dream_Noise_UV_Speed_X", 0);
                material.SetFloat("_Dream_Noise_Opacity", 0);

                // Detail mask (alpha) on the second UV: out = (1 - d) * (colour - d * detail colour) + d * detail colour.
                // Texel 0 is 0.5, texel 1 is 0; UV1 is 0.2 across the quad, where UV0 would read texel 1.
                material.SetTexture("_Matcap_Tex", rig.Texels(red));
                material.SetColor("_Detail_Mask_Colour", blue);
                material.SetTexture(Detail, rig.Texels(new Color(0, 0, 0, 0.5f), clear));
                Quad1(rig, 0.2f);
                Expect(material, new Color(0.5f, 0, 0.25f, 1), "half detail mask on UV1");
                Quad1(rig, 0.75f);
                Expect(material, red, "UV1 0.75 reads the clear texel");
                Quad1(rig, 0.2f);
                material.SetFloat("_Detail_Mask_UV_Speed_X", 0.5f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(material, red, "the detail mask scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Detail_Mask_UV_Speed_X", 0);
                material.SetTexture(Detail, rig.Texels(new Color(0, 0, 0, 1)));
                Expect(material, blue, "a full detail mask shows the detail colour");
                material.SetTexture(Detail, rig.Texels(clear));

                // _GlobalMipBias applies once per sample. The diffuse and detail span their UVs across the quad
                // (level 3 unbiased, level 4 at bias 1, level 5 if applied twice). A constant matcap UV has no
                // derivatives to bias, so the normals splay from x -0.8 to 0.8 across the quad: at the centre the
                // 128 matcap reads level 2 unbiased, 3 at bias 1 and 4 if applied twice.
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                rig.Quad.normals = new[] { new Vector3(-0.8f, 0, -0.6f), new Vector3(0.8f, 0, -0.6f), new Vector3(0.8f, 0, -0.6f), new Vector3(-0.8f, 0, -0.6f) };
                material.SetTexture("_Matcap_Tex", rig.MipLevels(128, level => level <= 2 ? red : level == 3 ? green : blue));
                Expect(material, red, "matcap: no bias reads level 2");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, green, "matcap: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                rig.SetNormals(Vector3.back);
                material.SetTexture("_Matcap_Tex", rig.Texels(red));
                material.SetFloat("_Use_Matcap_Bool", 0);
                material.SetTexture("_Diff_Texture", rig.MipLevels(256, level => level <= 3 ? grey : level == 4 ? red : green));
                Expect(material, grey, "diffuse: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, red, "diffuse: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetFloat("_Use_Matcap_Bool", 1);
                rig.Quad.SetUVs(1, new System.Collections.Generic.List<Vector4>
                    { new Vector4(0, 0, 0, 0), new Vector4(1, 0, 0, 0), new Vector4(1, 1, 0, 0), new Vector4(0, 1, 0, 0) });
                material.SetTexture(Detail, rig.MipLevels(256, level => new Color(0, 0, 0, level <= 3 ? 0 : level == 4 ? 1 : 0.5f)));
                Expect(material, red, "detail: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, blue, "detail: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "DepthOnly writes the depth");

                Debug.Log("Project Lucid HL_Gameplay_Dream_Orb GPU verification passed: 27 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }

        // UV1 set to (u, u) at every corner.
        private static void Quad1(RecoveredShaderRig rig, float u)
        {
            var uv = new Vector4(u, u, 0, 0);
            rig.Quad.SetUVs(1, new System.Collections.Generic.List<Vector4> { uv, uv, uv, uv });
        }
    }
}
