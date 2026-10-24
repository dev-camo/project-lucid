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
    public sealed class CharStandardContractTests
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Standard.shader";
        private Fixture active;

        [Test]
        public void CharStandardKeywordScopesAndPassMasksMatchOriginal()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            // Literal original52-name table and all ten Metal masks;49 names are used by a surviving pass.
            string[] names =
            {
                "STEREO_INSTANCING_ON",
                "UNITY_SINGLE_PASS_STEREO",
                "STEREO_MULTIVIEW_ON",
                "STEREO_CUBEMAP_RENDER_ON",
                "LIGHTMAP_ON",
                "DYNAMICLIGHTMAP_ON",
                "DIRLIGHTMAP_COMBINED",
                "_MAIN_LIGHT_SHADOWS",
                "_MAIN_LIGHT_SHADOWS_CASCADE",
                "_MAIN_LIGHT_SHADOWS_SCREEN",
                "_ADDITIONAL_LIGHTS_VERTEX",
                "_ADDITIONAL_LIGHTS",
                "LIGHTMAP_SHADOW_MIXING",
                "SHADOWS_SHADOWMASK",
                "_FORWARD_PLUS",
                "EVALUATE_SH_MIXED",
                "EVALUATE_SH_VERTEX",
                "DOTS_INSTANCING_ON",
                "_FOVEATED_RENDERING_NON_UNIFORM_RASTER",
                "INSTANCING_ON",
                "FOG_LINEAR",
                "FOG_EXP",
                "FOG_EXP2",
                "_RECEIVE_SHADOWS_OFF",
                "_USE_BALLMODE",
                "_USE_SUBSURFACE_SCATTERING",
                "_USE_MATCAP",
                "_SCREEN_SPACE_OCCLUSION",
                "_ADDITIONAL_LIGHT_SHADOWS",
                "_REFLECTION_PROBE_BLENDING",
                "_REFLECTION_PROBE_BOX_PROJECTION",
                "_SHADOWS_SOFT",
                "_SHADOWS_SOFT_LOW",
                "_SHADOWS_SOFT_MEDIUM",
                "_SHADOWS_SOFT_HIGH",
                "_DBUFFER_MRT1",
                "_DBUFFER_MRT2",
                "_DBUFFER_MRT3",
                "_LIGHT_LAYERS",
                "DEBUG_DISPLAY",
                "_LIGHT_COOKIES",
                "_SURFACE_TYPE_TRANSPARENT",
                "_WRITE_RENDERING_LAYERS",
                "_ALPHAPREMULTIPLY_ON",
                "_ALPHAMODULATE_ON",
                "_ALPHATEST_ON",
                "_SPECULAR_SETUP",
                "_MIXED_LIGHTING_SUBTRACTIVE",
                "_GBUFFER_NORMALS_OCT",
                "_RENDER_PASS_ENABLED",
                "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                "EDITOR_VISUALIZATION",
            };
            byte[] flags = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[][] masks =
            {
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 24, 25, 26, 50 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 24, 25, 26, 45 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 24, 25, 26 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 24, 25, 26, 45 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 24, 25, 26 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 24, 25, 26, 42, 45 },
                new[] { 0, 1, 2, 3, 18, 24, 25, 26, 51 },
                new[] { 0, 1, 2, 3, 18, 24, 25, 26, 45, 51 },
            };
            var indices = new HashSet<int>();
            var union = new HashSet<string>();
            for (int pass = 0; pass < 5; pass++)
            {
                for (int stage = 0; stage < 2; stage++)
                {
                    ShaderType type = stage == 0 ? ShaderType.Vertex : ShaderType.Fragment;
                    var expected = new List<string>();
                    foreach (int index in masks[pass * 2 + stage]) { expected.Add(names[index]); indices.Add(index); }
                    var actual = new List<string>();
                    foreach (LocalKeyword keyword in ShaderUtil.GetPassKeywords(shader,
                        new PassIdentifier(0, (uint)pass), type, ShaderCompilerPlatform.Metal))
                    { actual.Add(keyword.name); union.Add(keyword.name); }
                    Assert.That(actual, Is.EquivalentTo(expected), "Original Metal pass " + pass + " " + type);
                }
            }
            Assert.That(indices.Count, Is.EqualTo(49));
            foreach (int index in indices)
            {
                LocalKeyword keyword = shader.keywordSpace.FindKeyword(names[index]);
                Assert.That(keyword.isValid, Is.True, names[index]);
                Assert.That(keyword.isDynamic, Is.EqualTo((flags[index] & 2) != 0), names[index]);
                Assert.That(keyword.isOverridable, Is.EqualTo((flags[index] & 1) == 0), names[index]);
            }
            // These table names have no surviving pass mask; no stripped body/active pragma is fabricated.
            foreach (int index in new[] { 47, 48, 49 }) Assert.That(union.Contains(names[index]), Is.False, names[index]);
        }

        [Test]
        public void CharStandardCompilesEveryPassForOpenGLCore()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            string[][] sets =
            {
                new string[0],
                new[] { "DOTS_INSTANCING_ON", "_USE_MATCAP", "_USE_SUBSURFACE_SCATTERING", "_USE_BALLMODE" },
                new[] { "DEBUG_DISPLAY", "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON" },
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAMODULATE_ON", "_SPECULAR_SETUP" },
                new[] { "INSTANCING_ON", "_ALPHATEST_ON", "_WRITE_RENDERING_LAYERS", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            };
            int requests = 0;
            for (int pass = 0; pass < subshader.PassCount; pass++)
                foreach (string[] keywords in sets)
                    foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        ShaderData.VariantCompileInfo info = subshader.GetPass(pass).CompileVariant(stage, keywords,
                            ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64);
                        var errors = new List<string>();
                        foreach (ShaderMessage message in info.Messages)
                            if (message.severity == ShaderCompilerMessageSeverity.Error) errors.Add(message.message);
                        Assert.That(info.Success && errors.Count == 0, Is.True,
                            "OpenGLCore pass " + pass + " " + stage + " [" + string.Join(" ", keywords) + "] " + string.Join(" ", errors));
                        requests++;
                    }
            Assert.That(requests, Is.EqualTo(50));
        }

        [Test] public void CharStandardVertexStagesKeepDecodedOutputs() { Begin().CheckVertexOutputs(); }
        [Test] public void CharStandardFragmentStagesKeepDecodedFloatOutputs() { Begin().CheckFragmentOutputs(); }
        [Test] public void CharStandardDebugInputsKeepDeclaredLightmapAndSHFields() { Begin().CheckDebugInputs(); }
        [Test] public void CharStandardAlphaModulateUsesPinnedPreparation() { Begin().CheckAlphaModulate(); }
        [Test] public void CharStandardSetupFailureRestoresOwnedResources() { CharStandardVerification.VerifyResourceRestorationAfterFailure("setup"); }
        [Test] public void CharStandardBodyFailureRestoresOwnedResources() { CharStandardVerification.VerifyResourceRestorationAfterFailure("body"); }

        private Fixture Begin()
        {
            Assert.That(active, Is.Null, "A previous character fixture still owns resources.");
            active = new Fixture(); active.Create(); return active;
        }

        [TearDown]
        public void DeleteOwnedProbeAndCheckOriginals()
        {
            Fixture fixture = active;
            if (fixture == null) return;
            try { fixture.Cleanup(); } finally { active = null; }
        }

        private sealed class Fixture
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly Dictionary<string, string> originals = new Dictionary<string, string>();
            private readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
            private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            private string folder, source, previousPipeline;
            private RenderTexture previousTarget, target;
            private Matrix4x4 previousView, previousVP;
            private Texture2D sample;
            private Material material;
            private Mesh mesh;
            private bool borrowed;

            public void Create()
            {
                foreach (string path in new[] { ShaderPath, ShaderPath + ".meta" }) originals.Add(path, Hash(path));
                source = File.ReadAllText(ShaderPath);
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
                previousPipeline = Shader.globalRenderPipeline; previousTarget = RenderTexture.active;
                previousView = Shader.GetGlobalMatrix("unity_MatrixV");
                previousVP = Shader.GetGlobalMatrix("unity_MatrixVP");
                foreach (string name in new[] { "_GlobalMipBias", "_ShadowBias", "_LightDirection", "_WorldSpaceCameraPos",
                    "unity_OrthoParams", "unity_MetaVertexControl", "unity_MetaFragmentControl" }) vectors.Add(name, Shader.GetGlobalVector(name));
                foreach (string name in new[] { "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace" }) floats.Add(name, Shader.GetGlobalFloat(name));
                borrowed = true;
                string candidate = "Assets/CharStandardContract_" + Guid.NewGuid().ToString("N");
                Assert.That(Present(candidate) || Present(candidate + ".meta"), Is.False);
                folder = candidate;
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                string guid = Guid.NewGuid().ToString("N");
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Empty);
                string copy = folder + "/Probe.shader", nameCopy = "Hidden/ProjectLucid/CharStandardContract/" + guid;
                File.WriteAllText(copy, DiagnosticSource(source, nameCopy), new UTF8Encoding(false));
                File.WriteAllText(copy + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(copy);
                Assert.That(shader, Is.Not.Null); Assert.That(shader.name, Is.EqualTo(nameCopy));
                Assert.That(AssetDatabase.AssetPathToGUID(copy), Is.EqualTo(guid));
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                    Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);
                Shader.globalRenderPipeline = "UniversalPipeline";
                Assert.That(shader.isSupported, Is.True); Assert.That(shader.passCount, Is.EqualTo(5));
                material = Own(new Material(shader)); material.SetFloat("_SrcBlend", 1); material.SetFloat("_DstBlend", 0);
                material.SetVector("_LucidProbeNormal", new Vector4(1, 2, 3, 0));
                material.SetVector("_LucidProbeTangent", new Vector4(1, 1, 0, 2));
                mesh = Own(new Mesh()); mesh.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                mesh.triangles = new[] { 0, 1, 2 }; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100);
                target = Own(new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)); Assert.That(target.Create(), Is.True);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                Shader.SetGlobalVector("_ShadowBias", new Vector4(.25f, .5f, 0, 0));
                Shader.SetGlobalVector("_LightDirection", new Vector4(0, 0, 1, 0));
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(1, 0, 0, 0));
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1); Shader.SetGlobalFloat("unity_MaxOutputValue", 97); Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
            }
            private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
            private Texture2D Solid(Color color)
            {
                Texture2D texture = Own(new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true));
                texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Repeat;
                texture.SetPixels(new[] { color, color, color, color }); texture.Apply(false, false); return texture;
            }

            public void CheckVertexOutputs()
            {
                Matrix4x4 model = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.identity, new Vector3(2, 3, 4));
                Matrix4x4 rotated = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 4));
                float n = 1 / Mathf.Sqrt(181), t = 1 / Mathf.Sqrt(13);
                for (int pass = 0; pass < 5; pass++)
                {
                    Expect(pass, 0, model, new Color(.25f, .5f, .75f, 1), "all four UV0 lanes");
                    if (pass != 2)
                    {
                        Expect(pass, 3, model, new Color(6*n, 8*n, 9*n, 1), "inverse-transpose normalized normal");
                        Expect(pass, 3, rotated, new Color(-8*n, 6*n, 9*n, 1), "rotated normal");
                        material.SetVector("_LucidProbeNormal", Vector4.zero);
                        Expect(pass, 3, model, new Color(0, 0, 0, 1), "safe zero normal");
                        material.SetVector("_LucidProbeNormal", new Vector4(1, 2, 3, 0));
                    }
                    if (pass == 0 || pass == 3 || pass == 4)
                    {
                        Expect(pass, 4, model, new Color(2*t, 3*t, 0, 2), "normalized tangent with raw handedness");
                        Expect(pass, 4, rotated, new Color(-3*t, 2*t, 0, 2), "rotated tangent");
                    }
                    if (pass == 0 || pass == 4)
                    {
                        Expect(pass, 5, model, new Color(7, 13, 23, 1), "world position");
                        Expect(pass, 5, rotated, new Color(-1, 9, 23, 1), "rotated world position");
                    }
                    if (pass == 0) Expect(pass, 6, model, Color.clear, "retained fog/vertex-light float4 payload");
                    if (pass == 4)
                    {
                        Expect(pass, 1, model, new Color(.875f, .625f, .375f, .125f), "all four UV1 lanes");
                        Expect(pass, 2, model, new Color(.0625f, .1875f, .3125f, .4375f), "all four UV2 lanes");
                    }
                    if (pass == 1)
                    {
                        for (int row = 0; row < 4; row++)
                        {
                            Vector4 expected = Matrix4x4.identity.GetRow(row);
                            Expect(pass, 9 + row, model, new Color(expected.x, expected.y, expected.z, expected.w),
                                "actual vertex-stage VP row admission " + row);
                        }
                        float normalBias = .5f * (1 - 9*n);
                        Expect(pass, 7, model, new Color(7 + 6*n*normalBias, 13 + 8*n*normalBias,
                            SystemInfo.usesReversedZBuffer ? 1 : 23.25f + 9*n*normalBias, 1), "actual biased clip before diagnostic coverage override");
                    }
                }
            }
            public void CheckFragmentOutputs()
            {
                string depth = source.Substring(source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal));
                depth = depth.Substring(0, depth.IndexOf("ENDHLSL", StringComparison.Ordinal));
                // A scalar splat can match pixels; explicit known float4 stage types are checked separately.
                Assert.That(Regex.IsMatch(depth, @"\bfloat4\s+Frag\s*\(\s*Varyings\s+\w+\s*\)\s*:\s*SV_Target\b"), Is.True);
                Assert.That(Regex.Matches(source, @"\bout\s+half4\s+(?:outColor|outNormalWS)\b|\bhalf4\s+Frag\s*\(").Count, Is.Zero);
                Assert.That(source.Contains("float4 fogFactorAndVertexLight"), Is.True);
                Expect(2, 8, Matrix4x4.identity, new Color(.25f, .25f, .25f, .25f), "Depth raw zzzz");
                Expect(1, 8, Matrix4x4.identity, Color.clear, "Shadow float4 zero");
            }
            public void CheckDebugInputs()
            {
                material.EnableKeyword("DEBUG_DISPLAY");
                Expect(0, 21, Matrix4x4.identity, new Color(.125f, .25f, .5f, 1), "pinned DEBUG vertexSH route");
                material.EnableKeyword("LIGHTMAP_ON");
                Expect(0, 22, Matrix4x4.identity, new Color(.25f, .5f, 0, 1), "pinned DEBUG static UV route");
                material.EnableKeyword("DYNAMICLIGHTMAP_ON");
                Expect(0, 22, Matrix4x4.identity, new Color(.25f, .5f, 0, 1), "static UV with dynamic GI");
                Expect(0, 23, Matrix4x4.identity, new Color(.75f, .875f, 0, 1), "pinned DEBUG dynamic UV route");
            }
            public void CheckAlphaModulate()
            {
                material.SetTexture("_Diff_Texture", Solid(new Color(.2f, .4f, .6f, 1)));
                material.SetColor("_Diff_Colour", Color.white); material.SetTexture("_ID_Mask", Solid(new Color(0, 0, .25f, 0)));
                material.SetTexture("_Norm_Texture", Solid(new Color(.5f, .5f, 1, 1)));
                material.SetTexture("_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D", Solid(Color.black));
                material.SetFloat("_Rim_Opacity", 0); material.SetFloat("_Erosion_Progress", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHAMODULATE_ON");
                // Pinned PBRForwardPass/AlphaModulate contract, not a claim about an unavailable shipped multiply body.
                Expect(0, 24, Matrix4x4.identity, new Color(.8f, .85f, .9f, .25f), "multiply preparation before decals/PBR");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                Expect(0, 24, Matrix4x4.identity, new Color(.2f, .4f, .6f, .25f), "ordinary albedo preparation");
            }

            private void Expect(int pass, int field, Matrix4x4 model, Color expected, string label)
            {
                RenderTexture before = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; GL.Clear(true, true, new Color(-10, -10, -10, -10)); GL.PushMatrix();
                    try
                    {
                        GL.LoadProjectionMatrix(Matrix4x4.identity); GL.modelview = Matrix4x4.identity;
                        Shader.SetGlobalMatrix("unity_MatrixV", Matrix4x4.identity);
                        Shader.SetGlobalMatrix("unity_MatrixVP", Matrix4x4.identity);
                        Shader.SetGlobalVector("unity_OrthoParams", new Vector4(0, 0, 0, 1));
                        Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0, 0, -10000, 0));
                        material.SetFloat("_LucidProbeField", field); Assert.That(material.SetPass(pass), Is.True);
                        Graphics.DrawMeshNow(mesh, model);
                    }
                    finally { GL.PopMatrix(); }
                    sample.ReadPixels(new Rect(4, 4, 1, 1), 0, 0); sample.Apply(false, false);
                    Color actual = sample.GetPixel(0, 0);
                    for (int lane = 0; lane < 4; lane++)
                        Assert.That(actual[lane], Is.EqualTo(expected[lane]).Within(.01f), label + " pass " + pass + " lane " + lane);
                }
                finally { RenderTexture.active = before; }
            }
            private static string ReplaceOnce(string source, string oldValue, string newValue)
            {
                Assert.That(source.Split(new[] { oldValue }, StringSplitOptions.None), Has.Length.EqualTo(2), oldValue);
                return source.Replace(oldValue, newValue);
            }
            private static string DiagnosticSource(string original, string name)
            {
                string result = ReplaceOnce(original, "Shader \"HardLight/Character/HL_Char_Standard\"", "Shader \"" + name + "\"");
                result = ReplaceOnce(result, "    Properties\n    {", "    Properties\n    {\n        _LucidProbeField (\"Field\", Float) = 0\n        _LucidProbeNormal (\"Normal\", Vector) = (1,2,3,0)\n        _LucidProbeTangent (\"Tangent\", Vector) = (1,1,0,2)");
                result = ReplaceOnce(result, "HLSLINCLUDE", "HLSLINCLUDE\n        float _LucidProbeField;\n        float4 _LucidProbeNormal;\n        float4 _LucidProbeTangent;");
                string[] parts = result.Split(new[] { "HLSLPROGRAM" }, StringSplitOptions.None);
                Assert.That(parts, Has.Length.EqualTo(6)); var output = new StringBuilder(parts[0]);
                for (int pass = 0; pass < 5; pass++)
                {
                    string program = parts[pass + 1]; int end = program.IndexOf("ENDHLSL", StringComparison.Ordinal);
                    Assert.That(end, Is.GreaterThan(0)); string body = program.Substring(0, end);
                    body = ReplaceOnce(body, "#pragma vertex Vert", "#pragma vertex ProbeVert");
                    body = ReplaceOnce(body, "#pragma fragment Frag", "#pragma fragment ProbeFrag");
                    int varying = body.IndexOf("struct Varyings", StringComparison.Ordinal);
                    int opening = body.IndexOf('{', varying);
                    body = body.Insert(opening + 1, "\n                float4 lucidClipCS : TEXCOORD15;\n");
                    if (pass == 1)
                        body = body.Insert(opening + 1, "\n                float4 lucidVP0 : TEXCOORD11;\n                float4 lucidVP1 : TEXCOORD12;\n                float4 lucidVP2 : TEXCOORD13;\n                float4 lucidVP3 : TEXCOORD14;\n");
                    if (pass == 0)
                    {
                        body = ReplaceOnce(body, "                SurfaceData surfaceData = (SurfaceData)0;", @"
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = 0;
                #endif
                #if defined(DEBUG_DISPLAY)
                if (_LucidProbeField == 21) { outColor = float4(inputData.vertexSH, 1); return; }
                if (_LucidProbeField == 22) { outColor = float4(inputData.staticLightmapUV, 0, 1); return; }
                if (_LucidProbeField == 23) { outColor = float4(inputData.dynamicLightmapUV, 0, 1); return; }
                #endif
                SurfaceData surfaceData = (SurfaceData)0;");
                        body = ReplaceOnce(body, "                #if defined(_DBUFFER)", @"
                if (_LucidProbeField == 24) { outColor = float4(surfaceData.albedo, surfaceData.alpha); return; }
                #if defined(_DBUFFER)");
                    }
                    var wrapper = new StringBuilder(@"
            Varyings ProbeVert(uint id : SV_VertexID)
            {
                Attributes input = (Attributes)0;
                input.positionOS = float4(1, 2, 3, 1);
                input.uv0 = float4(.25, .5, .75, 1);");
                    if (pass != 2) wrapper.Append("                input.normalOS = _LucidProbeNormal.xyz;\n");
                    if (pass == 0 || pass == 3 || pass == 4) wrapper.Append("                input.tangentOS = _LucidProbeTangent;\n");
                    if (pass == 4) wrapper.Append("                input.uv1 = float4(.875, .625, .375, .125);\n                input.uv2 = float4(.0625, .1875, .3125, .4375);\n");
                    wrapper.Append(@"
                Varyings output = Vert(input);
                output.lucidClipCS = output.positionCS;");
                    if (pass == 1) wrapper.Append(@"
                output.lucidVP0 = UNITY_MATRIX_VP[0];
                output.lucidVP1 = UNITY_MATRIX_VP[1];
                output.lucidVP2 = UNITY_MATRIX_VP[2];
                output.lucidVP3 = UNITY_MATRIX_VP[3];
");
                    if (pass == 0) wrapper.Append(@"
                // Fixture-only inputs discriminate the actual pinned DEBUG forwarding assignments.
                #if defined(DEBUG_DISPLAY)
                #if defined(LIGHTMAP_ON)
                output.staticLightmapUV = float2(.25, .5);
                #else
                output.vertexSH = float3(.125, .25, .5);
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = float2(.75, .875);
                #endif
                #endif
");
                    wrapper.Append(@"
                float2 corner = float2((id << 1) & 2, id & 2);
                float rawDepth = .25;
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                output.positionCS = float4(corner * 2 - 1, rawDepth, 1);
                return output;
            }
            float4 ProbeFrag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                if (_LucidProbeField == 0) return input.uv;
                if (_LucidProbeField == 7) return input.lucidClipCS;");
                    if (pass == 1) wrapper.Append(@"
                if (_LucidProbeField == 9) return input.lucidVP0;
                if (_LucidProbeField == 10) return input.lucidVP1;
                if (_LucidProbeField == 11) return input.lucidVP2;
                if (_LucidProbeField == 12) return input.lucidVP3;
");
                    if (pass != 2) wrapper.Append("                if (_LucidProbeField == 3) return float4(input.normalWS, 1);\n");
                    if (pass == 0 || pass == 3 || pass == 4) wrapper.Append("                if (_LucidProbeField == 4) return input.tangentWS;\n");
                    if (pass == 0 || pass == 4) wrapper.Append("                if (_LucidProbeField == 5) return float4(input.positionWS, 1);\n");
                    if (pass == 0) wrapper.Append("                if (_LucidProbeField == 6) return input.fogFactorAndVertexLight;\n");
                    if (pass == 4) wrapper.Append("                if (_LucidProbeField == 1) return input.uv1;\n                if (_LucidProbeField == 2) return input.uv2;\n");
                    if (pass == 0 || pass == 3) wrapper.Append(@"
                float4 color;
                #if defined(_WRITE_RENDERING_LAYERS)
                float4 layers; Frag(input, isFrontFace, color, layers);
                #else
                Frag(input, isFrontFace, color);
                #endif
                return color;");
                    else if (pass == 4) wrapper.Append("                return Frag(input, isFrontFace);\n");
                    else wrapper.Append("                return Frag(input);\n");
                    wrapper.Append("            }\n");
                    output.Append("HLSLPROGRAM").Append(body).Append(wrapper).Append(program.Substring(end));
                }
                result = output.ToString();
                result = Regex.Replace(result, @"(?m)^\s*ColorMask [^\r\n]+", "            ColorMask RGBA");
                result = Regex.Replace(result, @"(?m)^\s*Blend [^\r\n]+", "            Blend One Zero");
                result = Regex.Replace(result, @"(?m)^\s*ZTest [^\r\n]+", "            ZTest Always");
                result = Regex.Replace(result, @"(?m)^\s*ZWrite [^\r\n]+", "            ZWrite Off");
                return Regex.Replace(result, @"(?m)^\s*Cull [^\r\n]+", "            Cull Off");
            }

            public void Cleanup()
            {
                var failures = new List<Exception>();
                void Attempt(Action action) { try { action(); } catch (Exception failure) { failures.Add(failure); } }
                if (borrowed)
                {
                    Attempt(() => RenderTexture.active = previousTarget);
                    Attempt(() => Shader.SetGlobalMatrix("unity_MatrixV", previousView));
                    Attempt(() => Shader.SetGlobalMatrix("unity_MatrixVP", previousVP));
                    foreach (var item in vectors) Attempt(() => Shader.SetGlobalVector(item.Key, item.Value));
                    foreach (var item in floats) Attempt(() => Shader.SetGlobalFloat(item.Key, item.Value));
                    Attempt(() => Shader.globalRenderPipeline = previousPipeline);
                }
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index]; Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                Attempt(() =>
                {
                    if (folder == null) return;
                    foreach (string path in new[] { folder, folder + ".meta" })
                        if (Present(path))
                            Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, Is.True, path);
                    if (Present(folder) || Present(folder + ".meta")) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
                    Assert.That(Present(folder) || Present(folder + ".meta"), Is.False);
                });
                foreach (var item in originals) Attempt(() => Assert.That(Hash(item.Key), Is.EqualTo(item.Value), item.Key));
                if (failures.Count != 0) throw new AggregateException("Owned character contract cleanup failed.", failures);
            }
            private static bool Present(string path)
            {
                try { File.GetAttributes(path); return true; }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
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
