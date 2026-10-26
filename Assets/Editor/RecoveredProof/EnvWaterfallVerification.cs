using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Waterfall, reconstructed from the shipped Metal programs. The surface is checked
    // unlit through the Meta pass (the colour-variation noise, the refracted scene colour and main colour, the
    // stepped foam, the graph fog, the emission and the vertex displacement), the ripple normal through
    // DepthNormals, and the doubled vertex alpha through Forward.
    public static class EnvWaterfallVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Waterfall.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Waterfall";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "ed692a23ae207ac43a0d40772f3d9754";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_Texture2DAsset_56213f5e12504974b7f5a49f56756455_Out_0_Texture2D", "5cecf516273ba544f8abc9afeafea881" },
        };
        private const int Forward = 0, DepthOnly = 2, DepthNormals = 3, Meta = 4;
        private const string Noise = "_Texture2DAsset_56213f5e12504974b7f5a49f56756455_Out_0_Texture2D";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING", "FOG_LINEAR" },
            new[] { "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_WRITE_RENDERING_LAYERS", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "LIGHTMAP_ON", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "_LIGHT_COOKIES", "_DBUFFER_MRT3", "EDITOR_VISUALIZATION", "_USE_DETAIL_MASK_MAP" },
            new[] { "DOTS_INSTANCING_ON", "_SCREEN_SPACE_OCCLUSION", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "DEBUG_DISPLAY", "EVALUATE_SH_MIXED", "FOG_EXP2" },
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
            RecoveredShaderProof.RequireImportedTextures(shader, ShaderPath, OriginalTextures);
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 5, "five shipped passes");
                rig.SaveFloats("unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace", "_HLShaderTimeGame");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl", "unity_FogColor", "_ScaledScreenParams");
                rig.SaveTextures("_CameraOpaqueTexture");
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalVector("unity_FogColor", new Color(0, 0, 1, 1));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1f / 32, 1 + 1f / 32));
                Vector4 albedo = new Vector4(1, 0, 0, 0), emission = new Vector4(0, 1, 0, 0);
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), white = Color.white, black = new Color(0, 0, 0, 1);
                Color N(float x) => new Color(x, x, x, 1);
                Color Rgb(float r, float g, float b) => new Color(r, g, b, 1);

                Material material = rig.Own(new Material(shader));
                material.SetColor("_Color_Var_1", red);
                material.SetColor("_Color_Var_2", green);
                material.SetColor("_Main_Colour", Color.clear);
                material.SetTexture(Noise, rig.Texels(N(0)));
                // A flat ripple (x and y 0 in the AG packing) and no foam (the amount is past texture plus vertex red).
                Color flat = new Color(0.5f, 0.5f, 1, 1), push = new Color(1, 0.5f, 1, 1);
                material.SetTexture("_Ripple_Texture_Normal", rig.Texels(flat));
                material.SetFloat("_Foam_Amount", 10);
                material.SetFloat("_Scene_Fog_Intensity", 0);
                material.SetFloat("_Vertex_Displacement_Amount", 0);
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(white));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass);
                    cases++;
                }

                // Colour variation: lerp(var 1, var 2, 2 * noise) times the scene colour plus the main colour.
                Expect(red, "noise 0 shows colour variation 1");
                material.SetTexture(Noise, rig.Texels(N(0.25f)));
                Expect(Rgb(0.5f, 0.5f, 0), "the noise is doubled before the lerp");
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(black));
                // Colour properties are linearised on set, so the case uses 0/1 channels.
                material.SetColor("_Main_Colour", new Color(1, 0, 1, 0));
                Expect(Rgb(0.5f, 0, 0), "the main colour adds to the scene colour");
                material.SetColor("_Main_Colour", Color.clear);
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(new Color(0.2f, 0.4f, 1, 1)));
                Expect(Rgb(0.1f, 0.2f, 0), "the scene colour multiplies the variation");

                // Refraction: the read pixel's screen u 0.516 reads the opaque texture's right texel; the unscaled
                // ripple x of 1 times the intensity 0.05 moves it to the left one.
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(red, white));
                Expect(Rgb(0.5f, 0.5f, 0), "no refraction reads the scene colour under the pixel");
                material.SetFloat("_Refraction_Intensity", 0.05f);
                Expect(Rgb(0.5f, 0.5f, 0), "a flat ripple does not refract");
                material.SetTexture("_Ripple_Texture_Normal", rig.Texels(push));
                Expect(Rgb(0.5f, 0, 0), "the ripple normal refracts the scene colour");
                material.SetFloat("_Normal_Intensity", 0);
                Expect(Rgb(0.5f, 0, 0), "the refraction ignores the normal strength");
                material.SetFloat("_Normal_Intensity", 1);
                // The ripple lookup (bilinear inline sampler, 16-texel bands): flat at the pixel, pushing at bands 1 and 5.
                material.SetTexture("_Ripple_Texture_Normal", rig.Bands(flat, push, flat, flat, flat, push, flat));
                Expect(Rgb(0.5f, 0.5f, 0), "the ripple band under the pixel is flat");
                material.SetTextureScale("_Ripple_Texture_Normal", new Vector2(0.5f, 1));
                Expect(Rgb(0.5f, 0, 0), "ripple tiling");
                material.SetTextureScale("_Ripple_Texture_Normal", Vector2.one);
                material.SetTextureOffset("_Ripple_Texture_Normal", new Vector2(0.25f, 0));
                Expect(Rgb(0.5f, 0, 0), "ripple offset");
                material.SetTextureOffset("_Ripple_Texture_Normal", Vector2.zero);
                material.SetVector("_Ripple_Speed_Direction", new Vector4(0.25f, 0, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Rgb(0.5f, 0, 0), "the ripple scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTexture("_Ripple_Texture_Normal", rig.Texels(flat));
                material.SetFloat("_Refraction_Intensity", 0);
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Texels(white));

                // The noise lookup: band 3 under the pixel (0, red), band 1 (0.5, green); the column scrolls half a
                // unit along v per second of game time to band 0 or 6 (0.5) whichever way v runs.
                material.SetTexture(Noise, rig.Bands(N(0), N(0.5f), N(0), N(0), N(0), N(0), N(0)));
                material.SetVector("_Colour_Variation_Noise_UV_Tiling", new Vector4(1, 1, 0, 0));
                Expect(red, "the noise band under the pixel");
                material.SetVector("_Colour_Variation_Noise_UV_Tiling", new Vector4(0.5f, 0.5f, 0, 0));
                Expect(green, "noise tiling");
                material.SetVector("_Colour_Variation_Noise_UV_Tiling", new Vector4(1, 1, 0, 0));
                material.SetTexture(Noise, Column(rig, N(0.5f), N(0), N(0), N(0), N(0), N(0), N(0.5f)));
                Expect(red, "the noise column under the pixel");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(green, "the noise scrolls along v with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // _GlobalMipBias applies once: level 3 unbiased, level 4 at bias 1.
                material.SetTexture(Noise, rig.MipLevels(256, level => level == 4 ? N(0.5f) : N(0)));
                Expect(red, "noise: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "noise: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture(Noise, rig.Texels(N(0.25f)));

                // Foam: foam red plus vertex red at or past the amount steps to 1, times the opacity, and lerps the
                // colour toward that much of the foam colour.
                Color water = Rgb(0.5f, 0.5f, 0);
                material.SetColor("_Foam_Color", new Color(0, 0, 1, 1));
                material.SetFloat("_Foam_Amount", 1.5f);
                material.SetTexture("_Foam_Texture", rig.Texels(N(0.6f)));
                Expect(Rgb(0, 0, 1), "foam at the amount replaces the colour");
                material.SetFloat("_Foam_Opacity", 0.5f);
                Expect(Rgb(0.25f, 0.25f, 0.25f), "foam opacity scales both the lerp and the foam colour");
                material.SetFloat("_Foam_Opacity", 1);
                material.SetTexture("_Foam_Texture", rig.Texels(N(0.4f)));
                Expect(water, "foam below the amount");
                material.SetTexture("_Foam_Texture", rig.Texels(N(0.6f)));
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                Expect(water, "the vertex red adds to the foam");
                rig.SetColors(Color.white);
                material.SetTexture("_Foam_Texture", rig.Bands(N(0), N(0), N(0), N(0), N(0), N(0.6f), N(0)));
                Expect(water, "the foam band under the pixel");
                material.SetTextureOffset("_Foam_Texture", new Vector2(0.25f, 0));
                Expect(Rgb(0, 0, 1), "foam offset");
                material.SetTextureOffset("_Foam_Texture", Vector2.zero);
                material.SetVector("_Foam_Speed_Direction", new Vector4(0.25f, 0, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Rgb(0, 0, 1), "the foam scrolls with game time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Foam_Amount", 10);

                // The graph fog has no density without a fog mode, so a negative scene fog intensity is what fades the
                // albedo toward the fog colour; the emission keeps the unfogged colour, times the boost.
                material.SetFloat("_Scene_Fog_Intensity", -0.5f);
                Expect(Rgb(0.25f, 0.25f, 0.5f), "the scene fog intensity fades the albedo to the fog colour");
                Shader.SetGlobalVector("unity_MetaFragmentControl", emission);
                Expect(water, "the emission is the unfogged colour");
                material.SetFloat("_Emission_Boost", 2);
                Expect(Rgb(1, 1, 0), "emission boost");
                material.SetFloat("_Emission_Boost", 1);
                material.SetTexture(Noise, rig.Texels(N(0.75f)));
                Expect(Rgb(-0.5f, 1.5f, 0), "the variation lerp is unclamped");
                material.SetTexture(Noise, rig.Texels(N(0.25f)));
                Shader.SetGlobalVector("unity_MetaFragmentControl", albedo);
                material.SetFloat("_Scene_Fog_Intensity", 0);

                // Vertex displacement along the normal: with normals +X the quad slides right, so the pixel reads
                // the noise strip nearer its left edge. The displacement texture is read at UV1, and vertex blue 0
                // leaves the full displacement.
                material.SetTexture(Noise, rig.Bands(N(0), N(0.5f), N(0), N(0), N(0), N(0), N(0)));
                rig.SetNormals(Vector3.right);
                rig.SetColors(new Color(1, 1, 0, 1));
                rig.Quad.SetUVs(1, new List<Vector2> { new Vector2(0.25f, 0), new Vector2(0.25f, 0), new Vector2(0.25f, 0), new Vector2(0.25f, 0) });
                material.SetTexture("_Vertex_Displacement_Texture", rig.Texels(N(0.5f), white));
                material.SetFloat("_Vertex_Displacement_Amount", 1);
                Expect(green, "half the displacement texture moves the quad by half");
                material.SetFloat("_Vertex_Displacement_Amount", 0);
                Expect(red, "displacement amount 0");
                material.SetFloat("_Vertex_Displacement_Amount", 0.5f);
                material.SetTextureOffset("_Vertex_Displacement_Texture", new Vector2(0.5f, 0));
                Expect(green, "the displacement texture offset at UV1");
                material.SetTextureOffset("_Vertex_Displacement_Texture", Vector2.zero);
                material.SetVector("_Vertex_Displacement_Speed_Direction", new Vector4(0.5f, 0, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(green, "the displacement scrolls with game time");
                rig.SetColors(Color.white);
                Expect(red, "vertex blue 1 holds the vertices still");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Vertex_Displacement_Amount", 0);
                rig.SetNormals(Vector3.back);
                material.SetTexture(Noise, rig.Texels(N(0.25f)));

                // Normals, read by DepthNormals: the AG-packed ripple normal through Normal Strength. The quad faces
                // -Z with tangent +X.
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                Expect(new Color(0, 0, -1, 0), "flat ripple normal", DepthNormals);
                material.SetTexture("_Ripple_Texture_Normal", rig.Texels(push));
                Expect(new Color(1, 0, 0, 0), "a ripple normal along the tangent", DepthNormals);
                material.SetFloat("_Normal_Intensity", 0.5f);
                Expect(new Color(0.707f, 0, -0.707f, 0), "half strength halves xy and lerps z halfway from 1", DepthNormals);
                material.SetFloat("_Normal_Intensity", 0);
                Expect(new Color(0, 0, -1, 0), "strength 0 is flat", DepthNormals);
                material.SetFloat("_Normal_Intensity", 1);
                material.SetTexture("_Ripple_Texture_Normal", rig.Texels(flat));

                // Forward alpha: saturate(2 * overall alpha * (1 - vertex green)), read back unchanged through the
                // shipped alpha factors (One OneMinusSrcAlpha) over the cleared target.
                void Alpha(float a, string label)
                {
                    float actual = rig.Draw(material, Forward).a;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(actual - a) < 1e-3f, label + ": expected alpha " + a + ", read " + actual);
                    cases++;
                }
                material.SetFloat("_Overall_Alpha", 0.25f);
                rig.SetColors(new Color(1, 0, 1, 1));
                Alpha(0.5f, "the overall alpha is doubled");
                rig.SetColors(new Color(1, 0.5f, 1, 1));
                Alpha(0.25f, "vertex green fades the alpha");
                material.SetFloat("_Overall_Alpha", 1);
                rig.SetColors(new Color(1, 0, 1, 1));
                Alpha(1, "the alpha saturates");
                rig.SetColors(Color.white);

                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "DepthOnly writes the depth");
                cases++;

                Debug.Log("Project Lucid HL_Env_Waterfall GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading, smoothness and the fogged graph density remain compile-checked only.");
            }
        }

        // A vertical strip of 16-texel bands, for the graph's bilinear inline sampler.
        private static Texture2D Column(RecoveredShaderRig rig, params Color[] colors)
        {
            var texture = rig.Own(new Texture2D(1, colors.Length * 16, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
            var pixels = new Color[colors.Length * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colors[i / 16];
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
