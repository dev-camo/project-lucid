using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Sky/HL_Skybox_Portal, reconstructed from the shipped Metal programs: the cubemap along the
    // offset, flipped and rotated view direction with its additive tint and inner glow, the gradient it
    // shows through by the mask, fresnel and depth fade, the edge highlight, and the clipped mask alpha.
    public static class SkyboxPortalVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Skybox_Portal.shader";
        private const string ShaderName = "HardLight/Sky/HL_Skybox_Portal";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "25aa679e9f8555d459542325f9fd9c14";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON" },
            new[] { "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "_DBUFFER_MRT3", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), KeywordSets);

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target.
        private static Color Blended(Color c, float a) => new Color(c.r * a, c.g * a, c.b * a, a);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                rig.SaveVectors("_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Perspective branches with a distant camera: the view direction is (0, 0, -1), and a scene eye
                // depth of 4 against the ortho quad's clip w of 1 gives depth fade (4 - 1) / 3 = 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                rig.Camera.farClipPlane = 20000;
                rig.Camera.transform.position = new Vector3(0, 0, -10000);

                // Faces: +X red, -X cyan, +Y green, -Y magenta, +Z blue, -Z yellow.
                var cube = rig.Own(new Cubemap(4, TextureFormat.RGBAFloat, false));
                Color yellow = new Color(1, 1, 0, 1);
                var faces = new[] { (CubemapFace.PositiveX, Color.red), (CubemapFace.NegativeX, Color.cyan), (CubemapFace.PositiveY, Color.green),
                                    (CubemapFace.NegativeY, Color.magenta), (CubemapFace.PositiveZ, Color.blue), (CubemapFace.NegativeZ, yellow) };
                foreach (var (face, colour) in faces)
                {
                    var pixels = new Color[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
                    cube.SetPixels(pixels, face);
                }
                cube.Apply(false);

                Color Solid(float r, float g, float b, float a) => new Color(r, g, b, a);
                Material material = rig.Own(new Material(shader));
                material.SetTexture("_Cubemap_Texture", cube);
                material.SetColor("_Cubemap_Colour_Tint", new Color(0, 0, 0, 0));
                material.SetColor("_Inner_Glow_Colour", Color.blue);
                material.SetColor("_Edge_Highlight_Colour", Color.green);
                Color grey = Solid(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Gradient_Texture", rig.Texels(grey, grey, grey, grey));
                Color full = Solid(0, 1, 0, 1);
                material.SetTexture("_Mask_Texture", rig.Texels(full, full, full, full));
                Color zero = Solid(0, 0, 0, 1);
                material.SetTexture("_Distortion_Noise", rig.Texels(zero, zero, zero, zero));

                // Mask 1 (green channel): no edge, full inner glow mix, full reveal of the cubemap.
                rig.Expect(material, Blended(yellow, 1), "the cubemap along -Z");
                material.SetFloat("_Cubemap_Rotation_Amount", 180);
                rig.Expect(material, Blended(Color.blue, 1), "180 degrees about Y faces +Z");
                material.SetFloat("_Cubemap_Rotation_Amount", 0);
                material.SetVector("_Cubemap_Transform", new Vector4(2, 0, 0, 0));
                rig.Expect(material, Blended(Color.red, 1), "the transform offsets the direction");
                material.SetVector("_Cubemap_Transform", new Vector4(0, 2, 0, 0));
                rig.Expect(material, Blended(Color.magenta, 1), "flip Y -1 turns +Y down");
                material.SetFloat("_Cubemap_Flip_Y", 1);
                rig.Expect(material, Blended(Color.green, 1), "flip Y 1 keeps +Y");
                material.SetFloat("_Cubemap_Flip_Y", -1);
                material.SetVector("_Cubemap_Transform", Vector4.zero);
                material.SetColor("_Cubemap_Colour_Tint", Color.blue);
                rig.Expect(material, Blended(Color.white, 1), "the tint is additive");
                material.SetColor("_Cubemap_Colour_Tint", new Color(0, 0, 0, 0));

                // Inner glow: smoothstep(0, 2, 1) = 0.5 halfway to the blue glow colour.
                material.SetFloat("_Inner_Glow_Mask_Max", 2);
                rig.Expect(material, Blended(grey, 1), "inner glow");
                material.SetFloat("_Inner_Glow_Mask_Max", 1);
                // Edge: smoothstep(0, 2, 1) = 0.5 halfway from the green edge colour.
                material.SetFloat("_Edge_Highlight_Mask_Min", 0);
                material.SetFloat("_Edge_Highlight_Mask_Max", 2);
                rig.Expect(material, Blended(Solid(0.5f, 1, 0, 1), 1), "edge highlight");
                material.SetFloat("_Edge_Highlight_Mask_Min", 0.5f);
                material.SetFloat("_Edge_Highlight_Mask_Max", 0.6f);

                // Reveal halved by depth fade (distance 6) or by fresnel (rim 0.2, amount 1, strength 2.5):
                // halfway from the grey gradient to yellow.
                Color half = Solid(0.75f, 0.75f, 0.25f, 1);
                material.SetFloat("_Gradient_Mask_Depth_Distance", 6);
                rig.Expect(material, Blended(half, 1), "depth fade halves the reveal");
                material.SetFloat("_Gradient_Mask_Depth_Distance", 3);
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.SetFloat("_Gradient_Mask_Fresnel_Amount", 1);
                material.SetFloat("_Gradient_Mask_Fresnel_Strength", 2.5f);
                rig.Expect(material, Blended(half, 1), "fresnel halves the reveal");
                rig.SetNormals(Vector3.back);
                material.SetFloat("_Gradient_Mask_Fresnel_Amount", 5);
                material.SetFloat("_Gradient_Mask_Fresnel_Strength", 2);

                // A real camera depth texture, scene eye depths differing between the lower and upper rows, with the
                // camera 2 from the quad: depth fades 0.5 and 1 reveal halfway and fully. Orthographic (clip w 1)
                // reconstructs depth through the inverse view-projection, perspective (clip w 2) through _ZBufferParams.
                rig.Camera.transform.position = new Vector3(0, 0, -2);
                rig.Camera.farClipPlane = 10;
                void DepthRows(string label)
                {
                    // The red channel is 0.5 + 0.5 * reveal.
                    float lower = rig.Draw(material, y: 8, projectionParams: rig.SceneDepthProjection).r * 2 - 1;
                    float upper = rig.Draw(material, y: 24, projectionParams: rig.SceneDepthProjection).r * 2 - 1;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(lower - 0.5f) < 0.01f && Mathf.Abs(upper - 1) < 0.01f,
                        label + " scene depth rows reveal halfway and fully: lower=" + lower + " upper=" + upper);
                }
                rig.PublishSceneDepth(2.5f, 4);
                DepthRows("orthographic");
                rig.Camera.orthographic = false;
                rig.PublishSceneDepth(3.5f, 5);
                DepthRows("perspective");
                rig.Camera.orthographic = true;
                rig.Camera.farClipPlane = 20000;
                rig.Camera.transform.position = new Vector3(0, 0, -10000);
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));

                // Gradient alone (mask range above 1): sampled at mask + time * speed. Bands red, green, blue,
                // black: time 0.5 at speed 0.25 reads u 1.125 (red), time 1.5 reads 1.375 (green).
                material.SetFloat("_Gradient_Mask_Min", 1.5f);
                material.SetFloat("_Gradient_Mask_Max", 2);
                material.SetTexture("_Gradient_Texture", rig.Bands(Color.red, Color.green, Color.blue, zero));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0.5f);
                rig.Expect(material, Blended(Color.red, 1), "gradient by mask plus time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1.5f);
                rig.Expect(material, Blended(Color.green, 1), "gradient scrolls with time");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Gradient_Mask_Min", 0);
                material.SetFloat("_Gradient_Mask_Max", 1);

                // Alpha: the saturated mask times _Alpha, clipped at 0.5.
                Color mask = Solid(0.2f, 0.6f, 0, 1);
                material.SetTexture("_Mask_Texture", rig.Texels(mask, mask, mask, mask));
                float alpha = rig.Draw(material).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 0.6f) < 0.01f, "mask 0.6 is the alpha: " + alpha);
                material.SetFloat("_Alpha", 0.5f);
                rig.Expect(material, new Color(0, 0, 0, 0), "_Alpha 0.5 clips 0.3");
                material.SetFloat("_Alpha", 1);
                material.SetVector("_Mask_Channel_Selection", new Vector4(1, 0, 0, 0));
                rig.Expect(material, new Color(0, 0, 0, 0), "the red channel (0.2) clips");
                material.SetVector("_Mask_Channel_Selection", new Vector4(0, 1, 0, 0));
                // Mask blur picks the mip level: the smallest mip of this mask is 0.6.
                material.SetTexture("_Mask_Texture", rig.Mipped(full, mask));
                material.SetFloat("_Mask_Texture_Blur", 10);
                alpha = rig.Draw(material).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 0.6f) < 0.01f, "mask blur reads the smallest mip: " + alpha);
                material.SetFloat("_Mask_Texture_Blur", 0);
                material.SetTexture("_Mask_Texture", rig.Texels(full, full, full, full));

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original: over UV0 a 256
                // texture with a colour per level reads level 3 unbiased and level 4 at bias 1 (level 5 if twice).
                void BiasCases(Color unbiased, Color once, string path)
                {
                    rig.Expect(material, unbiased, path + ": no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    rig.Expect(material, once, path + ": bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                // Distortion noise at cubemap distortion 1: level 4 (2, 0, 0) turns the view towards +X (red).
                material.SetFloat("_Cubemap_Distortion_Amount", 1);
                material.SetFloat("_Distortion_Amount", 0);
                material.SetTexture("_Distortion_Noise", rig.MipLevels(256, level => level <= 3 ? zero : level == 4 ? Solid(2, 0, 0, 1) : Solid(0, 2, 0, 1)));
                BiasCases(Blended(yellow, 1), Blended(Color.red, 1), "distortion noise");
                material.SetFloat("_Cubemap_Distortion_Amount", 0);
                material.SetTexture("_Distortion_Noise", rig.Texels(zero, zero, zero, zero));
                // Gradient alone (reveal 0, no edge): a mask ramp 0.25 + u moves the gradient lookup 1/32 per
                // pixel over a 256 x 1 strip (level 3 → 4 at bias 1; 5 if twice). The mask is the alpha, 0.766.
                material.SetFloat("_Gradient_Mask_Min", 1.5f);
                material.SetFloat("_Gradient_Mask_Max", 2);
                material.SetFloat("_Edge_Highlight_Mask_Min", -2);
                material.SetFloat("_Edge_Highlight_Mask_Max", -1);
                var maskRamp = new Color[200];
                for (int i = 0; i < maskRamp.Length; i++) maskRamp[i] = Solid(0, 0.25f + (i + 0.5f) / 200, 0, 1);
                material.SetTexture("_Mask_Texture", rig.Texels(maskRamp));
                material.SetTexture("_Gradient_Texture", rig.MipLevels(256, 1, level => level <= 3 ? Color.red : level == 4 ? Color.green : Color.blue));
                float rampMask = 0.25f + 16.5f / 32;
                BiasCases(Blended(Color.red, rampMask), Blended(Color.green, rampMask), "gradient");

                Debug.Log("Project Lucid HL_Skybox_Portal GPU verification passed: 22 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
