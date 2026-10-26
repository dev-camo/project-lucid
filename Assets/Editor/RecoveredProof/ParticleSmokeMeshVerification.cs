using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_SmokeMesh, reconstructed from the shipped Metal programs: the facing ratio
    // clipped at one minus the custom dissolve (UV0 z) in both passes, the vertex colour darkened by the shadow
    // factor away from the upward term, the opaque alpha and its alpha-to-coverage sharpening.
    public static class ParticleSmokeMeshVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_SmokeMesh.shader";
        private const string ShaderName = "HardLight/Particle/HL_Particle_SmokeMesh";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "aca849accd727724585fa4b4f236f834";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, 0);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_AlphaToMaskAvailable");
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                void Expect(Color expected, string label)
                {
                    rig.Expect(material, expected, label);
                    cases++;
                }
                bool Depth() => rig.Draw(material, 1).r != 0;
                void ExpectDepth(bool kept, string label)
                {
                    RecoveredShaderProof.Require(ShaderName, Depth() == kept, "DepthOnly: " + label);
                    cases++;
                }

                // The orthographic view direction is the view matrix's z row, (0, 0, -1) here: a facing normal has
                // a facing ratio of 1 and no upward term, so the colour is darkened by the shadow factor (0.5).
                var tint = new Color(0.8f, 0.4f, 0.2f, 1);
                Color clear = new Color(0, 0, 0, 0);
                rig.SetColors(tint);
                rig.SetUv0(new Vector4(0, 0, 1, 0));
                Expect(new Color(0.4f, 0.2f, 0.1f, 1), "a facing normal is fully shadowed");
                material.SetFloat("_ShadowFactor", 0.25f);
                Expect(new Color(0.2f, 0.1f, 0.05f, 1), "the shadow factor");
                material.SetFloat("_ShadowFactor", 0.5f);
                // Tilted up: facing 0.8, upward term saturate(dot((0, 2, 1), n)) = 1.2 - 0.8 = 0.4.
                var up = new Vector3(0, 0.6f, -0.8f);
                rig.SetNormals(up);
                Expect(new Color(0.56f, 0.28f, 0.14f, 1), "a normal tilted up lifts the colour by 0.4");
                rig.SetNormals(new Vector3(0, 0.8f, -0.6f));
                Expect(tint, "steeper still saturates the upward term");
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                Expect(new Color(0.4f, 0.2f, 0.1f, 1), "a sideways tilt has no upward term");

                // The clip: the facing ratio against 1 - UV0 z. A sideways tilt faces at 0.8.
                rig.SetUv0(new Vector4(0, 0, 0.25f, 0));
                Expect(new Color(0.4f, 0.2f, 0.1f, 1), "facing 0.8 survives threshold 0.75");
                ExpectDepth(true, "facing 0.8 survives threshold 0.75");
                rig.SetUv0(new Vector4(0, 0, 0.15f, 0));
                Expect(clear, "facing 0.8 is clipped at threshold 0.85");
                ExpectDepth(false, "facing 0.8 is clipped at threshold 0.85");
                rig.SetNormals(Vector3.back);
                Expect(new Color(0.4f, 0.2f, 0.1f, 1), "a facing normal survives threshold 0.85");
                rig.SetUv0(Vector4.zero);
                Expect(new Color(0.4f, 0.2f, 0.1f, 1), "facing 1 survives the full threshold");
                ExpectDepth(true, "facing 1 survives the full threshold");
                rig.SetNormals(new Vector3(0, 0.6f, 0.8f));
                rig.SetUv0(new Vector4(0, 0, 1, 0));
                // Facing away: the ratio saturates to 0, which survives threshold 0; the upward term is 1.
                Expect(tint, "a facing ratio of 0 survives threshold 0");
                ExpectDepth(true, "a facing ratio of 0 survives threshold 0");
                rig.SetNormals(Vector3.back);

                // With alpha-to-coverage available a zero threshold keeps alpha 1, and a facing ratio just under
                // the threshold ((a - clip - fwidth / 2) / max(fwidth, 1e-4) + 1) is sharpened to 0.5 and kept.
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 1);
                Expect(new Color(0.4f, 0.2f, 0.1f, 1), "a zero threshold keeps alpha 1 under alpha-to-coverage");
                rig.SetUv0(new Vector4(0, 0, 0.19995f, 0));
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                float sharpened = rig.Draw(material).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(sharpened - 0.5f) < 0.02f, "alpha-to-coverage sharpens it to 0.5: " + sharpened);
                cases++;
                Shader.SetGlobalFloat("_AlphaToMaskAvailable", 0);
                rig.SetNormals(Vector3.back);
                rig.SetUv0(Vector4.zero);
                rig.SetColors(Color.white);

                Debug.Log("Project Lucid HL_Particle_SmokeMesh GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The perspective view direction remains compile-checked only.");
            }
        }
    }
}
