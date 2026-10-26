using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Shockwave_nightmare, reconstructed from the shipped Metal programs: the diffuse times its
    // tint (its tiling and offset applied twice, scrolled by game time) under the stepped fresnel rim (its noise at
    // UV0 * 2 scrolling up, the fresnel amount, intensity, opacity and colour), the alpha from vertex red times the
    // alpha, and the vertex push along the normal.
    public static class ShockwaveNightmareVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Shockwave_nightmare.shader";
        private const string ShaderName = "HardLight/Character/HL_Shockwave_nightmare";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "1c06f37850a088a48b21d6ac2c69ea32";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D", "e8129d5371ff1e04d8070064644689a1" },
        };
        private const string RimNoise = "_SampleTexture2D_fd8594977b2b47a0a26ae627707dfdfa_Texture_1_Texture2D";
        // The graph's rim step scale (1 / 3.88).
        private const float Step = 0.257731944f;

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
                Color black = new Color(0, 0, 0, 1), white = Color.white, red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1),
                      blue = new Color(0, 0, 1, 1), magenta = new Color(1, 0, 1, 1), clear = new Color(0, 0, 0, 0);
                Color Noise(float z) => new Color(0, 0, z, 1);

                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target: a
                // fragment (rgb, a) reads (rgb * a, a).
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Diffuse_Texture", rig.Texels(magenta));
                material.SetTexture(RimNoise, rig.Texels(Noise(0)));
                material.SetColor("_Diff_Colour_Tint", white);
                material.SetColor("_Fresnel_Color", green);
                material.SetFloat("_Vertex_Push_Amount", 0);
                void Expect(Color c, string label, int x = 16)
                {
                    rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a), label, x: x);
                    cases++;
                }

                // Facing the camera there is no rim: the diffuse times the tint; alpha saturate(vertex red * alpha).
                Expect(magenta, "the diffuse, facing");
                material.SetColor("_Diff_Colour_Tint", new Color(0, 1, 1, 1));
                Expect(blue, "times the tint");
                material.SetColor("_Diff_Colour_Tint", white);
                rig.SetColors(new Color(0.5f, 0, 0, 0));
                Expect(new Color(1, 0, 1, 0.5f), "vertex red (only)");
                material.SetFloat("_Alpha", 4);
                Expect(magenta, "times the alpha, saturated");
                rig.SetColors(white);
                material.SetFloat("_Alpha", 0.5f);
                Expect(new Color(1, 0, 1, 0.5f), "the alpha");
                material.SetFloat("_Alpha", 1);

                // A grazing normal (n.v 0.2): the rim ceil(0.8 * 4 - noise * 3) / 3.88 saturates to 1 with no noise, and
                // the fresnel is 0.8^amount * intensity; the weight 1 - rim * fresnel * opacity blends the rim colour
                // (rim times the fresnel colour) towards the diffuse.
                rig.SetNormals(new Vector3(0.9798f, 0, -0.2f));
                Expect(new Color(0.2f, 0.8f, 0.2f, 1), "the fresnel (0.8) at the defaults");
                material.SetFloat("_Fresnel_Intensity", 2);
                Expect(green, "intensity 2 saturates to the fresnel colour");
                material.SetFloat("_Fresnel_Opacity", 0.25f);
                Expect(new Color(0.6f, 0.4f, 0.6f, 1), "the fresnel opacity");
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(new Color(0.68f, 0.32f, 0.68f, 1), "the fresnel amount (power)");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Opacity", 1);

                // The noise's blue times 3 steps the rim (fresnel 1.6 at intensity 2): 3.2 - 1.5 rounds up to 2 steps,
                // 3.2 - 2.25 to 1.
                Color Rim(float steps)
                {
                    float r = steps * Step, w = Mathf.Clamp01(1 - r * 1.6f);
                    return new Color(w, r * (1 - w), w, 1);
                }
                Color stepped = Rim(2);
                material.SetTexture(RimNoise, rig.Texels(Noise(0.5f)));
                Expect(stepped, "the noise steps the rim");
                material.SetTexture(RimNoise, rig.Texels(Noise(0.75f)));
                Expect(Rim(1), "one rim step");
                // The noise UV: UV0 * 2 (pixel 16 at u 1.031, band 0; pixel 4 at u 0.281, band 1), scrolled up by
                // frac(game time * 0.2).
                material.SetTexture(RimNoise, rig.Bands(Noise(0), Noise(0.5f), Noise(0), Noise(0)));
                Expect(green, "noise band 0 at pixel 16");
                Expect(stepped, "noise band 1 at pixel 4", 4);
                var vertical = rig.Own(new Texture2D(1, 64, TextureFormat.RGBAFloat, false, true)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
                var column = new Color[64];
                for (int i = 0; i < 64; i++) column[i] = Noise(i / 16 == 2 ? 0.5f : 0);
                vertical.SetPixels(column);
                vertical.Apply(false, false);
                material.SetTexture(RimNoise, vertical);
                Expect(green, "vertical noise band 0 at v 1.031");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2.5f);
                Expect(stepped, "game time 2.5 scrolls v by 0.5 (band 2)");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 7.5f);
                Expect(stepped, "the scroll wraps (frac(1.5))");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTexture(RimNoise, rig.Texels(Noise(0)));
                material.SetFloat("_Fresnel_Intensity", 1);
                rig.SetNormals(Vector3.back);

                // The diffuse UV on a 4-band strip (pixel 16 at u 0.5156, band 2): the tiling and offset apply twice
                // (offset 0.25 moves it by 0.5), the scroll once.
                material.SetTexture("_Diffuse_Texture", rig.Bands(red, green, blue, white));
                Expect(blue, "band 2");
                material.SetTextureOffset("_Diffuse_Texture", new Vector2(0.25f, 0));
                Expect(red, "the offset applies twice");
                material.SetTextureOffset("_Diffuse_Texture", Vector2.zero);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2);
                material.SetFloat("_UV_Scrolling_Horizontal", 0.125f);
                Expect(white, "the horizontal scroll (u + 0.25)");
                material.SetTextureScale("_Diffuse_Texture", new Vector2(2, 1));
                Expect(blue, "the scroll goes through the second tiling: (1.031 + 0.25) * 2");
                material.SetTextureScale("_Diffuse_Texture", Vector2.one);
                material.SetFloat("_UV_Scrolling_Horizontal", 0);
                material.SetFloat("_UV_Scrolling_Vertical", 0.125f);
                Expect(blue, "the vertical scroll leaves u");
                material.SetFloat("_UV_Scrolling_Vertical", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);

                // _GlobalMipBias applies once to the diffuse: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Diffuse_Texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : black));
                Expect(red, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Diffuse_Texture", rig.Texels(magenta));

                // The push along the object normal: normals along +x (edge on, a full rim of the fresnel colour) move the
                // quad right; 0.5 (8 px) uncovers pixel 4, pixel 12 stays covered.
                rig.SetNormals(Vector3.right);
                Expect(green, "no push at 0", 4);
                material.SetFloat("_Vertex_Push_Amount", 0.5f);
                Expect(clear, "the push moves the quad", 4);
                Expect(green, "the moved quad still covers pixel 12", 12);
                material.SetFloat("_Vertex_Push_Amount", 0.25f);
                Expect(green, "the push amount (4 px)", 4);
                rig.SetNormals(Vector3.back);

                Debug.Log("Project Lucid HL_Shockwave_nightmare GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The perspective view direction remains compile-checked only.");
            }
        }
    }
}
