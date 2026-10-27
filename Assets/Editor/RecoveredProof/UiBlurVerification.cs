using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // HardLight/RenderFeature/HL_UI_Blur_Render_Feature, reconstructed from the shipped Metal programs.
    // The shipped bundles carry only Metal programs, so this proves the source compiles for Direct3D and
    // checks the decoded five-tap blur on the GPU.
    public static class UiBlurVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_UI_Blur_Render_Feature.shader";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "ae05e3fda4799aa49a92776741aa42c4";

        public static void CompileForWindows()
        {
            Shader shader = Load();
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            int compiled = 0;
            for (int p = 0; p < subshader.PassCount; p++)
            {
                foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    ShaderData.VariantCompileInfo info = subshader.GetPass(p).CompileVariant(stage, new string[0], ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                    string errors = "";
                    foreach (ShaderMessage message in info.Messages)
                        if (message.severity == ShaderCompilerMessageSeverity.Error) errors += " " + message.message;
                    Require(info.Success && errors.Length == 0, "D3D compile failed: " + stage + errors);
                    compiled++;
                }
            }
            Debug.Log("Project Lucid HL_UI_Blur_Render_Feature compiled " + compiled + " Direct3D programs.");
        }

        private static Shader Load()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.name == "HardLight/RenderFeature/HL_UI_Blur_Render_Feature", "shader identity");
            Require(AssetDatabase.AssetPathToGUID(ShaderPath) == OriginalGuid, "original material binding GUID");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            return shader;
        }

        public static void Run()
        {
            Shader shader = Load();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");
            var owned = new List<UnityEngine.Object>();
            Vector4 priorMipBias = Shader.GetGlobalVector("_GlobalMipBias");
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            owned.Add(target); owned.Add(sample);
            try
            {
                target.Create();
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                Material material = new Material(shader);
                owned.Add(material);
                // Texels red, green, blue, black; the centre column (u 0.516) is blue.
                var strip = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                strip.SetPixels(new[] { new Color(1, 0, 0, 1), new Color(0, 1, 0, 1), new Color(0, 0, 1, 1), new Color(0, 0, 0, 1) });
                strip.Apply(false, false);
                owned.Add(strip);
                material.SetTexture("_BlitTexture", strip);

                material.SetFloat("_offset", 0);
                Expect(material, target, sample, new Color(0, 0, 1, 1), "offset 0 averages five centre taps");
                // One texel: the two right taps land on black, the two left taps on green.
                material.SetFloat("_offset", 1);
                Expect(material, target, sample, new Color(0, 0.4f, 0.2f, 1), "diagonal taps one texel out");

                // A blit keeps the source orientation on every graphics API: URP flips V only where UVs start at the
                // top. The lower half of the target reads the bottom texel row (red), the upper half the top (green).
                var column = new Texture2D(1, 2, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                column.SetPixels(new[] { new Color(1, 0, 0, 1), new Color(0, 1, 0, 1) });
                column.Apply(false, false);
                owned.Add(column);
                material.SetTexture("_BlitTexture", column);
                material.SetFloat("_offset", 0);
                Expect(material, target, sample, new Color(1, 0, 0, 1), "lower rows keep the bottom of the source", 8);
                Expect(material, target, sample, new Color(0, 1, 0, 1), "upper rows keep the top of the source", 24);

                // _GlobalMipBias applies once to every tap (URP's SAMPLE_TEXTURE2D). Spanning UV 0..1 across the
                // 32-pixel target, a 256 texture with a colour per level reads level 3 (blue) unbiased and level 4
                // (green) at bias 1; any tap biased twice would pull the average towards level 5 (red).
                var levels = new Texture2D(256, 256, TextureFormat.RGBAFloat, true, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                owned.Add(levels);
                for (int mip = 0; mip < levels.mipmapCount; mip++)
                {
                    int width = Math.Max(1, 256 >> mip);
                    var pixels = new Color[width * width];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = mip <= 3 ? new Color(0, 0, 1, 1) : mip == 4 ? new Color(0, 1, 0, 1) : new Color(1, 0, 0, 1);
                    levels.SetPixels(pixels, mip);
                }
                levels.Apply(false, false);
                material.SetTexture("_BlitTexture", levels);
                material.SetFloat("_offset", 0);
                Expect(material, target, sample, new Color(0, 0, 1, 1), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, target, sample, new Color(0, 1, 0, 1), "bias 1 applies once to every tap");
                Debug.Log("Project Lucid HL_UI_Blur_Render_Feature GPU verification passed: 6 cases on " + SystemInfo.graphicsDeviceType + ".");
            }
            finally
            {
                Shader.SetGlobalVector("_GlobalMipBias", priorMipBias);
                RenderTexture.active = previous;
                target.Release();
                foreach (UnityEngine.Object item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void Expect(Material material, RenderTexture target, Texture2D sample, Color expected, string label, int row = 16)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            Require(material.SetPass(0), "pass did not bind: " + label);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
            sample.ReadPixels(new Rect(16, row, 1, 1), 0, 0, false);
            sample.Apply(false);
            Color actual = sample.GetPixel(0, 0);
            Require(Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
                    Mathf.Abs(actual.b - expected.b) < 0.01f && Mathf.Abs(actual.a - expected.a) < 0.01f,
                    label + ": expected=" + expected + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HL_UI_Blur_Render_Feature verification failed: " + message);
        }
    }
}
