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
    public sealed class ParticleMainStageContractTests
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_Main.shader";
        private Fixture active;

        [Test]
        public void ParticleMainKeywordScopesAndPassMasksMatchOriginal()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            // Original m_KeywordNames, m_KeywordFlags and the six vertex/fragment masks, in table order.
            string[] names =
            {
                "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO", "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON", "FOG_LINEAR", "FOG_EXP", "FOG_EXP2",
                "_DEPTHFADE", "_GRADIENTMAPENABLE", "_DBUFFER_MRT1", "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_SURFACE_TYPE_TRANSPARENT", "_WRITE_RENDERING_LAYERS",
                "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_ALPHATEST_ON", "_GBUFFER_NORMALS_OCT",
                "_CASTING_PUNCTUAL_LIGHT_SHADOW",
            };
            byte[] flags = { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 };
            int[][] masks =
            {
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 },
                new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14 },
                new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 24 },
                new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 26 },
                new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 24 },
            };
            var expectedIndices = new HashSet<int>();
            var actualPassNames = new HashSet<string>();
            for (int pass = 0; pass < 3; pass++)
            {
                for (int stage = 0; stage < 2; stage++)
                {
                    ShaderType type = stage == 0 ? ShaderType.Vertex : ShaderType.Fragment;
                    var expectedNames = new List<string>();
                    foreach (int index in masks[pass * 2 + stage])
                    {
                        expectedNames.Add(names[index]);
                        expectedIndices.Add(index);
                    }
                    var actualNames = new List<string>();
                    foreach (LocalKeyword keyword in ShaderUtil.GetPassKeywords(shader, new PassIdentifier(0, (uint)pass), type, ShaderCompilerPlatform.Metal))
                    {
                        actualNames.Add(keyword.name);
                        actualPassNames.Add(keyword.name);
                    }
                    Assert.That(actualNames, Is.EquivalentTo(expectedNames), "Original Metal pass " + pass + " " + type + " keyword mask");
                }
            }
            foreach (int index in expectedIndices)
            {
                LocalKeyword keyword = shader.keywordSpace.FindKeyword(names[index]);
                Assert.That(keyword.isValid, Is.True, names[index]);
                Assert.That(keyword.isDynamic, Is.EqualTo((flags[index] & 2) != 0), names[index]);
                Assert.That(keyword.isOverridable, Is.EqualTo((flags[index] & 1) == 0), names[index]);
            }
            // Index25 is a global table name with no surviving pass mask; no pass or stripped body is invented for it.
            Assert.That(actualPassNames.Contains(names[25]), Is.False);
        }

        [Test]
        public void ParticleMainRetainsDecodedFoveatedDynamicKeyword()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            // The original keyword table marks this name dynamic (flag 0x02) on the shipped Metal path.
            LocalKeyword keyword = shader.keywordSpace.FindKeyword("_FOVEATED_RENDERING_NON_UNIFORM_RASTER");
            Assert.That(keyword.isValid, Is.True);
            Assert.That(keyword.isDynamic, Is.True);
        }

        [Test]
        public void ParticleMainVertexStagesKeepDecodedOutputs()
        {
            Fixture fixture = Begin();
            fixture.CheckVertexOutputs();
        }

        [Test]
        public void ParticleMainDepthFragmentKeepsFourFloatLanes()
        {
            Fixture fixture = Begin();
            fixture.CheckDepthOutput();
        }

        private Fixture Begin()
        {
            Assert.That(active, Is.Null, "A prior stage fixture still owns its resources.");
            active = new Fixture();
            active.Create();
            return active;
        }

        // Synchronous NUnit teardown remains the single owner if import, drawing or an assertion fails.
        [TearDown]
        public void DeleteOwnedStageProbeAndCheckSource()
        {
            Fixture fixture = active;
            if (fixture == null) return;
            try { fixture.Cleanup(); }
            finally { active = null; }
        }

        private sealed class Fixture
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly Dictionary<string, string> originals = new Dictionary<string, string>();
            private string folder;
            private Material material;
            private Mesh mesh;
            private RenderTexture target;
            private Texture2D sample;
            private string source;
            private string previousPipeline;
            private Vector4 previousBias, previousLight;
            private RenderTexture previousTarget;
            private bool borrowedState;

            public void Create()
            {
                foreach (string path in new[] { ShaderPath, ShaderPath + ".meta" }) originals.Add(path, Hash(path));
                source = File.ReadAllText(ShaderPath);
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
                previousPipeline = Shader.globalRenderPipeline;
                previousBias = Shader.GetGlobalVector("_ShadowBias");
                previousLight = Shader.GetGlobalVector("_LightDirection");
                previousTarget = RenderTexture.active;
                borrowedState = true;
                Shader.globalRenderPipeline = "UniversalPipeline";

                string candidate = "Assets/ParticleMainStageProbe_" + Guid.NewGuid().ToString("N");
                Assert.That(Directory.Exists(candidate) || File.Exists(candidate) || File.Exists(candidate + ".meta"), Is.False);
                folder = candidate;
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                string pathCopy = folder + "/StageProbe.shader";
                File.WriteAllText(pathCopy, DiagnosticSource(source), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(pathCopy, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(pathCopy);
                Assert.That(shader, Is.Not.Null);
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                    Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);
                Assert.That(shader.isSupported, Is.True);
                Assert.That(shader.passCount, Is.EqualTo(3));

                material = Own(new Material(shader));
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 0);
                mesh = Own(new Mesh());
                mesh.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100);
                target = Own(new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
                Assert.That(target.Create(), Is.True);
                Shader.SetGlobalVector("_ShadowBias", new Vector4(.25f, .5f, 0, 0));
                Shader.SetGlobalVector("_LightDirection", new Vector4(0, 0, 1, 0));
            }

            private T Own<T>(T value) where T : UnityEngine.Object
            {
                owned.Add(value);
                return value;
            }

            public void CheckVertexOutputs()
            {
                // Metal vertices 1–8, 41–44 and 51–54 copy uv/color and the unbiased world position.
                // Vertices 1–8 and 51–54 also normalize the inverse-transpose normal, including zero.
                Matrix4x4 unrotated = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.identity, new Vector3(2, 3, 4));
                Matrix4x4 rotated = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 4));
                float unit = 1f / Mathf.Sqrt(3f);
                material.SetVector("_LucidStageNormal", new Vector4(2, 3, 4, 0));
                for (int pass = 0; pass < 3; pass++)
                {
                    Expect(pass, 0, unrotated, new Color(.25f, .5f, .75f, 1), "UV copy");
                    Expect(pass, 1, unrotated, new Color(.125f, .25f, .5f, .75f), "color copy");
                    Expect(pass, 2, unrotated, new Color(7, 13, 23, 1), "unbiased world position");
                    Expect(pass, 2, rotated, new Color(-1, 9, 23, 1), "rotated unbiased world position");
                    if (pass == 1) continue;
                    Expect(pass, 3, unrotated, new Color(unit, unit, unit, 1), "inverse-transpose normalized normal");
                    Expect(pass, 3, rotated, new Color(-unit, unit, unit, 1), "rotated inverse-transpose normalized normal");
                    material.SetVector("_LucidStageNormal", Vector4.zero);
                    Expect(pass, 3, unrotated, new Color(0, 0, 0, 1), "safe zero normal");
                    material.SetVector("_LucidStageNormal", new Vector4(2, 3, 4, 0));
                }
            }

            public void CheckDepthOutput()
            {
                string depth = source.Substring(source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal));
                depth = depth.Substring(0, depth.IndexOf("ENDHLSL", StringComparison.Ordinal));
                // Pixel replication alone cannot distinguish an implicitly expanded scalar return type.
                Assert.That(Regex.IsMatch(depth, @"\bfloat4\s+Frag\s*\(\s*Varyings\s+\w+\s*\)\s*:\s*SV_Target\b"), Is.True,
                    "The shipped depth fragment has a float4 color declaration.");
                Expect(1, 4, Matrix4x4.identity, new Color(.25f, .25f, .25f, .25f), "four raw-depth lanes");
            }

            private void Expect(int pass, int output, Matrix4x4 model, Color expected, string label)
            {
                RenderTexture before = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    GL.Clear(true, true, new Color(-10, -10, -10, -10));
                    GL.PushMatrix();
                    try
                    {
                        GL.LoadProjectionMatrix(Matrix4x4.identity);
                        GL.modelview = Matrix4x4.identity;
                        material.SetFloat("_LucidStageOutput", output);
                        Assert.That(material.SetPass(pass), Is.True, "stage probe pass " + pass);
                        Graphics.DrawMeshNow(mesh, model);
                    }
                    finally { GL.PopMatrix(); }
                    sample.ReadPixels(new Rect(4, 4, 1, 1), 0, 0);
                    sample.Apply(false, false);
                    Color actual = sample.GetPixel(0, 0);
                    for (int lane = 0; lane < 4; lane++)
                        Assert.That(actual[lane], Is.EqualTo(expected[lane]).Within(.01f), "pass " + pass + " " + label + " lane " + lane);
                }
                finally { RenderTexture.active = before; }
            }

            private static string DiagnosticSource(string original)
            {
                string[] programs = original.Split(new[] { "HLSLPROGRAM" }, StringSplitOptions.None);
                Assert.That(programs, Has.Length.EqualTo(4));
                var result = new StringBuilder(programs[0].Replace("Shader \"HardLight/Particle/HL_Particle_Main\"", "Shader \"Hidden/ProjectLucid/ParticleMainStageProbe\""));
                for (int pass = 0; pass < 3; pass++)
                {
                    string program = programs[pass + 1];
                    int end = program.IndexOf("ENDHLSL", StringComparison.Ordinal);
                    Assert.That(end, Is.GreaterThan(0));
                    string body = program.Substring(0, end).Replace("#pragma vertex Vert", "#pragma vertex ProbeVert")
                        .Replace("#pragma fragment Frag", "#pragma fragment ProbeFrag");
                    result.Append("HLSLPROGRAM").Append(body).Append(@"
            float4 _LucidStageNormal;
            float _LucidStageOutput;
            Varyings ProbeVert(uint vertexID : SV_VertexID)
            {
                Attributes input = (Attributes)0;
                input.positionOS = float3(1, 2, 3);
                input.uv0 = float4(.25, .5, .75, 1);
                input.color = float4(.125, .25, .5, .75);
");
                    if (pass != 1) result.Append("                input.normalOS = _LucidStageNormal.xyz;\n");
                    result.Append(@"
                Varyings output = Vert(input);
                float2 corner = float2((vertexID << 1) & 2, vertexID & 2);
                float rawDepth = .25;
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                output.positionCS = float4(corner * 2 - 1, rawDepth, 1);
                return output;
            }
            float4 ProbeFrag(Varyings input) : SV_Target
            {
                if (_LucidStageOutput == 0) return input.uv0;
                if (_LucidStageOutput == 1) return input.color;
                if (_LucidStageOutput == 2) return float4(input.positionWS, 1);
");
                    if (pass != 1) result.Append("                return float4(input.normalWS, 1);\n");
                    else result.Append("                return Frag(input);\n");
                    result.Append("            }\n").Append(program.Substring(end));
                }
                // Expose the real stage results while removing only diagnostic raster-state restrictions.
                return result.ToString().Replace("ZTest [_ZTest]", "ZTest Always").Replace("ZTest LEqual", "ZTest Always")
                    .Replace("ZWrite [_ZWrite]", "ZWrite Off").Replace("ZWrite On", "ZWrite Off")
                    .Replace("Cull [_Cull]", "Cull Off").Replace("ColorMask R\n", "ColorMask RGBA\n")
                    .Replace("ColorMask 0\n", "ColorMask RGBA\n");
            }

            public void Cleanup()
            {
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                if (borrowedState)
                {
                    Attempt(() => RenderTexture.active = previousTarget);
                    Attempt(() => Shader.SetGlobalVector("_ShadowBias", previousBias));
                    Attempt(() => Shader.SetGlobalVector("_LightDirection", previousLight));
                    Attempt(() => Shader.globalRenderPipeline = previousPipeline);
                }
                for (int i = owned.Count - 1; i >= 0; i--)
                {
                    UnityEngine.Object item = owned[i];
                    Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                Attempt(() =>
                {
                    if (folder != null && (Directory.Exists(folder) || File.Exists(folder + ".meta")))
                        Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
                    Assert.That(folder == null || (!Directory.Exists(folder) && !File.Exists(folder + ".meta")), Is.True);
                });
                foreach (var original in originals)
                    Attempt(() => Assert.That(Hash(original.Key), Is.EqualTo(original.Value), original.Key));
                if (failures.Count != 0) throw new AggregateException("Owned stage probe cleanup failed.", failures);
            }
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
