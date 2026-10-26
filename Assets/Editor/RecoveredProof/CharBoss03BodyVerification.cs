using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Boss_03_Body, reconstructed from the shipped Metal programs. Through the Meta pass:
    // the depth fade towards its colour, the fresnel colour at grazing angles, the flow-mapped diffuse and emissive,
    // and the displacement along the normals (masked by the ID mask's red, also in DepthOnly). DepthNormals checks the
    // normal map blended with the noise normal map (masked by the ID mask's green); Forward's alpha is 1.
    public static class CharBoss03BodyVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Boss_03_Body.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Boss_03_Body";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "48b0a323ed002f246bd17468ad31f329";
        private const int Forward = 0, DepthOnly = 2, DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "_REFLECTION_PROBE_BOX_PROJECTION", "_ADDITIONAL_LIGHT_SHADOWS", "FOG_EXP" },
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
                // Meta: object position straight to clip space, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                // Perspective depth branch with a scene eye depth of 4 against the ortho quad's clip w 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), black = new Color(0, 0, 0, 1);
                Color diffuse = new Color(0.25f, 0.5f, 1, 1);
                Color flat = new Color(0.5f, 0.5f, 1, 1), tangent = new Color(1, 0.5f, 1, 1);

                // Neutral start: no displacement, no ID mask, a fade distance that saturates the depth fade
                // ((4 - 1) / 2), and no fresnel.
                Material material = rig.Own(new Material(shader));
                material.SetFloat("_Displacement_Amount", 0);
                material.SetFloat("_Depth_Fade_Distance", 2);
                material.SetColor("_Depth_Fade_Colour", blue);
                material.SetColor("_Fresnel_Colour", red);
                material.SetFloat("_Fresnel_Intensity", 0);
                material.SetTexture("_IDMask_Texture", rig.Texels(new Color(0, 0, 0, 0)));
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Norm_Noise_Texture", rig.Texels(flat));
                material.SetTexture("_Diff_Texture", rig.Texels(diffuse));
                void Expect(Color expected, string label, int pass = Meta, int x = 16)
                {
                    rig.Expect(material, expected, label, pass: pass, x: x);
                    cases++;
                }

                Expect(diffuse, "the diffuse is the albedo");
                // Depth fade: (4 - 1) / 6 = 0.5 of the way from the fade colour.
                material.SetFloat("_Depth_Fade_Distance", 6);
                Expect(Color.Lerp(blue, diffuse, 0.5f), "the depth fade");
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.5f));
                Expect(Color.Lerp(blue, diffuse, 1 / 6f), "a nearer scene fades more");
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                material.SetFloat("_Depth_Fade_Distance", 2);

                // Fresnel: 1 - (1 - N.V) ^ amount * intensity * opacity of the way from the fresnel colour, with the
                // perspective view from the pixel's position to the camera.
                Vector3 view = (rig.Camera.transform.position - new Vector3(1 / 32f, 1 / 32f, 0)).normalized;
                Color Fresnel(Vector3 normal, float amount, float intensity, float opacity)
                {
                    float facing = Mathf.Clamp01(Vector3.Dot(normal, view));
                    return Color.Lerp(red, diffuse, Mathf.Clamp01(1 - Mathf.Pow(1 - facing, amount) * intensity * opacity));
                }
                material.SetFloat("_Fresnel_Intensity", 1);
                Expect(Fresnel(Vector3.back, 1, 1, 1), "facing the camera: the colour");
                material.SetTexture("_Norm_Texture", rig.Texels(tangent));
                Expect(Fresnel(Vector3.right, 1, 1, 1), "edge-on (mapped normal): the fresnel colour");
                Vector3 sixty = new Vector3(Mathf.Sqrt(0.75f), 0, -0.5f);
                material.SetTexture("_Norm_Texture", rig.Texels(new Color(0.5f + 0.5f * Mathf.Sqrt(0.75f), 0.5f, 1, 1)));
                Expect(Fresnel(sixty, 1, 1, 1), "the fresnel at N.V 0.5");
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(Fresnel(sixty, 2, 1, 1), "the fresnel amount is the power");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Intensity", 1.5f);
                Expect(Fresnel(sixty, 1, 1.5f, 1), "the fresnel intensity");
                material.SetFloat("_Fresnel_Opacity", 0.5f);
                Expect(Fresnel(sixty, 1, 1.5f, 0.5f), "the fresnel opacity");
                material.SetFloat("_Fresnel_Opacity", 1);
                material.SetFloat("_Fresnel_Intensity", 0);
                material.SetTexture("_Norm_Texture", rig.Texels(flat));

                // Emission: the emissive texture times its intensity.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetTexture("_Emissive_Texture", rig.Texels(new Color(0.25f, 0.125f, 0, 1)));
                Expect(new Color(0.25f, 0.125f, 0, 1), "the emissive texture");
                material.SetFloat("_Emissive_Intensity", 2);
                Expect(new Color(0.5f, 0.25f, 0, 1), "the emissive intensity");
                material.SetFloat("_Emissive_Intensity", 1);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // The flow map: flow (0.5 - xy) * 2 * strength; at time 0 phase A is 0 and phase B 0.5, crossfaded by
                // |A * 2 - 1| (all phase B). Red 0.25 at strength 1 flows u by 0.5, so phase B moves u 0.516 to 0.766
                // (band 3); at time 1 and speed 0.25 the phases are 0.25 (u 0.641, band 2) and 0.75 (u 0.891, band 3),
                // crossfaded evenly.
                material.SetTexture("_Diff_Texture", rig.Bands(black, black, red, green));
                material.SetTexture("_Emissive_Texture", rig.Bands(black, black, red, green));
                material.SetTexture("_FlowMap_Texture", rig.Texels(new Color(0.25f, 0.5f, 0, 1)));
                Expect(red, "no flow: band 2");
                material.SetFloat("_FlowMap_Strength", 1);
                Expect(green, "flowed by phase B");
                material.SetFloat("_FlowMap_Speed", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Color mixed = Color.Lerp(red, green, 0.5f);
                Expect(mixed, "the phases crossfade");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(mixed, "the emissive flows too");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_FlowMap_Speed", 0);
                material.SetFloat("_FlowMap_Strength", 0);
                material.SetTexture("_Diff_Texture", rig.Texels(diffuse));

                // The displacement: normals along +x and a white displacement texture move the quad right by the
                // amount; 0.5 (8 px) uncovers pixel 4, pixel 12 stays covered. The ID mask's red holds it back.
                void Covered(int pass, int x, bool expected, string label)
                {
                    Color c = rig.Draw(material, pass, x: x);
                    bool covered = pass == Meta ? c.a != 0 : c.r != 0;
                    RecoveredShaderProof.Require(ShaderName, covered == expected, label + ": " + c);
                    cases++;
                }
                rig.SetNormals(Vector3.right);
                material.SetTexture("_Displacement_Texture", rig.Texels(Color.white));
                Covered(Meta, 1, true, "no displacement keeps the edge covered");
                material.SetFloat("_Displacement_Amount", 0.5f);
                Covered(Meta, 4, false, "the displacement moves the quad");
                Covered(Meta, 12, true, "pixel 12 stays covered");
                Covered(DepthOnly, 4, false, "DepthOnly displaces too");
                Covered(DepthOnly, 12, true, "DepthOnly writes depth");
                material.SetTexture("_Displacement_Texture", rig.Texels(new Color(0.5f, 0.5f, 0.5f, 1)));
                Covered(Meta, 4, true, "the displacement texture's red scales it");
                material.SetTexture("_Displacement_Texture", rig.Texels(Color.white));
                material.SetTexture("_IDMask_Texture", rig.Texels(new Color(1, 0, 0, 0)));
                Covered(Meta, 4, true, "the ID mask's red holds the vertices");
                Covered(DepthOnly, 4, true, "in DepthOnly too");
                material.SetTexture("_IDMask_Texture", rig.Texels(new Color(0, 0, 0, 0)));
                material.SetFloat("_Displacement_Amount", 0);
                rig.SetNormals(Vector3.back);

                // DepthNormals: the normal map at its intensity blended with the noise normal map at its intensity.
                Expect(new Color(0, 0, -1, 0), "flat normals", DepthNormals);
                material.SetTexture("_Norm_Texture", rig.Texels(tangent));
                Expect(new Color(1, 0, 0, 0), "the normal map", DepthNormals);
                material.SetFloat("_Normal_Intensity", 0);
                Expect(new Color(0, 0, -1, 0), "the normal intensity", DepthNormals);
                material.SetFloat("_Normal_Intensity", 1);
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Norm_Noise_Texture", rig.Texels(tangent));
                Expect(new Color(1, 0, 0, 0), "the noise normal map", DepthNormals);
                material.SetTexture("_IDMask_Texture", rig.Texels(new Color(0, 1, 0, 0)));
                Expect(new Color(0, 0, -1, 0), "the ID mask's green masks the noise", DepthNormals);
                material.SetTexture("_IDMask_Texture", rig.Texels(new Color(0, 0, 0, 0)));
                // Both at (0.6, 0, 0.8): xy add and z multiplies, (1.2, 0, 0.64) normalised.
                Color tilted = new Color(0.8f, 0.5f, 1, 1);
                material.SetTexture("_Norm_Texture", rig.Texels(tilted));
                material.SetTexture("_Norm_Noise_Texture", rig.Texels(tilted));
                Vector3 blended = new Vector3(1.2f, 0, 0.64f).normalized;
                Expect(new Color(blended.x, 0, -blended.z, 0), "the normal blend", DepthNormals);
                // The noise at half intensity: (0.3, 0, lerp(1, 0.8, 0.5)).
                material.SetFloat("_Normal_Noise_Intensity", 0.5f);
                Vector3 weaker = new Vector3(0.9f, 0, 0.8f * 0.9f).normalized;
                Expect(new Color(weaker.x, 0, -weaker.z, 0), "the noise intensity", DepthNormals);
                material.SetFloat("_Normal_Noise_Intensity", 1);
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Norm_Noise_Texture", rig.Texels(flat));

                // Forward alpha is 1, written through the shipped alpha factors (One OneMinusSrcAlpha) over a cleared
                // target.
                float alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 1) < 1e-3f, "Forward is opaque: " + alpha);
                cases++;

                // _GlobalMipBias applies once: across the quad a 256 texture is read at level 3.
                material.SetTexture("_Diff_Texture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : blue));
                Expect(red, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Char_Boss_03_Body GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The forward lighting remains compile-checked only.");
            }
        }
    }
}
