using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Zone2_Goo, reconstructed from the shipped Metal programs. The surface
    // colour is checked unlit through the Meta pass's emission output (the graph feeds its base colour
    // to emission too): the noise between the colour pairs by vertex alpha or fresnel, the flow map,
    // scrolling and doubled tiling, the depth-fade recolour, erosion and both vertex displacements.
    // The forward pass checks the clipped transparent alpha.
    public static class EnvZone2GooVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Zone2_Goo.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Zone2_Goo";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "834e4dabaab6ec8428bd6a64115a2c5c";
        private const int Forward = 0, DepthNormals = 3, Meta = 4;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "EVALUATE_SH_VERTEX", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_REFLECTION_PROBE_BLENDING", "_SHADOWS_SOFT" },
            new[] { "EVALUATE_SH_VERTEX", "FOG_LINEAR", "_ADDITIONAL_LIGHTS", "_MAIN_LIGHT_SHADOWS", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "LIGHTMAP_ON", "SHADOWS_SHADOWMASK", "_FORWARD_PLUS", "DOTS_INSTANCING_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "EDITOR_VISUALIZATION", "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT3" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        // Also: the layer-writing passes read each instance's rendering layers under GPU instancing.
        public static void CompileForWindows()
        {
            Shader shader = Load();
            RecoveredShaderProof.CompileForWindows(shader, KeywordSets);
            RecoveredShaderProof.RequireInstancedRenderingLayers(shader, Forward, DepthNormals);
        }

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        // Grey noise value n with colours A red, B green, C blue, D black and mask 0.25:
        // lerp(lerp(C, D, n), lerp(A, B, n), 0.25) = (0.25 (1 - n), 0.25 n, 0.75 (1 - n)).
        private static Color Goo(float n) => new Color(0.25f * (1 - n), 0.25f * n, 0.75f * (1 - n), 1);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 5, "five shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "unity_OneOverOutputBoost", "unity_MaxOutputValue", "unity_UseLinearSpace");
                rig.SaveVectors("unity_MetaVertexControl", "unity_MetaFragmentControl", "_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Meta: object position straight to clip space, emission branch, linear output.
                Shader.SetGlobalVector("unity_MetaVertexControl", Vector4.zero);
                Shader.SetGlobalVector("unity_MetaFragmentControl", new Vector4(0, 1, 0, 0));
                Shader.SetGlobalFloat("unity_OneOverOutputBoost", 1);
                Shader.SetGlobalFloat("unity_MaxOutputValue", 100);
                Shader.SetGlobalFloat("unity_UseLinearSpace", 1);
                // Perspective depth branch with a scene eye depth of 4 against the ortho quad's clip w 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));

                Material material = rig.Own(new Material(shader));
                material.SetColor("_Color_A", Color.red);
                material.SetColor("_Color_B", Color.green);
                material.SetColor("_Color_C", Color.blue);
                material.SetColor("_Color_D", Color.black);
                material.SetFloat("_Goo_Noise_Contrast", 1);
                material.SetFloat("_Depth_Fade_Distance", 2);
                material.SetFloat("_Depth_Fade_Contrast", 1);
                material.SetColor("_Erosion_Color", Color.black);
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1);
                material.SetTexture("_Flow_Map_Tex", rig.Texels(grey, grey, grey, grey));
                Color Grey(float v) => new Color(v, v, v, 1);
                // The noise is sampled bilinearly, so it is laid out in wide bands.
                material.SetTexture("_Goo_Noise_Tex", rig.Bands(Grey(0), Grey(0.2f), Grey(0.6f), Grey(1)));
                rig.SetColors(new Color(1, 1, 1, 0.25f));

                // Deep goo (depth fade 1): the centre column reads noise band 2 (0.6).
                rig.Expect(material, Goo(0.6f), "noise between the colour pairs by vertex alpha", pass: Meta);
                rig.SetColors(Color.white);
                rig.Expect(material, new Color(0.4f, 0.6f, 0, 1), "vertex alpha 1 picks the A/B pair", pass: Meta);
                rig.SetColors(new Color(1, 1, 1, 0.25f));
                material.SetFloat("_Goo_Noise_Contrast", 2);
                rig.Expect(material, Goo(1), "contrast 2 about 0.1 saturates 0.6", pass: Meta);
                material.SetFloat("_Goo_Noise_Contrast", 1);

                // Fresnel mask: normal (0.6, 0, -0.8) gives rim 0.2; intensity 2.5 makes the mask 0.5. The
                // depth branch above is the perspective one, so the view direction is camera minus surface:
                // a distant camera keeps it on the axis.
                material.SetFloat("_Use_Fresnel_Instead_of_Vertex_Mask", 1);
                material.SetFloat("_Fresnel_Intensity", 2.5f);
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                rig.Camera.farClipPlane = 20000;
                rig.Camera.transform.position = new Vector3(0, 0, -10000);
                rig.Expect(material, new Color(0.2f, 0.3f, 0.2f, 1), "fresnel mask", pass: Meta);
                rig.Camera.transform.position = new Vector3(0, 0, -2);
                rig.Camera.farClipPlane = 10;
                rig.SetNormals(Vector3.back);
                material.SetFloat("_Use_Fresnel_Instead_of_Vertex_Mask", 0);

                // UV: game-time scrolling, and the noise tiling applied twice (offset 0.25 moves 0.5).
                material.SetFloat("_UV_Scrolling_Direction_X", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                rig.Expect(material, Goo(1), "scrolling moves to texel 3", pass: Meta);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_UV_Scrolling_Direction_X", 0);
                material.SetTextureOffset("_Goo_Noise_Tex", new Vector2(0.25f, 0));
                rig.Expect(material, Goo(0), "noise offset applied twice", pass: Meta);
                material.SetTextureOffset("_Goo_Noise_Tex", Vector2.zero);

                // Flow map (0.75, 0.5) at strength 1 flows by (-0.5, 0) per phase. At time 0 the crossfade is
                // all on the half-cycle phase: u - 0.25, texel 1 (0.2). At time 1, speed 0.25 the phases are
                // 0.25 and 0.75 (texels 1 and 0) mixed half and half: 0.1.
                Color flow = new Color(0.75f, 0.5f, 0, 1);
                material.SetTexture("_Flow_Map_Tex", rig.Texels(flow, flow, flow, flow));
                rig.Expect(material, Goo(0.2f), "flow map half-cycle phase", pass: Meta);
                material.SetFloat("_Flow_Map_Speed", 0.25f);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                rig.Expect(material, Goo(0.1f), "flow phases crossfaded", pass: Meta);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTexture("_Flow_Map_Tex", rig.Texels(grey, grey, grey, grey));

                // Shallow goo: distance 6 gives depth fade (4 - 1) / 6 = 0.5. Fade colours red to green by
                // (1 - mask) * 2 * 0.5 = 0.75, mixed with the goo by 0.5, then towards goo * 0.2 by 0.5.
                material.SetFloat("_Depth_Fade_Distance", 6);
                material.SetColor("_Depth_Fade_Color_A", Color.red);
                material.SetColor("_Depth_Fade_Color_B", Color.green);
                rig.Expect(material, new Color(0.0975f, 0.24f, 0.105f, 1), "depth fade recolour", pass: Meta);
                material.SetFloat("_Depth_Fade_Distance", 2);

                // Erosion: the white erosion texture at progress 0.75 is fully past the 0.35 rim.
                material.SetColor("_Erosion_Color", Color.blue);
                material.SetFloat("_Erosion_Progress", 0.75f);
                Color eroded = Goo(0.6f);
                eroded.b += 1;
                rig.Expect(material, eroded, "erosion rim colour", pass: Meta);
                material.SetFloat("_Erosion_Progress", 0);
                material.SetColor("_Erosion_Color", Color.black);

                // Vertex displacement along a +X normal: pixel 5 (x -0.66) leaves the quad when it moves
                // by the white displacement noise (0.5) or by the sine wave smoothstep(sin(0.5)) = 0.469.
                // Near the left edge the noise reads texel 0.
                rig.SetNormals(Vector3.right);
                rig.Expect(material, Goo(0), "undisplaced edge", pass: Meta, x: 5);
                material.SetFloat("_Vertex_Displacement_Amount", 0.5f);
                rig.Expect(material, new Color(0, 0, 0, 0), "noise displacement", pass: Meta, x: 5);
                material.SetFloat("_Vertex_Displacement_Amount", 0);
                material.SetFloat("_Use_SineWave", 1);
                material.SetFloat("_SineWave_Mask_Tiling", 0);
                material.SetFloat("_SineWave_Mask_Min", 0);
                rig.Expect(material, new Color(0, 0, 0, 0), "sine wave displacement", pass: Meta, x: 5);
                rig.Expect(material, Goo(0), "sine wave displacement keeps x -0.34 covered", pass: Meta, x: 10);
                material.SetFloat("_Use_SineWave", 0);
                rig.SetNormals(Vector3.back);

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D), as in the original. A 256
                // texture spanning UV 0..1 across the 32-pixel quad reads level 3 unbiased and level 4 at bias 1;
                // applied twice the bias would reach level 5. Each level has its own colour.
                void BiasCases(Color unbiased, Color once, string path, int pass = Meta)
                {
                    rig.Expect(material, unbiased, path + ": no bias reads level 3", pass: pass);
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    rig.Expect(material, once, path + ": bias 1 applies once", pass: pass);
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }
                // Noise: at time 0 the crossfade shows only the half-cycle tap; at time 1 (speed 0.5) only the other.
                material.SetFloat("_Flow_Map_Speed", 0.5f);
                material.SetTexture("_Goo_Noise_Tex", rig.MipLevels(256, level => Grey(level <= 3 ? 0.6f : level == 4 ? 0.2f : 1)));
                BiasCases(Goo(0.6f), Goo(0.2f), "noise, half-cycle tap");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                BiasCases(Goo(0.6f), Goo(0.2f), "noise, other tap");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Flow map: level 4 (0.75) flows the half-cycle tap by -0.25, from noise band 2 (0.6) to band 1 (0.2).
                material.SetTexture("_Goo_Noise_Tex", rig.Bands(Grey(0), Grey(0.2f), Grey(0.6f), Grey(1)));
                material.SetTexture("_Flow_Map_Tex", rig.MipLevels(256, level => level <= 3 ? grey : level == 4 ? new Color(0.75f, 0.5f, 0, 1) : new Color(1, 0.5f, 0, 1)));
                BiasCases(Goo(0.6f), Goo(0.2f), "flow map");
                material.SetTexture("_Flow_Map_Tex", rig.Texels(grey, grey, grey, grey));
                // Erosion: blue 0 / 0.5 / 0.7 at progress 0.5 adds 0 / 0.5 / 1 of the blue rim colour.
                material.SetColor("_Erosion_Color", Color.blue);
                material.SetFloat("_Erosion_Progress", 0.5f);
                material.SetTexture("_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D",
                    rig.MipLevels(256, level => new Color(0, 0, level <= 3 ? 0 : level == 4 ? 0.5f : 0.7f, 1)));
                Color rimHalf = Goo(0.6f);
                rimHalf.b += 0.5f;
                BiasCases(Goo(0.6f), rimHalf, "erosion");
                material.SetFloat("_Erosion_Progress", 0);
                material.SetColor("_Erosion_Color", Color.black);
                // Normal map, read by DepthNormals: level 4 lies along the tangent (+X).
                rig.Quad.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
                Color flat = new Color(0.5f, 0.5f, 1, 1), along = new Color(1, 0.5f, 0.5f, 1);
                material.SetTexture("_Normal_Tex", rig.MipLevels(256, level => level == 4 ? along : flat));
                BiasCases(new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), "normal, half-cycle tap", DepthNormals);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                BiasCases(new Color(0, 0, -1, 0), new Color(1, 0, 0, 0), "normal, other tap", DepthNormals);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetTexture("_Normal_Tex", rig.Texels(flat, flat, flat, flat));


                // Forward alpha: 1 by default; with the custom alpha, smoothstep((0.5 - erosion) * 4) times the
                // depth fade and global alpha, clipped at _Alpha_Clip. Erosion texel blue 0.4 at progress 0.5
                // (no shift) gives 0.352.
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(rig.Draw(material, Forward).a - 1) < 0.01f, "forward alpha defaults to 1");
                material.SetFloat("_Use_Custom_Alpha_Value", 1);
                material.SetFloat("_Erosion_Progress", 0.5f);
                Color erosion = new Color(0, 0, 0.4f, 1);
                material.SetTexture("_SampleTexture2D_7c1737227cde428799e62df11901fdc3_Texture_1_Texture2D", rig.Texels(erosion, erosion, erosion, erosion));
                material.SetFloat("_Alpha_Clip", 0.5f);
                rig.Expect(material, new Color(0, 0, 0, 0), "alpha 0.352 is clipped at 0.5", pass: Forward);
                material.SetFloat("_Alpha_Clip", 0.2f);
                float alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 0.352f) < 0.01f, "eroded alpha 0.352 above clip 0.2: " + alpha);
                material.SetFloat("_Global_Alpha", 0.5f);
                rig.Expect(material, new Color(0, 0, 0, 0), "global alpha 0.5 drops it to 0.176, clipped", pass: Forward);
                material.SetFloat("_Global_Alpha", 1);
                material.SetFloat("_Depth_Fade_Distance", 6);
                material.SetFloat("_Alpha_Clip", 0.1f);
                alpha = rig.Draw(material, Forward).a;
                RecoveredShaderProof.Require(ShaderName, Mathf.Abs(alpha - 0.176f) < 0.01f, "depth fade 0.5 halves the alpha to 0.176 (clip 0.1): " + alpha);

                // A real camera depth texture, scene eye depths differing between the lower and upper rows: fades
                // 1/6 and 1/2 give alphas 0.0587 and 0.176. Orthographic (clip w 1) reconstructs depth through the
                // inverse view-projection, perspective (camera 2 from the quad, clip w 2) through _ZBufferParams.
                material.SetFloat("_Alpha_Clip", 0.01f);
                void DepthRows(string label)
                {
                    float lower = rig.Draw(material, Forward, y: 8, projectionParams: rig.SceneDepthProjection).a;
                    float upper = rig.Draw(material, Forward, y: 24, projectionParams: rig.SceneDepthProjection).a;
                    RecoveredShaderProof.Require(ShaderName, Mathf.Abs(lower - 0.0587f) < 0.005f && Mathf.Abs(upper - 0.176f) < 0.005f,
                        label + " scene depth rows: lower=" + lower + " upper=" + upper);
                }
                rig.PublishSceneDepth(2, 4);
                DepthRows("orthographic");
                rig.Camera.orthographic = false;
                rig.PublishSceneDepth(3, 5);
                DepthRows("perspective");

                Debug.Log("Project Lucid HL_Env_Zone2_Goo GPU verification passed: 33 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". Normal output is checked only through DepthNormals; the forward lit RGB, material response and in-game presentation remain unverified.");
            }
        }
    }
}
