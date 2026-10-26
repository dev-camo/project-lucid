using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone3_Spikes, reconstructed from the shipped Metal programs. Through the Meta pass:
    // the tinted diffuse under the fresnel colour (its amount, intensity and opacity, on the normal-mapped normal),
    // the emissive texture times its colour, the shared diffuse tiling, and the vertex displacement along the normal
    // (red and green amounts, the scroll, the vertex-blue mask). DepthNormals checks the normal map.
    public static class EnvZone3SpikesVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone3_Spikes.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone3_Spikes";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "6654c34f8f4c73b4084ce675c2f0423f";
        private const int DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2", "_MAIN_LIGHT_SHADOWS_SCREEN" },
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
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Meta: object position straight to clip space, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                int cases = 0;
                Color black = new Color(0, 0, 0, 1), white = Color.white, clear = new Color(0, 0, 0, 0);
                Color diffuse = new Color(0.2f, 0.4f, 0.6f, 1);
                Color flat = new Color(0.5f, 0.5f, 1, 1), tilted = new Color(0.8f, 0.5f, 0.9f, 1);

                // Neutral start: no displacement (black displacement texture), a flat normal, the quad's tangent
                // frame (tangent +x, bitangent -y, normal -z).
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Vertex_Displacement_Texture", rig.Texels(black));
                material.SetTexture("_Diff_Tex", rig.Texels(diffuse));
                material.SetTexture("_Normal_Tex", rig.Texels(flat));
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                void Expect(Color expected, string label, int pass = Meta, int x = 16)
                {
                    rig.Expect(material, expected, label, pass: pass, x: x);
                    cases++;
                }

                // The albedo: the tinted diffuse (no fresnel at the default opacity 0).
                Expect(diffuse, "the diffuse");
                material.SetColor("_Diff_Tint", new Color(0, 1, 1, 1));
                Expect(new Color(0, 0.4f, 0.6f, 1), "the tint");
                material.SetColor("_Diff_Tint", white);
                // The diffuse tiling and offset (a banded strip for the inline trilinear sampler: u 0.516 reads the right band).
                material.SetTexture("_Diff_Tex", rig.Bands(black, diffuse));
                Expect(diffuse, "the strip under the pixel");
                material.SetTextureScale("_Diff_Tex", new Vector2(2, 1));
                Expect(black, "the diffuse tiling");
                material.SetTextureScale("_Diff_Tex", Vector2.one);
                material.SetTextureOffset("_Diff_Tex", new Vector2(0.5f, 0));
                Expect(black, "the diffuse offset");
                material.SetTextureOffset("_Diff_Tex", Vector2.zero);
                material.SetTexture("_Diff_Tex", rig.Texels(diffuse));

                // The fresnel: pow(1 - n.v, amount) * intensity * opacity weights the fresnel colour (white). A facing
                // normal has none; the normal map's (0.6, 0, 0.8) faces at 0.8.
                material.SetFloat("_Fresnel_Opacity", 1);
                material.SetFloat("_Fresnel_Amount", 1);
                Expect(diffuse, "a facing normal has no fresnel");
                material.SetTexture("_Normal_Tex", rig.Texels(tilted));
                Color Fresnel(float f) => Color.Lerp(white, diffuse, 1 - f);
                Expect(Fresnel(0.2f), "the mapped normal's fresnel");
                material.SetFloat("_Fresnel_Intensity", 2);
                Expect(Fresnel(0.4f), "the fresnel intensity");
                material.SetFloat("_Fresnel_Intensity", 1);
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(Fresnel(0.04f), "the fresnel amount is a power");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Opacity", 0.5f);
                Expect(Fresnel(0.1f), "the fresnel opacity");
                material.SetColor("_Fresnel_Colour", new Color(1, 0, 0, 1));
                material.SetFloat("_Fresnel_Opacity", 1);
                material.SetFloat("_Fresnel_Intensity", 10);
                Expect(new Color(1, 0, 0, 1), "the fresnel weight saturates at the fresnel colour");
                material.SetFloat("_Fresnel_Intensity", 1);
                material.SetColor("_Fresnel_Colour", white);
                material.SetFloat("_Fresnel_Opacity", 0);
                material.SetFloat("_Fresnel_Amount", 5);

                // DepthNormals: the normal map through the tangent frame.
                Expect(new Color(0.6f, 0, -0.8f, 0), "the normal map", DepthNormals);
                material.SetTexture("_Normal_Tex", rig.Texels(flat));
                Expect(new Color(0, 0, -1, 0), "a flat normal", DepthNormals);

                // Emission: the emissive texture times its colour, at the diffuse tiling.
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(black, "no emission at the default colour (black)");
                material.SetColor("_Emissive_Colour", new Color(1, 0, 1, 1));
                material.SetTexture("_Emissive_Tex", rig.Bands(black, new Color(0.5f, 0.5f, 0.5f, 1)));
                Expect(new Color(0.5f, 0, 0.5f, 1), "the emissive texture times its colour");
                material.SetTextureScale("_Diff_Tex", new Vector2(2, 1));
                Expect(black, "the emission shares the diffuse tiling");
                material.SetTextureScale("_Diff_Tex", Vector2.one);

                // Displacement, read through the coverage of pixel x = 4 (8 px from the left edge): normals along +x
                // move the quad right by the displacement; 0.5 (8 px) uncovers it, pixel 12 stays covered. Vertex blue
                // masks the displacement, so it is 0 here.
                material.SetTexture("_Emissive_Tex", rig.Texels(white));
                Color lit = new Color(1, 0, 1, 1), unmasked = new Color(1, 1, 0, 1);
                rig.SetNormals(Vector3.right);
                rig.SetColors(unmasked);
                void Covered(bool covered, string label)
                {
                    Expect(covered ? lit : clear, label, x: 4);
                    Expect(lit, label + " (pixel 12)", x: 12);
                }
                Covered(true, "no displacement");
                material.SetTexture("_Vertex_Displacement_Texture", rig.Texels(new Color(0.25f, 0.25f, 0, 1)));
                Covered(false, "red plus green (0.5) move the quad");
                material.SetFloat("_Green_Vertex_Displacement_Noise_Amount", 0);
                Covered(true, "red alone (0.25) is 4 px");
                material.SetFloat("_Red_Vertex_Displacement_Noise_Amount", 2);
                Covered(false, "the red amount");
                material.SetFloat("_Red_Vertex_Displacement_Noise_Amount", 0);
                material.SetFloat("_Green_Vertex_Displacement_Noise_Amount", 2);
                Covered(false, "the green amount");
                rig.SetColors(white);
                Covered(true, "vertex blue masks the displacement");
                rig.SetColors(unmasked);
                Covered(false, "vertex blue 0 keeps it");
                material.SetFloat("_Red_Vertex_Displacement_Noise_Amount", 1);
                material.SetFloat("_Green_Vertex_Displacement_Noise_Amount", 1);
                // The scroll: vertices sit at u 0 and 1 (texel 0 of a repeating 4-texel strip); game time 1 at speed
                // 0.5 moves both to texel 2.
                Color push = new Color(0.25f, 0.25f, 0, 1);
                material.SetTexture("_Vertex_Displacement_Texture", rig.Texels(black, black, push, push));
                Covered(true, "texel 0 does not displace");
                material.SetFloat("_Vertex_Displacement_UV_Speed_Direction", 0.5f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Covered(false, "the displacement UV scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTexture("_Vertex_Displacement_Texture", rig.Texels(black));
                rig.SetColors(white);
                rig.SetNormals(Vector3.back);

                // _GlobalMipBias applies once per sample: over UV0 a 256 texture reads level 3 unbiased and level 4
                // at bias 1 (diffuse and emissive through Meta, the normal map through DepthNormals).
                void Bias(string texture, Texture2D levels, Color unbiased, Color once, int pass, Vector4 control)
                {
                    Shader.SetGlobalVector("unity_MetaFragmentControl", control);
                    material.SetTexture(texture, levels);
                    Expect(unbiased, texture + ": no bias reads level 3", pass);
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(once, texture + ": bias 1 applies once", pass);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1);
                Bias("_Diff_Tex", rig.MipLevels(256, level => level == 4 ? green : red), red, green, Meta, albedo);
                Bias("_Emissive_Tex", rig.MipLevels(256, level => level == 4 ? green : red), red * new Color(1, 0, 1, 1), black, Meta, emission);
                Bias("_Normal_Tex", rig.MipLevels(256, level => level == 4 ? tilted : flat), new Color(0, 0, -1, 0), new Color(0.6f, 0, -0.8f, 0), DepthNormals, albedo);

                Debug.Log("Project Lucid HL_Env_Zone3_Spikes GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading, the metal/smoothness texture and the perspective view direction remain compile-checked only.");
            }
        }
    }
}
