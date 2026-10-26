using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone1_Water, reconstructed from the shipped Metal programs. The surface colour is
    // checked through the Meta pass: the depth ramp against the scene depth, the colour variation noise, the colour
    // dodge by the scene colour and its refraction offset (only where the offset scene lies behind the water), the
    // depth-edge and vertex-colour foam, the emission intensity and the graph's fog blend on the albedo. Forward
    // checks the vertex-green alpha and DepthNormals the normal map at its strength.
    public static class EnvZone1WaterVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone1_Water.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone1_Water";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "1d9ed5e4b1ad01d4b85bc19c173c758a";
        private const int Forward = 0, DepthNormals = 2, Meta = 3;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "_REFLECTION_PROBE_BOX_PROJECTION", "_ADDITIONAL_LIGHT_SHADOWS", "FOG_EXP" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0, 2);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 4, "four shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl", "_ZBufferParams", "_ScaledScreenParams",
                    "_ProjectionParams", "unity_FogColor");
                rig.SaveTextures("_CameraOpaqueTexture");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Meta: object position straight to clip space, emission branch, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Vector4 emission = new Vector4(0, 1, 0, 0), albedo = new Vector4(1, 0, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                // Perspective depth branch: a scene eye depth of 4 against the ortho quad's clip w 1 and its water
                // depth (1 - view z) of 3.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;
                Color black = new Color(0, 0, 0, 1), white = Color.white;
                Color Noise(float r, float g) => new Color(r, g, 0, 1);
                Color Gray(float v) => new Color(v, v, v, 1);

                // Neutral start: black scene colour and noise, a flat normal, black vertex colour (no vertex foam,
                // full alpha), and a 4-texel ramp read at depth (4 - 1) / 10 = 0.3, texel 1.
                Color r0 = new Color(0.1f, 0, 0, 1), r1 = Gray(0.2f), r2 = new Color(0, 0.3f, 0, 1), r3 = new Color(0, 0, 0.4f, 1);
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Water_Colour_Gradient_Ramp_Texture", rig.Texels(r0, r1, r2, r3));
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(black));
                Color flat = new Color(0.5f, 0.5f, 1, 1);
                material.SetTexture("_Normal_Texture", rig.Texels(flat));
                material.SetFloat("_Emission_Intensity", 1);
                material.SetColor("_Foam_Color", white);
                material.SetColor("_Color_Varation", new Color(1, 0, 1, 0));
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(black));
                rig.SetColors(black);
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass: pass);
                    cases++;
                }

                // The depth ramp.
                Expect(r1, "the ramp at depth 0.3");
                material.SetFloat("_Water_Depth_Distance", 5);
                Expect(r2, "the depth distance (0.6)");
                material.SetFloat("_Water_Depth_Distance", 3.5f);
                Expect(r3, "a deeper reading (0.857)");
                material.SetFloat("_Water_Depth_Distance", 10);

                // Colour variation: the noise green times its amount, times the variation colour.
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(Noise(0, 0.5f)));
                Expect(new Color(0.7f, 0.2f, 0.7f, 1), "the colour variation adds");
                material.SetFloat("_Color_Varation_Amount", 2);
                Expect(new Color(1, 0.2f, 1, 1), "the variation amount, saturated");
                material.SetFloat("_Color_Varation_Amount", 1);
                // Its UV scrolls with game time (a banded strip: u 0.516 reads band 2, 0.766 band 3).
                material.SetTexture("_Packed_Noise_Texture", rig.Bands(Noise(0, 0), Noise(0, 0), Noise(0, 1), Noise(0, 0)));
                Expect(new Color(1, 0.2f, 1, 1), "the variation noise under the pixel");
                material.SetFloat("_Colour_Variation_Speed_Direction_X", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(r1, "the variation scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Colour_Variation_Speed_Direction_X", 0);
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(black));

                // The colour dodge: lerp(c, c / (1 - scene * blend), blend).
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(Gray(0.5f)));
                Expect(Gray(0.4f), "a mid-grey scene doubles the colour");
                material.SetFloat("_Refraction_Colour_Blend_Amount", 0.5f);
                Expect(Gray(0.2f * (1 + 1 / 0.75f) / 2), "the blend amount scales the scene and the lerp");
                material.SetFloat("_Refraction_Colour_Blend_Amount", 1);
                // The refraction offset: screen u 0.516 reads the grey half; the normal's x (0.5) times the intensity
                // (0.05) moves the sample to the black half.
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(black, Gray(0.5f)));
                Expect(Gray(0.4f), "a flat normal samples the scene under the pixel");
                material.SetTexture("_Normal_Texture", rig.Texels(new Color(0.75f, 0.5f, 1, 1)));
                Expect(r1, "the normal refracts the scene sample");
                material.SetFloat("_Refraction_Intensity", 0.01f);
                Expect(Gray(0.4f), "the refraction intensity");
                material.SetFloat("_Refraction_Intensity", 0.05f);
                // A scene depth of 2 lies in front of the water depth 3: no offset (and the ramp reads depth 0.1).
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.5f));
                Expect(new Color(0.2f, 0, 0, 1), "no refraction where the scene lies in front of the water");
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                material.SetTexture("_Normal_Texture", rig.Texels(flat));
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(black));

                // Depth-edge foam: at fade distance 6 the foam depth is 0.5; the edge where step >= depth - noise red,
                // foam (1 - depth) towards the foam colour by foam times its alpha.
                material.SetFloat("_Foam_Depth_Fade", 6);
                material.SetFloat("_Foam_Step_Amount", 0.1f);
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(Noise(0.45f, 0)));
                Expect(Gray(0.35f), "the foam edge");
                material.SetFloat("_Foam_Step_Amount", 0);
                Expect(r1, "the foam step");
                material.SetFloat("_Foam_Step_Amount", 0.1f);
                // The foam UV's u is tiled by the Y tiling (default 4): band 0 of the strip, then band 2 at 1.
                material.SetTexture("_Packed_Noise_Texture", rig.Bands(Noise(0, 0), Noise(0, 0), Noise(0.45f, 0), Noise(0, 0)));
                Expect(r1, "the foam's Y tiling tiles u");
                material.SetFloat("_Foam_UV_Tiling_Y", 1);
                Expect(Gray(0.35f), "the foam noise under the pixel");
                material.SetFloat("_Foam_UV_Tiling_Y", 4);
                material.SetFloat("_Foam_Depth_Fade", 40);
                material.SetFloat("_Foam_Step_Amount", -0.3f);
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(black));

                // Vertex-colour foam: noise red plus vertex red at or above the amount.
                rig.SetColors(new Color(1, 0, 0, 1));
                Expect(white, "vertex red reaches the foam amount");
                material.SetColor("_Foam_Color", new Color(0, 0, 1, 0.5f));
                Expect(new Color(0.1f, 0.1f, 0.6f, 1), "the foam colour by its alpha");
                material.SetColor("_Foam_Color", white);
                material.SetFloat("_VC_Foam_Amount", 1.5f);
                Expect(r1, "the vertex foam amount");
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(Noise(0.5f, 0)));
                Expect(white, "noise red adds to vertex red");
                material.SetFloat("_VC_Foam_Amount", 1);
                material.SetTexture("_Packed_Noise_Texture", rig.Texels(black));
                rig.SetColors(black);

                // Emission intensity, and the albedo's fog blend (no fog keyword: density 0, so the weight is the
                // saturated intensity + 1).
                material.SetFloat("_Emission_Intensity", 0.5f);
                Expect(Gray(0.1f), "the emission intensity");
                material.SetFloat("_Emission_Intensity", 1);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                Expect(r1, "the albedo is the water colour");
                Shader.SetGlobalVector("unity_FogColor", new Vector4(0, 0, 1, 1));
                material.SetFloat("_Scene_Fog_Intensity", -0.5f);
                Expect(new Color(0.1f, 0.1f, 0.6f, 1), "the scene fog intensity blends towards the fog colour");
                material.SetFloat("_Scene_Fog_Intensity", 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);

                // Forward alpha: saturate(2 * overall alpha * (1 - vertex green)), written through the shipped alpha
                // factors (One OneMinusSrcAlpha) over the cleared target.
                void Alpha(float a, string label)
                {
                    float actual = rig.Draw(material, Forward).a;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(actual - a) < 1e-3f, label + ": expected alpha " + a + ", read " + actual);
                    cases++;
                }
                Alpha(1, "black vertex colour is opaque");
                rig.SetColors(new Color(0, 0.75f, 0, 1));
                Alpha(0.5f, "vertex green fades the alpha, doubled");
                rig.SetColors(black);
                material.SetFloat("_Overall_Alpha", 0.2f);
                Alpha(0.4f, "the overall alpha, doubled");
                material.SetFloat("_Overall_Alpha", 1);

                // DepthNormals: the normal map through the quad's tangent frame (tangent +x, bitangent -y, normal -z),
                // at full strength and then at 0.5 (xy halved, z lerped from 1).
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                material.SetTexture("_Normal_Texture", rig.Texels(new Color(0.8f, 0.5f, 0.9f, 1)));
                Expect(new Color(0.6f, 0, -0.8f, 0), "the normal map", DepthNormals);
                material.SetFloat("_Normal_Intensity", 0.5f);
                float length = Mathf.Sqrt(0.3f * 0.3f + 0.9f * 0.9f);
                Expect(new Color(0.3f / length, 0, -0.9f / length, 0), "the normal strength", DepthNormals);
                material.SetFloat("_Normal_Intensity", 1);
                material.SetTexture("_Normal_Texture", rig.Texels(flat));

                // _GlobalMipBias applies once: over UV0 a 256 texture reads level 3 unbiased and level 4 at bias 1.
                material.SetTexture("_Packed_Noise_Texture", rig.MipLevels(256, level => level == 4 ? Noise(0, 1) : black));
                Expect(r1, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(new Color(1, 0.2f, 1, 1), "bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Env_Zone1_Water GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading, the smoothness, the fog-keyword density and the orthographic depth branch remain compile-checked only.");
            }
        }
    }
}
