using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Environment/HL_Env_Clouds, reconstructed from the shipped Metal programs: the subsurface ramp at the
    // half-Lambert main light (sample offset, intensity, tint and opacity) and the fresnel rim towards the fresnel
    // colour, with the perspective and orthographic view directions.
    public static class EnvCloudsVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Env_Clouds.shader";
        private const string ShaderName = "HardLight/Environment/HL_Env_Clouds";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "d3999c2e7b8b8424ca157f4cd3c26a9c";

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "FOG_EXP2", "DOTS_INSTANCING_ON" },
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
                rig.SaveVectors("_MainLightPosition");
                int cases = 0;
                Material material = rig.Own(new Material(shader));
                void Expect(Color expected, string label, int x = 16, Matrix4x4? model = null)
                {
                    rig.Expect(material, expected, label, x: x, model: model);
                    cases++;
                }
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), grey = new Color(0.5f, 0.5f, 0.5f, 1);
                Color Rgb(float r, float g, float b) => new Color(r, g, b, 1);

                // The ramp is sampled bilinearly (clamped), so it is four 16-texel bands: red, green, blue, grey. With
                // the light towards the camera the flat quad (normal -Z) is fully lit, so the ramp u is the offset.
                material.SetTexture("_SubSurfaceScattering_Ramp_Tex", rig.Bands(red, green, blue, grey));
                material.SetColor("_SubSurfaceScattering_Colour_Tint", Color.white);
                material.SetFloat("_SubSurfaceScattering_Opacity", 0);
                material.SetColor("_Fresnel_Colour", Color.red);
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, -1, 0));
                material.SetFloat("_SubSurfaceScattering_Sample_Offset", 0.375f);
                Expect(green, "the ramp at the sample offset");
                material.SetFloat("_SubSurfaceScattering_Sample_Offset", 0.875f);
                Expect(grey, "a larger offset reads further along the ramp");
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(1, 0, 0, 0));
                Expect(green, "a side light is half-Lambert 0.5 (u 0.44)");
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, 1, 0));
                Expect(red, "a back light is half-Lambert 0 (u 0)");
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(0, 0, -1, 0));
                material.SetFloat("_SubSurfaceScattering_Intensity", 2);
                Expect(Rgb(1, 1, 1), "the intensity scales the ramp");
                material.SetFloat("_SubSurfaceScattering_Intensity", 1);
                material.SetColor("_SubSurfaceScattering_Colour_Tint", new Color(1, 0, 1, 1));
                Expect(Rgb(0.5f, 0, 0.5f), "the tint multiplies the ramp");
                material.SetColor("_SubSurfaceScattering_Colour_Tint", Color.white);
                // Opacity: the ramp fades in from the tint by saturate(u + 1 - opacity).
                material.SetFloat("_SubSurfaceScattering_Opacity", 1);
                Expect(Rgb(0.5625f, 0.5625f, 0.5625f), "full opacity fades by the ramp u (0.875)");
                material.SetFloat("_SubSurfaceScattering_Opacity", 0.5f);
                Expect(grey, "the fade saturates");
                material.SetFloat("_SubSurfaceScattering_Opacity", 0);

                // Fresnel: normals tilted 45 degrees towards +X give N.V 0.707, rim 0.293 at amount 1. The light
                // towards the camera then reads u 0.854 * 0.6 = 0.51 (blue).
                rig.SetNormals(new Vector3(1, 0, -1));
                material.SetFloat("_SubSurfaceScattering_Sample_Offset", 0.6f);
                material.SetFloat("_Fresnel_Amount", 1);
                Expect(blue, "the fresnel opacity defaults to 0");
                material.SetFloat("_Fresnel_Opacity", 1);
                float rim = 1 - Mathf.Sqrt(0.5f);
                Color Rim(float fresnel) => Color.Lerp(red, blue, Mathf.Clamp01(1 - fresnel));
                Expect(Rim(rim), "the rim fades to the fresnel colour");
                material.SetFloat("_Fresnel_Amount", 2);
                Expect(Rim(rim * rim), "the amount is the power");
                material.SetFloat("_Fresnel_Amount", 1);
                material.SetFloat("_Fresnel_Intensity", 2);
                Expect(Rim(2 * rim), "the intensity scales the rim");
                material.SetFloat("_Fresnel_Intensity", 1);
                material.SetFloat("_Fresnel_Opacity", 0.5f);
                Expect(Rim(0.5f * rim), "the opacity scales the rim");
                material.SetFloat("_Fresnel_Opacity", 1);
                rig.SetNormals(Vector3.back);

                // View direction. Orthographic: the camera's back axis (view matrix row 2), here with a pitched and
                // turned camera looking at the quad (normal -Z, light -Z: the ramp stays at u 0.6, blue).
                Vector3 position = rig.Camera.transform.position;
                Quaternion rotation = rig.Camera.transform.rotation;
                try
                {
                    rig.Camera.transform.rotation = Quaternion.Euler(30, 40, 0);
                    rig.Camera.transform.position = -2 * rig.Camera.transform.forward;
                    Vector3 back = rig.Camera.worldToCameraMatrix.GetRow(2);
                    float facing = Mathf.Clamp01(Vector3.Dot(Vector3.back, back.normalized));
                    Expect(Rim(1 - facing), "orthographic: N.V uses the view matrix's back axis");
                    rig.Camera.transform.rotation = rotation;
                    rig.Camera.transform.position = position;
                    // Perspective (90 degrees, the quad scaled to fill the view): at pixel 2 the view ray leans 40
                    // degrees from the quad's normal.
                    rig.Camera.orthographic = false;
                    rig.Camera.fieldOfView = 90;
                    Vector3 pixelWS = rig.Camera.ViewportToWorldPoint(new Vector3(2.5f / 32, 16.5f / 32, 2));
                    Vector3 toCamera = (position - pixelWS).normalized;
                    Expect(Rim(1 - Mathf.Clamp01(Vector3.Dot(Vector3.back, toCamera))), "perspective: N.V uses the direction to the camera",
                           x: 2, model: Matrix4x4.Scale(new Vector3(4, 4, 1)));
                }
                finally
                {
                    rig.Camera.orthographic = true;
                    rig.Camera.transform.rotation = rotation;
                    rig.Camera.transform.position = position;
                }
                material.SetFloat("_Fresnel_Opacity", 0);

                // _GlobalMipBias applies once: normals splayed from 45 degrees left to right under a side light run the
                // ramp u (offset 1) across a 256 x 1 strip at 8 texels per pixel, reading level 3 at the centre
                // unbiased and level 4 at bias 1.
                rig.Quad.normals = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) };
                Shader.SetGlobalVector("_MainLightPosition", new Vector4(1, 0, 0, 0));
                material.SetFloat("_SubSurfaceScattering_Sample_Offset", 1);
                material.SetTexture("_SubSurfaceScattering_Ramp_Tex", rig.MipLevels(256, 1, level => level == 4 ? green : red));
                Expect(red, "ramp: no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(green, "ramp: bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Env_Clouds GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". In-game presentation QA remains unverified.");
            }
        }
    }
}
