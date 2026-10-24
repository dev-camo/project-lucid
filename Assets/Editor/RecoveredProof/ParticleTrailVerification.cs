using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_Trail, reconstructed from the shipped Metal programs: the picked
    // channel eroded by vertex alpha and faded at both trail edges, the primary/secondary or gradient-map
    // colour, the panner and its time category, the always-on depth fade, softness and opaque alpha.
    public static class ParticleTrailVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_Trail.shader";
        private const string ShaderName = "HardLight/Particle/HL_Particle_Trail";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "3d28166e35482e444b56af851ff24084";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(),
            new string[0],
            new[] { "_SURFACE_TYPE_TRANSPARENT", "_GRADIENTMAPENABLE", "_WRITE_RENDERING_LAYERS" },
            new[] { "INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "DOTS_INSTANCING_ON", "_GRADIENTMAPENABLE" });

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(),
            new string[0],
            new[] { "_SURFACE_TYPE_TRANSPARENT", "_GRADIENTMAPENABLE", "_WRITE_RENDERING_LAYERS" },
            new[] { "INSTANCING_ON", "_SURFACE_TYPE_TRANSPARENT", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "DOTS_INSTANCING_ON", "_GRADIENTMAPENABLE" });

        public static void VerifyOrthographicDepthRows()
        {
            ParticleDepthReadout.WithCopy(ShaderPath, false, VerifyOrthographicDepthRows);
        }

        private static void VerifyOrthographicDepthRows(ParticleDepthReadout readout)
            => VerifyOrthographicDepthRows(readout, ProjectionPublication.AfterGL, false, false);

        public static void VerifyProjectionPublicationOrders()
        {
            var failures = new List<Exception>();
            foreach (ProjectionPublication publication in new[] { ProjectionPublication.BeforeTarget, ProjectionPublication.AfterGL, ProjectionPublication.AfterSetPass })
                foreach (bool rgbaFirst in new[] { false, true })
                    foreach (bool negativeFirst in new[] { false, true })
                    {
                        string cell = "Trail " + publication + " rgbaFirst=" + rgbaFirst + " negativeFirst=" + negativeFirst;
                        Debug.Log("Project Lucid particle publication cell begin: " + cell);
                        try
                        {
                            ParticleDepthReadout.WithCopy(ShaderPath, false,
                                readout => VerifyOrthographicDepthRows(readout, publication, rgbaFirst, negativeFirst));
                            Debug.Log("Project Lucid particle publication cell complete: " + cell);
                        }
                        catch (Exception error)
                        {
                            Debug.Log("Project Lucid particle publication cell failed: " + cell + " " + error);
                            failures.Add(new InvalidOperationException(cell, error));
                        }
                    }
            if (failures.Count != 0) throw new AggregateException("Trail publication measurements failed.", failures);
        }

        private static void VerifyOrthographicDepthRows(ParticleDepthReadout readout, ProjectionPublication publication,
                                                       bool rgbaFirst, bool negativeFirst)
        {
            Matrix4x4 view = Shader.GetGlobalMatrix("unity_MatrixV");
            Matrix4x4 inverseVP = Shader.GetGlobalMatrix("unity_MatrixInvVP");
            try
            {
                using (var rig = new RecoveredShaderRig(Load()))
                {
                    rig.SaveTextures("_CameraDepthTexture");
                    rig.SaveVectors("_ScaledScreenParams", "_ProjectionParams");
                    Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                    var depth = rig.Own(new Texture2D(1, 32, TextureFormat.RFloat, false, true));
                    depth.filterMode = FilterMode.Point;
                    depth.wrapMode = TextureWrapMode.Clamp;
                    var rgbaDepth = rig.Own(new Texture2D(1, 32, TextureFormat.RGBAFloat, false, true));
                    rgbaDepth.filterMode = FilterMode.Point;
                    rgbaDepth.wrapMode = TextureWrapMode.Clamp;
                    var pixels = new Color[32];
                    float rawEye2 = 0, rawEye4 = 0;
                    for (int row = 0; row < pixels.Length; row++)
                    {
                        // Original orthographic raw depths for eye distances 2/4 at near .1, far 10.
                        float raw = row < 16 ? 19f / 99f : 39f / 99f;
                        if (SystemInfo.usesReversedZBuffer) raw = 1 - raw;
                        // Match logical screen rows through the shader's API-specific UV origin.
                        int textureRow = SystemInfo.graphicsUVStartsAtTop ? pixels.Length - 1 - row : row;
                        pixels[textureRow] = new Color(raw, 0, 0, 1);
                        if (row == 0) rawEye2 = raw;
                        if (row == 16) rawEye4 = raw;
                    }
                    depth.SetPixels(pixels);
                    depth.Apply(false, false);
                    rgbaDepth.SetPixels(pixels);
                    rgbaDepth.Apply(false, false);
                    Shader.SetGlobalTexture("_CameraDepthTexture", depth);
                    Matrix4x4 cameraView = rig.Camera.worldToCameraMatrix;
                    Shader.SetGlobalMatrix("unity_MatrixV", cameraView);
                    Shader.SetGlobalMatrix("unity_MatrixInvVP",
                        (GL.GetGPUProjectionMatrix(rig.Camera.projectionMatrix, true) * cameraView).inverse);

                    Material material = rig.Own(new Material(Load()));
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetFloat("_SrcBlend", 1);
                    material.SetFloat("_DstBlend", 0);
                    material.SetFloat("_Softness", 1);
                    material.SetFloat("_DepthFade_Distance", 6);
                    material.SetFloat("_DepthFade_Strenght", 1);
                    material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                    material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));
                    material.SetColor("_Primary_Color", Color.red);
                    material.SetColor("_Secondary_Color", Color.blue);
                    material.SetTexture("_Main_Texture", rig.Texels(new Color(0.2f, 0, 0, 1)));
                    rig.SetColors(new Color(1, 1, 1, 0.75f));
                    Color Draw(Material probe, int rowIndex, Vector4 intended, ProjectionPublication point, Action fault)
                    {
                        RecoveredShaderProof.Require(ShaderName, rig.BeforeReadback == null, "no owned readback fault is already active");
                        if (fault == null)
                            return rig.Draw(probe, y: rowIndex, projectionParams: intended, publication: point);
                        rig.BeforeReadback = fault;
                        try { return rig.Draw(probe, y: rowIndex, projectionParams: intended, publication: point); }
                        finally { rig.BeforeReadback = null; }
                    }
                    foreach (int sign in new[] { 1, -1 })
                    {
                        Vector4 intended = new Vector4(sign, 0.1f, 10, 0.1f);
                        Shader.SetGlobalVector("_ProjectionParams", new Vector4(sign, 0.1f, 10, 0.1f));
                        if (sign == 1)
                        {
                            Debug.Log("Project Lucid Trail depth CPU camera: projection=" + rig.Camera.projectionMatrix.ToString("R") +
                                " gpuProjection=" + GL.GetGPUProjectionMatrix(rig.Camera.projectionMatrix, true).ToString("R") +
                                " view=" + rig.Camera.worldToCameraMatrix.ToString("R"));
                            readout.Capture(material, depth, rgbaDepth, pixels, rawEye2, rawEye4,
                                Draw, "Trail actual RecoveredShaderRig.Draw", intended, publication, rgbaFirst, negativeFirst);
                        }
                        foreach (int row in new[] { 8, 24 })
                        {
                            float alpha = 0.7f * Edge(row);
                            float fade = (row < 16) == (sign == 1) ? 1f / 6f : 0.5f;
                            rig.Expect(material, Out(RedBlue(alpha), alpha * fade),
                                "orthographic varying depth, projection sign " + sign + ", row " + row, y: row, projectionParams: intended);
                        }
                    }
                }
            }
            finally
            {
                Shader.SetGlobalMatrix("unity_MatrixV", view);
                Shader.SetGlobalMatrix("unity_MatrixInvVP", inverseVP);
            }
        }

        // Edge fade saturate(uv.y (1 - uv.y) * 3.333) at a pixel row of the 32-pixel quad.
        private static float Edge(int row)
        {
            float v = (row + 0.5f) / 32f;
            return Mathf.Clamp01((1 - v) * v * 3.33333325f);
        }

        // Transparent output: colour premultiplied by alpha, alpha in .a.
        private static Color Out(Vector3 rgb, float alpha) => new Color(rgb.x * alpha, rgb.y * alpha, rgb.z * alpha, alpha);

        // lerp(secondary blue, primary red, t).
        private static Vector3 RedBlue(float t) => new Vector3(t, 0, 1 - t);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveFloats("_HLShaderTimeGame", "_HLShaderTimePlayerAnim");
                rig.SaveVectors("_TimeParameters", "_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", 0);
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                // Perspective depth branch: with _ZBufferParams.zw = (0, 0.25) the scene eye depth is 4 for any
                // depth sample; the ortho quad's clip w is 1, so distance 3 makes the depth fade exactly 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));

                Material material = rig.Own(new Material(shader));
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 0);
                material.SetFloat("_Softness", 1);
                material.SetFloat("_DepthFade_Distance", 3);
                material.SetFloat("_DepthFade_Strenght", 1);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));
                material.SetColor("_Primary_Color", Color.red);
                material.SetColor("_Secondary_Color", Color.blue);
                Color texel = new Color(0.2f, 0, 0, 0.1f);
                material.SetTexture("_Main_Texture", rig.Texels(texel, texel, texel, texel));
                // Vertex alpha 0.75 erodes by (1 - 0.75) * 2 - 1 = -0.5: value = red 0.2 + 0.5 = 0.7.
                rig.SetColors(new Color(1, 1, 1, 0.75f));
                float e = Edge(16), a = 0.7f * e;

                rig.Expect(material, Out(RedBlue(a), a), "picked red, eroded, edge-faded");
                rig.Expect(material, Out(RedBlue(0.7f * Edge(2)), 0.7f * Edge(2)), "edge fade near uv.y 0", y: 2);
                rig.SetColors(new Color(1, 1, 1, 0));
                rig.Expect(material, new Color(0, 0, 0, 0), "vertex alpha 0 erodes the trail away");
                rig.SetColors(new Color(1, 1, 1, 0.75f));
                material.SetVector("_Channel_Picker", new Vector4(0, 0, 0, 1));
                rig.Expect(material, Out(RedBlue(0.6f * e), 0.6f * e), "channel picker selects alpha");
                material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));

                // Softness divides the alpha, which also premultiplies the RGB; the colour lerp keeps the
                // unsoftened value.
                material.SetFloat("_Softness", 0.5f);
                rig.Expect(material, Out(RedBlue(a), 1), "softness 0.5 saturates alpha");
                material.SetFloat("_Softness", 1);
                material.SetFloat("_Color_Offset", 0.3f);
                rig.Expect(material, Out(RedBlue(a + 0.3f), a), "colour offset");
                material.SetFloat("_Color_Offset", 0);

                // Panner: at the centre column (u 0.516) texel 2; game time 1 at speed 0.25 moves to texel 3,
                // player-animation time 2 (with the category toggle) to texel 0.
                Color c0 = new Color(0, 0, 0, 1), c2 = new Color(0.2f, 0, 0, 1), c3 = new Color(0.1f, 0, 0, 1);
                material.SetTexture("_Main_Texture", rig.Texels(c0, c0, c2, c3));
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0.25f, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                rig.Expect(material, Out(RedBlue(0.6f * e), 0.6f * e), "game time pans");
                Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", 2);
                material.SetFloat("_Use_PlayerAnim_Time_Category", 1);
                rig.Expect(material, Out(RedBlue(0.5f * e), 0.5f * e), "player-animation time pans");
                material.SetFloat("_Use_PlayerAnim_Time_Category", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                Shader.SetGlobalFloat("_HLShaderTimePlayerAnim", 0);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetTexture("_Main_Texture", rig.Texels(texel, texel, texel, texel));

                // Gradient map at frac(value * 0.5 + time * speed), plus half the alpha, times vertex colour.
                material.EnableKeyword("_GRADIENTMAPENABLE");
                Color k = new Color(0, 0, 0, 1), g = new Color(0, 1, 0, 1), r = new Color(1, 0, 0, 1), b = new Color(0, 0, 1, 1);
                material.SetTexture("_gradientMap", rig.Texels(k, g, r, b));
                material.SetFloat("_GradientMapSpeed", 1);
                rig.Expect(material, Out(new Vector3(0.5f * a, 1 + 0.5f * a, 0.5f * a), a), "gradient at 0.35");
                Shader.SetGlobalVector("_TimeParameters", new Vector4(0.5f, 0, 0, 0));
                rig.Expect(material, Out(new Vector3(0.5f * a, 0.5f * a, 1 + 0.5f * a), a), "gradient at 0.85");
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                material.DisableKeyword("_GRADIENTMAPENABLE");

                // Depth fade: scene eye depth 2 leaves (2 - 1) / 3; strength 1.5 makes it 0.5.
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.5f));
                rig.Expect(material, Out(RedBlue(a), a / 3), "depth fade a third");
                material.SetFloat("_DepthFade_Strenght", 1.5f);
                rig.Expect(material, Out(RedBlue(a), a * 0.5f), "depth fade strength");
                material.SetFloat("_DepthFade_Strenght", 1);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));

                // Opaque programs keep the premultiplied colour and write alpha 1.
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Color opaque = Out(RedBlue(a), a);
                opaque.a = 1;
                rig.Expect(material, opaque, "opaque alpha is 1");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                // _GlobalMipBias applies once per sample (URP's SAMPLE_TEXTURE2D). The main texture spans UV 0..1,
                // so a 256 texture reads level 3 unbiased and level 4 (red 0.1: value 0.6) at bias 1; a second
                // application would read level 5 (red 0: value 0.5).
                material.SetTexture("_Main_Texture", rig.MipLevels(256, level => new Color(level <= 3 ? 0.2f : level == 4 ? 0.1f : 0f, 0, 0, 1)));
                rig.Expect(material, Out(RedBlue(a), a), "main: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                rig.Expect(material, Out(RedBlue(0.6f * e), 0.6f * e), "main: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                // Gradient map: a main-texture ramp (red = u, point-sampled, 200 texels, no mips) makes the
                // gradient lookup frac((u + 0.5) * 0.5), moving 0.5/32 per pixel: a 256 gradient reads level 2
                // unbiased, 3 at bias 1, and would read 4 with the bias applied twice. The gradient is a 256 x 1
                // strip, so the lookup's equal V movement adds nothing. The centre reads ramp texel 103, clear of
                // its edges.
                var ramp = new Color[200];
                for (int i = 0; i < ramp.Length; i++) ramp[i] = new Color((i + 0.5f) / 200, 0, 0, 1);
                material.SetTexture("_Main_Texture", rig.Texels(ramp));
                material.EnableKeyword("_GRADIENTMAPENABLE");
                material.SetTexture("_gradientMap", rig.MipLevels(256, 1, level => level <= 2 ? k : level == 3 ? r : g));
                float rampAlpha = Mathf.Clamp01((103.5f / 200 + 0.5f) * e);
                Vector3 half = new Vector3(0.5f * rampAlpha, 0.5f * rampAlpha, 0.5f * rampAlpha);
                rig.Expect(material, Out(half, rampAlpha), "gradient: no bias reads level 2");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                rig.Expect(material, Out(half + new Vector3(1, 0, 0), rampAlpha), "gradient: bias 1 applies once");
                material.DisableKeyword("_GRADIENTMAPENABLE");

                Debug.Log("Project Lucid HL_Particle_Trail GPU verification passed: 17 Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
