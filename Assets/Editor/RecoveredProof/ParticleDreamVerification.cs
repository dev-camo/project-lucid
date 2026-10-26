using UnityEngine;

namespace ProjectLucid
{
    // Hidden/HardLight/Particle/HL_Particle_Dream, reconstructed from the shipped Metal programs: the diffuse UV
    // pushed by Shader Graph's legacy gradient noise (scrolled by time or sine time), the alpha scale, and the
    // vertex and renderer tint, in both identical passes.
    public static class ParticleDreamVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_Dream.shader";
        private const string ShaderName = "Hidden/HardLight/Particle/HL_Particle_Dream";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "80a924db35b9fa644acb6d04a8c126cb";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_TIME_TYPE_SINE_TIME", "DEBUG_DISPLAY" },
            new[] { "_TIME_TYPE_TIME" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER" },
        };

        public static void CompileForWindows() => RecoveredShaderProof.CompileForWindows(Load(), KeywordSets);

        public static void CompileForOpenGL() => RecoveredShaderProof.CompileForOpenGL(Load(), KeywordSets);

        public static void Run()
        {
            Shader shader = Load();
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveVectors("_TimeParameters", "_RendererColor");
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                Shader.SetGlobalVector("_RendererColor", Vector4.one);
                Material material = rig.Own(new Material(shader));
                material.EnableKeyword("_TIME_TYPE_TIME");
                material.SetFloat("_Noise_Intensity", 0);

                // The passes blend SrcAlpha One (alpha: One One) over a cleared target: a fragment (rgb, a) reads
                // (rgb * a, a).
                Color red = new Color(1, 0, 0, 1), blue = new Color(0, 0, 1, 1);
                foreach (int pass in new[] { 0, 1 })
                {
                    void Expect(Color c, string label) =>
                        rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a), "pass " + pass + ": " + label, pass: pass);
                    material.SetTexture("_DiffTex", rig.Texels(red, blue));
                    Expect(blue, "diffuse at the vertex UV");
                    material.SetFloat("_Alpha", 0.5f);
                    Expect(new Color(0, 0, 1, 0.5f), "_Alpha scales the alpha");
                    material.SetFloat("_Alpha", 1);
                    rig.SetColors(new Color(1, 1, 0.5f, 0.5f));
                    Expect(new Color(0, 0, 0.5f, 0.5f), "vertex colour tint");
                    rig.SetColors(Color.white);
                    Shader.SetGlobalVector("_RendererColor", new Vector4(1, 1, 0.5f, 0.5f));
                    Expect(new Color(0, 0, 0.5f, 0.5f), "renderer colour tint");
                    Shader.SetGlobalVector("_RendererColor", Vector4.one);
                    // At noise scale 0 the gradient noise is exactly 0.5, so intensity 1 pushes u by 0.5 (texel 0).
                    material.SetFloat("_Noise_Scale", 0);
                    material.SetFloat("_Noise_Intensity", 1);
                    Expect(red, "noise 0.5 times the intensity pushes the UV");
                    material.SetFloat("_Noise_Intensity", 0);
                    material.SetFloat("_Noise_Scale", 2);
                    // _GlobalMipBias applies once: level 3 unbiased, level 4 at bias 1.
                    material.SetTexture("_DiffTex", rig.MipLevels(256, level => level <= 3 ? blue : level == 4 ? red : Color.green));
                    Expect(blue, "no bias reads level 3");
                    Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                    Expect(red, "bias 1 applies once");
                    Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);
                }

                // The noise value itself: a ramp in u (r) and v (g) reads back the pushed UV. The read pixel's UV
                // comes from the unpushed ramp; the expected noise is Shader Graph's node evaluated here.
                material.SetTexture("_DiffTex", Ramp(rig));
                Color at = rig.Draw(material, 1);
                Vector2 uv = new Vector2(at.r, at.g);
                material.SetFloat("_Noise_Intensity", 0.25f);
                material.SetFloat("_Noise_Speed", 0.5f);
                Shader.SetGlobalVector("_TimeParameters", new Vector4(3, 0.25f, 0, 0));
                int cases = 0;
                foreach (float scale in new[] { 2f, 5f, 11f })
                {
                    material.SetFloat("_Noise_Scale", scale);
                    foreach (bool sine in new[] { false, true })
                    {
                        material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_TIME_TYPE_TIME"), !sine);
                        material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_TIME_TYPE_SINE_TIME"), sine);
                        float time = sine ? 0.25f : 3;
                        float noise = GradientNoise(uv + Vector2.one * (time * 0.5f), scale);
                        float pushed = Mathf.Clamp01(uv.x + noise * 0.25f);
                        rig.Expect(material, new Color(pushed, Mathf.Clamp01(uv.y + noise * 0.25f), 0, 1),
                            "scale " + scale + (sine ? " sine time" : " time") + ": noise " + noise, pass: 1, tolerance: 0.005f);
                        cases++;
                    }
                }

                Debug.Log("Project Lucid HL_Particle_Dream GPU verification passed: " + (14 + cases) + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }

        // A 1024 x 1024 point-sampled, clamped ramp: r = u and g = v at each texel centre, alpha 1.
        private static Texture2D Ramp(RecoveredShaderRig rig)
        {
            const int size = 1024;
            var texture = rig.Own(new Texture2D(size, size, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color((x + 0.5f) / size, (y + 0.5f) / size, 0, 1);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // Shader Graph's legacy Gradient Noise node, as the original programs evaluate it (fmod semantics).
        private static Vector2 Dir(Vector2 p)
        {
            p = new Vector2(p.x % 289, p.y % 289);
            float x = (34 * p.x + 1) * p.x % 289 + p.y;
            x = (34 * x + 1) * x % 289;
            x = (x / 41 - Mathf.Floor(x / 41)) * 2 - 1;
            return new Vector2(x - Mathf.Floor(x + 0.5f), Mathf.Abs(x) - 0.5f).normalized;
        }

        private static float GradientNoise(Vector2 uv, float scale)
        {
            Vector2 p = uv * scale;
            Vector2 ip = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Vector2 fp = p - ip;
            float d00 = Vector2.Dot(Dir(ip), fp);
            float d01 = Vector2.Dot(Dir(ip + new Vector2(0, 1)), fp - new Vector2(0, 1));
            float d10 = Vector2.Dot(Dir(ip + new Vector2(1, 0)), fp - new Vector2(1, 0));
            float d11 = Vector2.Dot(Dir(ip + new Vector2(1, 1)), fp - new Vector2(1, 1));
            float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
            float fx = Fade(fp.x), fy = Fade(fp.y);
            return Mathf.Lerp(Mathf.Lerp(d00, d01, fy), Mathf.Lerp(d10, d11, fy), fx) + 0.5f;
        }
    }
}
