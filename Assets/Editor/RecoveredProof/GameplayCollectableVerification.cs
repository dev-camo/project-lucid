using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Collectable, reconstructed from the shipped Metal programs: the
    // object-space spin about Y by game time, the matcap lookup from the cross product of the view-space
    // view direction and normal, the vertex-colour toggle, the tint and opaque alpha.
    public static class GameplayCollectableVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Collectable.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Collectable";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "79454e735e6b2824d92c0ebe4fc51d38";
        private const int GridSize = 5;
        private const float Push = 0.4f;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(),
            new string[0],
            new[] { "INSTANCING_ON", "_WRITE_RENDERING_LAYERS" },
            new[] { "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" });

        // Unity's view space for this rig: x right, y up, z towards the viewer. Facing the matcap straight
        // on gives uv = 0.5 + push * (normalVS.x, normalVS.y).
        private static Vector2 MatcapUv(Vector3 normalVS) =>
            new Vector2(0.5f + Push * normalVS.x, 0.5f + Push * normalVS.y);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Matcap_Tex", rig.Grid(GridSize));
                material.SetFloat("_Matcap_Border_Push_Amount", Push);
                material.SetColor("_Diff_Tint", Color.white);
                material.SetFloat("_Cull", 0);

                rig.Expect(material, rig.GridColor(GridSize, new Vector2(0.5f, 0.5f)), "facing normal reads the matcap centre");
                Vector3 tilted = new Vector3(0.6f, -0.6f, -0.5292f);
                rig.SetNormals(tilted);
                rig.Expect(material, rig.GridColor(GridSize, MatcapUv(new Vector3(0.6f, -0.6f, 0.5292f))), "tilted normal pushes the lookup");

                // A pitched and turned orthographic camera: the lookup uses the view matrix's rows, not its
                // columns. The transposed transform must land on a different texel for this to be a check.
                Transform eye = rig.Camera.transform;
                eye.rotation = Quaternion.Euler(35, 60, 0);
                eye.position = -2 * eye.forward;
                Vector3 normalVS = new Vector3(-0.4f, 0.45f, 0.7984f);
                Vector3 normalWS = rig.Camera.cameraToWorldMatrix.MultiplyVector(normalVS);
                Vector3 transposed = rig.Camera.worldToCameraMatrix.transpose.MultiplyVector(normalWS);
                Vector2 right = MatcapUv(normalVS), wrong = MatcapUv(transposed);
                RecoveredShaderProof.Require(ShaderName, Mathf.Floor(right.x * GridSize) != Mathf.Floor(wrong.x * GridSize) ||
                                                         Mathf.Floor(right.y * GridSize) != Mathf.Floor(wrong.y * GridSize),
                    "camera pose does not separate the view matrix's rows from its columns");
                rig.SetNormals(Quaternion.Inverse(eye.rotation) * normalWS);
                rig.Expect(material, rig.GridColor(GridSize, right), "rotated orthographic view",
                           model: Matrix4x4.Rotate(eye.rotation));
                eye.rotation = Quaternion.identity;
                eye.position = new Vector3(0, 0, -2);

                // Spin: game time pi/8 at speed 4 turns a quad built in the YZ plane by +90 degrees about Y,
                // (x, y, z) -> (z, y, -x), into the XY plane facing the camera, and its normal with it.
                Mesh side = rig.Own(new Mesh());
                side.vertices = new[] { new Vector3(0, -1, -1), new Vector3(0, -1, 1), new Vector3(0, 1, 1), new Vector3(0, 1, -1) };
                Vector3 sideNormal = new Vector3(0.6f, -0.6f, -0.5292f);
                side.normals = new[] { sideNormal, sideNormal, sideNormal, sideNormal };
                side.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                side.RecalculateBounds();
                material.SetFloat("_Rotation_Speed", 4);
                Shader.SetGlobalFloat("_HLShaderTimeGame", Mathf.PI / 8);
                // Spun normal (-0.5292, -0.6, -0.6) is (-0.5292, -0.6, 0.6) in view space; the opposite
                // turn would face it away and land on a different column.
                rig.Expect(material, rig.GridColor(GridSize, MatcapUv(new Vector3(-0.5292f, -0.6f, 0.6f))), "game-time spin about Y", mesh: side);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                rig.Expect(material, new Color(0, 0, 0, 0), "without spin the side quad is edge-on", mesh: side);
                material.SetFloat("_Rotation_Speed", 0);

                // Vertex colour tint toggle, then the colour tint; alpha stays 1.
                rig.SetNormals(Vector3.back);
                rig.SetColors(new Color(0, 1, 1, 1));
                rig.Expect(material, new Color(0.5f, 0.5f, 0, 1), "vertex colour ignored by default");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 1);
                rig.Expect(material, new Color(0, 0.5f, 0, 1), "vertex colour tint");
                material.SetFloat("_USE_VERTEX_COLOUR_TINT", 0);
                rig.SetColors(Color.white);
                material.SetColor("_Diff_Tint", new Color(0, 1, 1, 0));
                rig.Expect(material, new Color(0, 0.5f, 0, 1), "diff tint, alpha 1");
                material.SetColor("_Diff_Tint", Color.white);

                // _GlobalMipBias applies once (URP's SAMPLE_TEXTURE2D). A flat quad gives the matcap a constant UV
                // and no derivatives, so splay the normals: the lookup then moves about 1.9 texels of a 256 texture
                // per pixel (level 0.9, read as 1). Bias 2 reaches level 2.9 (3) once; twice would reach 4.9 (5).
                rig.Quad.normals = new[] { new Vector3(-0.3f, -0.3f, -1), new Vector3(0.3f, -0.3f, -1), new Vector3(0.3f, 0.3f, -1), new Vector3(-0.3f, 0.3f, -1) };
                Color green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1);
                material.SetTexture("_Matcap_Tex", rig.MipLevels(256, level => level <= 2 ? Color.white : level <= 4 ? green : blue));
                rig.Expect(material, Color.white, "no bias reads level 1");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(2, 0, 0, 0));
                rig.Expect(material, green, "bias 2 applies once (level 3)");

                Debug.Log("Project Lucid HL_Gameplay_Collectable GPU verification passed: 10 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
