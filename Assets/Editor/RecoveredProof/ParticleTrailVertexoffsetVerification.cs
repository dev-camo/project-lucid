using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_Trail_Vertexoffset, reconstructed from the shipped Metal programs: the picked
    // channel eroded by vertex alpha and faded at both trail edges (2.857), the primary/secondary colour, the game-time
    // panner, the always-on depth fade, softness and opaque alpha, and the vertex displacement along the normal
    // outside the sphere mask.
    public static class ParticleTrailVertexoffsetVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_Trail_Vertexoffset.shader";
        private const string ShaderName = "HardLight/Particle/HL_Particle_Trail_Vertexoffset";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "85932df1a688126428c9b215b646e7ff";

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

        // Edge fade saturate(uv.y (1 - uv.y) * 2.857) at a pixel row of the 32-pixel quad.
        private static float Edge(int row)
        {
            float v = (row + 0.5f) / 32f;
            return Mathf.Clamp01((1 - v) * v * 2.85714293f);
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
                rig.SaveFloats("_HLShaderTimeGame");
                rig.SaveVectors("_ZBufferParams", "_ScaledScreenParams", "_ProjectionParams");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Perspective depth branch: with _ZBufferParams.zw = (0, 0.25) the scene eye depth is 4 for any
                // depth sample; the ortho quad's clip w is 1, so distance 3 makes the depth fade exactly 1.
                rig.WriteOrthoParams = false;
                Shader.SetGlobalVector("unity_OrthoParams", Vector4.zero);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;

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
                material.SetTexture("_Displacement_Texture", rig.Texels(new Color(0.5f, 0, 0, 1)));
                material.SetFloat("_Displacement_Speed_X", 0);
                material.SetFloat("_Displacement_Speed_Y", 0);
                Color texel = new Color(0.2f, 0, 0, 0.1f);
                material.SetTexture("_Main_Texture", rig.Texels(texel, texel, texel, texel));
                void Expect(Color expected, string label, int x = 16, int y = 16)
                {
                    rig.Expect(material, expected, label, x: x, y: y);
                    cases++;
                }
                // Vertex alpha 0.75 erodes by (1 - 0.75) * 2 - 1 = -0.5: value = red 0.2 + 0.5 = 0.7.
                rig.SetColors(new Color(1, 1, 1, 0.75f));
                float e = Edge(16), a = 0.7f * e;

                Expect(Out(RedBlue(a), a), "picked red, eroded, edge-faded (2.857)");
                Expect(Out(RedBlue(0.7f * Edge(2)), 0.7f * Edge(2)), "edge fade near uv.y 0", y: 2);
                rig.SetColors(new Color(1, 1, 1, 0));
                Expect(new Color(0, 0, 0, 0), "vertex alpha 0 erodes the trail away");
                rig.SetColors(new Color(1, 1, 1, 0.75f));
                material.SetVector("_Channel_Picker", new Vector4(0, 0, 0, 1));
                Expect(Out(RedBlue(0.6f * e), 0.6f * e), "channel picker selects alpha");
                material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));
                rig.SetColors(new Color(0, 1, 1, 0.75f));
                Expect(Out(new Vector3(0, 0, 1 - a), a), "times the vertex colour");
                rig.SetColors(new Color(1, 1, 1, 0.75f));

                // Softness divides the alpha, which also premultiplies the RGB; the colour lerp keeps the unsoftened value.
                material.SetFloat("_Softness", 0.25f);
                Expect(Out(RedBlue(a), 1), "softness 0.25 saturates alpha");
                material.SetFloat("_Softness", 1);
                material.SetFloat("_Color_Offset", 0.3f);
                Expect(Out(RedBlue(a + 0.3f), a), "colour offset");
                material.SetFloat("_Color_Offset", 0);

                // Panner: at the centre column (u 0.516) texel 2; game time 1 at speed 0.25 moves to texel 3, and
                // the frac wraps time 5 to the same place.
                Color c0 = new Color(0, 0, 0, 1), c2 = new Color(0.2f, 0, 0, 1), c3 = new Color(0.1f, 0, 0, 1);
                material.SetTexture("_Main_Texture", rig.Texels(c0, c0, c2, c3));
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0.25f, 0));
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                Expect(Out(RedBlue(0.6f * e), 0.6f * e), "game time pans");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 5);
                Expect(Out(RedBlue(0.6f * e), 0.6f * e), "the pan wraps");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(2, 1, 0, 0));
                Expect(Out(RedBlue(0.5f * e), 0.5f * e), "the tiling (u 1.03, texel 0)");
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetTexture("_Main_Texture", rig.Texels(texel, texel, texel, texel));

                // Depth fade: scene eye depth 2 leaves (2 - 1) / 3; strength 1.5 makes it 0.5.
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.5f));
                Expect(Out(RedBlue(a), a / 3), "depth fade a third");
                material.SetFloat("_DepthFade_Strenght", 1.5f);
                Expect(Out(RedBlue(a), a * 0.5f), "depth fade strength");
                material.SetFloat("_DepthFade_Strenght", 1);
                Shader.SetGlobalVector("_ZBufferParams", new Vector4(0, 0, 0, 0.25f));

                // Opaque programs keep the premultiplied colour and write alpha 1.
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                Color opaque = Out(RedBlue(a), a);
                opaque.a = 1;
                Expect(opaque, "opaque alpha is 1");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                // _GlobalMipBias applies once to the main texture: a 256 texture reads level 3 unbiased and level 4
                // (red 0.1: value 0.6) at bias 1.
                material.SetTexture("_Main_Texture", rig.MipLevels(256, level => new Color(level <= 3 ? 0.2f : level == 4 ? 0.1f : 0f, 0, 0, 1)));
                Expect(Out(RedBlue(a), a), "main: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(Out(RedBlue(0.6f * e), 0.6f * e), "main: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                material.SetTexture("_Main_Texture", rig.Texels(texel, texel, texel, texel));

                // Displacement: the noise red (0.5) times the amount along the object normal, read through the coverage
                // of pixel x = 4 with normals along +x (0.5 is 8 px). The quad's corners lie 0.87 to 2.18 from the mask
                // centre (0.5, 0.5, 0.5): the default radius 2.5 holds them all; radius -10 releases them all.
                Color lit = Out(RedBlue(a), a), clear = new Color(0, 0, 0, 0);
                rig.SetNormals(Vector3.right);
                material.SetFloat("_Displacement_Amount", 1);
                Expect(lit, "the default mask radius holds the vertices", 4);
                material.SetFloat("_Mask_Radius", -10);
                Expect(clear, "outside the mask the displacement moves the quad", 4);
                Expect(lit, "the moved quad still covers pixel 12", 12);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1, x: 4).r == 0, "DepthOnly displaces too");
                cases++;
                material.SetFloat("_Displacement_Amount", 0.5f);
                Expect(lit, "the amount (0.25, 4 px)", 4);
                material.SetFloat("_Displacement_Amount", 1);
                // The noise UV with the scale at 0: u = game time * speed x, on a 4-band strip (0, 0, 0.5, 0).
                Color none = new Color(0, 0, 0, 1);
                material.SetTexture("_Displacement_Texture", rig.Bands(none, none, new Color(0.5f, 0, 0, 1), none));
                material.SetFloat("_Displacement_Scale", 0);
                Expect(lit, "noise band 0 does not displace", 4);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetFloat("_Displacement_Speed_X", 0.625f);
                Expect(clear, "game time scrolls the noise to band 2", 4);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                material.SetFloat("_Displacement_Speed_X", 0);
                material.SetFloat("_Mask_Radius", 2.5f);
                rig.SetNormals(Vector3.back);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                Debug.Log("Project Lucid HL_Particle_Trail_Vertexoffset GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The mask hardness, the orthographic depth branch (shared with HL_Particle_Trail) " +
                          "and the ShadowCaster pass remain compile-checked only.");
            }
        }
    }
}
