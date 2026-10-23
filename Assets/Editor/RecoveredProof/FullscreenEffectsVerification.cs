using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // Hidden/HardLight/RenderFeature/HL_FullScreen_Effects, reconstructed from the shipped Metal programs.
    // The shipped bundles carry only Metal programs, so this proves the source compiles for Direct3D and
    // checks the decoded fragment on the GPU through the DrawProcedural pass: the scene load with edge
    // distortion, the fade to colour, the vignette mix and the speed lines. Both shipped copies are
    // checked: the bundled asset and the Resources asset, each under its own original GUID.
    public static class FullscreenEffectsVerification
    {
        // Shader paths and the shipped shader asset GUIDs in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private static readonly string[,] Copies =
        {
            { "Assets/Shaders/Recovered/HL_FullScreen_Effects.shader", "3518f69d26ca6d140b0b1b2d67fc8dea" },
            { "Assets/Shaders/Recovered/HL_FullScreen_Effects_Resources.shader", "89aa1b470a96afe49a9b172ec8300d40" },
        };

        public static void Run()
        {
            string previous = Shader.globalRenderPipeline;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                for (int copy = 0; copy < Copies.GetLength(0); copy++)
                    RunUnderUniversalPipeline(Load(copy));
            }
            finally { Shader.globalRenderPipeline = previous; }
        }

        public static void CompileForWindows()
        {
            for (int copy = 0; copy < Copies.GetLength(0); copy++)
                CompileForWindows(Load(copy));
        }

        private static void CompileForWindows(Shader shader)
        {
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            int compiled = 0;
            for (int p = 0; p < subshader.PassCount; p++)
            {
                ShaderData.Pass pass = subshader.GetPass(p);
                foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, new string[0], ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                    string errors = "";
                    foreach (ShaderMessage message in info.Messages)
                        if (message.severity == ShaderCompilerMessageSeverity.Error) errors += " " + message.message;
                    Require(info.Success && errors.Length == 0, "D3D compile failed: pass " + pass.Name + " " + stage + errors);
                    compiled++;
                }
            }
            Require(subshader.PassCount == 2, "the DrawProcedural and Blit passes");
            Debug.Log("Project Lucid HL_FullScreen_Effects (" + AssetDatabase.GetAssetPath(shader) + ") compiled " + compiled +
                      " Direct3D programs across " + subshader.PassCount + " passes.");
        }

        private static Shader Load(int copy)
        {
            string path = Copies[copy, 0];
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Require(shader != null && shader.name == "Hidden/HardLight/RenderFeature/HL_FullScreen_Effects", "shader identity: " + path);
            Require(AssetDatabase.AssetPathToGUID(path) == Copies[copy, 1], "original material binding GUID: " + path);
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            return shader;
        }

        private static readonly string[] Floats =
        {
            "_FlipY", "_Edge_Distortion_Amount", "_Vignette_Amount", "_Vignette_Flow_Map_Speed", "_Vignette_Opacity",
            "_Vignette_Flow_Map_Strength", "_Fade_To_Colour_Amount", "_Vignette_Colour_Boost", "_Speed_Lines_Scale",
            "_HLShaderTimePlayerAnim", "_Speed_Lines_Opacity", "_Speed_Lines_Amount", "_Speed_Lines_Speed", "_Vignette_UV_Scale",
        };
        private static readonly string[] Vectors = { "_Fade_To_Color", "_Vignette_Tint", "_Speed_Lines_Colour_Tint", "_ScreenSize", "_GlobalMipBias" };
        private static readonly string[] Textures = { "_BlitTexture", "_Vignette_Texture", "_Speed_Lines_Packed_Noise_Texture" };

        private static void RunUnderUniversalPipeline(Shader shader)
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");

            var owned = new List<UnityEngine.Object>();
            Texture2D Make(int width, int height, Func<int, int, Color> colour)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        texture.SetPixel(x, y, colour(x, y));
                texture.Apply(false, false);
                owned.Add(texture);
                return texture;
            }

            var priorFloats = new Dictionary<string, float>();
            var priorVectors = new Dictionary<string, Vector4>();
            var priorTextures = new Dictionary<string, Texture>();
            foreach (string name in Floats) priorFloats[name] = Shader.GetGlobalFloat(name);
            foreach (string name in Vectors) priorVectors[name] = Shader.GetGlobalVector(name);
            foreach (string name in Textures) priorTextures[name] = Shader.GetGlobalTexture(name);
            Material material = new Material(shader);
            owned.Add(material);
            var target = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            owned.Add(target); owned.Add(sample);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                foreach (string name in Floats) Shader.SetGlobalFloat(name, 0);
                foreach (string name in Vectors) Shader.SetGlobalVector(name, Vector4.zero);
                // The scene: an 8x8 texture whose red encodes the column.
                Shader.SetGlobalTexture("_BlitTexture", Make(8, 8, (x, y) => new Color(x / 8f, 0.5f, 0.25f, 1)));
                Shader.SetGlobalVector("_ScreenSize", new Vector4(8, 8, 1 / 8f, 1 / 8f));
                Shader.SetGlobalTexture("_Vignette_Texture", Make(1, 1, (x, y) => new Color(0.2f, 0.4f, 0.6f, 1)));
                Shader.SetGlobalTexture("_Speed_Lines_Packed_Noise_Texture", Make(1, 1, (x, y) => Color.white));
                Shader.SetGlobalFloat("_Vignette_UV_Scale", 1);
                Shader.SetGlobalFloat("_Vignette_Colour_Boost", 1);
                Shader.SetGlobalVector("_Vignette_Tint", Color.white);

                Color scene = new Color(0.5f, 0.5f, 0.25f);
                Expect(material, target, sample, 16, 16, scene, "the scene, loaded at column 4");

                // Edge distortion: column 2 of 32 (u 0.078) loads scene column 0; amount -10 moves it to column 1.
                Expect(material, target, sample, 2, 16, new Color(0, 0.5f, 0.25f), "undistorted edge column");
                Shader.SetGlobalFloat("_Edge_Distortion_Amount", -10);
                Expect(material, target, sample, 2, 16, new Color(0.125f, 0.5f, 0.25f), "edge distortion by the fourth power of the distance");
                Shader.SetGlobalFloat("_Edge_Distortion_Amount", 0);

                Shader.SetGlobalVector("_Fade_To_Color", new Vector4(0, 0, 1, 1));
                Shader.SetGlobalFloat("_Fade_To_Colour_Amount", 0.25f);
                Expect(material, target, sample, 16, 16, new Color(0.375f, 0.375f, 0.4375f), "a quarter fade to blue");
                Shader.SetGlobalFloat("_Fade_To_Colour_Amount", 0);

                // Vignette: alpha 1 against an amount of 1 is fully in; opacity scales the mix.
                Shader.SetGlobalFloat("_Vignette_Amount", 1);
                Shader.SetGlobalFloat("_Vignette_Opacity", 1);
                Expect(material, target, sample, 16, 16, new Color(0.2f, 0.4f, 0.6f), "the vignette colour replaces the scene");
                Shader.SetGlobalFloat("_Vignette_Opacity", 0.5f);
                Expect(material, target, sample, 16, 16, new Color(0.35f, 0.45f, 0.425f), "half vignette opacity");
                Shader.SetGlobalFloat("_Vignette_Opacity", 1);
                Shader.SetGlobalTexture("_Vignette_Texture", Make(1, 1, (x, y) => new Color(0.2f, 0.4f, 0.6f, 0.75f)));
                Shader.SetGlobalFloat("_Vignette_Amount", 0.5f);
                float edge = 0.25f * 0.25f * (3 - 2 * 0.25f);
                Expect(material, target, sample, 16, 16, Color.Lerp(scene, new Color(0.2f, 0.4f, 0.6f), edge), "smoothstep of alpha 0.75 above 1 - amount 0.5");
                Shader.SetGlobalFloat("_Vignette_Opacity", 0);

                // Speed lines: white noise saturates the shaped noise; near the centre the radial ramp
                // still saturates them at scale 4, and scale 0 removes them.
                Shader.SetGlobalVector("_Speed_Lines_Colour_Tint", new Vector4(1, 0, 0, 1));
                Shader.SetGlobalFloat("_Speed_Lines_Opacity", 0.5f);
                Expect(material, target, sample, 16, 16, scene, "scale 0 puts the centre inside the line-free radius");
                Shader.SetGlobalFloat("_Speed_Lines_Scale", 4);
                Expect(material, target, sample, 16, 16, scene + new Color(0.5f, 0, 0), "speed lines add half the tint");

                // Mip levels each in their own colour (bilinear within a level, point between levels). Spanning
                // UV 0..1 across the 32-pixel target, a 256 texture reads level 3 unbiased.
                Texture2D Levels(Func<int, Color> level)
                {
                    var levels = new Texture2D(256, 256, TextureFormat.RGBAFloat, true, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                    owned.Add(levels);
                    for (int mip = 0; mip < levels.mipmapCount; mip++)
                    {
                        int width = Math.Max(1, 256 >> mip);
                        var pixels = new Color[width * width];
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = level(mip);
                        levels.SetPixels(pixels, mip);
                    }
                    levels.Apply(false, false);
                    return levels;
                }

                // The speed-line noise uses the raw screen-UV gradients (level 3 here), whatever
                // _GlobalMipBias.y; URP's SAMPLE_TEXTURE2D_GRAD would double them at y 2 and read black level 4.
                Shader.SetGlobalTexture("_Speed_Lines_Packed_Noise_Texture", Levels(mip => mip <= 3 ? Color.white : Color.black));
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 2, 0, 0));
                Expect(material, target, sample, 16, 16, scene + new Color(0.5f, 0, 0), "speed-line noise keeps the raw gradients");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                Shader.SetGlobalFloat("_Speed_Lines_Scale", 0);

                // _GlobalMipBias.x applies once (URP's SAMPLE_TEXTURE2D) to each vignette phase: bias 1 reads level 4
                // (red); twice would read level 5 (green). Time 0 shows the first phase; player-anim time 0.5 at
                // flow speed 1 shows only the second.
                Shader.SetGlobalFloat("_Vignette_Amount", 1);
                Shader.SetGlobalFloat("_Vignette_Opacity", 1);
                Shader.SetGlobalTexture("_Vignette_Texture", Levels(mip => mip <= 3 ? new Color(0.2f, 0.4f, 0.6f, 1) : mip == 4 ? Color.red : Color.green));
                foreach (float time in new[] { 0f, 0.5f })
                {
                    Shader.SetGlobalFloat("_Vignette_Flow_Map_Speed", 1);
                    Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", time);
                    string phase = time == 0 ? "first vignette phase" : "second vignette phase";
                    Expect(material, target, sample, 16, 16, new Color(0.2f, 0.4f, 0.6f), phase + ": no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(material, target, sample, 16, 16, Color.red, phase + ": bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", 0);

                // The flow map: its level 4 flows the first phase by -0.25 (half of (0.75 - 0.5) * -2), from the
                // centre's blue vignette band to green; applied twice, level 5 would flow -0.5 to red.
                Shader.SetGlobalFloat("_Vignette_Flow_Map_Strength", 1);
                Shader.SetGlobalTexture("_Vignette_Texture", Make(64, 1, (x, y) => x < 16 ? Color.red : x < 32 ? Color.green : x < 48 ? Color.blue : Color.black));
                material.SetTexture("_Vignette_Flow_Map_Texture", Levels(mip => mip <= 3 ? new Color(0.5f, 0.5f, 0, 1) : mip == 4 ? new Color(0.75f, 0.5f, 0, 1) : new Color(1, 0.5f, 0, 1)));
                Expect(material, target, sample, 16, 16, Color.blue, "flow map: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, target, sample, 16, 16, Color.green, "flow map: bias 1 applies once");

                Debug.Log("Project Lucid HL_FullScreen_Effects (" + AssetDatabase.GetAssetPath(shader) + ") GPU verification passed: 16 Metal-derived cases on " + SystemInfo.graphicsDeviceType +
                          ". The flow-mapped vignette motion over time and the Blit pass geometry remain unverified on the GPU.");
            }
            finally
            {
                foreach (string name in Floats) Shader.SetGlobalFloat(name, priorFloats[name]);
                foreach (string name in Vectors) Shader.SetGlobalVector(name, priorVectors[name]);
                foreach (string name in Textures) Shader.SetGlobalTexture(name, priorTextures[name]);
                RenderTexture.active = previous;
                target.Release();
                foreach (UnityEngine.Object item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void Expect(Material material, RenderTexture target, Texture2D sample, int x, int y, Color expected, string label)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            Require(material.SetPass(0), "pass did not bind: " + label);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
            sample.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            sample.Apply(false);
            Color actual = sample.GetPixel(0, 0);
            Require(Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
                    Mathf.Abs(actual.b - expected.b) < 0.01f && Mathf.Abs(actual.a - 1) < 0.01f,
                    label + ": expected=" + expected + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HL_FullScreen_Effects verification failed: " + message);
        }
    }
}
