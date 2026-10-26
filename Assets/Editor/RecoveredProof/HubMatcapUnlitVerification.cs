using UnityEngine;

namespace ProjectLucid
{
    // HardLight/Hub/HL_Hub_Matcap_Unlit, reconstructed from the shipped Metal programs: the view-space matcap with its
    // push border, the dream gradient picked by two scrolling noise octaves and weighted by the dream opacity, the
    // alpha from vertex red times the opacity, and the vertex displacement along the normal by vertex green.
    public static class HubMatcapUnlitVerification
    {
        private const string ShaderPath = "Assets/Shaders/Recovered/HL_Hub_Matcap_Unlit.shader";
        private const string ShaderName = "HardLight/Hub/HL_Hub_Matcap_Unlit";
        // The shipped shader asset GUID in the 2026-10-05 baseline export (dev-camo/project-lucid#448).
        private const string OriginalGuid = "da114adcf264a2340a6474c5fc76fa72";
        // The original importer's immutable texture bindings, property and texture GUID (#538).
        private static readonly string[,] OriginalTextures =
        {
            { "_HLSubSgVertexDisplacementTexture_649c8a79f0c04a6785cb1ef073b96b53_Texture_4082694086_Texture2D", "5cecf516273ba544f8abc9afeafea881" },
            { "_Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D", "737b0c6c5bbc2af4198c74848846235c" },
            { "_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D", "673db981a8bcd6e45a15be7ec1409d6c" },
        };
        private const string DisplacementNoise = "_HLSubSgVertexDisplacementTexture_649c8a79f0c04a6785cb1ef073b96b53_Texture_4082694086_Texture2D";
        private const string DreamNoise = "_Texture2DAsset_d39bff2dfe9a47cba322697e35ffd3f9_Out_0_Texture2D";
        private const string DreamGradient = "_SampleTexture2D_3b9f3ee02411490484da157e5c0b5f78_Texture_1_Texture2D";
        private const int GridSize = 5;

        private static Shader Load() => RecoveredShaderProof.Load(ShaderPath, ShaderName, OriginalGuid);

        // The shipped keyword families, plus every declared keyword that the base variants leave off.
        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "FOG_LINEAR", "_WRITE_RENDERING_LAYERS", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON" },
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
            RecoveredShaderProof.RequireImportedTextures(shader, ShaderPath, OriginalTextures);
            using (var rig = new RecoveredShaderRig(shader))
            {
                RecoveredShaderProof.Require(ShaderName, shader.passCount == 2, "two shipped passes");
                rig.SaveFloats("_HLShaderTimeGame");
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                int cases = 0;
                Color red = new Color(1, 0, 0, 1), green = new Color(0, 1, 0, 1), blue = new Color(0, 0, 1, 1), clear = new Color(0, 0, 0, 0);
                Color Noise(float x, float y = 0) => new Color(x, y, 0, 1);

                // The pass blends SrcAlpha OneMinusSrcAlpha (alpha: One OneMinusSrcAlpha) over a cleared target: a
                // fragment (rgb, a) reads (rgb * a, a).
                Material material = rig.Own(new Material(shader));
                material.SetTexture(DisplacementNoise, rig.Texels(Noise(0.5f)));
                material.SetTexture(DreamNoise, rig.Texels(Noise(0.3125f, 0.125f)));
                material.SetTexture(DreamGradient, rig.Texels(blue));
                void Expect(Color c, string label, int x = 16)
                {
                    rig.Expect(material, new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a), label, x: x);
                    cases++;
                }

