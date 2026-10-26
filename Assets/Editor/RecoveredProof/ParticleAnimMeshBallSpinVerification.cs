using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Particle/HL_Particle_AnimMesh_BallSpin, reconstructed from the shipped Metal programs: the picked
    // channel and its UV ramp, the panner on wrapped time, the colour ramp and offset, the vertex tint, the
    // mesh-normal fresnel, softness, the custom stream, the side mask and the transparent alpha.
    public static class ParticleAnimMeshBallSpinVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Particle_AnimMesh_BallSpin.shader";
        private const string ShaderName = "HardLight/Particle/HL_Particle_AnimMesh_BallSpin";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "d3731901c626f6e49b5f60aa88da48cc";
        private const int Forward = 0, DepthOnly = 1;
        // The read pixel's world x and y (+-y with the V flip): its centre, half a pixel off the quad's centre.
        private const float PixelOffset = 1f / 32;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "_FRESNELENABLE", "_USE_UV_MASK", "_WRITE_RENDERING_LAYERS" },
            new[] { "_FRESNELENABLE", "_SURFACE_TYPE_TRANSPARENT", "_ALPHATEST_ON", "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "_GRADIENTMAPENABLE", "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "_SAMPLE_GI", "FOG_EXP2", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON", "_DBUFFER_MRT2", "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION", "_ALPHAPREMULTIPLY_ON" },
            new[] { "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "_ALPHAMODULATE_ON" },
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
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 3, "three shipped passes");
                rig.SaveVectors("_TimeParameters");
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), blue = new Color(0, 0, 1, 1);

                Material material = rig.Own(new Material(shader));
                // The toggles default on; the UV ramp is checked on its own below.
                material.DisableKeyword("_USE_UV_MASK");
                material.SetColor("_Primary_Color", red);
                material.SetColor("_Secondary_Color", blue);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetFloat("_Softness", 1);
                // Neutral fresnel and side mask: the quad's mesh normal (0, 0, -1) faces the camera (dot 1), which
                // min -1, max 1 saturates to 1; the side mask's v * (1 - v) + 1 saturates to 1.
                material.SetFloat("_Fresnel_Min", -1);
                material.SetFloat("_Fresnel_Max", 1);
                material.SetFloat("_SideMask_Min", -1);
                material.SetFloat("_SideMask_Max", 1);

                // An opaque fragment is (lerp(secondary, primary, saturate(mask + offset)) * tint * alpha, 1).
                Color Out(float mask, float alpha, float offset = 0, Color? tint = null, bool transparent = false)
                {
                    Color t = tint ?? Color.white;
                    float c = Mathf.Clamp01(mask + offset);
                    return new Color(c * t.r * alpha, 0, (1 - c) * t.b * alpha, transparent ? alpha : 1);
                }
                void Expect(Color expected, string label, Mesh mesh = null, Matrix4x4? model = null, int y = 16)
                {
                    rig.Expect(material, expected, label, Forward, mesh, model, y: y);
                    cases++;
                }

                // A 7-texel strip: the read pixel's u 0.516 lands on texel 3 (red 0.5, green 0.8, alpha 0.3),
                // u 0.258 on texel 1 (1) and u 0.766 on texel 5 (0.25).
                Color none = new Color(0, 0, 0, 0);
                material.SetTexture("_Main_Texture", rig.Texels(none, new Color(1, 0, 0, 0), none, new Color(0.5f, 0.8f, 0, 0.3f),
                    none, new Color(0.25f, 0, 0, 0), none));
                Expect(Out(0.5f, 0.5f), "the picked red channel colours and fades");
                material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_FRESNELENABLE"), true);
                Expect(Out(0.5f, 0.5f), "_FRESNELENABLE does not change the output");
                material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_FRESNELENABLE"), false);
                material.SetVector("_Channel_Picker", new Vector4(0, 1, 0, 0));
                Expect(Out(0.8f, 0.8f), "green channel picker");
                material.SetVector("_Channel_Picker", new Vector4(0.5f, 0.5f, 0, 0));
                Expect(Out(0.65f, 0.65f), "the picker is a dot product");
                material.SetVector("_Channel_Picker", new Vector4(0, 0, 0, 1));
                Expect(Out(0.3f, 0.3f), "alpha channel picker");
                material.SetVector("_Channel_Picker", new Vector4(2, 0, 0, 0));
                Expect(Out(1, 1), "the picked value saturates");
                material.SetVector("_Channel_Picker", new Vector4(1, 0, 0, 0));

                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(0.5f, 1, 0, 0));
                Expect(Out(1, 1), "tiling scales the UV");
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));
                material.SetVector("_Texture_Offset", new Vector4(0.25f, 0, 0, 0));
                Expect(Out(0.25f, 0.25f), "texture offset");
                material.SetVector("_Texture_Offset", Vector4.zero);
                // frac(1.5) * 0.5 pans by 0.25 (texel 5); frac(1.5 * 0.5) or the unwrapped 0.75 would read texel 1.
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0.5f, 0));
                Shader.SetGlobalVector("_TimeParameters", new Vector4(1.5f, 0, 0, 0));
                Expect(Out(0.25f, 0.25f), "the panner scales the wrapped time");
                Shader.SetGlobalVector("_TimeParameters", Vector4.zero);
                material.SetVector("_Tiling_and_PannerSpeed", new Vector4(1, 1, 0, 0));

                material.SetFloat("_Color_Offset", 0.25f);
                Expect(Out(0.5f, 0.5f, 0.25f), "colour offset moves the ramp, not the alpha");
                material.SetFloat("_Color_Offset", -1);
                Expect(Out(0.5f, 0.5f, -1), "colour offset -1 shows the secondary colour");
                material.SetFloat("_Color_Offset", 0);
                Color tint = new Color(0.5f, 1, 1, 0.5f);
                rig.SetColors(tint);
                Expect(Out(0.5f, 0.25f, 0, tint), "vertex colour tints and fades");
                rig.SetColors(Color.white);

                rig.SetUv0(new Vector4(0, 0, 0.25f, 0));
                Expect(Out(0.5f, 0.25f), "the custom stream subtracts from the alpha");
                material.SetFloat("_Softness", 0.5f);
                Expect(Out(0.5f, 0.5f), "softness divides the alpha");
                material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_SURFACE_TYPE_TRANSPARENT"), true);
                material.SetFloat("_Softness", 1);
                Expect(Out(0.5f, 0.25f, transparent: true), "transparent programs write the alpha");
                material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_SURFACE_TYPE_TRANSPARENT"), false);
                rig.SetUv0(Vector4.zero);

                // Side mask: v * (1 - v) at the read pixel's v (0.516 or 0.484 alike).
                float v = 0.5f + PixelOffset / 2, ramp = v * (1 - v);
                material.SetFloat("_SideMask_Min", 0.24f);
                material.SetFloat("_SideMask_Max", 0.02f);
                Expect(Out(0.5f, 0.5f * Mathf.Clamp01((ramp - 0.24f) / 0.02f)), "side mask min and max");
                material.SetFloat("_SideMask_Min", 0.1f);
                material.SetFloat("_SideMask_Max", 0.2f);
                Expect(Out(0.5f, 0.5f * Mathf.Clamp01((ramp - 0.1f) / 0.2f)), "default side mask");
                material.SetFloat("_SideMask_Min", -1);
                material.SetFloat("_SideMask_Max", 1);

                // _USE_UV_MASK subtracts (1 - v) * 2 - 1: at rows 4 and 27 (v 0.14 and 0.86, in either order) a
                // value of 0.75 reads 0.03125 and 1. Without it both rows read 0.75.
                material.SetTexture("_Main_Texture", rig.Texels(new Color(0.75f, 0, 0, 0)));
                Expect(Out(0.75f, 0.75f), "no UV mask at row 4", y: 4);
                Expect(Out(0.75f, 0.75f), "no UV mask at row 27", y: 27);
                material.EnableKeyword("_USE_UV_MASK");
                Color low = rig.Draw(material, Forward, y: 4), high = rig.Draw(material, Forward, y: 27);
                Color faded = Out(0.03125f, 0.03125f), full = Out(1, 1);
                bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
                RecoveredShaderProof.Require(ShaderName, Near(low, faded) && Near(high, full) || Near(low, full) && Near(high, faded),
                    "the UV mask ramps along v: rows 4 and 27 read " + low + " and " + high);
                cases += 2;
                material.DisableKeyword("_USE_UV_MASK");

                // Fresnel: the normal is the mesh normal times the world-to-object matrix, normalized, as in all three
                // original vertex programs. Against the camera's backward axis (0, 0, -1) a unit normal n gives -n.z.
                material.SetTexture("_Main_Texture", rig.Texels(red));
                Matrix4x4 near = Matrix4x4.Translate(new Vector3(0, 0, -0.05f)), far = Matrix4x4.Translate(new Vector3(0, 0, 0.05f));
                Matrix4x4 aside = Matrix4x4.Translate(new Vector3(0.5f, 0.25f, 0.05f));
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                material.SetFloat("_Fresnel_Min", 0);
                Expect(Out(1, 0.8f), "fresnel of the mesh normal", model: near);
                material.SetFloat("_Fresnel_Min", 0.5f);
                material.SetFloat("_Fresnel_Max", 0.5f);
                Expect(Out(1, (0.8f - 0.5f) / 0.5f), "fresnel min and max", model: near);
                material.SetFloat("_Fresnel_Min", -1);
                material.SetFloat("_Fresnel_Max", 2);
                Expect(Out(1, (0.8f + 1) / 2), "front face", model: near);
                // Orthographic, so translating the object leaves its normal, and the term, unchanged; a normal taken
                // from the world position would change with each of these.
                Expect(Out(1, (0.8f + 1) / 2), "translated in depth: the same normal", model: far);
                Expect(Out(1, (0.8f + 1) / 2), "translated across the view: the same normal", model: aside);
                // A different mesh normal at the same place changes the term.
                rig.SetNormals(new Vector3(0.8f, 0, -0.6f));
                Expect(Out(1, (0.6f + 1) / 2), "a changed mesh normal", model: near);
                // A nonuniform scale (2, 1, 1): the inverse transpose gives (0.3, 0, -0.8), facing 0.8 / 0.8544; the
                // object-to-world matrix itself would give (1.2, 0, -0.8), facing 0.5547.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                Expect(Out(1, (0.8f / Mathf.Sqrt(0.73f) + 1) / 2), "a nonuniform scale: the inverse transpose",
                    model: near * Matrix4x4.Scale(new Vector3(2, 1, 1)));
                var back = rig.Own(Object.Instantiate(rig.Quad));
                back.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                Expect(Out(1, (1 - 0.8f) / 2), "back faces flip the normal", back, near);
                rig.SetNormals(Vector3.back);
                material.SetFloat("_Fresnel_Min", -1);
                material.SetFloat("_Fresnel_Max", 1);

                // _GlobalMipBias applies once: level 3 unbiased, level 4 at bias 1.
                Color R(float x) => new Color(x, 0, 0, 0);
                material.SetTexture("_Main_Texture", rig.MipLevels(256, level => R(level <= 3 ? 0.5f : level == 4 ? 1 : 0.25f)));
                Expect(Out(0.5f, 0.5f), "no bias reads level 3");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(1, 0, 0, 0));
                Expect(Out(1, 1), "bias 1 applies once");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                float depth = rig.Draw(material, DepthOnly).r;
                RecoveredShaderProof.Require(ShaderName, depth != 0, "DepthOnly writes the depth");
                cases++;

                Debug.Log("Project Lucid HL_Particle_AnimMesh_BallSpin GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The perspective view direction and the shadow pass remain compile-checked only.");
            }
        }
    }
}
