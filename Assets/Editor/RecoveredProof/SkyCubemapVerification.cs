using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    public static class SkyCubemapVerification
    {
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

        private static void RunUnderUniversalPipeline()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Recovered/HL_Sky_Cubemap.shader");
            Require(shader != null && shader.name == "HardLight/Sky/HL_Sky_Cubemap", "shader identity");
            Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shader)) == "c24466ea4a824924e820666d8ffb14a4",
                    "original material binding GUID");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity.ToString() != "Error", message.message);
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU verification requires graphics; omit -nographics");
            Require(shader.isSupported, "shader unsupported on current graphics device");

            // Flat, distinct cubemap faces make the decoded Metal calculations
            // observable without depending on extracted art or scene lighting.
            Color[] faces = { Color.red, Color.yellow, Color.green, Color.magenta, Color.blue, Color.cyan };
            Cubemap cube = new Cubemap(4, TextureFormat.RGBAFloat, true);
            cube.filterMode = FilterMode.Trilinear;
            for (int mip = 0; mip < cube.mipmapCount; mip++)
            {
                int width = Math.Max(1, 4 >> mip);
                for (int face = 0; face < 6; face++)
                {
                    Color color = faces[face] * (mip == 0 ? 1f : 0.5f);
                    color.a = 1;
                    Color[] pixels = new Color[width * width];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
                    cube.SetPixels(pixels, (CubemapFace)face, mip);
                }
            }
            cube.Apply(false, false);
            Material material = new Material(shader);
            Mesh mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            RenderTexture target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Texture2D sample = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            GameObject cameraObject = new GameObject("ShaderVerificationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10;
            camera.transform.position = new Vector3(0, 0, -2);
            material.SetTexture("_Cubemap_Texture", cube);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                VerifyPixel(material, mesh, camera, target, sample, Vector3.back, 0, 0, 0, Color.cyan);
                VerifyPixel(material, mesh, camera, target, sample, Vector3.back, 90, 0, 0, Color.yellow);
                VerifyPixel(material, mesh, camera, target, sample, Vector3.up, 0, 0, 0, Color.green);
                VerifyPixel(material, mesh, camera, target, sample, Vector3.up, 0, 1, 0, Color.magenta);
                Color dim = Color.cyan * 0.5f; dim.a = 1;
                VerifyPixel(material, mesh, camera, target, sample, Vector3.back, 0, 0, 1, dim);
                Debug.Log("Project Lucid sky cubemap GPU verification passed: 5 Metal-derived cases on " + SystemInfo.graphicsDeviceType + ". Original-game presentation QA remains unverified.");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(sample);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        private static void VerifyPixel(Material material, Mesh mesh, Camera camera, RenderTexture target, Texture2D sample,
                                        Vector3 normal, float rotation, float flip, float lod, Color expected)
        {
            mesh.normals = new[] { normal, normal, normal, normal };
            material.SetFloat("_Cubemap_Rotation", rotation);
            material.SetFloat("_Flip_Y_Axis", flip);
            material.SetFloat("_Cubemap_LOD", lod);
            Graphics.SetRenderTarget(target);
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            try
            {
                GL.LoadProjectionMatrix(camera.projectionMatrix);
                GL.modelview = camera.worldToCameraMatrix;
                Require(material.SetPass(0), "pass did not bind");
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
            finally { GL.PopMatrix(); }
            sample.ReadPixels(new Rect(16, 16, 1, 1), 0, 0, false);
            sample.Apply(false);
            Color actual = sample.GetPixel(0, 0);
            Require(Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
                    Mathf.Abs(actual.b - expected.b) < 0.01f && Mathf.Abs(actual.a - 1f) < 0.01f,
                    "pixel differs: normal=" + normal + " rotation=" + rotation + " flip=" + flip + " LOD=" + lod +
                    " expected=" + expected + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Sky cubemap verification failed: " + message);
        }
    }
}
