using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone4_Cloud_Plane, reconstructed from the shipped Metal programs: the two-octave
    // colour noise at world XZ (its scroll, the (0.3, 0.2) offset and 0.5 scale of the second octave, contrast and
    // lightness) times the colour; the depth-fade alpha times vertex red; and the vertex displacement along the
    // world normal by the displacement noise times its amount.
    public static class EnvZone4CloudPlaneVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone4_Cloud_Plane.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone4_Cloud_Plane";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "5367cace296c0a64794ff50aa63d7686";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D", "5cecf516273ba544f8abc9afeafea881" },
            { "_Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D", "5cecf516273ba544f8abc9afeafea881" },
        };
        private const string ColourNoise = "_Texture2DAsset_98ffcd9dfa7244b3a64ded4333b20935_Out_0_Texture2D";
        private const string DisplacementNoise = "_Texture2DAsset_994fbcb52e0341d68bebd7d5c495b39f_Out_0_Texture2D";

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
                rig.SaveVectors("_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Perspective depth branch with a scene eye depth of 4 against the ortho quad's clip w 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;
                Color black = new Color(0, 0, 0, 1), white = Color.white, clear = new Color(0, 0, 0, 0);
                Color Noise(float x) => new Color(x, 0, 0, 1);
                Color Gray(float v, float a = 1) => new Color(v, v, v, a);

                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target: a
                // fragment (rgb, a) reads (rgb * a, a).
                Material material = rig.Own(new Material(shader));
                material.SetTexture(ColourNoise, rig.Texels(Noise(0.25f)));
                material.SetTexture(DisplacementNoise, rig.Texels(black));
                void Expect(Color c, string label, int x = 16)
                {
                    rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a), label, x: x);
                    cases++;
                }

                // Colour: saturate(contrast * (octave 1 + octave 2) + lightness) times the colour; the depth fade
                // ((4 - 1) / 3 * 1) saturates the alpha to vertex red (1).
                Expect(Gray(0.5f), "two octaves of 0.25");
                material.SetFloat("_Colour_Noise_Contrast", 0.5f);
                Expect(Gray(0.25f), "the contrast");
                material.SetFloat("_Colour_Noise_Lightness", 0.5f);
                Expect(Gray(0.75f), "the lightness");
                material.SetFloat("_Colour_Noise_Contrast", 4);
                Expect(white, "saturated");
                material.SetFloat("_Colour_Noise_Contrast", 1);
                material.SetFloat("_Colour_Noise_Lightness", 0);
                material.SetColor("_Colour", new Color(1, 0, 1, 1));
                Expect(new Color(0.5f, 0, 0.5f, 1), "the colour");
                material.SetColor("_Colour", white);

                // The octave UVs, on a 4-band strip with the scale at 0 (constant UVs): octave 1 reads u = time *
                // speed.x, octave 2 reads (0.3 + time * speed.z) * 0.5.
                material.SetFloat("_Colour_Noise_UV_Scale", 0);
                material.SetTexture(ColourNoise, rig.Bands(Noise(0.1f), Noise(0), Noise(0.2f), Noise(0)));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetVector("_Colour_Noise_UV_Speed_Direction", new Vector4(0.125f, 0, 0.85f, 0));
                Expect(Gray(0.3f), "octave 1 at u 0.125 (band 0), octave 2 at u 0.575 (band 2)");
                material.SetVector("_Colour_Noise_UV_Speed_Direction", new Vector4(0.375f, 0, 0.85f, 0));
                Expect(Gray(0.2f), "octave 1 scrolls to band 1");
                material.SetVector("_Colour_Noise_UV_Speed_Direction", new Vector4(0.125f, 0, 0.45f, 0));
                Expect(Gray(0.1f), "octave 2 scrolls to band 1 (u 0.375)");
                material.SetVector("_Colour_Noise_UV_Speed_Direction", Vector4.zero);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Colour_Noise_UV_Scale", 1);
                material.SetTexture(ColourNoise, rig.Texels(Noise(0.25f)));

                // Alpha: saturate((4 - 1) / distance * strength) times vertex red.
                material.SetFloat("_Depth_Distance", 6);
                Expect(Gray(0.5f, 0.5f), "the depth distance");
                material.SetFloat("_Depth_Strength", 0.5f);
                Expect(Gray(0.5f, 0.25f), "the depth strength");
                material.SetFloat("_Depth_Strength", 1);
                material.SetFloat("_Depth_Distance", 3);
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                Expect(Gray(0.5f, 0.5f), "vertex red");
                rig.SetColors(new Color(1, 0, 0, 0));
                Expect(Gray(0.5f), "only vertex red");
                rig.SetColors(white);

                // Displacement along the world normal: two octaves of 0.25 times the amount. Normals along +x move the
                // quad right; 0.5 (8 px) uncovers pixel 4, pixel 12 stays covered.
                rig.SetNormals(Vector3.right);
                material.SetTexture(DisplacementNoise, rig.Texels(Noise(0.25f)));
                Expect(Gray(0.5f), "no displacement at the default amount 0", 4);
                material.SetFloat("_VD_Noise_Amount", 1);
                Expect(clear, "the displacement moves the quad", 4);
                Expect(Gray(0.5f), "the moved quad still covers pixel 12", 12);
                material.SetFloat("_VD_Noise_Amount", 0.5f);
                Expect(Gray(0.5f), "the amount (0.25, 4 px)", 4);
                material.SetFloat("_VD_Noise_Amount", 0);
                rig.SetNormals(Vector3.back);

                // _GlobalMipBias applies once per sample: at scale 5 octave 1 moves 1/32 of the texture per pixel
                // (level 3 of 256) and octave 2 half that (level 2); bias 1 reads levels 4 and 3.
                material.SetFloat("_Colour_Noise_UV_Scale", 5);
                material.SetTexture(ColourNoise, rig.MipLevels(256, level => Noise(level == 2 ? 0.1f : level == 3 ? 0.2f : level == 4 ? 0.4f : 0)));
                Expect(Gray(0.3f), "no bias reads levels 3 and 2");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(Gray(0.6f), "bias 1 applies once to each octave");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Env_Zone4_Cloud_Plane GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The displacement noise UVs and the orthographic depth branch remain compile-checked only.");
            }
        }
    }
}
