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
    public sealed class EnvStandardLitContractTests
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Standard_Lit.shader";
        private Fixture active;

        [Test]
        public void EnvStandardLitKeywordScopesAndPassMasksMatchOriginal()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            // Literal original m_KeywordNames/Flags and all ten Metal stage masks, in table order.
            string[] names =
            {
                "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO", "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN", "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                "FOG_LINEAR", "FOG_EXP", "FOG_EXP2", "_USE_SCROLLING_UVS",
                "_RECEIVE_SHADOWS_OFF", "_SCREEN_SPACE_OCCLUSION", "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1", "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                "_LIGHT_LAYERS", "DEBUG_DISPLAY", "_LIGHT_COOKIES", "_SURFACE_TYPE_TRANSPARENT",
                "_WRITE_RENDERING_LAYERS", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_ALPHATEST_ON",
                "_SPECULAR_SETUP", "_USE_FRESNEL", "_USE_DETAIL_MAPS", "_MIXED_LIGHTING_SUBTRACTIVE",
                "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW", "EDITOR_VISUALIZATION",
            };
            byte[] flags = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[][] masks =
            {
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 23, 50 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 43, 45, 46 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 23 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 43, 45, 46 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 23 },
                new[] { 0, 1, 2, 3, 17, 18, 19, 40, 43, 45, 46 },
                new[] { 0, 1, 2, 3, 18, 23, 51 },
                new[] { 0, 1, 2, 3, 18, 43, 45, 46, 51 },
            };
            var activeIndices = new HashSet<int>();
            var actualUnion = new HashSet<string>();
            for (int pass = 0; pass < 5; pass++)
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
                    {
                        actual.Add(keyword.name);
                        actualUnion.Add(keyword.name);
                    }
                    Assert.That(actual, Is.EquivalentTo(expected), "Original Metal pass " + pass + " " + type + " mask");
                }
            }
            foreach (int index in activeIndices)
            {
                LocalKeyword keyword = shader.keywordSpace.FindKeyword(names[index]);
                Assert.That(keyword.isValid, Is.True, names[index]);
                Assert.That(keyword.isDynamic, Is.EqualTo((flags[index] & 2) != 0), names[index]);
                Assert.That(keyword.isOverridable, Is.EqualTo((flags[index] & 1) == 0), names[index]);
            }
            // These serialized table entries have no pass mask; no stripped bodies/pragmas are invented.
            foreach (int index in new[] { 47, 48, 49 }) Assert.That(actualUnion.Contains(names[index]), Is.False, names[index]);
        }

        [Test]
        public void EnvStandardLitCompilesEveryPassForOpenGLCore()
        {
            ProjectLucid.EnvStandardLitVerification.CompileForLinux();
        }

        [Test]
        public void EnvStandardLitVertexStagesKeepDecodedOutputs()
        {
            Fixture fixture = Begin();
            fixture.CheckVertexOutputs();
        }

        [Test]
        public void EnvStandardLitDepthFragmentKeepsFourFloatLanes()
        {
            Fixture fixture = Begin();
            fixture.CheckDepth();
        }

        [Test]
        public void EnvStandardLitFresnelUsesMappedNormalsAndGraphViewNormalization()
        {
            Fixture fixture = Begin();
            fixture.CheckFresnel();
        }

        [Test]
        public void EnvStandardLitDebugInputsKeepDeclaredLightmapAndSHFields()
        {
            Fixture fixture = Begin();
            fixture.CheckDebugInputs();
        }

        private Fixture Begin()
        {
            Assert.That(active, Is.Null, "A prior environment fixture still owns its assets.");
            active = new Fixture();
            active.Create();
            return active;
        }

        [TearDown]
        public void DeleteOwnedProbeAndCheckOriginals()
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
            private readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
            private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            private string folder, source, previousPipeline;
            private RenderTexture previousTarget, target;
            private Matrix4x4 previousView, view = Matrix4x4.identity;
            private Vector3 cameraPosition = new Vector3(1, 2.6f, 2.2f);
            private float ortho = 1;
            private Texture2D sample;
            private Material material;
            private Mesh mesh;
            private Camera camera;
            private bool borrowed;

            public void Create()
            {
                foreach (string path in new[] { ShaderPath, ShaderPath + ".meta" }) originals.Add(path, Hash(path));
                source = File.ReadAllText(ShaderPath);
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
                previousPipeline = Shader.globalRenderPipeline;
                previousTarget = RenderTexture.active;
                previousView = Shader.GetGlobalMatrix("unity_MatrixV");
                foreach (string name in new[] { "_GlobalMipBias", "_ShadowBias", "_LightDirection", "unity_OrthoParams",
                    "_WorldSpaceCameraPos", "unity_MetaVertexControl", "unity_MetaFragmentControl" })
                    vectors.Add(name, Shader.GetGlobalVector(name));
                foreach (string name in new[] { "_HLShaderTimeGame", "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace" })
                    floats.Add(name, Shader.GetGlobalFloat(name));
                borrowed = true;
                Shader.globalRenderPipeline = "UniversalPipeline";

                string candidate = "Assets/EnvStandardLitProbe_" + Guid.NewGuid().ToString("N");
                Assert.That(Directory.Exists(candidate) || File.Exists(candidate) || File.Exists(candidate + ".meta")
                    || Directory.Exists(candidate + ".meta"), Is.False);
                folder = candidate;
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                string guid = Guid.NewGuid().ToString("N");
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Empty);
                string copy = folder + "/Probe.shader";
                string shaderName = "Hidden/ProjectLucid/EnvStandardLitProbe/" + guid;
                File.WriteAllText(copy, DiagnosticSource(source, shaderName), new UTF8Encoding(false));
                File.WriteAllText(copy + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(copy);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.name, Is.EqualTo(shaderName));
                Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo(copy));
                Assert.That(AssetDatabase.AssetPathToGUID(copy), Is.EqualTo(guid));
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                    Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);
                Assert.That(shader.isSupported, Is.True);
                Assert.That(shader.passCount, Is.EqualTo(5));
                material = Own(new Material(shader));
                material.SetFloat("_SrcBlend", 1); material.SetFloat("_DstBlend", 0);
                material.SetVector("_LucidProbeNormal", new Vector4(1, 2, 3, 0));
                material.SetVector("_LucidProbeTangent", new Vector4(1, 1, 0, 2));
                mesh = Own(new Mesh());
                mesh.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100);
                target = Own(new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
                Assert.That(target.Create(), Is.True);
                GameObject cameraObject = Own(new GameObject("EnvStandardLitContractCamera"));
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = cameraPosition;
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                Shader.SetGlobalVector("_ShadowBias", new Vector4(.25f, .5f, 0, 0));
                Shader.SetGlobalVector("_LightDirection", new Vector4(0, 0, 1, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 2);
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(1, 0, 0, 0));
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 97);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
            }

            private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }

            private Texture2D Solid(Color color)
            {
                Texture2D texture = Own(new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true));
                texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Repeat;
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply(false, false);
                return texture;
            }

            public void CheckVertexOutputs()
            {
                Matrix4x4 model = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.identity, new Vector3(2, 3, 4));
                Matrix4x4 rotated = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 4));
                float n = 1 / Mathf.Sqrt(181), t = 1 / Mathf.Sqrt(13);
                material.SetTextureScale("_Diff_Tex", new Vector2(2, 3));
                material.SetTextureOffset("_Diff_Tex", new Vector2(.125f, -.25f));
                for (int pass = 0; pass < 5; pass++)
                {
                    Expect(pass, 7, model, new Color(.625f, 1.25f, 0, 1), "main UV");
                    material.EnableKeyword("_USE_SCROLLING_UVS");
                    material.SetFloat("_UV_Scrolling_Horizontal", .25f); material.SetFloat("_UV_Scrolling_Vertical", -.125f);
                    Expect(pass, 7, model, new Color(1.125f, 1, 0, 1), "game-time scrolling");
                    material.DisableKeyword("_USE_SCROLLING_UVS");
                    if (pass == 2) continue;
                    Expect(pass, 3, model, new Color(6*n, 8*n, 9*n, 1), "normalized inverse-transpose normal");
                    Expect(pass, 3, rotated, new Color(-8*n, 6*n, 9*n, 1), "rotated normalized normal");
                    if (pass == 1) continue;
                    Expect(pass, 0, model, new Color(.25f, .5f, .75f, 1), "all raw UV0 lanes");
                    Expect(pass, 4, model, new Color(2*t, 3*t, 0, 2), "normalized tangent/raw nonunit handedness");
                    Matrix4x4 mirrored = Matrix4x4.TRS(new Vector3(5, 7, 11), Quaternion.identity, new Vector3(-2, 3, 4));
                    Expect(pass, 4, mirrored, new Color(-2*t, 3*t, 0, 2), "handedness stays raw under negative scale");
                    if (pass == 3) continue;
                    Expect(pass, 1, model, new Color(.125f, .25f, .5f, .75f), "vertex color");
                    Expect(pass, 2, model, new Color(7, 13, 23, 1), "world position");
                    Expect(pass, 2, rotated, new Color(-1, 9, 23, 1), "rotated world position");
                    if (pass == 0) Expect(pass, 8, model, Color.clear, "retained variants publish zero vertex light/fog payload");
                    else
                    {
                        Expect(pass, 5, model, new Color(.875f, .625f, .375f, .125f), "all raw UV1 lanes");
                        Expect(pass, 6, model, new Color(.0625f, .1875f, .3125f, .4375f), "all raw UV2 lanes");
                    }
                }
            }

            public void CheckDepth()
            {
                string depth = source.Substring(source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal));
                depth = depth.Substring(0, depth.IndexOf("ENDHLSL", StringComparison.Ordinal));
                // An implicit scalar splat is numerically indistinguishable; this checks the known declaration separately.
                Assert.That(Regex.IsMatch(depth, @"\bfloat4\s+Frag\s*\(\s*Varyings\s+\w+\s*\)\s*:\s*SV_Target\b"), Is.True);
                Expect(2, 9, Matrix4x4.identity, new Color(.25f, .25f, .25f, .25f), "original four-channel raw depth");
                Assert.That(Regex.Matches(source, @"\bout\s+half4\s+(?:outColor|outNormalWS)\b|\bhalf4\s+Frag\s*\(").Count, Is.Zero,
                    "Known shipped fragment color outputs retain explicit float4 types; this is a declaration check.");
            }

            public void CheckFresnel()
            {
                material.SetVector("_LucidProbeNormal", new Vector4(0, 0, -1, 0));
                material.SetVector("_LucidProbeTangent", new Vector4(1, 0, 0, 1));
                material.SetColor("_Diff_Tint", Color.black); material.SetTexture("_Diff_Tex", Solid(Color.black));
                material.SetColor("_Fresnel_Colour", Color.white);
                material.SetColor("_Emissive_Colour", Color.black);
                material.SetFloat("_Fresnel_Amount", 1); material.SetFloat("_Fresnel_Intensity", 1); material.SetFloat("_Fresnel_Opacity", 1);
                material.SetTexture("_Normal_Tex", Solid(new Color(.8f, .5f, 1, 1)));
                material.SetTexture("_Detail_Normal_Tex", Solid(new Color(.5f, .8f, 1, 1)));
                material.SetTexture("_Detail_Diffuse_Tex", Solid(Color.white));
                material.EnableKeyword("_USE_FRESNEL");
                float length = Mathf.Sqrt(1.1296f);
                foreach (int pass in new[] { 0, 4 })
                {
                    ortho = 1; view.SetRow(2, new Vector4(0, .6f, -.8f, 0));
                    AdmitView(pass);
                    Expect(pass, 10, Matrix4x4.identity, new Color(.36f, .36f, .36f, 1), "base mapped normal Fresnel");
                    material.EnableKeyword("_USE_DETAIL_MAPS");
                    float detail = 1 - .152f / length;
                    Expect(pass, 10, Matrix4x4.identity, new Color(detail, detail, detail, 1), "summed-xy/product-z detail normal Fresnel");
                    material.SetVector("_LucidProbeTangent", new Vector4(1, 0, 0, 0));
                    float zeroW = 1 - .872f / length;
                    Expect(pass, 10, Matrix4x4.identity, new Color(zeroW, zeroW, zeroW, 1), "zero tangent.w selects negative handedness");
                    material.SetVector("_LucidProbeTangent", new Vector4(1, 0, 0, 2));
                    Expect(pass, 10, Matrix4x4.identity, new Color(detail, detail, detail, 1), "positive nonunit tangent.w selects positive sign");
                    material.SetVector("_LucidProbeTangent", new Vector4(1, 0, 0, 1));
                    RecordScaleInputs(pass, Matrix4x4.identity, "identity");
                    RecordScaleInputs(pass, Matrix4x4.Scale(new Vector3(-1, 1, 1)), "mirrored");
                    Expect(pass, 10, Matrix4x4.Scale(new Vector3(-1, 1, 1)), new Color(detail, detail, detail, 1), "negative object scale contributes exactly one sign");
                    view.SetRow(2, new Vector4(0, 1.2f, -1.6f, 0));
                    AdmitView(pass);
                    Expect(pass, 10, Matrix4x4.identity, new Color(detail, detail, detail, 1), "graph Fresnel normalizes a nonunit orthographic view");
                    if (pass == 0) Expect(pass, 12, Matrix4x4.identity, new Color(0, 1.2f, -1.6f, 1), "PBR retains its raw selected view");
                    foreach (float flag in new[] { 0f, -2f })
                    {
                        ortho = flag; AdmitView(pass);
                        Expect(pass, 10, Matrix4x4.identity, new Color(detail, detail, detail, 1), "original perspective/nonzero-orthographic branch");
                    }
                    material.DisableKeyword("_USE_DETAIL_MAPS");
                }
                material.EnableKeyword("_USE_DETAIL_MAPS"); ortho = 1;
                Expect(3, 11, Matrix4x4.identity, new Color(.6f/length, -.6f/length, -.64f/length, 0), "DepthNormals uses the original signed tangent frame");
                material.SetVector("_LucidProbeTangent", new Vector4(1, 0, 0, 0));
                Expect(3, 11, Matrix4x4.identity, new Color(.6f/length, .6f/length, -.64f/length, 0), "DepthNormals zero handedness is negative");
            }

            private void AdmitView(int pass)
            {
                Expect(pass, 13, Matrix4x4.identity, new Color(view.m20, view.m21, view.m22, 1), "actual published view row admission");
                Expect(pass, 14, Matrix4x4.identity, new Color(ortho, cameraPosition.x, cameraPosition.y, cameraPosition.z), "actual ortho flag/camera admission");
            }

            public void CheckDebugInputs()
            {
                material.EnableKeyword("DEBUG_DISPLAY");
                Expect(0, 31, Matrix4x4.identity, new Color(.125f, .25f, .5f, 1), "incoming SH and fragment DEBUG admission");
                Expect(0, 21, Matrix4x4.identity, new Color(.125f, .25f, .5f, 1), "DEBUG_DISPLAY vertex SH");
                material.EnableKeyword("LIGHTMAP_ON");
                Expect(0, 32, Matrix4x4.identity, new Color(.25f, .5f, 0, 1), "incoming static UV and fragment DEBUG admission");
                Expect(0, 22, Matrix4x4.identity, new Color(.25f, .5f, 0, 1), "DEBUG_DISPLAY static lightmap UV");
                material.EnableKeyword("DYNAMICLIGHTMAP_ON");
                Expect(0, 32, Matrix4x4.identity, new Color(.25f, .5f, 0, 1), "incoming static UV with dynamic lightmap and fragment DEBUG admission");
                Expect(0, 22, Matrix4x4.identity, new Color(.25f, .5f, 0, 1), "static UV with dynamic lightmap");
                Expect(0, 33, Matrix4x4.identity, new Color(.75f, .875f, 0, 1), "incoming dynamic UV and fragment DEBUG admission");
                Expect(0, 23, Matrix4x4.identity, new Color(.75f, .875f, 0, 1), "DEBUG_DISPLAY dynamic lightmap UV");
            }

            private void Expect(int pass, int field, Matrix4x4 model, Color expected, string label)
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
                        camera.worldToCameraMatrix = view;
                        GL.modelview = camera.worldToCameraMatrix;
                        Shader.SetGlobalMatrix("unity_MatrixV", view);
                        Shader.SetGlobalVector("unity_OrthoParams", new Vector4(0, 0, 0, ortho));
                        Shader.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
                        material.SetFloat("_LucidProbeField", field);
                        Assert.That(material.SetPass(pass), Is.True, "probe pass " + pass);
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

            // These are measured per-draw dependencies, not replacements for the original Fresnel oracle.
            private void RecordScaleInputs(int pass, Matrix4x4 model, string label)
            {
                for (int field = 15; field <= 17; field++)
                {
                    Color actual = ReadScaleInput(pass, field, model);
                    var format = System.Globalization.CultureInfo.InvariantCulture;
                    Debug.Log("EnvStandardLit scale input " + label + " pass " + pass + " field " + field
                        + " modelDet=" + model.determinant.ToString("R", format)
                        + " rgba=(" + actual.r.ToString("R", format) + "," + actual.g.ToString("R", format)
                        + "," + actual.b.ToString("R", format) + "," + actual.a.ToString("R", format) + ")");
                }
            }

            private Color ReadScaleInput(int pass, int field, Matrix4x4 model)
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
                        camera.worldToCameraMatrix = view;
                        GL.modelview = camera.worldToCameraMatrix;
                        Shader.SetGlobalMatrix("unity_MatrixV", view);
                        Shader.SetGlobalVector("unity_OrthoParams", new Vector4(0, 0, 0, ortho));
                        Shader.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
                        material.SetFloat("_LucidProbeField", field);
                        Assert.That(material.SetPass(pass), Is.True, "probe pass " + pass);
                        Graphics.DrawMeshNow(mesh, model);
                    }
                    finally { GL.PopMatrix(); }
                    sample.ReadPixels(new Rect(4, 4, 1, 1), 0, 0); sample.Apply(false, false);
                    Color actual = sample.GetPixel(0, 0);
                    return actual;
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
                string result = ReplaceOnce(original, "Shader \"HardLight/Environment/HL_Env_Standard_Lit\"", "Shader \"" + name + "\"");
                result = ReplaceOnce(result, "    Properties\n    {", "    Properties\n    {\n        _LucidProbeField (\"Field\", Float) = 0\n        _LucidProbeNormal (\"Normal\", Vector) = (1,2,3,0)\n        _LucidProbeTangent (\"Tangent\", Vector) = (1,1,0,2)");
                result = ReplaceOnce(result, "HLSLINCLUDE", "HLSLINCLUDE\n        float _LucidProbeField;\n        float4 _LucidProbeNormal;\n        float4 _LucidProbeTangent;");
                string[] parts = result.Split(new[] { "HLSLPROGRAM" }, StringSplitOptions.None);
                Assert.That(parts, Has.Length.EqualTo(6));
                var output = new StringBuilder(parts[0]);
                for (int pass = 0; pass < 5; pass++)
                {
                    string program = parts[pass + 1]; int end = program.IndexOf("ENDHLSL", StringComparison.Ordinal);
                    Assert.That(end, Is.GreaterThan(0)); string body = program.Substring(0, end);
                    body = ReplaceOnce(body, "#pragma vertex Vert", "#pragma vertex ProbeVert");
                    body = ReplaceOnce(body, "#pragma fragment Frag", "#pragma fragment ProbeFrag");
                    if (pass == 0)
                    {
                        // Read actual production InputData and graph albedo before lighting, without retyping their initialization.
                        string initialized = "                // As the generated pass: alpha test clips the graph alpha, transparency keeps it, and";
                        body = ReplaceOnce(body, initialized, initialized + @"
                #if defined(_WRITE_RENDERING_LAYERS)
                outRenderingLayers = 0;
                #endif
                if (_LucidProbeField == 12) { outColor = float4(inputData.viewDirectionWS, 1); return; }
                #if defined(DEBUG_DISPLAY)
                if (_LucidProbeField == 21) { outColor = float4(inputData.vertexSH, 1); return; }
                if (_LucidProbeField == 22) { outColor = float4(inputData.staticLightmapUV, 0, 1); return; }
                if (_LucidProbeField == 23) { outColor = float4(inputData.dynamicLightmapUV, 0, 1); return; }
                #endif
");
                        string albedo = "                surfaceData.albedo = HLEnvAlbedo(uv, uv0, input.color, graphNormalWS, inputData.viewDirectionWS);";
                        body = ReplaceOnce(body, albedo, albedo + "\n                if (_LucidProbeField == 10) { outColor = float4(surfaceData.albedo, 1); return; }");
                    }
                    var wrapper = new StringBuilder(@"
            Varyings ProbeVert(uint id : SV_VertexID)
            {
                Attributes input = (Attributes)0;
                input.positionOS = float3(1, 2, 3);
                input.uv0 = float4(.25, .5, .75, 1);
");
                    if (pass != 2) wrapper.Append("                input.normalOS = _LucidProbeNormal.xyz;\n");
                    if (pass == 0 || pass == 3 || pass == 4) wrapper.Append("                input.tangentOS = _LucidProbeTangent;\n");
                    if (pass == 0 || pass == 4) wrapper.Append("                input.color = float4(.125, .25, .5, .75);\n");
                    if (pass == 0) wrapper.Append("                input.staticLightmapUV = float4(.875, .625, .375, .125);\n                input.dynamicLightmapUV = float4(.0625, .1875, .3125, .4375);\n");
                    if (pass == 4) wrapper.Append("                input.uv1 = float4(.875, .625, .375, .125);\n                input.uv2 = float4(.0625, .1875, .3125, .4375);\n");
                    wrapper.Append("                Varyings output = Vert(input);\n");
                    if (pass == 0) wrapper.Append(@"
                // Fixture-only fragment inputs discriminate the pinned DEBUG_DISPLAY forwarding branches.
                // DEBUG_DISPLAY is fragment-only; publish its vertex inputs independently.
                #if defined(LIGHTMAP_ON)
                output.staticLightmapUV = float2(.25, .5);
                #else
                output.vertexSH = float3(.125, .25, .5);
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                output.dynamicLightmapUV = float2(.75, .875);
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
            float4 ProbeFrag(Varyings input) : SV_Target
            {
                if (_LucidProbeField == 7) return float4(input.uv, 0, 1);
                if (_LucidProbeField == 13) return float4(UNITY_MATRIX_V[2].xyz, 1);
                if (_LucidProbeField == 15) return float4(unity_WorldTransformParams.w, GetOddNegativeScale(), determinant((float3x3)GetObjectToWorldMatrix()), 1);
                if (_LucidProbeField == 14) return float4(unity_OrthoParams.w, _WorldSpaceCameraPos.xyz);
");
                    if (pass == 0 || pass == 3 || pass == 4) wrapper.Append("                if (_LucidProbeField == 16) return float4(input.normalWS, 1);\n                if (_LucidProbeField == 17) return input.tangentWS;\n");
                    if (pass != 1 && pass != 2) wrapper.Append("                if (_LucidProbeField == 0) return input.uv0;\n                if (_LucidProbeField == 4) return input.tangentWS;\n");
                    if (pass != 2) wrapper.Append("                if (_LucidProbeField == 3) return float4(input.normalWS, 1);\n");
                    if (pass == 0 || pass == 4) wrapper.Append("                if (_LucidProbeField == 1) return input.color;\n                if (_LucidProbeField == 2) return float4(input.positionWS, 1);\n");
                    if (pass == 0) wrapper.Append("                if (_LucidProbeField == 8) return input.fogFactorAndVertexLight;\n");
                    if (pass == 0) wrapper.Append(@"
                // Admit incoming varyings and fragment DEBUG selection before the production readbacks.
                #if defined(DEBUG_DISPLAY)
                const float debugSelected = 1;
                #else
                const float debugSelected = 0;
                #endif
                #if defined(LIGHTMAP_ON)
                if (_LucidProbeField == 32) return float4(input.staticLightmapUV, 0, debugSelected);
                #else
                if (_LucidProbeField == 31) return float4(input.vertexSH, debugSelected);
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                if (_LucidProbeField == 33) return float4(input.dynamicLightmapUV, 0, debugSelected);
                #endif
");
                    if (pass == 4) wrapper.Append("                if (_LucidProbeField == 5) return input.uv1;\n                if (_LucidProbeField == 6) return input.uv2;\n");
                    if (pass == 0 || pass == 3) wrapper.Append(@"
                float4 color;
                #if defined(_WRITE_RENDERING_LAYERS)
                float4 layers; Frag(input, color, layers);
                #else
                Frag(input, color);
                #endif
                return color;
");
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
                    foreach (var item in vectors) Attempt(() => Shader.SetGlobalVector(item.Key, item.Value));
                    foreach (var item in floats) Attempt(() => Shader.SetGlobalFloat(item.Key, item.Value));
                    Attempt(() => Shader.globalRenderPipeline = previousPipeline);
                }
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index];
                    Attempt(() => { if (item != null) UnityEngine.Object.DestroyImmediate(item); });
                }
                Attempt(() =>
                {
                    if (folder != null && (Directory.Exists(folder) || File.Exists(folder + ".meta")))
                        Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
                    Assert.That(folder == null || (!Directory.Exists(folder) && !File.Exists(folder + ".meta")), Is.True);
                });
                foreach (var item in originals) Attempt(() => Assert.That(Hash(item.Key), Is.EqualTo(item.Value), item.Key));
                if (failures.Count != 0) throw new AggregateException("Owned environment contract cleanup failed.", failures);
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
