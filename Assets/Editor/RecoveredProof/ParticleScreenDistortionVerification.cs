using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_ScreenDistortion, reconstructed from the shipped Metal programs: the opaque scene
    // colour at the screen UV offset by the main texture's red and green (times the intensity and amount), the alpha
    // from its blue times vertex alpha, the _DEPTHFADE scene depth fade, opaque alpha, and the DepthOnly depth.
    public static class ParticleScreenDistortionVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_ScreenDistortion.shader";
        private const string ShaderName = "HardLight/Particle/HL_Particle_ScreenDistortion";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "a5da1a8df03ee0a47abd64594b7661b4";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_DEPTHFADE", "_SURFACE_TYPE_TRANSPARENT", "_WRITE_RENDERING_LAYERS", "FOG_LINEAR" },
            new[] { "INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_CASTING_PUNCTUAL_LIGHT_SHADOW", "_ALPHATEST_ON" },
            new[] { "DOTS_INSTANCING_ON", "_DEPTHFADE", "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "FOG_EXP2", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON" },
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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveVectors("_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                rig.SaveTextures("_CameraOpaqueTexture");
                // Perspective depth branch: with _ZBufferParams.zw = (0, 0.25) the scene eye depth is 4 for any
                // depth sample; the ortho quad's clip w is 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color Main(float r, float b = 1) => new Color(r, 0.5f, b, 1);
                // The opaque scene on a 4-band strip: pixel 16 sits at screen u 0.5156 (band 2).
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Bands(red, green, blue, white));

                Material material = rig.Own(new Material(shader));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 0);
                material.SetFloat("_Distortion_Intensity", 1);
                material.SetFloat("_Distortion_Amount", 0.5f);
                material.SetTexture("_MainTex", rig.Texels(Main(0.5f)));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }

                // Mid-grey red and green do not distort; the alpha is blue times vertex alpha.
                Expect(blue, "the scene colour behind the fragment");
                rig.SetColors(new Color(1, 1, 1, 0.5f));
                Expect(new Color(0, 0, 1, 0.5f), "vertex alpha");
                rig.SetColors(Color.white);
                material.SetTexture("_MainTex", rig.Texels(Main(0.5f, 0.25f)));
                Expect(new Color(0, 0, 1, 0.25f), "the main texture's blue");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Expect(blue, "opaque programs write alpha 1");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                // The distortion: (red * 2 - 1) * intensity * amount moves the screen u.
                material.SetTexture("_MainTex", rig.Texels(Main(0.75f)));
                Expect(white, "red 0.75 moves u by 0.25 (band 3)");
                material.SetTexture("_MainTex", rig.Texels(Main(0.25f)));
                Expect(green, "red 0.25 moves u by -0.25 (band 1)");
                material.SetFloat("_Distortion_Intensity", -1);
                Expect(white, "a negative intensity reverses it");
                material.SetFloat("_Distortion_Intensity", 1);
                material.SetFloat("_Distortion_Amount", 1);
                Expect(red, "the amount (u - 0.5, band 0)");
                material.SetFloat("_Distortion_Amount", 0.5f);
                material.SetTexture("_MainTex", rig.Texels(Main(0.5f)));

                // _DEPTHFADE: saturate((scene depth 4 - clip w 1) / distance * strength) scales the alpha.
                Expect(blue, "no depth fade by default");
                material.EnableKeyword("_DEPTHFADE");
                Expect(blue, "distance 1 saturates the fade");
                material.SetFloat("_Depth_Fade_Distance", 6);
                Expect(new Color(0, 0, 1, 0.5f), "the fade distance");
                material.SetFloat("_Depth_Fade_Strength", 0.5f);
                Expect(new Color(0, 0, 1, 0.25f), "the fade strength");
                material.SetFloat("_Depth_Fade_Strength", 1);
                material.SetFloat("_Depth_Fade_Distance", 1);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.5f));
                material.SetFloat("_Depth_Fade_Distance", 2);
                Expect(new Color(0, 0, 1, 0.5f), "a nearer scene (depth 2)");
                material.SetFloat("_Depth_Fade_Distance", 1);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                material.DisableKeyword("_DEPTHFADE");

                // _GlobalMipBias applies once to each sample: the main texture spans UV 0..1 and the scene texture the
                // screen, so a 256 texture is read at level 3 by both; bias 1 reads level 4.
                material.SetTexture("_MainTex", rig.MipLevels(256, level => level == 4 ? Main(0.5f, 0.5f) : Main(0.5f)));
                Expect(blue, "main: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(new Color(0, 0, 1, 0.5f), "main: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_MainTex", rig.Texels(Main(0.5f)));
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : blue));
                Expect(red, "scene: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "scene: bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Particle_ScreenDistortion GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The orthographic depth branch (shared with HL_Particle_Trail) and the " +
                          "ShadowCaster pass remain compile-checked only.");
            }
        }
    }
}