                // The matcap: (r.y, -r.x) * push + 0.5 for r = cross(view, normal) in view space, which for a head-on
                // orthographic view is 0.5 + push * the view-space normal's xy.
                material.SetTexture("_Matcap_Texture", rig.Grid(GridSize));
                material.SetFloat("_Matcap_Push_Border", 0.4f);
                Expect(rig.GridColor(GridSize, new Vector2(0.5f, 0.5f)), "a facing normal reads the matcap centre");
                rig.SetNormals(new Vector3(0.6f, -0.6f, -0.5292f));
                Expect(rig.GridColor(GridSize, new Vector2(0.74f, 0.26f)), "a tilted normal moves along the view-space normal");
                material.SetFloat("_Matcap_Push_Border", 0);
                Expect(rig.GridColor(GridSize, new Vector2(0.5f, 0.5f)), "push border 0 keeps the centre");
                material.SetFloat("_Matcap_Push_Border", 0.4f);

                // The view-space normal is the world normal times the inverse view, as in the original programs. For
                // the rigid view that is the view rotation: the normal (0.6, 0, -0.8) gives r = (0, 0.6, 0), u 0.74 at
                // push 0.4. Under the non-rigid view diag(2, 1, -1) the original gives r = (0, 0.3, 0), u 0.65 at push
                // 0.5; the view matrix itself would give r = (0, 1.2, 0), u 1.1, the last texel.
                rig.SetNormals(new Vector3(0.6f, 0, -0.8f));
                Expect(rig.GridColor(GridSize, new Vector2(0.74f, 0.5f)), "rigid view: the normal through the view rotation");
                Matrix4x4 rigid = rig.Camera.worldToCameraMatrix;
                try
                {
                    rig.Camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(2, 1, 1)) * rigid;
                    material.SetFloat("_Matcap_Push_Border", 0.5f);
                    Expect(rig.GridColor(GridSize, new Vector2(0.65f, 0.5f)), "non-rigid view: the normal through the inverse view");
                }
                finally
                {
                    rig.Camera.ResetWorldToCameraMatrix();
                    material.SetFloat("_Matcap_Push_Border", 0.4f);
                }
                RecoveredShaderProof.Require(ShaderName, rig.Camera.worldToCameraMatrix == rigid, "the rig view restored");
                rig.SetNormals(Vector3.back);

                // The dream gradient over a red matcap: the octaves sum to (0.625, 0.25) at offset 1; red times the dream
                // opacity weights the gradient.
                material.SetTexture("_Matcap_Texture", rig.Texels(red));
                Expect(red, "dream opacity 0 (the default) is the matcap");
                material.SetFloat("_Dream_Scrolling_Amount", 1);
                Expect(new Color(0.375f, 0, 0.625f, 1), "the gradient weighted by the summed noise red");
                material.SetFloat("_Dream_Scrolling_Amount", 0.5f);
                Expect(new Color(0.6875f, 0, 0.3125f, 1), "the dream opacity");
                material.SetFloat("_Dream_Scrolling_Amount", 1);

                // The gradient UV is the summed noise times the offset, on a 4-band strip: u 0.625 is band 2, 0.375 band 1.
                material.SetTexture(DreamGradient, rig.Bands(red, green, blue, red));
                Expect(new Color(0.375f, 0, 0.625f, 1), "offset 1 reads band 2");
                material.SetFloat("_Dream_Gradient_Offset", 0.6f);
                Expect(new Color(0.375f, 0.625f, 0, 1), "offset 0.6 reads band 1");
                material.SetFloat("_Dream_Gradient_Offset", 1);
                material.SetTexture(DreamGradient, rig.Texels(blue));

