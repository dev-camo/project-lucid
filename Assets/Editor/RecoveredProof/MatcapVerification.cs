using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Matcap, reconstructed from the shipped Metal programs: the rotated matcap
    // lookup with its tiling, brightness and intensity, the vertex-colour toggle, the ambient SH blend and
    // the graph's own linear fog with _Remove_Scene_Fog.
    public static class MatcapVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Matcap.shader";
        private const string ShaderName = "HardLight/Environment/HL_Matcap";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "fc62d258c9bcd2648b4158e86c2d4178";
        private const int GridSize = 5;
        private const float Push = 0.4f;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(),
            new string[0],
            new[] { "FOG_LINEAR", "INSTANCING_ON", "_WRITE_RENDERING_LAYERS" },
            new[] { "FOG_EXP2", "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" });

        // The decoded lookup for a view-space normal seen head on: 0.5 + push * the normal's xy, rotated by
        // -degrees, i.e. (nx cos + ny sin, ny cos - nx sin).
        private static Vector2 MatcapUv(Vector3 n, float degrees)
        {
            float c = Mathf.Cos(degrees * Mathf.Deg2Rad), s = Mathf.Sin(degrees * Mathf.Deg2Rad);
            return new Vector2(0.5f + Push * (n.x * c + n.y * s), 0.5f + Push * (n.y * c - n.x * s));
        }

        public static void Run()
        {
            Shader shader = Load();
            bool fogWasOn = Shader.IsKeywordEnabled("FOG_LINEAR");
            try
            {
                using (var rig = new RecoveredShaderRig(shader))
                {
                    RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                    rig.SaveVectors("unity_FogParams", "unity_FogColor", "_ProjectionParams");
                    Shader.DisableKeyword("FOG_LINEAR");
                    Material material = rig.Own(new Material(shader));
                    material.SetTexture("_Matcap_Tex", rig.Grid(GridSize));
                    material.SetFloat("_Matcap_Border_Push_Amount", Push);
                    material.SetFloat("_Matcap_Rotation", 0);

                    rig.Expect(material, rig.GridColor(GridSize, new Vector2(0.5f, 0.5f)), "facing normal reads the matcap centre");
                    rig.SetNormals(new Vector3(0.6f, -0.6f, -0.5292f));
                    Vector3 normalVS = new Vector3(0.6f, -0.6f, 0.5292f);
                    rig.Expect(material, rig.GridColor(GridSize, MatcapUv(normalVS, 0)), "rotation 0");
                    material.SetFloat("_Matcap_Rotation", 90);
                    rig.Expect(material, rig.GridColor(GridSize, MatcapUv(normalVS, 90)), "rotation 90");
                    material.SetFloat("_Matcap_Rotation", 180);
                    rig.Expect(material, rig.GridColor(GridSize, MatcapUv(normalVS, 180)), "rotation 180 (the default)");
                    material.SetFloat("_Matcap_Rotation", 0);

                    // A pitched and turned orthographic camera: rows of the view matrix, not its columns.
                    Transform eye = rig.Camera.transform;
                    eye.rotation = Quaternion.Euler(35, 60, 0);
                    eye.position = -2 * eye.forward;
                    Vector3 pitchedVS = new Vector3(-0.4f, 0.45f, 0.7984f);
                    Vector3 pitchedWS = rig.Camera.cameraToWorldMatrix.MultiplyVector(pitchedVS);
                    Vector2 right = MatcapUv(pitchedVS, 0);
                    Vector2 wrong = MatcapUv(rig.Camera.worldToCameraMatrix.transpose.MultiplyVector(pitchedWS), 0);
                    RecoveredShaderProof.Require(ShaderName, Mathf.Floor(right.x * GridSize) != Mathf.Floor(wrong.x * GridSize) ||
                                                             Mathf.Floor(right.y * GridSize) != Mathf.Floor(wrong.y * GridSize),
                        "camera pose does not separate the view matrix's rows from its columns");
                    rig.SetNormals(Quaternion.Inverse(eye.rotation) * pitchedWS);
                    rig.Expect(material, rig.GridColor(GridSize, right), "rotated orthographic view", model: Matrix4x4.Rotate(eye.rotation));
                    eye.rotation = Quaternion.identity;
                    eye.position = new Vector3(0, 0, -2);
                    rig.SetNormals(Vector3.back);

                    // Tiling then offset after the push: u = 0.5 * 0.5 + 0.4 lands on column 3.
                    material.SetTextureScale("_Matcap_Tex", new Vector2(0.5f, 1));
                    material.SetTextureOffset("_Matcap_Tex", new Vector2(0.4f, 0));
                    rig.Expect(material, rig.GridColor(GridSize, new Vector2(0.65f, 0.5f)), "matcap tiling and offset");
                    material.SetTextureScale("_Matcap_Tex", Vector2.one);
                    material.SetTextureOffset("_Matcap_Tex", Vector2.zero);

                    // (matcap + brightness) * intensity on the centre texel (0.5, 0.5, 0).
                    material.SetFloat("_Matcap_Brightness", 0.25f);
                    material.SetFloat("_Matcap_Intensity", 2);
                    rig.Expect(material, new Color(1.5f, 1.5f, 0.5f, 1), "brightness then intensity");
                    material.SetFloat("_Matcap_Brightness", 0);
                    material.SetFloat("_Matcap_Intensity", 1);

                    rig.SetColors(new Color(0, 1, 1, 1));
                    rig.Expect(material, new Color(0.5f, 0.5f, 0, 1), "vertex colour ignored by default");
                    material.SetFloat("_UseVertexColourTint", 1);
                    rig.Expect(material, new Color(0, 0.5f, 0, 1), "vertex colour tint");
                    material.SetFloat("_UseVertexColourTint", 0);
                    rig.SetColors(Color.white);

                    // Scene lighting: flat SH of 0.5 (per-draw, so set through a property block) halves the
                    // colour at full influence.
                    var flat = new SphericalHarmonicsL2();
                    flat.AddAmbientLight(new Color(0.5f, 0.5f, 0.5f));
                    var probe = new MaterialPropertyBlock();
                    probe.CopySHCoefficientArraysFrom(new[] { flat });
                    rig.Expect(material, new Color(0.5f, 0.5f, 0, 1), "scene lighting influence 0 ignores SH", properties: probe);
                    material.SetFloat("_Scene_Lighting_Influence", 1);
                    rig.Expect(material, new Color(0.25f, 0.25f, 0, 1), "full scene lighting influence multiplies SH", properties: probe);
                    material.SetFloat("_Scene_Lighting_Influence", 0);

                    // Linear fog with factor = saturate(z * params.z + params.w), z = view depth beyond the near
                    // plane (2 - 0.1 here): factor 1 - 0.25 * 1.9 = 0.525, density 0.475 towards blue.
                    Shader.EnableKeyword("FOG_LINEAR");
                    Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                    Shader.SetGlobalVector("unity_FogParams", new Vector4(0, 0, -0.25f, 1));
                    Shader.SetGlobalVector("unity_FogColor", new Vector4(0, 0, 1, 1));
                    rig.Expect(material, new Color(0.5f, 0.5f, 0, 1), "_Remove_Scene_Fog (default on) removes fog");
                    material.SetFloat("_Remove_Scene_Fog", 0);
                    rig.Expect(material, new Color(0.2625f, 0.2625f, 0.475f, 1), "graph fog by view depth");
                    material.SetFloat("_Remove_Scene_Fog", 1);
                    Shader.DisableKeyword("FOG_LINEAR");

                    // _GlobalMipBias applies once (URP's SAMPLE_TEXTURE2D). A flat quad gives the matcap a constant UV
                    // and no derivatives, so splay the normals: the lookup then moves about 1.9 texels of a 256 texture
                    // per pixel (level 0.9, read as 1). Bias 2 reaches level 2.9 (3) once; twice would reach 4.9 (5).
                    rig.Quad.normals = new[] { new Vector3(-0.3f, -0.3f, -1), new Vector3(0.3f, -0.3f, -1), new Vector3(0.3f, 0.3f, -1), new Vector3(-0.3f, 0.3f, -1) };
                    Color green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1);
                    material.SetTexture("_Matcap_Tex", rig.MipLevels(256, level => level <= 2 ? Color.white : level <= 4 ? green : blue));
                    rig.Expect(material, Color.white, "no bias reads level 1");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(2, 0, 0, 0));
                    rig.Expect(material, green, "bias 2 applies once (level 3)");

                    Debug.Log("Project Lucid HL_Matcap GPU verification passed: 15 Metal-derived cases on " +
                              SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
                }
            }
            finally
            {
                if (fogWasOn) Shader.EnableKeyword("FOG_LINEAR");
                else Shader.DisableKeyword("FOG_LINEAR");
            }
        }
    }
}
