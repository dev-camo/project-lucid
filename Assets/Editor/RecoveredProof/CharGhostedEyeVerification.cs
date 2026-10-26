using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Character/HL_Char_Ghosted_Eye, reconstructed from Metal blobs 1/3: four lookups of one eye
    // mask (each eye's iris and background, each eye's glint), combined into the iris, glint and
    // background colours, tinted by the vertex colour, faded and given _Alpha.
    public static class CharGhostedEyeVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Char_Ghosted_Eye.shader";
        private const string ShaderName = "HardLight/Character/HL_Char_Ghosted_Eye";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "07afcdc7cc70212439bca8be8a805cc0";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), new string[0]);

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), new string[0]);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 1, "one shipped pass");
                Material material = rig.Own(new Material(shader));
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1);
                material.SetColor("_EyeColor", green);
                material.SetColor("_GlintColour", red);
                material.SetColor("_Eye_Background_Colour", blue);
                material.SetColor("_Fade_Colour", red);
                rig.SetColors(Color.white);

                // A uniform mask (iris r, background g, glint b): iris = r * r, glint = b + b, background = 1 - (g + g).
                // The pass blends SrcAlpha OneMinusSrcAlpha over a cleared target, so alpha 1 shows the colour.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0.5f, 0, 1)));
                rig.Expect(material, green, "full iris, no glint, no background");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(0.5f, 0.5f, 0, 1)));
                rig.Expect(material, new Color(0, 0.25f, 0, 1), "the two irises multiply");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0.5f, 0.25f, 1)));
                rig.Expect(material, new Color(0.5f, 0.5f, 0, 1), "the two glints add, then lerp to the glint colour");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0, 0, 1)));
                rig.Expect(material, blue, "no background mask shows the background colour, scaled by the iris");
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0.25f, 0, 1)));
                rig.Expect(material, new Color(0, 0.5f, 0.5f, 1), "half background");

                // The vertex colour always tints (_Vertex_Colour is unused), then the fade, then _Alpha.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(new Color(1, 0, 0, 1)));
                rig.SetColors(new Color(1, 1, 0.5f, 1));
                rig.Expect(material, new Color(0, 0, 0.5f, 1), "vertex colour tint");
                material.SetFloat("_Vertex_Colour", 1);
                rig.Expect(material, new Color(0, 0, 0.5f, 1), "_Vertex_Colour does not gate the tint");
                rig.SetColors(Color.white);
                material.SetFloat("_Fade_To_Colour", 0.5f);
                rig.Expect(material, new Color(0.5f, 0, 0.5f, 1), "half fade to red");
                material.SetFloat("_Fade_To_Colour", 0);
                material.SetFloat("_Alpha", 0.5f);
                rig.Expect(material, new Color(0, 0, 0.5f, 0.25f), "_Alpha is the output alpha");
                material.SetFloat("_Alpha", 1);

                // Offsets and scales, on a strip of seven texels. At the read pixel the UV is 0.515625 (0.484375 if
                // the axis is flipped); with scale 1 and no offset each lookup reads (uv - offset) + 0.5, which repeats
                // onto texel 0 (or 6), a plain iris. An offset of -0.25 moves a lookup to texel 1 (no iris), and -0.5,
                // like a scale of 0, to the centre texel 3 (a half glint). No case lands near a texel boundary.
                Color iris = new Color(1, 0.5f, 0, 1);
                Color[] strip = { iris, new Color(0, 0.5f, 0, 1), iris, new Color(1, 0.5f, 0.5f, 1), iris, iris, iris };
                Color black = new Color(0, 0, 0, 1), halfGlint = new Color(0.5f, 0.5f, 0, 1);
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(strip));
                OffsetCases(rig, material, "Horizontal", black, halfGlint);
                material.SetFloat("_Right_Eye_UV_Scale", 0);
                rig.Expect(material, halfGlint, "right eye scale 0 reads the texture centre for the right eye and glint");
                material.SetFloat("_Right_Eye_UV_Scale", 1);
                material.SetFloat("_Left_Eye_UV_Scale", 0);
                rig.Expect(material, halfGlint, "left eye scale 0 reads the texture centre for the left eye and glint");
                material.SetFloat("_Left_Eye_UV_Scale", 1);
                material.SetFloat("_Global_UV_Horizontal", 0);
                rig.Expect(material, red, "global horizontal 0 reads the centre for all four lookups");
                material.SetFloat("_Global_UV_Horizontal", 1);
                material.SetFloat("_Global_UV_Scale", 0);
                rig.Expect(material, red, "global scale 0 reads the centre for all four lookups");
                material.SetFloat("_Global_UV_Scale", 1);

                // The same strip down a column checks the vertical offsets and scale.
                material.SetTexture("_EyeTextureAlbedoAlpha", Column(rig, strip));
                OffsetCases(rig, material, "Vertical", black, halfGlint);
                material.SetFloat("_Global_UV_Vertical", 0);
                rig.Expect(material, red, "global vertical 0 reads the centre for all four lookups");
                material.SetFloat("_Global_UV_Vertical", 1);

                // The mask's tiling and offset move the vertex UV: an offset of 0.25 reads texel 1 for every lookup.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.Texels(strip));
                material.SetTextureOffset("_EyeTextureAlbedoAlpha", new Vector2(0.25f, 0));
                rig.Expect(material, black, "the mask offset moves every lookup");
                material.SetTextureOffset("_EyeTextureAlbedoAlpha", Vector2.zero);

                // _GlobalMipBias applies once per lookup. A 256 mask spanning UV 0..1 across the quad reads level 3
                // unbiased and level 4 at bias 1. Level 3 is a plain iris; at level 4 the result is
                // (0.25, 0.25, 0.5), and a second bias on any one lookup would read level 5 there and change it.
                material.SetTexture("_EyeTextureAlbedoAlpha", rig.MipLevels(256, level =>
                    level <= 3 ? new Color(1, 0.5f, 0, 1) : level == 4 ? new Color(1, 0.25f, 0.25f, 1) : new Color(0.5f, 0.5f, 0, 1)));
                rig.Expect(material, green, "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                rig.Expect(material, new Color(0.25f, 0.25f, 0.5f, 1), "bias 1 applies once to every lookup");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Char_Ghosted_Eye GPU verification passed: 25 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }

        // Each eye and glint offset on its own: the eye offset moves its iris onto texel 1 (no iris, so black),
        // and the glint offset moves only its glint onto texel 3 (a half glint).
        private static void OffsetCases(RecoveredShaderRig rig, Material material, string axis, Color black, Color halfGlint)
        {
            foreach (string eye in new[] { "Right", "Left" })
            {
                material.SetFloat("_" + eye + "Eye" + axis, -0.25f);
                rig.Expect(material, black, eye.ToLowerInvariant() + " eye " + axis.ToLowerInvariant() + " offset moves its iris and glint");
                material.SetFloat("_" + eye + "Eye" + axis, 0);
                material.SetFloat("_" + eye + "Glint" + axis, -0.5f);
                rig.Expect(material, halfGlint, eye.ToLowerInvariant() + " glint " + axis.ToLowerInvariant() + " offset moves only its glint");
                material.SetFloat("_" + eye + "Glint" + axis, 0);
            }
        }

        // The colours down a 1-wide column, point sampled and repeating.
        private static Texture2D Column(RecoveredShaderRig rig, Color[] colors)
        {
            var texture = rig.Own(new Texture2D(1, colors.Length, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat });
            texture.SetPixels(colors);
            texture.Apply(false, false);
            return texture;
        }
    }
}
