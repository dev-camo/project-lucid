using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Boss_02_Goo, reconstructed from the shipped Metal programs. The colour (base colour
    // and emission alike) is checked through the Meta pass's emission output: the red noise between its colours
    // (contrast, scrolling, tiling), the green noise towards its colour (offset UV, opacity), the matcap fresnel and
    // reflection (and their view-space UV), and the depth fade against the scene depth. DepthNormals and Forward check
    // the vertex normal and the alpha of 1.
    public static class CharBoss02GooVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Boss_02_Goo.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Boss_02_Goo";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "6499a3a7f1326c14e8a2ee6cd0c8d04d";
        private const int Forward = 0, DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "_REFLECTION_PROBE_BOX_PROJECTION", "_ADDITIONAL_LIGHT_SHADOWS" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0, 3);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 5, "five shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl", "_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Meta: object position straight to clip space, emission branch, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(0, 1, 0, 0));
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                // Perspective depth branch with a scene eye depth of 4 against the ortho quad's clip w 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), black = new Color(0, 0, 0, 1);
                Color clear = new Color(0, 0, 0, 0);
                Color Noise(float r, float g) => new Color(r, g, 0, 1);

                // Neutral start: no fresnel (alpha 0), a black reflection, no green noise, and a fade distance that
                // saturates the depth fade ((4 - 1) / 2).
                Material material = rig.Own(new Material(shader));
                material.SetColor("_R_Noise_Color_A", red);
                material.SetColor("_R_Noise_Color_B", green);
                material.SetColor("_G_Noise_Colour", blue);
                material.SetColor("_Depth_Fade_Color", blue);
                material.SetFloat("_R_Noise_Contrast", 1);
                material.SetFloat("_G_Noise_Opacity", 0);
                material.SetFloat("_Depth_Fade_Distance", 2);
                material.SetTexture("_Matcap_Fresnel_Tex", rig.Texels(clear));
                material.SetTexture("_Matcap_Reflection_Tex", rig.Texels(black));
                material.SetTexture("_Packed_Noise_Tex", rig.Texels(Noise(0.25f, 0.5f)));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass: pass);
                    cases++;
                }

                // Red noise: saturate((r - 0.1) * contrast + 0.1) from colour A to B.
                Expect(new Color(0.75f, 0.25f, 0, 1), "red noise 0.25 at contrast 1");
                material.SetFloat("_R_Noise_Contrast", 2);
                Expect(new Color(0.6f, 0.4f, 0, 1), "the contrast pivots about 0.1");
                material.SetFloat("_R_Noise_Contrast", 1);
                // Green noise times its opacity towards the green colour.
                material.SetFloat("_G_Noise_Opacity", 0.5f);
                Expect(new Color(0.5625f, 0.1875f, 0.25f, 1), "green noise 0.5 at opacity 0.5");
                material.SetFloat("_G_Noise_Opacity", 0);

                // Scrolling and tiling on a banded strip (the inline sampler is bilinear): u 0.516 reads band 2.
                material.SetTexture("_Packed_Noise_Tex", rig.Bands(Noise(0, 0), Noise(0.25f, 0), Noise(0.5f, 0), Noise(0.75f, 1)));
                Color Red(float r) => Color.Lerp(red, green, r);
                Expect(Red(0.5f), "the red noise under the pixel");
                material.SetFloat("_R_Noise_UV_Speed_Direction_X", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Red(0.75f), "the red noise scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_R_Noise_UV_Speed_Direction_X", 0);
                material.SetVector("_Tiling_R_Noise_UV", new Vector4(2, 1, 0, 0));
                Expect(Red(0), "the red tiling");
                material.SetVector("_Tiling_R_Noise_UV", new Vector4(1, 1, 0.25f, 0));
                Expect(Red(0.75f), "the red offset");
                material.SetVector("_Tiling_R_Noise_UV", new Vector4(1, 1, 0, 0));
                // The green layer reads 0.3 further along u (band 3, green 1).
                material.SetFloat("_G_Noise_Opacity", 1);
                Expect(blue, "the green layer's UV is offset by (0.3, 0.2)");
                material.SetVector("_Tiling_G_Noise_UV", new Vector4(1, 1, -0.3f, 0));
                Expect(Red(0.5f), "the green offset");
                material.SetVector("_Tiling_G_Noise_UV", new Vector4(1, 1, 0, 0));
                material.SetFloat("_G_Noise_UV_Speed_Direction_X", -0.3f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Red(0.5f), "the green noise scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_G_Noise_UV_Speed_Direction_X", 0);
                material.SetFloat("_G_Noise_Opacity", 0);
                material.SetTexture("_Packed_Noise_Tex", rig.Texels(Noise(0.25f, 0.5f)));

                // Matcaps: the fresnel over the colour by its alpha, plus the reflection.
                material.SetTexture("_Matcap_Fresnel_Tex", rig.Texels(new Color(0, 0, 1, 0.5f)));
                Expect(new Color(0.375f, 0.125f, 0.5f, 1), "the matcap fresnel by its alpha");
                material.SetTexture("_Matcap_Fresnel_Tex", rig.Texels(clear));
                material.SetTexture("_Matcap_Reflection_Tex", rig.Texels(new Color(0.25f, 0, 0.5f, 1)));
                Expect(new Color(1, 0.25f, 0.5f, 1), "the matcap reflection adds");
                material.SetTexture("_Matcap_Reflection_Tex", rig.Texels(black));
                // The matcap UV, r = cross(view, normal) in view space, (r.y, -r.x) * 0.45 + 0.5: a normal tilted along
                // +x moves u to about 0.77, along +y moves v there.
                const int size = 5;
                material.SetTexture("_Matcap_Fresnel_Tex", rig.Grid(size));
                Vector2 MatcapUV(Vector3 normal)
                {
                    Vector3 view = (rig.Camera.transform.position - new Vector3(1 / 32f, 1 / 32f, 0)).normalized;
                    Matrix4x4 v = rig.Camera.worldToCameraMatrix;
                    Vector3 r = Vector3.Cross(v.MultiplyVector(view).normalized, v.MultiplyVector(normal.normalized));
                    return new Vector2(r.y * 0.45f + 0.5f, -r.x * 0.45f + 0.5f);
                }
                Expect(rig.GridColor(size, MatcapUV(Vector3.back)), "a facing normal reads the matcap centre");
                var tiltX = new Vector3(0.6f, 0, -0.8f);
                rig.SetNormals(tiltX);
                Expect(rig.GridColor(size, MatcapUV(tiltX)), "a normal tilted along x");
                var tiltY = new Vector3(0, 0.6f, -0.8f);
                rig.SetNormals(tiltY);
                Expect(rig.GridColor(size, MatcapUV(tiltY)), "a normal tilted along y");
                rig.SetNormals(Vector3.back);
                material.SetTexture("_Matcap_Fresnel_Tex", rig.Texels(clear));

                // Depth fade: distance 6 gives (4 - 1) / 6 = 0.5; lerp(lerp(fade colour, colour, 0.5), colour / 2, 0.5).
                material.SetFloat("_Depth_Fade_Distance", 6);
                Expect(new Color(0.375f, 0.125f, 0.25f, 1), "the depth fade");
                material.SetFloat("_Depth_Fade_Distance", 30);
                Expect(new Color(0.345f, 0.115f, 0.09f, 1), "a shallow fade (0.1) leans to the halved colour");
                material.SetFloat("_Depth_Fade_Distance", 2);

                // DepthNormals: the vertex normal; Forward: alpha 1.
                rig.SetNormals(tiltX);
                Expect(new Color(0.6f, 0, -0.8f, 0), "the vertex normal", DepthNormals);
                rig.SetNormals(Vector3.back);
                float alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 1) < 1e-3f, "the forward alpha is 1: " + alpha);
                cases++;

                // _GlobalMipBias applies once per sample: over UV0 a 256 texture reads level 3 unbiased, level 4 at
                // bias 1. The matcaps sample through the same macro.
                material.SetTexture("_Packed_Noise_Tex", rig.MipLevels(256, level => level == 4 ? Noise(1, 0) : Noise(0, 0)));
                Expect(red, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Char_Boss_02_Goo GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading, the smoothness, the matcap mip bias and the orthographic depth branch remain compile-checked only.");
            }
        }
    }
}
