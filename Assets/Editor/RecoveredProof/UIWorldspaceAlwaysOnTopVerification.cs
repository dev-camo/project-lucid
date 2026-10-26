using UnityEngine;

namespace ProjectLucid
{
    // HardLight/UI/HL_UI_Worldspace_AlwaysOnTop, reconstructed from the shipped Metal programs: the sprite plus the
    // texture sample add, times the vertex colour and the tint, its alpha masked by the clip rectangle in object space.
    public static class UIWorldspaceAlwaysOnTopVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_UI_Worldspace_AlwaysOnTop.shader";
        private const string ShaderName = "HardLight/UI/HL_UI_Worldspace_AlwaysOnTop";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "ca885f86c79e05b4bb0dd865854fa0ed";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "UNITY_UI_ALPHACLIP" },
        };

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), KeywordSets);

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                rig.SaveVectors("_TextureSampleAdd", "_ClipRect");
                Shader.SetGlobalVector("_TextureSampleAdd", Vector4.zero);
                // The quad spans -1 to 1 in object space; this rectangle holds all of it.
                Shader.SetGlobalVector("_ClipRect", new Vector4(-2, -2, 2, 2));
                int cases = 0;
                Color sprite = new Color(0.25f, 0.5f, 1, 0.5f);
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_MainTex", rig.Texels(sprite));
                // The pass blends SrcAlpha OneMinusSrcAlpha for colour and alpha alike over a cleared target: a colour
                // c with alpha a writes (c * a, a * a).
                void Expect(Color c, string label, int x = 16)
                {
                    rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a * c.a), label, x: x);
                    cases++;
                }

                Expect(sprite, "the sprite");
                material.SetColor("_Color", new Color(1, 0, 1, 1));
                Expect(new Color(0.25f, 0, 1, 0.5f), "the tint");
                material.SetColor("_Color", Color.white);
                rig.SetColors(new Color(1, 1, 0, 0.5f));
                Expect(new Color(0.25f, 0.5f, 0, 0.25f), "the vertex colour");
                rig.SetColors(Color.white);
                Shader.SetGlobalVector("_TextureSampleAdd", new Vector4(0.25f, 0, 0, 0.25f));
                Expect(new Color(0.5f, 0.5f, 1, 0.75f), "the texture sample add");
                Shader.SetGlobalVector("_TextureSampleAdd", Vector4.zero);

                // The clip rectangle in object space: pixel 16 sits at x 0.03, pixel 20 at x 0.28.
                Shader.SetGlobalVector("_ClipRect", new Vector4(0.1f, -2, 2, 2));
                Expect(new Color(0.25f, 0.5f, 1, 0), "left of the rectangle: transparent");
                Expect(sprite, "inside the rectangle", 20);
                Shader.SetGlobalVector("_ClipRect", new Vector4(-2, -2, 0.1f, 2));
                Expect(sprite, "right edge: inside");
                Expect(new Color(0.25f, 0.5f, 1, 0), "right of the rectangle: transparent", 20);
                Shader.SetGlobalVector("_ClipRect", new Vector4(-2, -2, 2, 2));

                // The original fragments take no bias input: across the quad a 256 texture is read at level 3
                // whatever _GlobalMipBias holds. Every other level is opaque red.
                material.SetTexture("_MainTex", rig.MipLevels(256, level => level == 3 ? sprite : new Color(1, 0, 0, 1)));
                Expect(sprite, "mip chain: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(sprite, "mip chain: a positive global bias is not applied");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(-1, 0, 0, 0));
                Expect(sprite, "mip chain: a negative global bias is not applied");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_MainTex", rig.Texels(sprite));

                Debug.Log("Project Lucid HL_UI_Worldspace_AlwaysOnTop GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The alpha clip remains compile-checked only.");
            }
        }
    }
}
