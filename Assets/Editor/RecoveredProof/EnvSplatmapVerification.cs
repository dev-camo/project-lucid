using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Splatmap, reconstructed from the shipped Metal programs. The splat-weighted,
    // tinted diffuse layers (slice, UV scale/offset and blend order per mask channel) are checked through the Meta
    // pass, the blended normals through DepthNormals, and the opaque alpha through Forward.
    public static class EnvSplatmapVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Splatmap.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Splatmap";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "22056f8a589ee2f4db00e4ff1596eb40";
        private const int Forward = 0, DepthOnly = 2, DepthNormals = 3, Meta = 4;

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
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl");
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;

                // A 64 x 1 array, 16 texels per band: the graph's inline sampler is bilinear, and the read pixel's
                // u 0.516 (x 33) lands inside band 2, u 0.766 (offset 0.25) in band 3 and u 0.258 (scale 0.5) in band 1.
                Texture2DArray Slices(params Color[][] slices)
                {
                    var array = rig.Own(new Texture2DArray(64, 1, slices.Length, TextureFormat.RGBAFloat, false, true)
                        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat });
                    for (int slice = 0; slice < slices.Length; slice++) array.SetPixels(slices[slice], slice, 0);
                    array.Apply(false, false);
                    return array;
                }
                Color[] Bands(params Color[] colors)
                {
                    var pixels = new Color[64];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = colors[i * colors.Length / 64];
                    return pixels;
                }

                Material material = rig.Own(new Material(shader));
                foreach (string layer in new[] { "Red", "Green", "Blue", "Alpha" })
                    material.SetColor("_Diff_Colour_Tint_" + layer, white);
                Texture2DArray plain = Slices(Bands(red), Bands(green), Bands(blue), Bands(white));
                material.SetTexture("_Diff_Texture2DArray", plain);
                void Mask(Color mask) => material.SetTexture("_Splatmap_Mask_Texture", rig.Texels(mask));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass);
                    cases++;
                }

                // The mask's red is ignored (the red layer is the base); green, blue and alpha lerp their layers in.
                Mask(new Color(1, 0, 0, 0));
                Expect(red, "an empty mask shows the red layer (slice 0)");
                Mask(new Color(0, 1, 0, 0));
                Expect(green, "the mask's green selects the green layer (slice 1)");
                Mask(new Color(0, 0, 1, 0));
                Expect(blue, "the mask's blue selects the blue layer (slice 2)");
                Mask(new Color(0, 0, 0, 1));
                Expect(white, "the mask's alpha selects the alpha layer (slice 3)");
                Mask(new Color(0, 0.5f, 0, 0));
                Expect(new Color(0.5f, 0.5f, 0, 1), "the green weight lerps over the red layer");
                Mask(new Color(0, 1, 1, 0));
                Expect(blue, "blue is lerped in after green");
                Mask(new Color(0, 1, 1, 0.5f));
                Expect(new Color(0.5f, 0.5f, 1, 1), "alpha is lerped in last");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(new Color(0, 0, 0, 1), "no emission");
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);

                // Slices and tints per layer.
                Mask(Color.clear);
                material.SetFloat("_ArrayIndex_Red", 2);
                Expect(blue, "the red layer's array index picks the slice");
                material.SetFloat("_ArrayIndex_Red", 0.6f);
                Expect(green, "the array index is rounded");
                material.SetFloat("_ArrayIndex_Red", 3);
                material.SetColor("_Diff_Colour_Tint_Red", new Color(0, 1, 1, 0));
                Expect(new Color(0, 1, 1, 1), "the red tint multiplies its layer");
                material.SetColor("_Diff_Colour_Tint_Red", white);
                material.SetFloat("_ArrayIndex_Red", 0);
                string[] layers = { "Green", "Blue", "Alpha" };
                Color[] masks = { new Color(0, 1, 0, 0), new Color(0, 0, 1, 0), new Color(0, 0, 0, 1) };
                Color[] slices = { green, blue, white };
                for (int i = 0; i < layers.Length; i++)
                {
                    Mask(masks[i]);
                    material.SetColor("_Diff_Colour_Tint_" + layers[i], new Color(1, 0, 1, 0));
                    Expect(slices[i] * new Color(1, 0, 1, 1), "the " + layers[i].ToLowerInvariant() + " tint multiplies its layer");
                    material.SetColor("_Diff_Colour_Tint_" + layers[i], white);
                }

                // UV scale/offset per layer, on a banded slice 0: green, blue, red, white.
                material.SetTexture("_Diff_Texture2DArray", Slices(Bands(green, blue, red, white), Bands(Color.black), Bands(Color.black), Bands(Color.black)));
                Mask(Color.clear);
                Expect(red, "the banded slice at the read pixel");
                foreach (string layer in new[] { "Red", "Green", "Blue", "Alpha" })
                {
                    int index = System.Array.IndexOf(new[] { "Red", "Green", "Blue", "Alpha" }, layer);
                    Mask(index == 0 ? Color.clear : masks[index - 1]);
                    material.SetFloat("_ArrayIndex_" + layer, 0);
                    material.SetVector("_" + layer + "_UV_Scale_Offset", new Vector4(1, 1, 0.25f, 0));
                    Expect(white, "the " + layer.ToLowerInvariant() + " UV offset");
                    material.SetVector("_" + layer + "_UV_Scale_Offset", new Vector4(0.5f, 1, 0, 0));
                    Expect(blue, "the " + layer.ToLowerInvariant() + " UV scale");
                    material.SetVector("_" + layer + "_UV_Scale_Offset", new Vector4(1, 1, 0, 0));
                    material.SetFloat("_ArrayIndex_" + layer, index);
                }

                // _GlobalMipBias applies once to the splatmap mask and not at all to the arrays (level 3 unbiased,
                // level 4 at bias 1).
                material.SetTexture("_Diff_Texture2DArray", plain);
                material.SetTexture("_Splatmap_Mask_Texture", rig.MipLevels(256, level => level == 4 ? new Color(0, 1, 0, 0) : Color.clear));
                Expect(red, "mask: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "mask: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                Mask(Color.clear);
                var mipped = rig.Own(new Texture2DArray(256, 256, 4, TextureFormat.RGBAFloat, true, true)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat });
                for (int slice = 0; slice < 4; slice++)
                    for (int mip = 0; mip < mipped.mipmapCount; mip++)
                    {
                        var pixels = new Color[Mathf.Max(1, 256 >> mip) * Mathf.Max(1, 256 >> mip)];
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = mip == 4 ? red : green;
                        mipped.SetPixels(pixels, slice, mip);
                    }
                mipped.Apply(false, false);
                material.SetTexture("_Diff_Texture2DArray", mipped);
                Expect(green, "array: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "array: the bias does not apply");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Diff_Texture2DArray", plain);

                // Normals, read by DepthNormals: slice 0 flat, slice 1 along the tangent (AG packed), blended by the
                // mask before the world transform normalises. The quad faces -Z with tangent +X.
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                Color flat = new Color(0.5f, 0.5f, 1, 1), tangent = new Color(1, 0.5f, 1, 1);
                material.SetTexture("_Normal_Texture2DArray", Slices(Bands(flat), Bands(tangent), Bands(flat), Bands(flat)));
                Mask(Color.clear);
                Expect(new Color(0, 0, -1, 0), "the red layer's flat normal", DepthNormals);
                Mask(new Color(0, 1, 0, 0));
                Expect(new Color(1, 0, 0, 0), "the green layer's normal", DepthNormals);
                Mask(new Color(0, 0.5f, 0, 0));
                Expect(new Color(Mathf.Sqrt(0.5f), 0, -Mathf.Sqrt(0.5f), 0), "the normals lerp, then normalise", DepthNormals);
                material.SetTexture("_Normal_Texture2DArray", Slices(Bands(new Color(1, 0.5f, 1, 0.5f)), Bands(flat), Bands(flat), Bands(flat)));
                Mask(Color.clear);
                Expect(new Color(0, 0, -1, 0), "the AG packing multiplies red by alpha", DepthNormals);

                // Opaque: Forward writes alpha 1; DepthOnly writes the depth.
                float alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, alpha == 1, "Forward alpha is 1: " + alpha);
                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "DepthOnly writes the depth");
                cases += 2;

                Debug.Log("Project Lucid HL_Env_Splatmap GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading and the metal/smoothness array remain compile-checked only.");
            }
        }
    }
}
