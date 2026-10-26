using UnityEngine;

namespace ProjectLucid
{
    // Hidden/HardLight/Particle/HL_Particle_Dream_Ripple, reconstructed from the shipped Metal programs: the ring about
    // the UV centre (its size, fractional, and thickness), its smoothstep alpha and discard, the opaque scene colour
    // shifted by the ring times the offset direction's X and the magnification, the debug view, and the vertex and
    // renderer colour, in both identical passes.
    public static class ParticleDreamRippleVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_Dream_Ripple.shader";
        private const string ShaderName = "Hidden/HardLight/Particle/HL_Particle_Dream_Ripple";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "5c5794901bfadfd429b999754c9f7fc7";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "DEBUG_DISPLAY" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), KeywordSets);

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        // The ring s * (1 - s) at a pixel, s the smoothstep of its UV distance from the centre.
        private static float Ring(int x, int y, float size, float thickness)
        {
            float r = new Vector2((x + 0.5f) / 32f - 0.5f, (y + 0.5f) / 32f - 0.5f).magnitude;
            float s = Smooth(size - thickness, size + thickness, r);
            return s * (1 - s);
        }

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveVectors("_RendererColor", "_ScaledScreenParams", "_ProjectionParams");
                rig.SaveTextures("_CameraOpaqueTexture");
                Shader.SetGlobalVector("_RendererColor", Vector4.one);
                Shader.SetGlobalVector("_ScaledScreenParams", new Vector4(32, 32, 1 + 1 / 32f, 1 + 1 / 32f));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(1, 0.1f, 10, 0.1f));
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), white = Color.white;
                Color clear = new Color(0, 0, 0, 0);
                // The opaque scene on a 4-band strip along screen U: pixel 6 sits at u 0.203 (band 0, red).
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.Bands(red, green, blue, white));
                Material material = rig.Own(new Material(shader));
                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target: a
                // colour c with alpha a writes (c * a, a).
                void Expect(Color c, float a, string label, int x = 6, int pass = 0)
                {
                    rig.Expect(material, new Color(c.r * a, c.g * a, c.b * a, a), label, pass: pass, x: x);
                    cases++;
                }
                float Alpha(float ring) => Smooth(0, 0.26f, ring);

                // Pixel 6 is 0.297 from the UV centre, inside the default ring (size 0.3, thickness 0.1).
                float ring = Ring(6, 16, 0.3f, 0.1f);
                Expect(red, Alpha(ring), "the scene behind the ring, at the ring's alpha");
                Expect(red, Alpha(ring), "the forward pass matches", 6, 1);
                rig.Expect(material, clear, "the centre is outside the ring: discarded");
                cases++;
                material.SetFloat("_Debug_View", 1);
                Expect(new Color(ring, ring, ring), Alpha(ring), "the debug view shows the ring");
                material.SetFloat("_Thickness", 0.05f);
                float thin = Ring(6, 16, 0.3f, 0.05f);
                Expect(new Color(thin, thin, thin), Alpha(thin), "the thickness");
                material.SetFloat("_Thickness", 0.1f);
                material.SetFloat("_Size", 1.25f);
                float smaller = Ring(6, 16, 0.25f, 0.1f);
                Expect(new Color(smaller, smaller, smaller), Alpha(smaller), "the size is fractional");
                material.SetFloat("_Size", 0.3f);
                material.SetFloat("_Debug_View", 0);

                // The magnification: both screen axes move by ring * the offset direction's X (about -1 at pixel 6)
                // * the magnification; -2 moves u by about +0.5 (band 2).
                material.SetFloat("_Magnification", -2);
                Expect(blue, Alpha(ring), "the magnification shifts the scene");
                material.SetFloat("_Magnification", -0.1f);

                // The vertex and renderer colours tint the colour and the alpha.
                rig.SetColors(new Color(1, 1, 0, 0.5f));
                Expect(red, Alpha(ring) * 0.5f, "vertex colour and alpha");
                rig.SetColors(Color.white);
                Shader.SetGlobalVector("_RendererColor", new Vector4(0, 1, 1, 0.5f));
                Expect(Color.black, Alpha(ring) * 0.5f, "the renderer colour");
                Shader.SetGlobalVector("_RendererColor", Vector4.one);

                // _GlobalMipBias applies once: across the screen a 256 texture is read at level 3.
                Shader.SetGlobalTexture("_CameraOpaqueTexture", rig.MipLevels(256, level => level == 3 ? red : level == 4 ? green : blue));
                Expect(red, Alpha(ring), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, Alpha(ring), "bias 1 reads level 4");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Particle_Dream_Ripple GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ".");
            }
        }
    }
}
