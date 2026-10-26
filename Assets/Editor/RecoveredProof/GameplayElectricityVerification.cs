using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/HL_Gameplay_Electricity, reconstructed from the shipped Metal programs: vertex colour times
    // the colour, faded by saturate(2 v (1 - v)), premultiplied by its own alpha; transparent alpha or opaque 1; and
    // the DepthOnly depth.
    public static class GameplayElectricityVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Gameplay_Electricity.shader";
        private const string ShaderName = "HardLight/Gameplay/HL_Gameplay_Electricity";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "9e626579d1f15764fb66332d338bae7f";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_SURFACE_TYPE_TRANSPARENT", "_WRITE_RENDERING_LAYERS", "FOG_LINEAR" },
            new[] { "INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_CASTING_PUNCTUAL_LIGHT_SHADOW", "_ALPHATEST_ON" },
            new[] { "DOTS_INSTANCING_ON", "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "FOG_EXP2", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        // The edge fade saturate(2 v (1 - v)) at a pixel row of the 32-pixel quad.
        private static float Edge(int row)
        {
            float v = (row + 0.5f) / 32f;
            return Mathf.Clamp01(2 * v * (1 - v));
        }

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 0);
                material.SetColor("_Color", Color.white);
                // The colour c = edge * vertex colour * colour writes (c.rgb * c.a, c.a).
                void Expect(Color c, string label, int y = 16, bool opaque = false)
                {
                    rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, opaque ? 1 : c.a), label, y: y);
                    cases++;
                }
                float e = Edge(16);

                Expect(new Color(e, e, e, e), "white, faded at the centre row");
                Expect(new Color(Edge(2), Edge(2), Edge(2), Edge(2)), "faded towards v 0", 2);
                Expect(new Color(Edge(29), Edge(29), Edge(29), Edge(29)), "faded towards v 1", 29);
                material.SetColor("_Color", new Color(1, 0, 1, 1));
                Expect(new Color(e, 0, e, e), "times the colour");
                material.SetColor("_Color", new Color(1, 1, 1, 0.5f));
                Expect(new Color(e, e, e, 0.5f * e), "the colour's alpha");
                material.SetColor("_Color", Color.white);
                rig.SetColors(new Color(0, 1, 0, 0.5f));
                Expect(new Color(0, e, 0, 0.5f * e), "times the vertex colour and alpha");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Expect(new Color(0, e, 0, 0.5f * e), "opaque programs keep the premultiplied colour and write alpha 1", opaque: true);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                rig.SetColors(Color.white);

                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Gameplay_Electricity GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The ShadowCaster pass remains compile-checked only.");
            }
        }
    }
}
