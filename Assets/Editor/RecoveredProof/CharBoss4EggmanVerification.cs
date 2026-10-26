using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Boss_4_Eggman, reconstructed from the shipped Metal programs. The subsurface
    // scattering, the rotated detail overlay and the diffuse are checked as the Meta albedo; the fresnel emission, its
    // distance fade and the SSS emission as the Meta emission; the normal and fabric normal blend through
    // DepthNormals; and the ID mask's blue as the Forward alpha.
    public static class CharBoss4EggmanVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Boss_4_Eggman.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Boss_4_Eggman";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "5958bf96c5aa33e43a8a750f1b76bb40";
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
                rig.SaveFloats("unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl", "_MainLightPosition");
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                // The main light faces the quad (normal -Z), so the half-Lambert term is 1.
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, -1, 0));
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), black = new Color(0, 0, 0, 1), white = Color.white;
                Color Grey(float v) => new Color(v, v, v, 1);
                Color flat = new Color(0.5f, 0.5f, 1, 1), tangent = new Color(1, 0.5f, 1, 1);

                // Most textures go through the graph's bilinear inline samplers, so they are uniform here.
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Diff_Texture", rig.Texels(green));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Fabric_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Detail_Texture", rig.Texels(white));
                material.SetTexture("_SSS_Gradient_Ramp_Texture", rig.Texels(red));
                material.SetTexture("_Emissive_Texture", rig.Texels(white));
                material.SetTexture("_Fresnel_Texture", rig.Texels(red));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass);
                    cases++;
                }

                Expect(green, "the diffuse is the albedo");

                // Subsurface scattering on a white diffuse with a red ramp: lerp(diffuse, diffuse * ramp, u) with u the
                // half-Lambert term times the offset, faded in by the ID red times the opacity.
                material.SetTexture("_Diff_Texture", rig.Texels(white));
                material.SetFloat("_SSS_Offset", 0.5f);
                Expect(white, "no SSS without the ID mask's red");
                material.SetTexture("_ID_Mask", rig.Texels(new Color(1, 0, 1, 0)));
                Expect(new Color(1, 0.5f, 0.5f, 1), "the ramp at u 0.5");
                material.SetFloat("_SSS_Opacity", 0.5f);
                Expect(new Color(1, 0.75f, 0.75f, 1), "the SSS opacity");
                material.SetFloat("_SSS_Opacity", 1);
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(1, 0, 0, 0));
                Expect(new Color(1, 0.75f, 0.75f, 1), "a side light is half-Lambert 0.5 (u 0.25)");
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, -1, 0));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));

                // Detail overlay at 0.3 of the strength, by the ID alpha: a dark base (0.25) doubles towards white
                // detail (0.5); a light base (0.75) screens towards black detail (0.5).
                material.SetTexture("_Diff_Texture", rig.Texels(Grey(0.25f)));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 1)));
                const float detailOpacity = 0.3003003f;
                Expect(Grey(Mathf.Lerp(0.25f, 0.5f, detailOpacity)), "the detail overlays a dark base");
                material.SetFloat("_Detail_Strength", 0.5f);
                Expect(Grey(Mathf.Lerp(0.25f, 0.5f, 0.5f * detailOpacity)), "the detail strength");
                material.SetFloat("_Detail_Strength", 1);
                material.SetTexture("_Diff_Texture", rig.Texels(Grey(0.75f)));
                material.SetTexture("_Detail_Texture", rig.Texels(black));
                Expect(Grey(Mathf.Lerp(0.75f, 0.5f, detailOpacity)), "the detail overlays a light base");
                // The 4-texel detail strip (white only in texel 2) under the pixel (u 0.516), rotated half a turn
                // (u 0.484, texel 1) and scaled by 0.5 (u 0.258, texel 1).
                material.SetTexture("_Diff_Texture", rig.Texels(Grey(0.25f)));
                material.SetTexture("_Detail_Texture", rig.Texels(black, black, white, black));
                Color lifted = Grey(Mathf.Lerp(0.25f, 0.5f, detailOpacity)), darkened = Grey(Mathf.Lerp(0.25f, 0f, detailOpacity));
                Expect(lifted, "the detail texel under the pixel");
                material.SetFloat("_Detail_Rotation", Mathf.PI);
                Expect(darkened, "the detail rotation turns the UV about the centre");
                material.SetFloat("_Detail_Rotation", 0);
                material.SetFloat("_Detail_Size", 0.5f);
                Expect(darkened, "the detail size scales the UV");
                material.SetFloat("_Detail_Size", 1);
                material.SetTexture("_Detail_Texture", rig.Texels(white));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetTexture("_Diff_Texture", rig.Texels(green));

                // Emission: facing the camera the fresnel term is 1, which shows the tinted emissive texture; facing
                // away (a normal map along the tangent) it shows the fresnel texture.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                material.SetColor("_Emissive_Colour", new Color(0, 1, 0, 1));
                Expect(green, "facing the camera: the tinted emissive texture");
                material.SetFloat("_Global_Emission_Intensity", 0.5f);
                Expect(new Color(0, 0.5f, 0, 1), "the global emission intensity");
                material.SetFloat("_Global_Emission_Intensity", 1);
                material.SetTexture("_Norm_Texture", rig.Texels(tangent));
                Expect(red, "edge-on (mapped normal): the fresnel texture");
                // Mapped normal at 60 degrees (N.V 0.5): 1 - 0.5 ^ amount * intensity.
                float half = 0.5f;
                material.SetTexture("_Norm_Texture", rig.Texels(new Color(0.5f + 0.5f * Mathf.Sqrt(0.75f), 0.5f, 1, 1)));
                Color Fresnel(float f) => Color.Lerp(red, green, Mathf.Clamp01(f));
                Expect(Fresnel(1 - half), "the fresnel at N.V 0.5");
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(Fresnel(1 - half * half), "the fresnel amount is the power");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Intensity", 1.5f);
                Expect(Fresnel(1 - 1.5f * half), "the fresnel intensity");
                material.SetFloat("_Fresnel_Intensity", 1);
                // Distance fade: saturate((distance - D) / (5 D)) scales the emission when enabled.
                float distance = (new Vector3(1 / 32f, 1 / 32f, 0) - rig.Camera.transform.position).magnitude;
                material.SetFloat("_Fresnel_Fade_Distance", 0.4f);
                Expect(Fresnel(1 - half), "the distance fade is off by default");
                material.SetFloat("_Fresnel_Distance_Fade", 1);
                Color faded = Fresnel(1 - half) * Mathf.Clamp01((distance - 0.4f) / 2f);
                faded.a = 1;
                Expect(faded, "the distance fade");
                material.SetFloat("_Fresnel_Distance_Fade", 0);
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                // SSS emission: towards the scattered diffuse (white diffuse, red ramp at u 0.5: (1, 0.5, 0.5)).
                material.SetTexture("_Diff_Texture", rig.Texels(white));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(1, 0, 1, 0)));
                Expect(new Color(1, 0.5f, 0.5f, 1), "the SSS emissive intensity shows the scattered diffuse");
                material.SetFloat("_SSS_Emissive_Intensity", 0.5f);
                Expect(new Color(0.5f, 0.75f, 0.25f, 1), "half SSS emission");
                material.SetFloat("_SSS_Emissive_Intensity", 1);
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetTexture("_Diff_Texture", rig.Texels(green));
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // Normals, read by DepthNormals (the quad faces -Z with tangent +X): Shader Graph's Normal Blend of the
                // normal map and the fabric normal map (faded in by the ID green).
                Expect(new Color(0, 0, -1, 0), "flat normals", DepthNormals);
                material.SetTexture("_Norm_Texture", rig.Texels(tangent));
                Expect(new Color(1, 0, 0, 0), "the normal map", DepthNormals);
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Fabric_Norm_Texture", rig.Texels(tangent));
                Expect(new Color(0, 0, -1, 0), "the fabric normal needs the ID green", DepthNormals);
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 1, 1, 0)));
                Expect(new Color(1, 0, 0, 0), "the fabric normal", DepthNormals);
                // Both at (0.6, 0, 0.8): xy add and z multiplies, (1.2, 0, 0.64) normalised.
                Color tilted = new Color(0.8f, 0.5f, 1, 1);
                material.SetTexture("_Norm_Texture", rig.Texels(tilted));
                material.SetTexture("_Fabric_Norm_Texture", rig.Texels(tilted));
                Vector3 blended = new Vector3(1.2f, 0, 0.64f).normalized;
                Expect(new Color(blended.x, 0, -blended.z, 0), "the normal blend", DepthNormals);
                // The fabric tiling on a banded strip (flat, flat, tangent, tangent): u 0.516 reads the tangent band,
                // u 0.258 (size 0.5) the flat one.
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_Fabric_Norm_Texture", rig.Bands(flat, flat, tangent, tangent));
                Expect(new Color(1, 0, 0, 0), "the fabric band under the pixel", DepthNormals);
                material.SetFloat("_Fabric_Norm_Size", 0.5f);
                Expect(new Color(0, 0, -1, 0), "the fabric normal size", DepthNormals);
                material.SetFloat("_Fabric_Norm_Size", 1);
                material.SetTexture("_Fabric_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));

                // Forward alpha: the saturated ID blue, written through the shipped alpha factors (One
                // OneMinusSrcAlpha) over a cleared target.
                float Alpha() => rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(Alpha() - 1) < 1e-4f, "ID blue 1 is opaque");
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 0.5f, 0)));
                float alpha = Alpha();
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 0.5f) < 1e-4f, "ID blue 0.5 blends at 0.5: " + alpha);
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 2, 0)));
                alpha = Alpha();
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 1) < 1e-4f, "the alpha is saturated: " + alpha);
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));
                cases += 3;

                // _GlobalMipBias applies once per sample: over UV0 a 256 texture reads level 3 unbiased and level 4
                // at bias 1.
                void BiasCases(string texture, Texture2D mipped, Color unbiased, Color once, int pass)
                {
                    material.SetTexture(texture, mipped);
                    Expect(unbiased, texture + ": no bias reads level 3", pass);
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(once, texture + ": bias 1 applies once", pass);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                BiasCases("_Diff_Texture", rig.MipLevels(256, level => level == 4 ? green : red), red, green, Meta);
                material.SetTexture("_Diff_Texture", rig.Texels(Grey(0.25f)));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 1)));
                BiasCases("_Detail_Texture", rig.MipLevels(256, level => level == 4 ? white : black), darkened, lifted, Meta);
                material.SetTexture("_Detail_Texture", rig.Texels(white));
                BiasCases("_ID_Mask", rig.MipLevels(256, level => level == 4 ? new Color(0, 0, 1, 1) : new Color(0, 0, 1, 0)), Grey(0.25f), lifted, Meta);
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetTexture("_Diff_Texture", rig.Texels(green));
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                BiasCases("_Emissive_Texture", rig.MipLevels(256, level => level == 4 ? white : black), black, green, Meta);
                material.SetTexture("_Emissive_Texture", rig.Texels(white));
                material.SetTexture("_Norm_Texture", rig.Texels(tangent));
                BiasCases("_Fresnel_Texture", rig.MipLevels(256, level => level == 4 ? red : black), black, red, Meta);
                material.SetTexture("_Fresnel_Texture", rig.Texels(red));
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                BiasCases("_Norm_Texture", rig.MipLevels(256, level => level == 4 ? tangent : flat), new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), DepthNormals);
                material.SetTexture("_Norm_Texture", rig.Texels(flat));
                material.SetTexture("_ID_Mask", rig.Texels(new Color(0, 1, 1, 0)));
                BiasCases("_Fabric_Norm_Texture", rig.MipLevels(256, level => level == 4 ? tangent : flat), new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), DepthNormals);

                Debug.Log("Project Lucid HL_Char_Boss_4_Eggman GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading, the metal/smoothness texture and the SSS ramp's mip bias remain compile-checked only.");
            }
        }
    }
}
