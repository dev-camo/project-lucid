using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Gameplay/Blob Shadow/HL_Blob_Shadow, reconstructed from the shipped Metal programs around Shader Graph's
    // generated screen-space decal passes. Through the decal mesh pass: the alpha is the base map's red, saturated (the
    // colour is the base colour, lit, and the projector pass needs the decal renderer's per-decal data, so those remain
    // compile-checked).
    public static class GameplayBlobShadowVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Blob_Shadow.shader";
        private const string ShaderName = "HardLight/Gameplay/Blob Shadow/HL_Blob_Shadow";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448): the
        // Assets/Data/Art copy, which the blob shadow materials use.
        private const string OriginalGuid = "93a675ddf129efd40864a975e8f45a20";
        private const int Mesh = 1;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new[] { "_DECAL_NORMAL_BLEND_LOW" },
            new[] { "_DECAL_NORMAL_BLEND_LOW", "_DECAL_LAYERS", "_ADDITIONAL_LIGHTS", "_MAIN_LIGHT_SHADOWS", "_SHADOWS_SOFT", "FOG_LINEAR", "INSTANCING_ON" },
            new[] { "_DECAL_NORMAL_BLEND_LOW", "_DECAL_LAYERS", "_ADDITIONAL_LIGHTS", "_MAIN_LIGHT_SHADOWS_CASCADE" },
            new[] { "_DECAL_NORMAL_BLEND_MEDIUM", "_MAIN_LIGHT_SHADOWS_SCREEN", "_ADDITIONAL_LIGHTS_VERTEX", "_FORWARD_PLUS", "FOG_EXP", "_LIGHT_COOKIES", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT_HIGH" },
            new[] { "_DECAL_NORMAL_BLEND_HIGH", "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK", "FOG_EXP2", "DOTS_INSTANCING_ON" },
            new[] { "_DECAL_NORMAL_BLEND_LOW", "DYNAMICLIGHTMAP_ON", "_SHADOWS_SOFT_LOW", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), KeywordSets);

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                // The pass blends SrcAlpha OneMinusSrcAlpha for colour and alpha alike over a cleared target, so an alpha
                // a reads back as a * a.
                void Alpha(Color baseMap, float expected, string label)
                {
                    material.SetTexture("_Base_Map", rig.Texels(baseMap));
                    float actual = rig.Draw(material, Mesh).a;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(actual - expected * expected) < 1e-3f, label + ": " + actual);
                    cases++;
                }

                Alpha(new Color(1, 0, 0, 0), 1, "red 1 is opaque (the base map's alpha unused)");
                Alpha(new Color(0.5f, 1, 1, 1), 0.5f, "red 0.5");
                Alpha(new Color(0, 1, 1, 1), 0, "red 0 is transparent");
                Alpha(new Color(2, 0, 0, 0), 1, "the alpha is saturated");
                material.SetColor("_Color", new Color(0, 0, 0, 0));
                Alpha(new Color(0.5f, 0, 0, 0), 0.5f, "the colour's alpha is unused");

                Debug.Log("Project Lucid HL_Blob_Shadow GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The lit colour and the projector pass remain compile-checked only.");
            }
        }
    }
}
