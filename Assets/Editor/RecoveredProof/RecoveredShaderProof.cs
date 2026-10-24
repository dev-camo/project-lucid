using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // Shared harness for the recovered-shader proofs. The shipped bundles carry only Metal programs, so
    // each proof loads its maintained shader by the original asset GUID, compiles every pass for
    // Direct3D 11, and renders a quad into a linear float target to compare pixels with the decoded math.
    internal static class RecoveredShaderProof
    {
        public static Shader Load(string path, string name, string guid)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Require(name, shader != null && shader.name == name, "shader identity");
            Require(name, AssetDatabase.AssetPathToGUID(path) == guid, "original material binding GUID");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(name, message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            return shader;
        }

        // Compiles both stages of every pass for Direct3D 11 / StandaloneWindows64 under each keyword set.
        public static int CompileForWindows(Shader shader, params string[][] keywordSets)
            => Compile(shader, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, keywordSets);

        public static int CompileForOpenGL(Shader shader, params string[][] keywordSets)
            => Compile(shader, ShaderCompilerPlatform.OpenGLCore, BuildTarget.StandaloneLinux64, keywordSets);

        private static int Compile(Shader shader, ShaderCompilerPlatform platform, BuildTarget target,
                                   params string[][] keywordSets)
        {
            ShaderData.Subshader subshader = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0);
            int compiled = 0;
            for (int p = 0; p < subshader.PassCount; p++)
            {
                ShaderData.Pass pass = subshader.GetPass(p);
                foreach (string[] keywords in keywordSets)
                {
                    foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        ShaderData.VariantCompileInfo info = pass.CompileVariant(stage, keywords,
                            platform, target);
                        string errors = "";
                        foreach (ShaderMessage message in info.Messages)
                            if (message.severity == ShaderCompilerMessageSeverity.Error) errors += " " + message.message;
                        Require(shader.name, info.Success && errors.Length == 0,
                            platform + " compile failed: pass " + pass.Name + " " + stage + " [" + string.Join(" ", keywords) + "]" + errors);
                        compiled++;
                    }
                }
            }
            Debug.Log("Project Lucid " + shader.name + " compiled " + compiled + " " + platform + " programs across " + subshader.PassCount + " passes.");
            return compiled;
        }

        public static void Require(string shaderName, bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(shaderName + " verification failed: " + message);
        }

        // The rig restores the pipeline tag, the active target and the globals it saved, and destroys its
        // objects, both when disposed and when its constructor fails after changing them.
        public static void VerifyRigRestoresState()
        {
            const string label = "RecoveredShaderRig";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Recovered/HL_PFX_AlphaBlendAdd.shader");
            Require(label, shader != null, "probe shader");
            string pipeline = Shader.globalRenderPipeline;
            RenderTexture active = RenderTexture.active;
            Vector4 mipBias = Shader.GetGlobalVector("_GlobalMipBias");
            var sentinel = new RenderTexture(4, 4, 0);
            try
            {
                Shader.globalRenderPipeline = "LucidRigSentinel";
                RenderTexture.active = sentinel;
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(7, 0, 0, 0));
                void CheckRestored(string when)
                {
                    Require(label, Shader.globalRenderPipeline == "LucidRigSentinel", when + ": pipeline tag restored");
                    Require(label, RenderTexture.active == sentinel, when + ": active target restored");
                    Require(label, Shader.GetGlobalVector("_GlobalMipBias").x == 7, when + ": saved global restored");
                    foreach (GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())
                        Require(label, item.name != "ShaderVerificationCamera", when + ": rig objects destroyed");
                }

                new RecoveredShaderRig(shader).Dispose();
                CheckRestored("after Dispose");

                RecoveredShaderRig.ConstructionProbe = () => throw new InvalidOperationException("construction probe");
                bool threw = false;
                try { new RecoveredShaderRig(shader); }
                catch (InvalidOperationException e) when (e.Message == "construction probe") { threw = true; }
                Require(label, threw, "the probe fails construction");
                CheckRestored("after a failed constructor");
                Debug.Log("Project Lucid RecoveredShaderRig restores state after Dispose and after a failed constructor.");
            }
            finally
            {
                RecoveredShaderRig.ConstructionProbe = null;
                Shader.globalRenderPipeline = pipeline;
                RenderTexture.active = active;
                Shader.SetGlobalVector("_GlobalMipBias", mipBias);
                UnityEngine.Object.DestroyImmediate(sentinel);
            }
        }
    }

    // A 32x32 float target, a camera looking down +Z from (0, 0, -2), and a unit quad facing it. Every
    // global the proof writes is saved first and restored on Dispose, along with the render pipeline tag.
    internal sealed class RecoveredShaderRig : IDisposable
    {
        public readonly Camera Camera;
        public readonly Mesh Quad;
        // Off for proofs that set unity_OrthoParams themselves (e.g. to take a shader's perspective branch).
        public bool WriteOrthoParams = true;
        private readonly string shaderName;
        private readonly RenderTexture target;
        private readonly Texture2D sample;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
        private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        private readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        private readonly string previousPipeline;
        private readonly RenderTexture previousTarget;

        // Test hook: invoked once construction has changed global state and allocated its resources, so a
        // proof can check that a failing constructor still restores and frees everything.
        internal static Action ConstructionProbe;

        public RecoveredShaderRig(Shader shader)
        {
            shaderName = shader.name;
            RecoveredShaderProof.Require(shaderName, SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
                "GPU verification requires graphics; omit -nographics");
            previousPipeline = Shader.globalRenderPipeline;
            previousTarget = RenderTexture.active;
            try
            {
                Shader.globalRenderPipeline = "UniversalPipeline";
                SaveVectors("_GlobalMipBias", "_WorldSpaceCameraPos", "unity_OrthoParams");
                RecoveredShaderProof.Require(shaderName, shader.isSupported, "shader unsupported on current graphics device");

                Quad = Own(new Mesh());
                Quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                Quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                Quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                Quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                SetUv0(Vector4.zero);
                Quad.RecalculateBounds();

                target = Own(new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
                target.Create();
                sample = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
                var cameraObject = Own(new GameObject("ShaderVerificationCamera"));
                Camera = cameraObject.AddComponent<Camera>();
                Camera.enabled = false;
                Camera.orthographic = true;
                Camera.orthographicSize = 1;
                Camera.aspect = 1;
                Camera.nearClipPlane = 0.1f;
                Camera.farClipPlane = 10;
                Camera.transform.position = new Vector3(0, 0, -2);

                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                ConstructionProbe?.Invoke();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public T Own<T>(T item) where T : UnityEngine.Object
        {
            owned.Add(item);
            return item;
        }

        // UV0 per corner: xy run 0..1 across the quad; zw carry the given custom stream values.
        public void SetUv0(Vector4 custom)
        {
            Quad.SetUVs(0, new List<Vector4>
            {
                new Vector4(0, 0, custom.z, custom.w), new Vector4(1, 0, custom.z, custom.w),
                new Vector4(1, 1, custom.z, custom.w), new Vector4(0, 1, custom.z, custom.w),
            });
        }

        public void SetNormals(Vector3 normal) => Quad.normals = new[] { normal, normal, normal, normal };

        public void SetColors(Color color) => Quad.colors = new[] { color, color, color, color };

        // A size x size point-sampled grid whose texel (x, y) is (x / (size - 1), y / (size - 1), 0, 1),
        // so the colour read back names the texel a lookup landed on.
        public Texture2D Grid(int size)
        {
            var texture = Own(new Texture2D(size, size, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(x / (size - 1f), y / (size - 1f), 0, 1);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // The Grid texel a UV lands on; fails if the UV sits within 0.1 texel of a boundary.
        public Color GridColor(int size, Vector2 uv)
        {
            float x = uv.x * size, y = uv.y * size;
            RecoveredShaderProof.Require(shaderName, Mathf.Abs(x - Mathf.Round(x)) > 0.1f && Mathf.Abs(y - Mathf.Round(y)) > 0.1f,
                "expected UV " + uv + " is too close to a texel boundary");
            return new Color(Mathf.Clamp(Mathf.Floor(x), 0, size - 1) / (size - 1f), Mathf.Clamp(Mathf.Floor(y), 0, size - 1) / (size - 1f), 0, 1);
        }

        // Clip-space w - z of a world point, as the GPU sees it (reversed Z where the platform uses it).
        public float ClipDistance(Vector3 positionWS)
        {
            Vector4 clip = GL.GetGPUProjectionMatrix(Camera.projectionMatrix, true) * Camera.worldToCameraMatrix *
                           new Vector4(positionWS.x, positionWS.y, positionWS.z, 1);
            return clip.w - clip.z;
        }

        public void SaveVectors(params string[] names)
        {
            foreach (string name in names)
                if (!vectors.ContainsKey(name)) vectors[name] = Shader.GetGlobalVector(name);
        }

        public void SaveFloats(params string[] names)
        {
            foreach (string name in names)
                if (!floats.ContainsKey(name)) floats[name] = Shader.GetGlobalFloat(name);
        }

        public void SaveTextures(params string[] names)
        {
            foreach (string name in names)
                if (!textures.ContainsKey(name)) textures[name] = Shader.GetGlobalTexture(name);
        }

        public Texture2D Texels(params Color[] colors)
        {
            var texture = Own(new Texture2D(colors.Length, 1, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
            texture.SetPixels(colors);
            texture.Apply(false, false);
            return texture;
        }

        // Bands of 16 texels per colour, for samplers that filter bilinearly regardless of the texture
        // (a graph's inline sampler states): lookups away from band edges read the band colour exactly.
        public Texture2D Bands(params Color[] colors)
        {
            var pixels = new Color[colors.Length * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colors[i / 16];
            return Texels(pixels);
        }

        // A 4x4 mipmapped texture: mip 0 is one colour and every smaller mip another.
        public Texture2D Mipped(Color mip0, Color smaller)
        {
            var texture = Own(new Texture2D(4, 4, TextureFormat.RGBAFloat, true, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                int width = Math.Max(1, 4 >> mip);
                var pixels = new Color[width * width];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = mip == 0 ? mip0 : smaller;
                texture.SetPixels(pixels, mip);
            }
            texture.Apply(false, false);
            return texture;
        }

        // A size x size texture (size a power of two) with its full mip chain, each level coloured by level(index),
        // filtered bilinearly within a level and point between levels (as the graphs' inline samplers are).
        // Spanning UV 0..1 across the 32-pixel quad, a 256 texture is read at level 3 without bias.
        public Texture2D MipLevels(int size, Func<int, Color> level) => MipLevels(size, size, level);

        // As above for a width x height texture: a 256 x 1 strip keeps a lookup that moves along both U and V
        // (such as a gradient read at u.xx) at the footprint of its U movement alone.
        public Texture2D MipLevels(int width, int height, Func<int, Color> level)
        {
            var texture = Own(new Texture2D(width, height, TextureFormat.RGBAFloat, true, true)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat });
            for (int mip = 0; mip < texture.mipmapCount; mip++)
            {
                var pixels = new Color[Math.Max(1, width >> mip) * Math.Max(1, height >> mip)];
                Color colour = level(mip);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
                texture.SetPixels(pixels, mip);
            }
            texture.Apply(false, false);
            return texture;
        }

        // Renders the mesh with one pass (the camera's matrices, Unity's view globals) and reads one pixel.
        // Per-draw values that DrawMeshNow cannot carry, such as SH coefficients, go through a command
        // buffer draw with a property block instead.
        public Color Draw(Material material, int pass = 0, Mesh mesh = null, Matrix4x4? model = null, int x = 16, int y = 16,
                          MaterialPropertyBlock properties = null)
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            Shader.SetGlobalVector("_WorldSpaceCameraPos", Camera.transform.position);
            if (WriteOrthoParams)
                Shader.SetGlobalVector("unity_OrthoParams", new Vector4(Camera.orthographicSize, Camera.orthographicSize, 0,
                    Camera.orthographic ? 1 : 0));
            if (properties != null)
            {
                using (var commands = new CommandBuffer { name = "ShaderVerification" })
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));
                    commands.SetViewProjectionMatrices(Camera.worldToCameraMatrix, Camera.projectionMatrix);
                    commands.DrawMesh(mesh ?? Quad, model ?? Matrix4x4.identity, material, 0, pass, properties);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target;
            }
            else
            {
                GL.PushMatrix();
                try
                {
                    GL.LoadProjectionMatrix(Camera.projectionMatrix);
                    GL.modelview = Camera.worldToCameraMatrix;
                    RecoveredShaderProof.Require(shaderName, material.SetPass(pass), "pass " + pass + " did not bind");
                    Graphics.DrawMeshNow(mesh ?? Quad, model ?? Matrix4x4.identity);
                }
                finally { GL.PopMatrix(); }
            }
            sample.ReadPixels(new Rect(x, y, 1, 1), 0, 0, false);
            sample.Apply(false);
            return sample.GetPixel(0, 0);
        }

        public void Expect(Material material, Color expected, string label, int pass = 0, Mesh mesh = null,
                           Matrix4x4? model = null, int x = 16, int y = 16, float tolerance = 0.01f,
                           MaterialPropertyBlock properties = null)
        {
            Color actual = Draw(material, pass, mesh, model, x, y, properties);
            RecoveredShaderProof.Require(shaderName,
                Mathf.Abs(actual.r - expected.r) < tolerance && Mathf.Abs(actual.g - expected.g) < tolerance &&
                Mathf.Abs(actual.b - expected.b) < tolerance && Mathf.Abs(actual.a - expected.a) < tolerance,
                label + ": expected=" + expected + " actual=" + actual);
        }

        // Restores every saved global, the pipeline tag and the active target, and destroys what the rig made.
        // Safe on a partly constructed rig.
        public void Dispose()
        {
            foreach (var pair in vectors) Shader.SetGlobalVector(pair.Key, pair.Value);
            foreach (var pair in floats) Shader.SetGlobalFloat(pair.Key, pair.Value);
            foreach (var pair in textures) Shader.SetGlobalTexture(pair.Key, pair.Value);
            Shader.globalRenderPipeline = previousPipeline;
            RenderTexture.active = previousTarget;
            if (target != null) target.Release();
            foreach (UnityEngine.Object item in owned)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            owned.Clear();
        }
    }
}
