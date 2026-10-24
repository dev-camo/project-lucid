using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_Main, reconstructed from the shipped Metal programs. The shipped
    // bundles carry only Metal programs, so this proves the source compiles for Direct3D and checks
    // the decoded forward output on the GPU: colour sources, alpha, dissolve, softness, the twice-applied
    // tiling with scrolling, the gradient map, and the depth and camera-distance fades.
    public static class ParticleMainVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_Main.shader";
        // The shipped shader asset GUID in the 2026-10-05 baseline export, referenced by all 159 bundled materials.
        private const string OriginalGuid = "beb9536db5dec634f9859840dcb2976b";

        public static void Run()
        {
            string previous = Shader.globalRenderPipeline;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                RunUnderUniversalPipeline();
            }
            finally { Shader.globalRenderPipeline = previous; }
        }

        // Compiles every pass for Direct3D 11 with the keyword sets the shipped variants combine.
        public static void CompileForWindows()
        {
            CompileForPlatform(ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
        }

        public static void CompileForLinux()
        {
            CompileForPlatform(ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64);
        }

        private static void CompileForPlatform(ShaderCompilerPlatform platform, BuildTarget target)
        {
            Shader shader = Load();
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            var sets = new List<string[]>
            {
                new string[0],
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_DEPTHFADE", "_GRADIENTMAPENABLE", "FOG_LINEAR", "_WRITE_RENDERING_LAYERS" },
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "_DEPTHFADE" },
                new[] { "_GRADIENTMAPENABLE", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
                new[] { "INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_DEPTHFADE" },
                new[] { "DOTS_INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_GRADIENTMAPENABLE" },
            };
            int compiled = 0;
            for (int p = 0; p < subshader.PassCount; p++)
            {
                ShaderData.Pass pass = subshader.GetPass(p);
                foreach (string[] keywords in sets)
                {
                    foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, keywords,
                            platform, target);
                        string errors = "";
                        foreach (ShaderMessage message in info.Messages)
                            if (message.severity == ShaderCompilerMessageSeverity.Error) errors += " " + message.message;
                        Require(info.Success && errors.Length == 0,
                            platform + " compile failed: pass " + pass.Name + " " + stage + " [" + string.Join(" ", keywords) + "]" + errors);
                        compiled++;
                    }
                }
            }
            Debug.Log("Project Lucid HL_Particle_Main compiled " + compiled + " " + platform + " programs across " + subshader.PassCount + " passes.");
        }

        private static Shader Load()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.name == "HardLight/Particle/HL_Particle_Main", "shader identity");
            Require(AssetDatabase.AssetPathToGUID(ShaderPath) == OriginalGuid, "original material binding GUID");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            return shader;
        }

        private static void RunUnderUniversalPipeline()
        {
            Shader shader = Load();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");
            Require(shader.passCount == 3, "three shipped passes");

            var owned = new List<UnityEngine.Object>();
            Texture2D Texels(params Color[] colors)
            {
                var texture = new Texture2D(colors.Length, 1, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                texture.SetPixels(colors);
                texture.Apply(false, false);
                owned.Add(texture);
                return texture;
            }
            Color texel = new Color(0.8f, 1f, 0.2f, 0.4f);

            Material material = new Material(shader);
            owned.Add(material);
            Mesh mesh = new Mesh();
            owned.Add(mesh);
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            void Stream(float custom) => mesh.SetUVs(0, new List<Vector4>
            {
                new Vector4(0, 0, custom, 0), new Vector4(1, 0, custom, 0), new Vector4(1, 1, custom, 0), new Vector4(0, 1, custom, 0),
            });
            Stream(0);
            mesh.RecalculateBounds();

            var target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var cameraObject = new GameObject("ShaderVerificationCamera");
            owned.Add(target); owned.Add(sample); owned.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.aspect = 1;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10;
            camera.transform.position = new Vector3(0, 0, -2);

            string[] globals = { "_TimeParameters", "_ZBufferParams", "unity_OrthoParams", "_ScaledScreenParams", "_ProjectionParams", "_GlobalMipBias" };
            var priorVectors = new Dictionary<string, Vector4>();
            foreach (string name in globals) priorVectors[name] = Shader.GetGlobalVector(name);
            float priorTime = Shader.GetGlobalFloat("_HLShaderTimeGame");
            Texture priorDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
            Matrix4x4 priorView = Shader.GetGlobalMatrix("unity_MatrixV");
            Matrix4x4 priorInverseViewProjection = Shader.GetGlobalMatrix("unity_MatrixInvVP");
            RenderTexture previous = RenderTexture.active;
            string probeFolder = null, shaderHash = null, metaHash = null;
            Material depthReadout = null;
            try
            {
                // Import the owned diagnostic before publishing the draw's globals and depth texture.
                shaderHash = Hash(ShaderPath);
                metaHash = Hash(ShaderPath + ".meta");
                string candidate = "Assets/ParticleMainDepthReadout_" + Guid.NewGuid().ToString("N");
                Require(!Directory.Exists(candidate) && !File.Exists(candidate) && !File.Exists(candidate + ".meta")
                    && !Directory.Exists(candidate + ".meta"), "depth readout path already exists");
                probeFolder = candidate;
                Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(probeFolder))), "depth readout folder");
                string probeGuid = Guid.NewGuid().ToString("N");
                Require(string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(probeGuid)), "depth readout GUID already exists");
                string probeName = "Hidden/ProjectLucid/ParticleMainDepthReadout/" + probeGuid;
                string probePath = probeFolder + "/Probe.shader";
                File.WriteAllText(probePath, DepthReadoutSource(File.ReadAllText(ShaderPath), probeName), new UTF8Encoding(false));
                File.WriteAllText(probePath + ".meta", "fileFormatVersion: 2\nguid: " + probeGuid + "\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Shader probeShader = AssetDatabase.LoadAssetAtPath<Shader>(probePath);
                Require(probeShader != null && probeShader.name == probeName && AssetDatabase.GetAssetPath(probeShader) == probePath
                    && AssetDatabase.AssetPathToGUID(probePath) == probeGuid, "depth readout shader identity");
                depthReadout = new Material(probeShader);
                owned.Add(depthReadout);
                depthReadout.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                depthReadout.EnableKeyword("_DEPTHFADE");
                Require(depthReadout.SetPass(0), "depth readout preflight pass");
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(probeShader))
                    Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);

                target.Create();
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 0);
                material.SetFloat("_Softness", 1);
                material.SetColor("_HDR", Color.white);
                material.SetTexture("_MainTex", Texels(texel, texel, texel, texel));

                // Texture colour: red is the alpha, and the output colour is premultiplied by it.
                material.SetFloat("_Use_Texture_Colour", 1);
                Expect(material, mesh, camera, target, sample, new Color(0.64f, 0.8f, 0.16f), "texture colour times red alpha", 0.8f);
                material.SetFloat("_Use_Alpha_Instead_of_Red", 1);
                Expect(material, mesh, camera, target, sample, new Color(0.32f, 0.4f, 0.08f), "alpha instead of red", 0.4f);
                material.SetFloat("_Use_Alpha_Instead_of_Red", 0);
                material.SetFloat("_Softness", 0.5f);
                Expect(material, mesh, camera, target, sample, new Color(0.8f, 1f, 0.2f), "softness 0.5 saturates 0.8 to 1", 1f);
                material.SetFloat("_Softness", 1);
                Stream(0.95f);
                Expect(material, mesh, camera, target, sample, new Color(0.08f, 0.1f, 0.02f), "custom stream 0.95 dissolves green 1 to alpha 0.1", 0.1f);
                Stream(0);

                // Primary/secondary lerp by red plus the colour offset.
                material.SetFloat("_Use_Texture_Colour", 0);
                material.SetColor("_Primary_Color", Color.red);
                material.SetColor("_Secondary_Color", Color.blue);
                material.SetFloat("_Color_Offset", -0.3f);
                Expect(material, mesh, camera, target, sample, new Color(0.4f, 0, 0.4f), "lerp(secondary, primary, red + offset)", 0.8f);

                // Gradient map: u = frac(time * speed + red * 0.5).
                material.EnableKeyword("_GRADIENTMAPENABLE");
                material.SetTexture("_gradienmap", Texels(Color.black, Color.red, Color.green, Color.blue));
                material.SetFloat("_GradientMapSpeed", 1);
                Expect(material, mesh, camera, target, sample, new Color(0.8f, 0, 0), "gradient at red * 0.5 = 0.4", 0.8f);
                Shader.SetGlobalVector("_TimeParameters", new Vector4(0.5f, 0, 0, 0));
                Expect(material, mesh, camera, target, sample, new Color(0, 0, 0.8f), "gradient at time 0.5 + 0.4", 0.8f);
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                material.DisableKeyword("_GRADIENTMAPENABLE");

                // UV: (uv0 * tiling + time * speed + offset) * tiling + offset. At the centre column
                // (u 0.516) texel 4 is green; one offset of 0.25 alone would land on blue (texel 6);
                // the offset applied twice lands on red (texel 0, wrapped).
                material.SetFloat("_Use_Texture_Colour", 1);
                Color r = new Color(1, 0, 0, 1), g = new Color(0, 1, 0, 1), b = new Color(0, 0, 1, 1), k = new Color(0, 0, 0, 1);
                material.SetFloat("_Use_Alpha_Instead_of_Red", 1);
                material.SetTexture("_MainTex", Texels(r, k, k, k, g, k, b, k));
                Expect(material, mesh, camera, target, sample, new Color(0, 1, 0), "untransformed UV", 1f);
                material.SetTextureOffset("_MainTex", new Vector2(0.25f, 0));
                Expect(material, mesh, camera, target, sample, new Color(1, 0, 0), "the offset is applied twice", 1f);
                material.SetTextureOffset("_MainTex", Vector2.zero);
                material.SetVector("_UV_Speed_XY", new Vector4(0.5f, 0, 0, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(material, mesh, camera, target, sample, new Color(1, 0, 0), "game time times UV speed", 1f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetVector("_UV_Speed_XY", Vector4.zero);
                material.SetFloat("_Use_Alpha_Instead_of_Red", 0);
                material.SetTexture("_MainTex", Texels(texel, texel, texel, texel));

                // Camera distance fade: far beyond this ortho quad's clip distance fades to zero.
                material.SetFloat("_Camera_Distance_Fade_Near", 100);
                material.SetFloat("_Camera_Distance_Fade_Far", 200);
                Expect(material, mesh, camera, target, sample, new Color(0.64f, 0.8f, 0.16f), "distance fade is off by default", 0.8f);
                material.SetFloat("_Camera_Distance_Fade", 1);
                Expect(material, mesh, camera, target, sample, Color.black, "distance fade nearer than its range removes the particle", 0f);
                // The original reversed-Z orthographic w-z is (eye-near)/(far-near),
                // independent of the backend running this proof; retain the same half-alpha oracle.
                float distance = 19f / 99f;
                material.SetFloat("_Camera_Distance_Fade_Near", distance - 0.1f);
                material.SetFloat("_Camera_Distance_Fade_Far", distance + 0.1f);
                Expect(material, mesh, camera, target, sample, new Color(0.32f, 0.4f, 0.08f), "distance fade at the middle of its range", 0.4f);
                material.SetFloat("_Camera_Distance_Fade", 0);

                // Opaque programs write alpha 1 over the same premultiplied colour.
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Expect(material, mesh, camera, target, sample, new Color(0.64f, 0.8f, 0.16f), "opaque alpha is 1", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original. A 256
                // texture spanning UV 0..1 across the 32-pixel quad reads level 3 unbiased and level 4 at bias 1;
                // applying the bias twice would read level 5. Each level has its own colour.
                Texture2D Levels(int width, int height, Func<int, Color> level)
                {
                    var levels = new Texture2D(width, height, TextureFormat.RGBAFloat, true, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
                    owned.Add(levels);
                    for (int mip = 0; mip < levels.mipmapCount; mip++)
                    {
                        var pixels = new Color[Math.Max(1, width >> mip) * Math.Max(1, height >> mip)];
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = level(mip);
                        levels.SetPixels(pixels, mip);
                    }
                    levels.Apply(false, false);
                    return levels;
                }
                material.SetTexture("_MainTex", Levels(256, 256, mip => mip <= 3 ? texel : mip == 4 ? new Color(0.5f, 1f, 1f, 1f) : new Color(0.2f, 1f, 1f, 1f)));
                Expect(material, mesh, camera, target, sample, new Color(0.64f, 0.8f, 0.16f), "main: no bias reads level 3", 0.8f);
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, mesh, camera, target, sample, new Color(0.25f, 0.5f, 0.5f), "main: bias 1 applies once", 0.5f);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                // Gradient map: a red ramp (point-sampled, 200 texels, no mips) makes the gradient lookup
                // frac(u * 0.5), moving 0.5/32 per pixel: a 256 x 1 gradient strip reads level 2 unbiased and 3
                // at bias 1; twice would read 4. The centre column reads ramp texel 103 (red and alpha 0.5175).
                material.SetFloat("_Use_Texture_Colour", 0);
                var ramp = new Color[200];
                for (int i = 0; i < ramp.Length; i++) ramp[i] = new Color((i + 0.5f) / 200, 1, 0, 1);
                material.SetTexture("_MainTex", Texels(ramp));
                material.EnableKeyword("_GRADIENTMAPENABLE");
                material.SetTexture("_gradienmap", Levels(256, 1, mip => mip <= 2 ? Color.red : mip == 3 ? Color.green : Color.blue));
                float rampAlpha = 103.5f / 200;
                Expect(material, mesh, camera, target, sample, new Color(rampAlpha, 0, 0), "gradient: no bias reads level 2", rampAlpha);
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, mesh, camera, target, sample, new Color(0, rampAlpha, 0), "gradient: bias 1 applies once", rampAlpha);
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.DisableKeyword("_GRADIENTMAPENABLE");
                material.SetFloat("_Use_Texture_Colour", 1);
                material.SetTexture("_MainTex", Texels(texel, texel, texel, texel));

                // Depth fade: with _ZBufferParams.zw = (0, 0.25) the scene eye depth is 4 for any depth
                // sample; the ortho quad's clip w is 1, so (4 - 1) / 6 * 1 halves the alpha.
                material.EnableKeyword("_DEPTHFADE");
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                material.SetFloat("_DepthFadeDistance", 6);
                material.SetFloat("_DepthFadeStrength", 1);
                Expect(material, mesh, camera, target, sample, new Color(0.32f, 0.4f, 0.08f), "depth fade halves the alpha", 0.4f);

                // In the original inverse-VP branch, scene eye depths 2 and 4 give fades 1/6 and 1/2
                // against this orthographic particle's clip w=1. Opposite texture rows must stay distinct.
                var depth = new Texture2D(1, 32, TextureFormat.RGBAFloat, false, true)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                };
                owned.Add(depth);
                var depths = new Color[32];
                for (int row = 0; row < depths.Length; row++)
                {
                    float raw = row < 16 ? 19f / 99f : 39f / 99f;
                    if (SystemInfo.usesReversedZBuffer) raw = 1 - raw;
                    depths[row] = new Color(raw, 0, 0, 0);
                }
                depth.SetPixels(depths);
                depth.Apply(false, false);
                // URP publishes these independently; immediate GL matrix setup does not promise the inverse.
                Matrix4x4 view = camera.worldToCameraMatrix;
                Shader.SetGlobalMatrix("unity_MatrixV", view);
                Shader.SetGlobalMatrix("unity_MatrixInvVP",
                    (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * view).inverse);
                Shader.SetGlobalTexture("_CameraDepthTexture", depth);
                Shader.SetGlobalVector("unity_OrthoParams", new Vector4(1, 1, 0, 1));
                depthReadout.CopyPropertiesFromMaterial(material);
                depthReadout.shaderKeywords = material.shaderKeywords;
                foreach (int row in new[] { 8, 24 })
                {
                    Color coordinates = ReadDepthReadout(depthReadout, mesh, camera, target, sample, row, 0);
                    Color depthsRead = ReadDepthReadout(depthReadout, mesh, camera, target, sample, row, 1);
                    Debug.Log("Project Lucid depth readout row " + row + ": [SVy/height, projectionSign, orthoFlag, sampleV]="
                        + coordinates.ToString("R") + "; [sampledRaw, actualSceneEyeDepth, directRawV.25, directRawV.75]=" + depthsRead.ToString("R"));
                }
                Expect(material, mesh, camera, target, sample, new Color(0.64f, 0.8f, 0.16f) / 6,
                       "orthographic lower row reconstructs scene eye depth 2", 0.8f / 6, 8);
                Expect(material, mesh, camera, target, sample, new Color(0.32f, 0.4f, 0.08f),
                       "orthographic upper row reconstructs scene eye depth 4", 0.4f, 24);
                material.DisableKeyword("_DEPTHFADE");

                // Near=.1, far=10, eye=9 gives original perspective clip z=1/99 and w=9.
                // A fixed range [8.9,9.1] retains w-z; eye distance would give 1/2, over .04 too much alpha.
                camera.orthographic = false;
                camera.fieldOfView = 60;
                camera.transform.position = new Vector3(0, 0, -9);
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                material.SetFloat("_Camera_Distance_Fade_Near", 8.9f);
                material.SetFloat("_Camera_Distance_Fade_Far", 9.1f);
                material.SetFloat("_Camera_Distance_Fade", 1);
                float perspectiveFade = 89f / 198f;
                Expect(material, mesh, camera, target, sample, new Color(0.64f, 0.8f, 0.16f) * perspectiveFade,
                       "perspective distance fade preserves the original clip quantity", 0.8f * perspectiveFade);

                Debug.Log("Project Lucid HL_Particle_Main GPU verification passed: 22 Metal-derived cases on " + SystemInfo.graphicsDeviceType +
                          ". In-game presentation QA remains unverified.");
            }
            finally
            {
                try
                {
                    Shader.SetGlobalFloat("_HLShaderTimeGame", priorTime);
                    Shader.SetGlobalTexture("_CameraDepthTexture", priorDepth);
                    Shader.SetGlobalMatrix("unity_MatrixV", priorView);
                    Shader.SetGlobalMatrix("unity_MatrixInvVP", priorInverseViewProjection);
                    foreach (string name in globals) Shader.SetGlobalVector(name, priorVectors[name]);
                    RenderTexture.active = previous;
                    target.Release();
                    foreach (UnityEngine.Object item in owned) UnityEngine.Object.DestroyImmediate(item);
                }
                finally
                {
                    try
                    {
                        if (probeFolder != null && (Directory.Exists(probeFolder) || File.Exists(probeFolder + ".meta")))
                            Require(AssetDatabase.DeleteAsset(probeFolder), "depth readout folder cleanup");
                        Require(probeFolder == null || (!Directory.Exists(probeFolder) && !File.Exists(probeFolder + ".meta")),
                            "depth readout folder remains");
                    }
                    finally
                    {
                        bool sourceUnchanged = shaderHash == null || Hash(ShaderPath) == shaderHash;
                        bool metaUnchanged = metaHash == null || Hash(ShaderPath + ".meta") == metaHash;
                        Require(sourceUnchanged && metaUnchanged, "depth readout changed maintained shader or metadata");
                    }
                }
            }
        }

        private static string DepthReadoutSource(string original, string name)
        {
            const string declaration = "Shader \"HardLight/Particle/HL_Particle_Main\"";
            Require(original.IndexOf(declaration, StringComparison.Ordinal) == original.LastIndexOf(declaration, StringComparison.Ordinal)
                && original.Contains(declaration), "depth readout source declaration");
            string source = original.Replace(declaration, "Shader \"" + name + "\"")
                .Replace("    Properties\n    {", "    Properties\n    {\n        [HideInInspector]_LucidDepthReadoutMode (\"Readout mode\", Float) = 0");
            int start = source.IndexOf("HLSLPROGRAM", StringComparison.Ordinal);
            int end = source.IndexOf("ENDHLSL", start, StringComparison.Ordinal);
            Require(start >= 0 && end > start, "depth readout forward program");
            string program = source.Substring(start, end - start);
            Require(program.Contains("#pragma fragment Frag"), "depth readout forward fragment");
            program = program.Replace("#pragma fragment Frag", "#pragma fragment LucidDepthReadoutFrag");
            return source.Substring(0, start) + program + @"
            float _LucidDepthReadoutMode;
            float4 LucidDepthReadoutFrag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float y = _ProjectionParams.x < 0.0 ? _ScaledScreenParams.y - input.positionCS.y : input.positionCS.y;
                float2 uv = float2(input.positionCS.x, y) / _ScaledScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                if (_LucidDepthReadoutMode == 0)
                    return float4(input.positionCS.y / _ScaledScreenParams.y, _ProjectionParams.x, unity_OrthoParams.w, uv.y);
                return float4(SampleSceneDepth(uv), SceneEyeDepth(input.positionCS),
                    SampleSceneDepth(float2(.5, .25)), SampleSceneDepth(float2(.5, .75)));
            }
