using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid.Tests
{
    public sealed class FullscreenShaderStageContractTests
    {
        private const string ProbePath = "Assets/Tests/Editor/FullscreenVertexContract.shader";
        private const string SharedPath = "Assets/Shaders/Recovered/HL_FullScreen_Effects.hlsl";
        private Fixture active;

        [Test]
        public void FullscreenVertexEntriesKeepDecodedViewDirections()
        {
            Assert.That(active, Is.Null, "A prior fullscreen fixture still owns its resources.");
            active = new Fixture();
            active.Run();
        }

        [Test]
        public void FullscreenAndBlurKeepDecodedKeywordMasksAndScopes()
        {
            // The original serialized keyword tables and every surviving Metal stage mask.
            string[] names =
            {
                "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO", "STEREO_MULTIVIEW_ON",
                "STEREO_CUBEMAP_RENDER_ON", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER",
            };
            byte[] flags = { 0, 0, 0, 0, 2 };
            CheckKeywords("Assets/Shaders/Recovered/HL_FullScreen_Effects.shader",
                "3518f69d26ca6d140b0b1b2d67fc8dea", 2, names, flags);
            CheckKeywords("Assets/Shaders/Recovered/HL_FullScreen_Effects_Resources.shader",
                "89aa1b470a96afe49a9b172ec8300d40", 2, names, flags);
            CheckKeywords("Assets/Shaders/Recovered/HL_UI_Blur_Render_Feature.shader",
                "ae05e3fda4799aa49a92776741aa42c4", 1,
                new[] { names[0], names[1], names[2], names[3] }, new byte[] { 0, 0, 0, 0 });
        }

        [Test]
        public void FullscreenAndBlurKeepFloat4FragmentContracts()
        {
            // Declared output precision is known from Metal; this is not a GPU quantization measurement.
            string shared = File.ReadAllText(SharedPath);
            Assert.That(Regex.IsMatch(shared, @"\bfloat4\s+FullscreenEffects\s*\("), Is.True);
            Assert.That(Regex.IsMatch(shared, @"\b(?:half4|fixed4)\s*\("), Is.False);
            string blur = File.ReadAllText("Assets/Shaders/Recovered/HL_UI_Blur_Render_Feature.shader");
            Assert.That(Regex.IsMatch(blur, @"\bfloat4\s+Frag\s*\("), Is.True);
            Assert.That(Regex.IsMatch(blur, @"\b(?:half4|fixed4)\s*\("), Is.False);
        }

        private static void CheckKeywords(string path, string guid, int passCount, string[] names, byte[] flags)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            // The pass count of a URP-tagged subshader depends on the active pipeline tag, so the check runs
            // under URP's and restores the previous one.
            string pipeline = Shader.globalRenderPipeline;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                Assert.That(shader.passCount, Is.EqualTo(passCount));
            }
            finally { Shader.globalRenderPipeline = pipeline; }
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);
            for (int pass = 0; pass < passCount; pass++)
            {
                foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    var actual = new List<string>();
                    foreach (LocalKeyword keyword in ShaderUtil.GetPassKeywords(shader,
                        new PassIdentifier(0, (uint)pass), stage, ShaderCompilerPlatform.Metal))
                        actual.Add(keyword.name);
                    ImportedKeywordContext.AssertPass(actual, names, path + " pass " + pass + " " + stage);
                }
            }
            for (int index = 0; index < names.Length; index++)
            {
                ImportedKeywordContext.AssertKeyword(shader, names[index], flags[index]);
            }
        }

        [TearDown]
        public void RestoreFullscreenProbeStateAndInputs()
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
            private Material material;
            private RenderTexture target, previousTarget;
            private Texture2D sample;
            private string previousPipeline;
            private Matrix4x4 previousView, previousInverseViewProjection;
            private Vector4 previousCamera, previousOrtho;
            private float previousFlip;
            private bool borrowedState;

            public void Run()
            {
                foreach (string path in new[] { ProbePath, ProbePath + ".meta", SharedPath, SharedPath + ".meta" })
                    originals.Add(path, Hash(path));
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
                previousPipeline = Shader.globalRenderPipeline;
                previousTarget = RenderTexture.active;
                previousView = Shader.GetGlobalMatrix("unity_MatrixV");
                previousInverseViewProjection = Shader.GetGlobalMatrix("unity_MatrixInvVP");
                previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
                previousOrtho = Shader.GetGlobalVector("unity_OrthoParams");
                previousFlip = Shader.GetGlobalFloat("_FlipY");
                borrowedState = true;
                Shader.globalRenderPipeline = "UniversalPipeline";

                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ProbePath);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.name, Is.EqualTo("Hidden/ProjectLucid/FullscreenVertexContract"));
                Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo(ProbePath));
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                    Assert.That(message.severity, Is.Not.EqualTo(ShaderCompilerMessageSeverity.Error), message.message);
                Assert.That(shader.isSupported, Is.True);
                material = Own(new Material(shader));
                target = Own(new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
                Assert.That(target.Create(), Is.True);

                // Literal inputs distinguish homogeneous division, matrix rows/columns and normalization.
                var inverse = new Matrix4x4();
                inverse.SetRow(0, new Vector4(2, 1, 0, 3));
                inverse.SetRow(1, new Vector4(-1, 3, 0, -2));
                inverse.SetRow(2, new Vector4(.5f, -.25f, 1, 4));
                inverse.SetRow(3, new Vector4(0, 0, 0, 2));
                Matrix4x4 view = Matrix4x4.identity;
                view.SetRow(2, new Vector4(.25f, -.5f, 2, 0));
                Shader.SetGlobalMatrix("unity_MatrixV", view);
                Shader.SetGlobalMatrix("unity_MatrixInvVP", inverse);
                Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(7, 11, 13, 1));
                // Admission reads the same shader-visible bindings that the actual vertex entries use.
                Expect(2, new Color(7, 11, 13, 1), "camera binding");
                Expect(3, new Color(.25f, -.5f, 2, 0), "view matrix row binding");
                Expect(4, new Color(3, -2, 5, 2), "inverse VP homogeneous binding");

                // Original triangle ids0..2 and blit ids0..3, evaluated at clip z=w=1 (the near clip value with a
                // reversed depth buffer). At GL's near clip value of -1 the world z row (w = 2) loses 2, so the view z
                // gains 1. The UV flips only where UVs start at the top (Shader Graph 14 FullscreenCommon.hlsl).
                float nearClip = SystemInfo.usesReversedZBuffer ? 1f : -1f;
                bool flipsAtTop = SystemInfo.graphicsUVStartsAtTop;
                Color[][] perspective =
                {
                    new[] { new Color(7, 13, 10.625f, 0), new Color(3, 15, 9.625f, 0), new Color(5, 7, 11.125f, 0) },
                    new[] { new Color(6, 10, 10.875f, 0), new Color(7, 13, 10.625f, 0), new Color(5, 14, 10.125f, 0), new Color(4, 11, 10.375f, 0) },
                };
                Vector2[][] uv =
                {
                    new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(0, 2) },
                    new[] { new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1) },
                };
                foreach (float ortho in new[] { 0f, 1f, -2f })
                {
                    Shader.SetGlobalVector("unity_OrthoParams", new Vector4(0, 0, 0, ortho));
                    Expect(5, new Color(0, 0, 0, ortho), "orthographic branch binding");
                    foreach (float flip in new[] { 0f, 1f })
                    {
                        Shader.SetGlobalFloat("_FlipY", flip);
                        for (int entry = 0; entry < perspective.Length; entry++)
                        {
                            material.SetFloat("_ProbeUseBlit", entry);
                            for (int id = 0; id < perspective[entry].Length; id++)
                            {
                                material.SetFloat("_ProbeVertexId", id);
                                string label = "entry " + entry + " id " + id + " ortho " + ortho + " flip " + flip;
                                Color direction = perspective[entry][id];
                                direction.b += (1 - nearClip) * .5f;
                                Expect(0, ortho == 0 ? direction : new Color(.25f, -.5f, 2, 0), label + " view");
                                Vector2 corner = uv[entry][id];
                                Expect(1, new Color(corner.x, flipsAtTop && flip < .5f ? 1 - corner.y : corner.y, nearClip * .5f + .5f, 1), label + " UV");
                            }
                        }
                    }
                }
            }

            private T Own<T>(T item) where T : UnityEngine.Object
            {
                owned.Add(item);
                return item;
            }

            private void Expect(int output, Color expected, string label)
            {
                material.SetFloat("_ProbeOutput", output);
                RenderTexture prior = RenderTexture.active;
                try
                {
                    Graphics.SetRenderTarget(target);
                    GL.Clear(false, true, Color.clear);
                    Assert.That(material.SetPass(0), Is.True, label);
                    Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
                    sample.ReadPixels(new Rect(4, 4, 1, 1), 0, 0, false);
                    sample.Apply(false, false);
                    Color actual = sample.GetPixel(0, 0);
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(.0005f), label + " R");
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(.0005f), label + " G");
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(.0005f), label + " B");
                    Assert.That(actual.a, Is.EqualTo(expected.a).Within(.0005f), label + " A");
                }
                finally { RenderTexture.active = prior; }
            }

            public void Cleanup()
            {
                var failures = new List<Exception>();
                void Restore(Action action)
                {
                    try { action(); }
                    catch (Exception failure) { failures.Add(failure); }
                }
                if (borrowedState)
                {
                    Restore(() => Shader.SetGlobalMatrix("unity_MatrixV", previousView));
                    Restore(() => Shader.SetGlobalMatrix("unity_MatrixInvVP", previousInverseViewProjection));
                    Restore(() => Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera));
                    Restore(() => Shader.SetGlobalVector("unity_OrthoParams", previousOrtho));
                    Restore(() => Shader.SetGlobalFloat("_FlipY", previousFlip));
                    Restore(() => Shader.globalRenderPipeline = previousPipeline);
                    Restore(() => RenderTexture.active = previousTarget);
                }
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object item = owned[index];
                    Restore(() => UnityEngine.Object.DestroyImmediate(item));
                }
                foreach (var original in originals)
                    Restore(() => Assert.That(Hash(original.Key), Is.EqualTo(original.Value), original.Key));
                if (failures.Count != 0) throw new AggregateException("Fullscreen probe cleanup failed.", failures);
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
