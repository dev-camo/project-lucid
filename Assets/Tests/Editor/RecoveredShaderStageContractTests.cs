using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid.Tests
{
    public sealed class RecoveredShaderStageContractTests
    {
        private Fixture active;

        [Test]
        public void CollectableRetainsDepthAndShadowVertexPayloads()
        {
            Run("HL_Gameplay_Collectable", new[] { 1, 2 });
        }

        [Test]
        public void MatcapRetainsBothUvStreamsAndShadowNormal()
        {
            Run("HL_Matcap", new[] { 0, 2 });
        }

        [Test]
        public void TrailRetainsEveryPassVertexPayload()
        {
            Run("HL_Particle_Trail", new[] { 0, 1, 2 });
        }

        [Test]
        public void GhostedVertexNormalizesBothProjectionViewVectors()
        {
            Begin().CheckGhostedViewVectors();
        }

        [Test]
        public void CollectableDepthFragmentRetainsFourFloatChannels()
        {
            Begin().CheckDepth("HL_Gameplay_Collectable");
        }

        [Test]
        public void MatcapDepthFragmentRetainsFourFloatChannels()
        {
            Begin().CheckDepth("HL_Matcap");
        }

        [Test]
        public void TrailDepthFragmentRetainsFourFloatChannels()
        {
            Begin().CheckDepth("HL_Particle_Trail");
        }

        [Test]
        public void ShadersRetainOriginalKeywordScopesAndFloatOutputTypes()
        {
            // Original serialized tables/masks; the unused _GBUFFER_NORMALS_OCT name has flag0 but no surviving pass location.
            CheckDeclarations("HL_PFX_AlphaBlendAdd",
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON"
                },
                new byte[] { 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3 }, // HL Particle Main HLSL vertex
                    new[] { 0, 1, 2, 3 } // HL Particle Main HLSL fragment
                });
            CheckDeclarations("HL_Char_Ghosted",
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3 }, // ForwardLit vertex
                    new[] { 0, 1, 2, 3 } // ForwardLit fragment
                });
            CheckDeclarations("HL_Gameplay_Collectable",
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_SURFACE_TYPE_TRANSPARENT", "_WRITE_RENDERING_LAYERS",
                    "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON",
                    "_ALPHATEST_ON", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 22 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 24 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 22 } // ShadowCaster fragment
                });
            CheckDeclarations("HL_Matcap",
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "FOG_LINEAR",
                    "DOTS_INSTANCING_ON", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER",
                    "INSTANCING_ON", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 10 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 10 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 10, 20 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 10 } // ShadowCaster fragment
                });
            CheckDeclarations("HL_Particle_Trail",
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_GRADIENTMAPENABLE",
                    "_DBUFFER_MRT1", "_DBUFFER_MRT2",
                    "_DBUFFER_MRT3", "DEBUG_DISPLAY",
                    "_SCREEN_SPACE_OCCLUSION", "_SURFACE_TYPE_TRANSPARENT",
                    "_WRITE_RENDERING_LAYERS", "_ALPHAPREMULTIPLY_ON",
                    "_ALPHAMODULATE_ON", "_ALPHATEST_ON",
                    "_GBUFFER_NORMALS_OCT", "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 23 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 25 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 23 } // ShadowCaster fragment
                });
        }

        private static void CheckDeclarations(string name, string[] names, byte[] flags, int[][] masks)
        {
            string path = "Assets/Shaders/Recovered/" + name + ".shader";
            string source = File.ReadAllText(path);
            Assert.That(Regex.IsMatch(source, @"\b(?:half|fixed)(?:[1-4])?\b"), Is.False,
                name + ": the original Metal output and arithmetic types are float");
            if (name == "HL_Char_Ghosted")
            {
                string depthPass = source.Substring(source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal));
                depthPass = depthPass.Substring(0, depthPass.IndexOf("ENDHLSL", StringComparison.Ordinal));
                Assert.That(Regex.IsMatch(depthPass, @"\bVert\s*\(float3\s+positionOS\s*:\s*POSITION"), Is.True,
                    "Original depth vertex1 reads a float3 position; forward vertex5 reads float4.");
            }
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0).PassCount * 2, Is.EqualTo(masks.Length));
            var activeIndices = new HashSet<int>();
            for (int pass = 0; pass < masks.Length / 2; pass++)
            {
                for (int stage = 0; stage < 2; stage++)
                {
                    ShaderType type = stage == 0 ? ShaderType.Vertex : ShaderType.Fragment;
                    var expected = new List<string>();
                    foreach (int index in masks[pass * 2 + stage])
                    {
                        expected.Add(names[index]);
                        activeIndices.Add(index);
                    }
                    var actual = new List<string>();
                    foreach (LocalKeyword keyword in ShaderUtil.GetPassKeywords(shader,
                        new PassIdentifier(0, (uint)pass), type, ShaderCompilerPlatform.Metal))
                        actual.Add(keyword.name);
                    Assert.That(actual, Is.EquivalentTo(expected), name + " original Metal pass " + pass + " " + type + " mask");
                }
            }
            foreach (int index in activeIndices)
            {
                LocalKeyword keyword = shader.keywordSpace.FindKeyword(names[index]);
                Assert.That(keyword.isValid, Is.True, names[index]);
                Assert.That(keyword.isOverridable, Is.EqualTo((flags[index] & 1) == 0), names[index]);
                Assert.That(keyword.isDynamic, Is.EqualTo((flags[index] & 2) != 0), names[index]);
            }
        }

        private void Run(string name, int[] passes)
        {
            Begin().CheckVertices(name, passes);
        }

        private Fixture Begin()
        {
            Assert.That(active, Is.Null);
            active = new Fixture();
            return active;
        }

        [TearDown]
        public void DeleteOwnedCopiesAndCheckOriginals()
        {
            Fixture owned = active;
            if (owned == null) return;
            try { owned.Cleanup(); }
            finally { active = null; }
        }

        private sealed class Fixture
        {
            private readonly Dictionary<string, string> originals = new Dictionary<string, string>();
            private string folder;

            public void CheckGhostedViewVectors()
            {
                Shader shader = Copy("HL_Char_Ghosted", true);
                Matrix4x4 previousView = Shader.GetGlobalMatrix("unity_MatrixV");
                Vector4 previousOrtho = Shader.GetGlobalVector("unity_OrthoParams");
                Vector4 previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
                try
                {
                    using (var rig = new RecoveredShaderRig(shader))
                    {
                        rig.WriteOrthoParams = false;
                        rig.Camera.transform.position = new Vector3(9, 17, 29);
                        Matrix4x4 view = Matrix4x4.identity;
                        view.SetRow(2, new Vector4(0.25f, -0.5f, 2, 0));
                        rig.Camera.worldToCameraMatrix = view;
                        Shader.SetGlobalMatrix("unity_MatrixV", view);
                        Shader.SetGlobalVector("unity_OrthoParams", new Vector4(0, 0, 0, 1));
                        Material material = rig.Own(new Material(shader));
                        material.SetTextureScale("_Diffuse_Texture", new Vector2(2, 3));
                        material.SetTextureOffset("_Diffuse_Texture", new Vector2(0.125f, -0.25f));
                        Matrix4x4 model = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.identity, new Vector3(2, 3, 4));
                        material.SetFloat("_LucidProbeField", 3);
                        rig.Expect(material, new Color(0.25f, -0.5f, 2, 1), "published nonunit view row admission", pass: 1, model: model);
                        material.SetFloat("_LucidProbeField", 0);
                        rig.Expect(material, new Color(0.625f, 1.25f, 0, 1), "original diffuse UV transform", pass: 1, model: model);
                        material.SetFloat("_LucidProbeField", 1);
                        float ortho = 1 / Mathf.Sqrt(69);
                        rig.Expect(material, new Color(ortho, -2 * ortho, 8 * ortho, 1), "normalized original orthographic view row", pass: 1, model: model);
                        material.SetFloat("_LucidProbeField", 2);
                        float normal = 1 / Mathf.Sqrt(3);
                        rig.Expect(material, new Color(normal, normal, normal, 1), "original inverse-transpose normal", pass: 1, model: model);
                        material.SetFloat("_LucidProbeZeroNormal", 1);
                        rig.Expect(material, new Color(0, 0, 0, 1), "original safe zero surface normal", pass: 1, model: model);
                        material.SetFloat("_LucidProbeZeroNormal", 0);
                        Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                        material.SetFloat("_LucidProbeField", 1);
                        // Original perspective delta: camera(9,17,29) - world(7,13,23) = (2,4,6).
                        float perspective = 1 / Mathf.Sqrt(14);
                        rig.Expect(material, new Color(perspective, 2 * perspective, 3 * perspective, 1), "normalized original perspective view delta", pass: 1, model: model);
                    }
                }
                finally
                {
                    Shader.SetGlobalMatrix("unity_MatrixV", previousView);
                    Shader.SetGlobalVector("unity_OrthoParams", previousOrtho);
                    Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
                }
            }

            public void CheckVertices(string name, int[] passes)
            {
                Shader shader = Copy(name, true);
                using (var rig = new RecoveredShaderRig(shader))
                {
                    rig.SaveFloats("_HLShaderTimeGame");
                    rig.SaveVectors("_LightDirection", "_ShadowBias");
                    Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                    Shader.SetGlobalVector("_LightDirection", new Vector4(0, 0, 1, 0));
                    Shader.SetGlobalVector("_ShadowBias", new Vector4(0.75f, 0.5f, 0, 0));
                    Material material = rig.Own(new Material(shader));
                    if (name == "HL_Gameplay_Collectable") material.SetFloat("_Rotation_Speed", Mathf.PI / 2);
                    Matrix4x4 model = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.identity, new Vector3(2, 3, 4));
                    Matrix4x4 rotated = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 4));
                    bool spins = name == "HL_Gameplay_Collectable";
                    float n = spins ? 1 / Mathf.Sqrt(21) : 1 / Mathf.Sqrt(3);
                    Color normal = spins ? new Color(4 * n, 2 * n, -n, 1) : new Color(n, n, n, 1);
                    Color turnedNormal = spins ? new Color(-2 * n, 4 * n, -n, 1) : new Color(-n, n, n, 1);
                    Color position = spins ? new Color(11, 13, 7, 1) : new Color(7, 13, 23, 1);
                    Color turnedPosition = spins ? new Color(-1, 13, 7, 1) : new Color(-1, 9, 23, 1);
                    // These literals follow the original input copies, spin, inverse transpose and normalization.
                    foreach (int pass in passes)
                    {
                        string[] fields = Fields(name, pass);
                        for (int field = 0; field < fields.Length; field++)
                        {
                            material.SetFloat("_LucidProbeField", field);
                            Color expected = Expected(fields[field], position, normal);
                            rig.Expect(material, expected, name + " pass " + pass + " " + fields[field], pass: pass, model: model);
                            if (fields[field] == "positionWS" || fields[field] == "normalWS")
                                rig.Expect(material, Expected(fields[field], turnedPosition, turnedNormal),
                                    name + " rotated pass " + pass + " " + fields[field], pass: pass, model: rotated);
                            if (fields[field] == "normalWS")
                            {
                                material.SetFloat("_LucidProbeZeroNormal", 1);
                                rig.Expect(material, new Color(0, 0, 0, 1), "zero normal remains zero", pass: pass, model: model);
                                material.SetFloat("_LucidProbeZeroNormal", 0);
                            }
                        }
                    }
                }
            }

            public void CheckDepth(string name)
            {
                Shader shader = Copy(name, false);
                using (var rig = new RecoveredShaderRig(shader))
                {
                    Material material = rig.Own(new Material(shader));
                    float depth = SystemInfo.usesReversedZBuffer ? 80f / 99f : 19f / 99f;
                    rig.Expect(material, new Color(depth, depth, depth, depth), name + " four depth channels", pass: 1);
                }
                string source = File.ReadAllText("Assets/Shaders/Recovered/" + name + ".shader");
                string depthPass = source.Substring(source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal));
                depthPass = depthPass.Substring(0, depthPass.IndexOf("ENDHLSL", StringComparison.Ordinal));
                Assert.That(Regex.IsMatch(depthPass,
                    @"\bfloat4\s+Frag\s*\(float4\s+positionCS\s*:\s*SV_POSITION\)"), Is.True,
                    "The original Depth fragment has a float4 result, independently of masked channels.");
            }

            private static Color Expected(string field, Color position, Color normal)
            {
                switch (field)
                {
                    case "uv0":
                    case "uv1": return new Color(0.25f, 0.5f, 0.75f, 1);
                    case "uv2": return new Color(0.875f, 0.625f, 0.375f, 0.125f);
                    case "color": return new Color(0.125f, 0.25f, 0.5f, 0.75f);
                    case "positionWS": return position;
                    case "normalWS": return normal;
                    default: throw new InvalidOperationException(field);
                }
            }

            private static string[] Fields(string name, int pass)
            {
                if (name == "HL_Char_Ghosted") return new[] { "uv", "viewDirWS", "normalWS", "viewRow" };
                if (name == "HL_Gameplay_Collectable") return new[] { "color", "positionWS", "normalWS" };
                if (name == "HL_Matcap")
                    return pass == 0 ? new[] { "uv1", "uv2", "color", "positionWS", "normalWS" } : new[] { "normalWS" };
                return pass == 1 ? new[] { "uv0", "color", "positionWS" } : new[] { "uv0", "color", "positionWS", "normalWS" };
            }

            private Shader Copy(string name, bool vertices)
            {
                string sourcePath = "Assets/Shaders/Recovered/" + name + ".shader";
                foreach (string path in new[] { sourcePath, sourcePath + ".meta" }) originals.Add(path, Hash(path));
                string source = File.ReadAllText(sourcePath);
                string candidate = "Assets/RecoveredStageContract_" + Guid.NewGuid().ToString("N");
                Assert.That(Directory.Exists(candidate) || File.Exists(candidate) || File.Exists(candidate + ".meta")
                    || Directory.Exists(candidate + ".meta"), Is.False);
                folder = candidate;
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                string guid = Guid.NewGuid().ToString("N");
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Empty);
                var shaderName = new Regex(@"Shader ""[^""]+""");
                Assert.That(shaderName.Matches(source).Count, Is.EqualTo(1));
                string copyName = "Hidden/RecoveredStageContract/" + guid;
                source = shaderName.Replace(source, "Shader \"" + copyName + "\"", 1);
                if (vertices)
                    source = source.Replace("    Properties\n    {", "    Properties\n    {\n        _LucidProbeField (\"Field\", Float) = 0\n        _LucidProbeZeroNormal (\"Zero normal\", Float) = 0");
                int pass = -1;
                source = Regex.Replace(source, @"HLSLPROGRAM(.*?)ENDHLSL", match =>
                {
                    pass++;
                    string program = match.Groups[1].Value;
                    bool selected = name == "HL_Char_Ghosted" ? pass == 1
                        : name == "HL_Gameplay_Collectable" ? pass != 0 : name == "HL_Matcap" ? pass != 1 : true;
                    if (!vertices || !selected) return match.Value;
                    // The owned Ghosted vertex-ID probe needs SM3.5; its production target stays unchanged.
                    if (name == "HL_Char_Ghosted") program = "\n#pragma target 3.5\n" + program;
                    program = program.Replace("#pragma vertex Vert", "#pragma vertex LucidProbeVert")
                        .Replace("#pragma fragment Frag", "#pragma fragment LucidProbeFrag");
                    var wrapper = new StringBuilder("\nfloat _LucidProbeField;\nfloat _LucidProbeZeroNormal;\nVaryings LucidProbeVert(uint id : SV_VertexID)\n{\n    Attributes input = (Attributes)0;\n");
                    wrapper.Append(name == "HL_Char_Ghosted" ? "    input.positionOS = float4(1, 2, 3, 1);\n"
                        : "    input.positionOS = float3(1, 2, 3);\n");
                    if (name != "HL_Particle_Trail" || pass != 1)
                        wrapper.Append("    input.normalOS = _LucidProbeZeroNormal == 1 ? float3(0, 0, 0) : float3(2, 3, 4);\n");
                    if (name != "HL_Char_Ghosted" && (name != "HL_Matcap" || pass == 0))
                        wrapper.Append("    input.color = float4(.125, .25, .5, .75);\n");
                    if (name == "HL_Char_Ghosted") wrapper.Append("    input.uv = float2(.25, .5);\n");
                    if (name == "HL_Particle_Trail") wrapper.Append("    input.uv0 = float4(.25, .5, .75, 1);\n");
                    if (name == "HL_Matcap" && pass == 0)
                        wrapper.Append("    input.uv1 = float4(.25, .5, .75, 1);\n    input.uv2 = float4(.875, .625, .375, .125);\n");
                    wrapper.Append("    Varyings output = Vert(input);\n    float2 corner = float2((id << 1) & 2, id & 2);\n    output.positionCS = float4(corner * 2 - 1, .5, 1);\n    return output;\n}\nfloat4 LucidProbeFrag(Varyings input) : SV_Target\n{\n");
                    string[] fields = Fields(name, pass);
                    for (int index = 0; index < fields.Length; index++)
                    {
                        string value = "input." + fields[index];
                        if (fields[index] == "positionWS" || fields[index] == "normalWS" || fields[index] == "viewDirWS")
                            value = "float4(" + value + ", 1)";
                        if (fields[index] == "uv") value = "float4(" + value + ", 0, 1)";
                        if (fields[index] == "viewRow") value = "float4(UNITY_MATRIX_V[2].xyz, 1)";
                        wrapper.Append("    if (_LucidProbeField == " + index + ") return " + value + ";\n");
                    }
                    wrapper.Append("    return float4(0, 0, 0, 0);\n}\n");
                    return "HLSLPROGRAM" + program + wrapper + "ENDHLSL";
                }, RegexOptions.Singleline);
                source = Regex.Replace(source, @"(?m)^\s*ColorMask [^\r\n]+", "            ColorMask RGBA");
                source = Regex.Replace(source, @"(?m)^\s*Blend [^\r\n]+", "            Blend One Zero");
                source = Regex.Replace(source, @"(?m)^\s*ZTest [^\r\n]+", "            ZTest Always");
                source = Regex.Replace(source, @"(?m)^\s*ZWrite [^\r\n]+", "            ZWrite Off");
                source = Regex.Replace(source, @"(?m)^\s*Cull [^\r\n]+", "            Cull Off");
                string copy = folder + "/Probe.shader";
                File.WriteAllText(copy, source, new UTF8Encoding(false));
                File.WriteAllText(copy + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(copy);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.name, Is.EqualTo(copyName));
                Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo(copy));
                Assert.That(AssetDatabase.AssetPathToGUID(copy), Is.EqualTo(guid));
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                    Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);
                return shader;
            }

            public void Cleanup()
            {
                var failures = new List<Exception>();
                try
                {
                    if (folder != null && (Directory.Exists(folder) || File.Exists(folder + ".meta")))
                        Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
                    Assert.That(folder == null || (!Directory.Exists(folder) && !File.Exists(folder + ".meta")), Is.True);
                }
                catch (Exception failure) { failures.Add(failure); }
                foreach (var original in originals)
                {
                    try { Assert.That(Hash(original.Key), Is.EqualTo(original.Value), original.Key); }
                    catch (Exception failure) { failures.Add(failure); }
                }
                if (failures.Count != 0) throw new AggregateException("Owned shader probe cleanup failed.", failures);
            }

            private static string Hash(string path)
            {
                using (var algorithm = SHA256.Create())
                using (var stream = File.OpenRead(path))
                    return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