                // The octave UVs on a 4-band noise strip (0.25, 0, 0.125, 0) with the tiling at 0: octave 1 reads u =
                // time * speed.x, octave 2 reads 0.3 + time * speed.z; no 0.5 scale on the second octave.
                material.SetTexture(DreamNoise, rig.Bands(Noise(0.25f), Noise(0), Noise(0.125f), Noise(0)));
                material.SetFloat("_Dream_UV_Speed_1", 0);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 1);
                material.SetVector("_Dream_UV_Speed", new Vector4(0.125f, 0, 0.325f, 0));
                Expect(new Color(0.625f, 0, 0.375f, 1), "octave 1 at u 0.125 (band 0), octave 2 at u 0.625 (band 2)");
                material.SetVector("_Dream_UV_Speed", new Vector4(0.375f, 0, 0.325f, 0));
                Expect(new Color(0.875f, 0, 0.125f, 1), "octave 1 scrolls to band 1");
                material.SetVector("_Dream_UV_Speed", new Vector4(0.125f, 0, 0.075f, 0));
                Expect(new Color(0.75f, 0, 0.25f, 1), "octave 2 scrolls to band 1 (u 0.375)");
                material.SetVector("_Dream_UV_Speed", Vector4.zero);
                Shader.SetGlobalFloat("_HLShaderTimeGame", 0);
                // Tiling 0.25 at pixel 16 (UV 0.5156): octave 1 at u 0.129 (band 0), octave 2 at u 0.429 (band 1).
                material.SetFloat("_Dream_UV_Speed_1", 0.25f);
                Expect(new Color(0.75f, 0, 0.25f, 1), "the tiling");
                material.SetFloat("_Dream_UV_Speed_1", 1);
                material.SetFloat("_Dream_Scrolling_Amount", 0);

                // Alpha: saturate(vertex red * opacity).
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                Expect(new Color(1, 0, 0, 0.5f), "vertex red");
                rig.SetColors(new Color(1, 1, 0, 0));
                material.SetFloat("_Opacity", 0.5f);
                Expect(new Color(1, 0, 0, 0.5f), "the opacity, ignoring vertex blue and alpha");
                rig.SetColors(new Color(0.5f, 1, 1, 1));
                Expect(new Color(1, 0, 0, 0.25f), "vertex red times the opacity");
                material.SetFloat("_Opacity", 1);
                rig.SetColors(Color.white);

                // Displacement along the object normal: noise 0.5 times the intensity times vertex green. Normals along
                // +x move the quad right; 0.5 (8 px) uncovers pixel 4, pixel 12 stays covered.
                rig.SetNormals(Vector3.right);
                Expect(red, "no displacement at the default intensity 0", 4);
                material.SetFloat("_Vertex_Displacement_Intensity", 1);
                Expect(clear, "the displacement moves the quad", 4);
                Expect(red, "the moved quad still covers pixel 12", 12);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1, x: 4).r == 0, "DepthOnly displaces too");
                cases++;
                material.SetFloat("_Vertex_Displacement_Intensity", 0.5f);
                Expect(red, "the intensity (0.25, 4 px)", 4);
                material.SetFloat("_Vertex_Displacement_Intensity", 1);
                rig.SetColors(new Color(1, 0, 1, 1));
                Expect(red, "vertex green 0 holds the quad", 4);
                rig.SetColors(Color.white);
                material.SetFloat("_Vertex_Displacement_Intensity", 0);
                rig.SetNormals(Vector3.back);
                RecoveredShaderProof.Require(ShaderName, rig.Draw(material, 1).r != 0, "DepthOnly writes depth");
                cases++;

                // _GlobalMipBias applies once to the matcap: splayed normals move the lookup about 1.9 texels of a 256
                // texture per pixel (level 1); bias 2 reaches level 3 once, twice would reach 5.
                rig.Quad.normals = new[] { new Vector3(-0.3f, -0.3f, -1), new Vector3(0.3f, -0.3f, -1), new Vector3(0.3f, 0.3f, -1), new Vector3(-0.3f, 0.3f, -1) };
                material.SetTexture("_Matcap_Texture", rig.MipLevels(256, level => level <= 2 ? Color.white : level <= 4 ? green : blue));
                Expect(Color.white, "no bias reads level 1");
                Shader.SetGlobalVector("_GlobalMipBias", new Vector4(2, 0, 0, 0));
                Expect(green, "bias 2 applies once (level 3)");
                Shader.SetGlobalVector("_GlobalMipBias", Vector4.zero);

                Debug.Log("Project Lucid HL_Hub_Matcap_Unlit GPU verification passed: " + cases + " Metal-derived cases on " +
                          SystemInfo.graphicsDeviceType + ". The displacement noise UVs and the perspective view direction remain compile-checked only.");
            }
        }
    }
}
