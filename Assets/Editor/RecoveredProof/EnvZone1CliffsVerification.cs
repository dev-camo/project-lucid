using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone1_Cliffs, reconstructed from the shipped Metal programs. The albedo is
    // checked through the Meta pass: the triplanar main layer (projections X: zy, Y: xz, Z: xy, |normal|^blend
    // weights, tiling, tint), the edge shadow and the edge layer under its UV0 mask, and the planar XZ top layer
    // under its UV0 blend. DepthNormals checks the triplanar, edge and top normal maps.
    public static class EnvZone1CliffsVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone1_Cliffs.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone1_Cliffs";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "263e7ca97ab99654c8faa42cf66feded";
        private const int Forward = 0, DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_REFLECTION_PROBE_BLENDING", "_SHADOWS_SOFT" },
            new[] { "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON", "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_WRITE_RENDERING_LAYERS", "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "EDITOR_VISUALIZATION", "_FORWARD_PLUS" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
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
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(1, 0, 0, 0));
                int cases = 0;

                // The graph samples with a trilinear inline sampler, so the diffuse is 4x4 bands of 16 texels
                // whose colour names the band: (column / 3, row / 3, 0).
                var bands = rig.Own(new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Repeat });
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        pixels[y * 64 + x] = new Color(x / 16 / 3f, y / 16 / 3f, 0, 1);
                bands.SetPixels(pixels);
                bands.Apply(false, false);
                Color Band(int column, int row) => new Color(column / 3f, row / 3f, 0, 1);
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1);
                Color half = new Color(0.5f, 0.5f, 0.5f, 1);
                Color Scaled(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, 1);

                // Start on the main layer alone: the top blend pushed off (amount -2) and an edge alpha of 0 (no
                // edge mask, no shadow).
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Main_DiffA_Tex", bands);
                material.SetFloat("_Main_UV_Tiling", 10);
                material.SetFloat("_Main_Tri_Blend_Amount", 2);
                material.SetFloat("_Top_Edge_Blend_Amount", -2);
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0)));
                // Pushed back to z 0.4, pixel (18, 18) sits at world (0.156, 0.156, 0.4): with tiling 10 the UV is
                // the world position, so Z reads band (0, 0), Y (xz) band (0, 1) and X (zy) band (1, 0).
                Matrix4x4 back = Matrix4x4.Translate(new Vector3(0, 0, 0.4f));
                void Expect(Color expected, string label, int pass = Meta)
                {
                    rig.Expect(material, expected, label, pass: pass, model: back, x: 18, y: 18);
                    cases++;
                }
                void Uv(float u, float v)
                {
                    var uv = new Vector4(u, v, 0, 0);
                    rig.Quad.SetUVs(0, new List<Vector4> { uv, uv, uv, uv });
                }

                Expect(Band(0, 0), "a -Z normal projects the main layer onto xy");
                rig.SetNormals(Vector3.up);
                Expect(Band(0, 1), "a Y normal projects onto xz");
                rig.SetNormals(Vector3.right);
                Expect(Band(1, 0), "an X normal projects onto zy");
                // Normal (0.6, 0, -0.8): blend 2 weighs X 0.36 and Z 0.64; blend 1 weighs them 0.6 / 1.4.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                Expect(new Color(0.12f, 0, 0, 1), "blend 2 weights");
                material.SetFloat("_Main_Tri_Blend_Amount", 1);
                Expect(new Color(0.6f / 1.4f / 3, 0, 0, 1), "blend 1 weights");
                material.SetFloat("_Main_Tri_Blend_Amount", 2);
                rig.SetNormals(Vector3.back);
                material.SetFloat("_Main_UV_Tiling", 20);
                Expect(Band(1, 1), "tiling 20 doubles the UV");
                material.SetFloat("_Main_UV_Tiling", 10);
                material.SetTexture("_Main_DiffA_Tex", rig.Texels(half));
                material.SetColor("_Main_Diff_Colour_Tint", new Color(1, 0, 1, 1));
                Expect(new Color(0.5f, 0, 0.5f, 1), "the main tint");
                material.SetColor("_Main_Diff_Colour_Tint", Color.white);

                // The edge: its alpha shadows the main layer (1 - alpha, at least the shadow opacity) and, at or
                // above the step amount, replaces it with the edge diffuse; so does the UV0 band v 0.96 to 0.9675.
                material.SetFloat("_Edge_DiffA_Alpha_Step_Amount", 0.9f);
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0.75f)));
                Expect(Scaled(half, 0.25f), "the edge alpha shadows the main layer");
                material.SetFloat("_Edge_Shadow_Opacity", 0.5f);
                Expect(Scaled(half, 0.5f), "the shadow opacity is its floor");
                material.SetFloat("_Edge_Shadow_Opacity", 0);
                material.SetFloat("_Edge_DiffA_Alpha_Step_Amount", 0.75f);
                Expect(blue, "an edge alpha at the step amount shows the edge");
                material.SetFloat("_Edge_DiffA_Alpha_Step_Amount", 0.9f);
                Uv(0.5f, 0.98f);
                Expect(blue, "the top of UV0 is edge");
                Uv(0.5f, 0.963f);
                float band = Mathf.Clamp01((0.963f - 0.96f) * 133.6f);
                Expect(Color.Lerp(Scaled(half, 0.25f), blue, band), "the UV0 edge band ramps in");
                rig.SetUv0(Vector4.zero);
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0)));

                // The top: planar XZ (world (0.156, 0.4) at tiling 10 is band (0, 1), at 20 band (1, 3)) under
                // saturate((v + amount) * 10).
                material.SetTexture("_Top_DiffA_Tex", bands);
                material.SetFloat("_Top_UV_Tiling", 10);
                material.SetFloat("_Top_Edge_Blend_Amount", 0);
                Expect(Band(0, 1), "the top layer projects onto xz");
                material.SetFloat("_Top_UV_Tiling", 20);
                Expect(Band(1, 3), "the top tiling");
                material.SetTexture("_Top_DiffA_Tex", rig.Texels(red));
                Uv(0.5f, 0.05f);
                Expect(Color.Lerp(red, half, 0.5f), "half top at v 0.05");
                material.SetFloat("_Top_Edge_Blend_Amount", 0.03f);
                Expect(Color.Lerp(red, half, 0.2f), "the blend amount shifts v");
                material.SetFloat("_Top_Edge_Blend_Amount", -2);
                rig.SetUv0(Vector4.zero);

                // Normals, read by DepthNormals (tangent +X). The packed normal maps unpack (r * a, g) * 2 - 1:
                // flat is (0, 0), along is (1, 0).
                Color flat = new Color(1, 0.5f, 0.5f, 0.5f), along = new Color(1, 0.5f, 0.5f, 1);
                float r2 = Mathf.Sqrt(0.5f);
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                material.SetTexture("_Main_Normal_Tex", rig.Texels(flat));
                material.SetTexture("_Edge_Normal_Tex", rig.Texels(along));
                material.SetTexture("_Top_Normal_Tex", rig.Texels(along));
                Expect(new Color(0, 0, -1, 0), "flat main normal", DepthNormals);
                // The triplanar normal adds the unpacked xy to the projection's world axes (Z: x, y).
                material.SetTexture("_Main_Normal_Tex", rig.Texels(along));
                Expect(new Color(r2, 0, -r2, 0), "the triplanar main normal", DepthNormals);
                material.SetTexture("_Main_Normal_Tex", rig.Texels(flat));
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 1)));
                Expect(new Color(1, 0, 0, 0), "the edge normal under the edge mask", DepthNormals);
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetFloat("_Top_Edge_Blend_Amount", 0);
                Expect(new Color(1, 0, 0, 0), "the top normal under the top blend", DepthNormals);
                Uv(0.5f, 0.05f);
                Expect(new Color(r2, 0, -r2, 0), "half top normal", DepthNormals);
                rig.SetUv0(Vector4.zero);
                material.SetFloat("_Top_Edge_Blend_Amount", -2);

                // The bitangent sign, as in the original Forward and DepthNormals fragments: the strict sign of the
                // interpolated tangent w (w > 0 ? 1 : -1), times the world transform's sign, applied separately. The
                // edge map's tangent-space normal (0, 0.6, 0.8) mixes Y and Z: with N (0, 0, -1) and T +X it reads
                // 0.8 N + 0.6 B, B = sign * cross(N, T) = sign * (0, -1, 0). A magnitude-scaled bitangent would
                // shorten B for a nonunit w and drop it for w 0.
                void Tangents(float w0, float w1, float w2, float w3) => rig.Quad.tangents = new[]
                    { new Vector4(1, 0, 0, w0), new Vector4(1, 0, 0, w1), new Vector4(1, 0, 0, w2), new Vector4(1, 0, 0, w3) };
                Color down = new Color(0, -0.6f, -0.8f, 0), up = new Color(0, 0.6f, -0.8f, 0);
                material.SetTexture("_Edge_Normal_Tex", rig.Texels(new Color(1, 0.8f, 0.5f, 0.5f)));
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 1)));
                Tangents(1, 1, 1, 1);
                Expect(down, "mixed normal, w 1", DepthNormals);
                Tangents(0.5f, 0.5f, 0.5f, 0.5f);
                Expect(down, "w 0.5: the sign, not the magnitude", DepthNormals);
                Tangents(0.1f, 0.6f, 0.3f, 0.9f);
                Expect(down, "an interpolated positive w", DepthNormals);
                Tangents(-0.5f, -0.5f, -0.5f, -0.5f);
                Expect(up, "w -0.5", DepthNormals);
                Tangents(0, 0, 0, 0);
                Expect(up, "w 0 takes the negative sign", DepthNormals);
                // The world transform's sign: neither the immediate nor the command-buffer draw here sets the
                // per-draw unity_WorldTransformParams.w for a mirrored matrix, so CliffsMirroredRendererTests renders
                // the mirrored cases through a real MeshRenderer. This keeps both passes on the one bitangent function.
                string source = System.IO.File.ReadAllText(ShaderPath);
                RecoveredShaderProof.Require(ShaderName,
                    source.Contains("float bitangentSign = (tangentWS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();")
                    && Regex.Matches(source, @"float3 bitangent = CliffBitangent\(input\.normalWS, input\.tangentWS\);").Count == 2
                    && Regex.Matches(source, @"output\.tangentWS = float4\(normalInput\.tangentWS, input\.tangentOS\.w\);").Count == 2,
                    "both passes interpolate the raw w and apply the strict sign times the transform sign");
                cases++;
                Tangents(1, 1, 1, 1);
                material.SetTexture("_Edge_Normal_Tex", rig.Texels(along));
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0)));

                // _GlobalMipBias applies once per sample. The triplanar and top UVs move 1/16 per pixel at tiling 10
                // (level 4 of a 256 texture, level 5 at bias 1); UV0 moves 1/32 (level 3, then 4).
                void BiasCases(string texture, Texture2D levels, Color unbiased, Color once, int pass = Meta)
                {
                    material.SetTexture(texture, levels);
                    Expect(unbiased, texture + ": no bias", pass);
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(once, texture + ": bias 1 applies once", pass);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                Texture2D Levels(Color unbiased, Color once, Color twice) => rig.MipLevels(256, level => level <= 4 ? unbiased : level == 5 ? once : twice);
                Texture2D UvLevels(Color unbiased, Color once, Color twice) => rig.MipLevels(256, level => level <= 3 ? unbiased : level == 4 ? once : twice);
                BiasCases("_Main_DiffA_Tex", Levels(half, red, green), half, red);
                material.SetTexture("_Main_DiffA_Tex", rig.Texels(half));
                material.SetFloat("_Edge_DiffA_Alpha_Step_Amount", 0.5f);
                BiasCases("_Edge_DiffA_Tex", UvLevels(red, blue, green), red, blue);
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetFloat("_Top_UV_Tiling", 10);
                material.SetFloat("_Top_Edge_Blend_Amount", 0);
                BiasCases("_Top_DiffA_Tex", Levels(half, red, green), half, red);
                material.SetFloat("_Top_Edge_Blend_Amount", -2);
                material.SetFloat("_Main_Normal_UV_Tiling", 10);
                BiasCases("_Main_Normal_Tex", Levels(flat, along, flat), new Color(0, 0, -1, 0), new Color(r2, 0, -r2, 0), DepthNormals);
                material.SetTexture("_Main_Normal_Tex", rig.Texels(flat));
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 1)));
                BiasCases("_Edge_Normal_Tex", UvLevels(flat, along, flat), new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), DepthNormals);
                material.SetTexture("_Edge_DiffA_Tex", rig.Texels(new Color(0, 0, 1, 0)));
                material.SetFloat("_Top_Edge_Blend_Amount", 0);
                BiasCases("_Top_Normal_Tex", Levels(flat, along, flat), new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), DepthNormals);

                // The forward pass is opaque: alpha 1.
                float alpha = rig.Draw(material, Forward, model: back, x: 18, y: 18).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 1) < 0.01f, "opaque forward alpha: " + alpha);
                cases++;

                Debug.Log("Project Lucid HL_Env_Zone1_Cliffs GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Lit shading and the layered smoothness remain compile-checked only.");
            }
        }
    }
}
