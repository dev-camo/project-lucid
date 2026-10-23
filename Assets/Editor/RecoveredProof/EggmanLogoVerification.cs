using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    public static class EggmanLogoVerification
    {
        public static void Run()
        {
            string previous = Shader.globalRenderPipeline;
            try
            {
                // This release's only subshader is tagged UniversalPipeline.
                // An unconfigured Editor otherwise selects its error fallback.
                Shader.globalRenderPipeline = "UniversalPipeline";
                RunUnderUniversalPipeline();
            }
            finally { Shader.globalRenderPipeline = previous; }
        }

        private static void RunUnderUniversalPipeline()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Recovered/HL_Gameplay_Eggman_Logo.shader");
            Require(shader != null && shader.name == "HardLight/Gameplay/HL_Gameplay_Eggman_Logo", "shader identity");
            Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shader)) == "109d879a0e474224f9576baad15f0273",
                    "original material binding GUID");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");
            Require(SystemInfo.supportedRenderTargetCount >= 2, "rendering-layer variant requires two render targets");

            // Every object below is owned from its allocation: a failed Require or
            // setup step still releases what was already made. The shader is borrowed.
            Material material = null;
            Texture2D mask = null, sample = null;
            Mesh mesh = null;
            RenderTexture target = null, layers = null;
            GameObject cameraObject = null;
            RenderTexture previous = RenderTexture.active;
            Vector4 previousMipBias = Shader.GetGlobalVector("_GlobalMipBias");
            int previousLayerMaximum = Shader.GetGlobalInt("_RenderingLayerMaxInt");
            float previousLayerReciprocal = Shader.GetGlobalFloat("_RenderingLayerRcpMaxInt");
            bool completed = false;
            try
            {
                material = new Material(shader);
                string[] passNames = new string[material.passCount];
                for (int pass = 0; pass < passNames.Length; pass++) passNames[pass] = material.GetPassName(pass);
                Require(material.passCount == 1 && material.GetPassName(0) == "Universal Forward",
                        "original single pass: count=" + material.passCount + " names=" + string.Join(" | ", passNames));
                Require(material.renderQueue == (int)RenderQueue.Transparent, "original transparent queue");
                Require(material.GetVector("_Colour_A") == new Vector4(1, 0, 0, 1) &&
                        material.GetVector("_Colour_B") == new Vector4(0, 1, 0, 1) &&
                        material.GetVector("_Colour_C") == new Vector4(0, 0, 1, 1) &&
                        material.GetFloat("_Alpha") == 1f, "original defaults");

                mask = new Texture2D(4, 4, TextureFormat.RGBAFloat, true, true);
                mask.filterMode = FilterMode.Point;
                mask.wrapMode = TextureWrapMode.Clamp;
                mesh = new Mesh();
                mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                layers = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
                cameraObject = new GameObject("EggmanLogoVerificationCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 1;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10;
                camera.transform.position = new Vector3(0, 0, -2);
                target.Create();
                layers.Create();
                material.SetTexture("_Mask", mask);
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 1, 0, 0));
                Shader.SetGlobalInt("_RenderingLayerMaxInt", 255);
                Shader.SetGlobalFloat("_RenderingLayerRcpMaxInt", 1f / 255f);
                Color background = new Color(0.17f, 0.29f, 0.43f, 0.31f);
                Vector4 a = new Vector4(0.83f, 0.13f, 0.31f, 0.21f);
                Vector4 b = new Vector4(0.17f, 0.91f, 0.23f, 0.37f);
                Vector4 c = new Vector4(0.11f, 0.27f, 0.79f, 0.64f);
                Color[] masks = { new Color(1,0,0,1), new Color(0,1,0,1), new Color(0,0,1,1),
                                  new Color(0.25f,0.4f,0.6f,0.7f), new Color(0,0,0,1),
                                  new Color(0.8f,0.2f,0.3f,0), new Color(0.9f,0.7f,0.1f,0.5f) };
                int calculations = 0;
                foreach (bool writeLayers in new[] { false, true })
                {
                    if (writeLayers) material.EnableKeyword("_WRITE_RENDERING_LAYERS");
                    else material.DisableKeyword("_WRITE_RENDERING_LAYERS");
                    foreach (Color value in masks)
                    {
                        FillMip(mask, 0, value);
                        mask.Apply(false, false);
                        SetColours(material, a, b, c, 0.73f);
                        Draw(material, mesh, camera, target, layers, background, writeLayers, Matrix4x4.identity, true);
                        AssertPixel(Read(target, sample), Composite(MetalReference(value, ShaderColour(a), ShaderColour(b), ShaderColour(c), 0.73f), background),
                                    "mask=" + value + " renderingLayers=" + writeLayers);
                        if (writeLayers)
                            // Blob 5 writes layer alpha zero. The original non-separate
                            // transparent blend leaves target 1's cleared colour intact.
                            AssertPixel(Read(layers, sample), background, "rendering-layer target blend");
                        calculations++;
                    }
                    Color chosen = new Color(0.25f,0.4f,0.6f,1);
                    FillMip(mask, 0, chosen);
                    mask.Apply(false, false);
                    SetColours(material, a, b, c, 0);
                    Draw(material, mesh, camera, target, layers, background, writeLayers, Matrix4x4.identity, true);
                    AssertPixel(Read(target, sample), background, "zero material alpha");
                    SetColours(material, new Vector4(1,0,0,-1), new Vector4(0,1,0,-1), new Vector4(0,0,1,-1), 1);
                    Draw(material, mesh, camera, target, layers, background, writeLayers, Matrix4x4.identity, true);
                    AssertPixel(Read(target, sample), background, "negative colour alpha saturates to zero");
                    calculations += 2;
                }

                material.DisableKeyword("_WRITE_RENDERING_LAYERS");
                SetColours(material, new Vector4(1,0,0,1), new Vector4(0,1,0,1), new Vector4(0,0,1,1), 1);
                FillMip(mask, 0, new Color(1,0,0,1));
                FillMip(mask, 1, new Color(0,1,0,1));
                FillMip(mask, 2, new Color(0,0,1,1));
                mask.Apply(false, false);
                Draw(material, mesh, camera, target, layers, background, false, Matrix4x4.identity, true);
                AssertPixel(Read(target, sample), Color.red, "base mip, raw UV0");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(10, 1, 0, 0));
                Draw(material, mesh, camera, target, layers, background, false, Matrix4x4.identity, true);
                AssertPixel(Read(target, sample), Color.blue, "URP global mip bias reaches last mip");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(0, 1, 0, 0));

                // Far geometry still overlays a previous near draw: the pass has ZWrite Off.
                Draw(material, mesh, camera, target, layers, background, false, Matrix4x4.Translate(new Vector3(0,0,-0.5f)), true);
                SetColours(material, new Vector4(0,0,1,1), b, c, 1);
                Draw(material, mesh, camera, target, layers, background, false, Matrix4x4.Translate(new Vector3(0,0,0.5f)), false);
                AssertPixel(Read(target, sample), Color.blue, "disabled depth writes");
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                Draw(material, mesh, camera, target, layers, background, false, Matrix4x4.identity, true);
                AssertPixel(Read(target, sample), background, "back-face culling");
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    Require(message.severity.ToString() != "Error", message.message);
                Debug.Log("Project Lucid Eggman logo GPU verification passed: " + calculations +
                          " fragment/blend cases, both surviving variants, mip bias, depth writes and culling on " +
                          SystemInfo.graphicsDeviceType + ". Original-game presentation QA remains unverified.");
                completed = true;
            }
            finally
            {
                CleanUp(completed,
                    () => Shader.SetGlobalVector("_GlobalMipBias", previousMipBias),
                    () => Shader.SetGlobalInt("_RenderingLayerMaxInt", previousLayerMaximum),
                    () => Shader.SetGlobalFloat("_RenderingLayerRcpMaxInt", previousLayerReciprocal),
                    () => RenderTexture.active = previous,
                    () => { if (target != null) target.Release(); },
                    () => { if (layers != null) layers.Release(); },
                    () => Destroy(cameraObject),
                    () => Destroy(sample),
                    () => Destroy(target),
                    () => Destroy(layers),
                    () => Destroy(mesh),
                    () => Destroy(material),
                    () => Destroy(mask));
            }
        }

        private static void Destroy(UnityEngine.Object owned)
        {
            if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
        }

        // Runs every step even after one fails. After a completed proof the first
        // step failure propagates; after a failed one the proof's own exception
        // stays the result and step failures are logged as warnings.
        private static void CleanUp(bool completed, params Action[] steps)
        {
            Exception first = null;
            foreach (Action step in steps)
            {
                try { step(); }
                catch (Exception failure)
                {
                    if (completed && first == null) first = failure;
                    else Debug.LogWarning("Eggman logo verification cleanup failed: " + failure);
                }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }

        private static Color MetalReference(Color mask, Vector4 a, Vector4 b, Vector4 c, float alpha)
        {
            // Independent scalar transcription of blob 4/5's four FMA lanes.
            float[] colours = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float first = mask.r * a[i];
                float second = first + mask.g * (b[i] * mask.g - first);
                colours[i] = second + mask.b * (c[i] - second);
            }
            return new Color(colours[0], colours[1], colours[2], mask.a * Mathf.Clamp01(colours[3] * 2f) * alpha);
        }

        private static Color Composite(Color source, Color background)
        {
            return new Color(source.r * source.a + background.r * (1 - source.a),
                             source.g * source.a + background.g * (1 - source.a),
                             source.b * source.a + background.b * (1 - source.a),
                             source.a + background.a * (1 - source.a));
        }

        // The shipped project renders in Linear color space, where the engine
        // hands the shader the _Colour_A/B/C colour properties converted from
        // gamma to linear (rgb only; alpha and the cleared target stay as set).
        // In Gamma the values arrive unchanged.
        private static Vector4 ShaderColour(Vector4 colour)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return colour;
            return new Vector4(Mathf.GammaToLinearSpace(colour.x), Mathf.GammaToLinearSpace(colour.y),
                               Mathf.GammaToLinearSpace(colour.z), colour.w);
        }

        private static void SetColours(Material material, Vector4 a, Vector4 b, Vector4 c, float alpha)
        {
            material.SetVector("_Colour_A", a);
            material.SetVector("_Colour_B", b);
            material.SetVector("_Colour_C", c);
            material.SetFloat("_Alpha", alpha);
        }

        private static void FillMip(Texture2D mask, int mip, Color colour)
        {
            int width = Math.Max(1, mask.width >> mip);
            Color[] pixels = new Color[width * width];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
            mask.SetPixels(pixels, mip);
        }

        private static void Draw(Material material, Mesh mesh, Camera camera, RenderTexture target,
                                 RenderTexture layers, Color background, bool writeLayers, Matrix4x4 transform, bool clear)
        {
            if (writeLayers) Graphics.SetRenderTarget(new[] { target.colorBuffer, layers.colorBuffer }, target.depthBuffer);
            else Graphics.SetRenderTarget(target);
            if (clear) GL.Clear(true, true, background);
            GL.PushMatrix();
            try
            {
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                Require(material.SetPass(0), "pass did not bind");
                Graphics.DrawMeshNow(mesh, transform);
            }
            finally { GL.PopMatrix(); }
        }

        private static Color Read(RenderTexture target, Texture2D sample)
        {
            RenderTexture.active = target;
            sample.ReadPixels(new Rect(16, 16, 1, 1), 0, 0, false);
            sample.Apply(false);
            return sample.GetPixel(0, 0);
        }

        private static void AssertPixel(Color actual, Color expected, string message)
        {
            Require(Mathf.Abs(actual.r - expected.r) < 0.005f && Mathf.Abs(actual.g - expected.g) < 0.005f &&
                    Mathf.Abs(actual.b - expected.b) < 0.005f && Mathf.Abs(actual.a - expected.a) < 0.005f,
                    message + " expected=" + expected + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Eggman logo verification failed: " + message);
        }
    }
}