" + source.Substring(end);
        }

        private static Color ReadDepthReadout(Material material, Mesh mesh, Camera camera, RenderTexture target,
                                             Texture2D sample, int row, int mode)
        {
            material.SetFloat("_LucidDepthReadoutMode", mode);
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix();
            try
            {
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                Require(material.SetPass(0), "depth readout pass");
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
            finally { GL.PopMatrix(); }
            sample.ReadPixels(new Rect(16, row, 1, 1), 0, 0, false);
            sample.Apply(false);
            return sample.GetPixel(0, 0);
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void Expect(Material material, Mesh mesh, Camera camera, RenderTexture target, Texture2D sample,
                                   Color expected, string label, float expectedAlpha, int y = 16)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix();
            try
            {
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                Require(material.SetPass(0), "pass did not bind: " + label);
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
            finally { GL.PopMatrix(); }
            sample.ReadPixels(new Rect(16, y, 1, 1), 0, 0, false);
            sample.Apply(false);
            Color actual = sample.GetPixel(0, 0);
            Require(Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
                    Mathf.Abs(actual.b - expected.b) < 0.01f && Mathf.Abs(actual.a - expectedAlpha) < 0.01f,
                    label + ": expected=" + expected + " alpha " + expectedAlpha + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HL_Particle_Main verification failed: " + message);
        }
    }
}
