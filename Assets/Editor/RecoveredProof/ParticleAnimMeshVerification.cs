using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_AnimMesh, reconstructed from the shipped Metal programs. The shipped
    // bundles carry only Metal programs, so this proves the source compiles for Direct3D and checks the
    // decoded forward output on the GPU: the channel mask and UV ramp, both colour sources, fresnel,
    // depth fade, softness, the custom stream, the time category and the end-of-graph vertex alpha.
    public static class ParticleAnimMeshVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_AnimMesh.shader";
        // The shipped shader asset GUID in the 2026-10-05 baseline export, referenced by all 81 shipped materials.
        private const string OriginalGuid = "a39de842318b98643a7250ed212218ce";
        private static Action afterDepthOverride;

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
            Compile(ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
        }

        public static void CompileForLinux()
        {
            Compile(ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64);
        }

        private static void Compile(ShaderCompilerPlatform platform, BuildTarget target)
        {
            Shader shader = Load();
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            var sets = new List<string[]>
            {
                new string[0],
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_FRESNELENABLE", "_GRADIENTMAPENABLE", "_USE_UV_MASK", "_WRITE_RENDERING_LAYERS" },
                new[] { "_SURFACE_TYPE_TRANSPARENT", "_USE_UV_MASK" },
                new[] { "_GRADIENTMAPENABLE", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
                new[] { "INSTANCING_ON", "FOG_LINEAR", "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON" },
                new[] { "DOTS_INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_USE_UV_MASK" },
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
            Debug.Log("Project Lucid HL_Particle_AnimMesh compiled " + compiled + " " + platform + " requests across " + subshader.PassCount + " passes.");
        }

        public static void VerifyDepthTextureRestorationAfterFailure()
        {
            Require(afterDepthOverride == null, "a depth-override failure control is already active");
            Texture previous = Shader.GetGlobalTexture("_CameraDepthTexture");
            Texture2D sentinel = null;
            var expected = new InvalidOperationException("Owned failure immediately after the orthographic depth override.");
            try
            {
                sentinel = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                sentinel.SetPixel(0, 0, new Color(.375f, 0, 0, 1));
                sentinel.Apply(false, false);
                Shader.SetGlobalTexture("_CameraDepthTexture", sentinel);
                afterDepthOverride = () => { throw expected; };
                bool reached = false;
                try { Run(); }
                catch (Exception failure)
                {
                    if (!ReferenceEquals(failure, expected)) throw;
                    reached = true;
                }
                Require(reached, "the genuine Run must reach the injected depth-override failure");
                Require(Shader.GetGlobalTexture("_CameraDepthTexture") == sentinel,
                    "the failed Run must restore its distinct borrowed depth texture");
            }
            finally
            {
                afterDepthOverride = null;
                Shader.SetGlobalTexture("_CameraDepthTexture", previous);
                if (sentinel != null) UnityEngine.Object.DestroyImmediate(sentinel);
            }
        }

        private static Shader Load()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.name == "HardLight/Particle/HL_Particle_AnimMesh", "shader identity");
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
                owned.Add(texture);
                texture.SetPixels(colors);
                texture.Apply(false, false);
                return texture;
            }
            Color texel = new Color(0.6f, 0.2f, 0, 1);

            Material material = new Material(shader);
            owned.Add(material);
            Mesh mesh = new Mesh();
            owned.Add(mesh);
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            void Stream(float custom, float vertexAlpha)
            {
                mesh.SetUVs(0, new List<Vector4>
                {
                    new Vector4(0, 0, custom, 0), new Vector4(1, 0, custom, 0), new Vector4(1, 1, custom, 0), new Vector4(0, 1, custom, 0),
                });
                var c = new Color(1, 1, 1, vertexAlpha);
                mesh.colors = new[] { c, c, c, c };
            }
            Stream(0, 1);
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

            string[] vectors = { "_TimeParameters", "_ZBufferParams", "unity_OrthoParams", "_ScaledScreenParams", "_ProjectionParams", "_WorldSpaceCameraPos", "_GlobalMipBias" };
            string[] floats = { "_HLShaderTimeGame", "_HLShaderTimePlayerAnim" };
            var priorVectors = new Dictionary<string, Vector4>();
            var priorFloats = new Dictionary<string, float>();
            foreach (string name in vectors) priorVectors[name] = Shader.GetGlobalVector(name);
            foreach (string name in floats) priorFloats[name] = Shader.GetGlobalFloat(name);
            RenderTexture previous = RenderTexture.active;
            Texture priorDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
            Matrix4x4 priorView = Shader.GetGlobalMatrix("unity_MatrixV");
            Matrix4x4 priorInverseVP = Shader.GetGlobalMatrix("unity_MatrixInvVP");
            try
            {
                target.Create();
                // A distant camera makes every pixel head-on for the fresnel term; scene eye depth 4 against the
                // ortho quad's clip w of 1, so the default depth fade (distance 1) saturates to 1.
                foreach (string name in floats) Shader.SetGlobalFloat(name, 0);
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0, 0, -10000, 0));
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 0);
                material.SetFloat("_Softness", 1);
                material.SetFloat("_Fresnel_Min", 0);
                material.SetFloat("_Fresnel_Max", 1);
                material.SetFloat("_DepthFade_Distance", 1);
                material.SetFloat("_DepthFade_Strenght", 1);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));
                material.SetColor("_Primary_Color", Color.red);
                material.SetColor("_Secondary_Color", Color.blue);
                material.SetTexture("_Main_Texture", Texels(texel, texel, texel, texel));
                // The shipped default turns the UV mask on; the base cases start without it. 80 of the 81
                // shipped materials are transparent, so the cases run that variant and check its alpha.
                material.DisableKeyword("_USE_UV_MASK");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Expect(material, mesh, camera, target, sample, 16, new Color(0.36f, 0, 0.24f), 0.6f, "red mask 0.6 lerps secondary to primary");
                material.SetFloat("_Color_Offset", -0.2f);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.24f, 0, 0.36f), 0.6f, "colour offset -0.2");
                material.SetFloat("_Color_Offset", 0);
                material.SetVector("_Channel_Picker", new Vector4(0, 1, 0, 0));
                Expect(material, mesh, camera, target, sample, 16, new Color(0.04f, 0, 0.16f), 0.2f, "green picked by the channel picker");
                material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));

                // UV ramp: row 4 (v 0.14) subtracts 0.72, row 28 (v 0.89) adds 0.78.
                material.EnableKeyword("_USE_UV_MASK");
                Expect(material, mesh, camera, target, sample, 4, Color.black, 0, "the UV ramp removes the lower rows");
                Expect(material, mesh, camera, target, sample, 28, Color.red, 1, "the UV ramp saturates the upper rows");
                material.DisableKeyword("_USE_UV_MASK");

                material.SetFloat("_Fresnel_Min", 0.5f);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0, 0.12f), 0.3f, "fresnel min 0.5 halves a head-on alpha");
                material.SetFloat("_Fresnel_Min", 0);
                material.SetFloat("_DepthFade_Distance", 6);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0, 0.12f), 0.3f, "depth fade (4 - 1) / 6 halves the alpha");
                material.SetFloat("_DepthFade_Distance", 1);
                Stream(0.1f, 1);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.3f, 0, 0.2f), 0.5f, "custom stream 0.1 is subtracted");
                material.SetFloat("_Softness", 0.5f);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.6f, 0, 0.4f), 1f, "softness 0.5 doubles before saturating");
                material.SetFloat("_Softness", 1);
                Stream(0, 1);

                // Gradient: u = frac(mask * 0.5 + time * speed); colour = gradient + mask * 0.5.
                material.EnableKeyword("_GRADIENTMAPENABLE");
                material.SetTexture("_gradientMap", Texels(Color.black, Color.green, Color.black, Color.black));
                material.SetFloat("_GradientMapSpeed", 1);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0.78f, 0.18f), 0.6f, "gradient at 0.3 plus half the mask");
                material.DisableKeyword("_GRADIENTMAPENABLE");

                // Time category: the panner reads player-anim time only when the category is set.
                Color r = new Color(1, 0, 0, 1), k = new Color(0, 0, 0, 1);
                material.SetTexture("_Main_Texture", Texels(r, k, k, k, k, k, k, k));
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0.484375f, 0));
                Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", 1);
                Expect(material, mesh, camera, target, sample, 16, Color.black, 0, "game time 0 leaves the panner at rest");
                material.SetFloat("_Use_PlayerAnim_Time_Category", 1);
                Expect(material, mesh, camera, target, sample, 16, Color.red, 1, "player-anim time 1 pans 0.484 onto texel 0");
                material.SetFloat("_Use_PlayerAnim_Time_Category", 0);
                Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", 0);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetTexture("_Main_Texture", Texels(texel, texel, texel, texel));

                // Vertex alpha scales the mask, and again the output alpha when added at the end.
                Stream(0, 0.5f);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0, 0.12f), 0.3f, "vertex alpha 0.5");
                material.SetFloat("_Add_VertexColor_Alpha_at_end", 1);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0, 0.12f), 0.15f, "vertex alpha added again at the end");
                // Opaque programs write alpha 1 and skip the end-of-graph vertex alpha.
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0, 0.12f), 1f, "opaque alpha is 1");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_Add_VertexColor_Alpha_at_end", 0);
                Stream(0, 1);

                // Original reversed-Z ortho near.1/far10/eye2 gives w-z=19/99 on every backend.
                float distance = 19f / 99f;
                material.SetFloat("_Camera_Distance_Fade_Near", distance - 0.1f);
                material.SetFloat("_Camera_Distance_Fade_Far", distance + 0.1f);
                material.SetFloat("_Camera_Distance_Fade", 1);
                Expect(material, mesh, camera, target, sample, 16, new Color(0.18f, 0, 0.12f), 0.3f, "distance fade at the middle of its range");
                material.SetFloat("_Camera_Distance_Fade", 0);

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
                material.SetTexture("_Main_Texture", Levels(256, 256, mip => mip <= 3 ? texel : mip == 4 ? new Color(0.2f, 0, 0, 1) : new Color(0.9f, 0, 0, 1)));
                Expect(material, mesh, camera, target, sample, 16, new Color(0.36f, 0, 0.24f), 0.6f, "main: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, mesh, camera, target, sample, 16, new Color(0.04f, 0, 0.16f), 0.2f, "main: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                // Gradient map: a red ramp (point-sampled, 200 texels, no mips) makes the gradient lookup
                // frac(mask * 0.5) with mask = u, moving 0.5/32 per pixel: a 256 x 1 gradient strip reads level 2
                // unbiased and 3 at bias 1; twice would read 4. The centre column reads ramp texel 103 (0.5175);
                // the colour is the gradient plus half the mask, premultiplied by the alpha (the mask).
                var ramp = new Color[200];
                for (int i = 0; i < ramp.Length; i++) ramp[i] = new Color((i + 0.5f) / 200, 0, 0, 1);
                material.SetTexture("_Main_Texture", Texels(ramp));
                material.EnableKeyword("_GRADIENTMAPENABLE");
                material.SetTexture("_gradientMap", Levels(256, 1, mip => mip <= 2 ? Color.black : mip == 3 ? Color.green : Color.blue));
                float m = 103.5f / 200;
                Expect(material, mesh, camera, target, sample, 16, new Color(0.5f * m, 0.5f * m, 0.5f * m) * m, m, "gradient: no bias reads level 2");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(material, mesh, camera, target, sample, 16, new Color(0.5f * m, 1 + 0.5f * m, 0.5f * m) * m, m, "gradient: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.DisableKeyword("_GRADIENTMAPENABLE");
                material.SetTexture("_Main_Texture", Texels(texel, texel, texel, texel));

                // Back faces flip the normal, so a head-on back face has no fresnel and vanishes.
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                Expect(material, mesh, camera, target, sample, 16, Color.black, 0f, "a back face flips the fresnel normal");
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };

                // Orthographic: the view direction is the view matrix's third row. A camera pitched and
                // turned (a single-axis turn leaves that block symmetric) over a tilted normal separates
                // the row from the column, which the case checks before relying on it.
                Vector3 tilted = new Vector3(0.381f, 0.889f, -0.254f).normalized;
                mesh.normals = new[] { tilted, tilted, tilted, tilted };
                camera.transform.rotation = Quaternion.Euler(30, 40, 0);
                camera.transform.position = -2 * camera.transform.forward;
                Matrix4x4 view = camera.worldToCameraMatrix;
                Vector3 row = new Vector3(view.m20, view.m21, view.m22);
                Vector3 column = new Vector3(view.m02, view.m12, view.m22);
                Require(Mathf.Abs(Mathf.Clamp01(Vector3.Dot(tilted, row)) - Mathf.Clamp01(Vector3.Dot(tilted, column))) > 0.1f,
                        "the orthographic case separates the view row from the column");
                Shader.SetGlobalVector("unity_OrthoParams", new Vector4(1, 1, 0, 1));
                PublishCameraMatrices(camera);
                // Far depth is raw0 on reversed-Z APIs and raw1 on GL; this preserves the original facing oracle.
                Shader.SetGlobalTexture("_CameraDepthTexture", SystemInfo.usesReversedZBuffer ? Texture2D.blackTexture : Texture2D.whiteTexture);
                afterDepthOverride?.Invoke();
                float facing = Mathf.Clamp01(Vector3.Dot(tilted, row));
                Expect(material, mesh, camera, target, sample, 16, new Color(0.36f, 0, 0.24f) * facing, 0.6f * facing,
                       "orthographic fresnel against the camera's backward axis (" + facing + ")");
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                camera.transform.rotation = Quaternion.identity;
                camera.transform.position = new Vector3(0, 0, -2);
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };

                // Interior depth separates opposite screen rows and raw-depth-to-clip conversion.
                var depths = new Texture2D(1, 32, TextureFormat.RFloat, false, true);
                owned.Add(depths);
                depths.filterMode = FilterMode.Point;
                depths.wrapMode = TextureWrapMode.Clamp;
                Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
                for (int y = 0; y < 32; y++)
                {
                    float eyeDepth = y < 16 ? 2 : 4;
                    Vector4 clip = gpuProjection * new Vector4(0, 0, -eyeDepth, 1);
                    float rawDepth = clip.z / clip.w;
                    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore) rawDepth = rawDepth * .5f + .5f;
                    // Match logical screen rows through the API's texture UV origin.
                    int textureRow = SystemInfo.graphicsUVStartsAtTop ? depths.height - 1 - y : y;
                    depths.SetPixel(0, textureRow, new Color(rawDepth, 0, 0, 1));
                }
                depths.Apply(false, false);
                Shader.SetGlobalTexture("_CameraDepthTexture", depths);
                Shader.SetGlobalVector("unity_OrthoParams", new Vector4(1, 1, 0, 1));
                PublishCameraMatrices(camera);
                material.SetFloat("_DepthFade_Distance", 6);
                Expect(material, mesh, camera, target, sample, 8, new Color(.06f, 0, .04f), .1f, "interior lower-row depth2 gives (2-1)/6");
                Expect(material, mesh, camera, target, sample, 24, new Color(.18f, 0, .12f), .3f, "interior upper-row depth4 gives (4-1)/6");
                material.SetFloat("_DepthFade_Distance", 1);

                camera.orthographic = false;
                camera.transform.position = new Vector3(0, 0, -9);
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, .05f));
                PublishCameraMatrices(camera);
                material.SetFloat("_Camera_Distance_Fade_Near", 8.9f);
                material.SetFloat("_Camera_Distance_Fade_Far", 9.1f);
                material.SetFloat("_Camera_Distance_Fade", 1);
                // Original near.1/far10/eye9 has w-z=890/99: this fade is89/198, distinct from eye-distance .5.
                float perspectiveFade = 89f / 198f;
                Expect(material, mesh, camera, target, sample, 16, new Color(.36f, 0, .24f) * perspectiveFade, .6f * perspectiveFade,
                    "perspective camera fade retains the original reversed clip quantity");

                Debug.Log("Project Lucid HL_Particle_AnimMesh GPU verification passed: 25 Metal-derived cases on " + SystemInfo.graphicsDeviceType +
                          ". In-game presentation QA remains unverified.");
            }
            finally
            {
                foreach (string name in vectors) Shader.SetGlobalVector(name, priorVectors[name]);
                foreach (string name in floats) Shader.SetGlobalFloat(name, priorFloats[name]);
                Shader.SetGlobalTexture("_CameraDepthTexture", priorDepth);
                Shader.SetGlobalMatrix("unity_MatrixV", priorView);
                Shader.SetGlobalMatrix("unity_MatrixInvVP", priorInverseVP);
                RenderTexture.active = previous;
                target.Release();
                foreach (UnityEngine.Object item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void PublishCameraMatrices(Camera camera)
        {
            // URP declares these independently; GL's matrix stack does not guarantee inverse-VP publication.
            Matrix4x4 view = camera.worldToCameraMatrix;
            Shader.SetGlobalMatrix("unity_MatrixV", view);
            Shader.SetGlobalMatrix("unity_MatrixInvVP", (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * view).inverse);
        }

        private static void Expect(Material material, Mesh mesh, Camera camera, RenderTexture target, Texture2D sample,
                                   int y, Color expected, float expectedAlpha, string label)
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
            if (!condition) throw new InvalidOperationException("HL_Particle_AnimMesh verification failed: " + message);
        }
    }
}
