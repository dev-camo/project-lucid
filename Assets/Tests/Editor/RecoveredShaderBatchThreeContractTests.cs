using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLucid.Tests
{
    // Declaration contracts for the batch-three recovered shaders (#448): the original keyword tables and
    // stage masks, float stage types, four-lane depth outputs, and the decoded vertex payloads.
    public sealed class RecoveredShaderBatchThreeContractTests
    {
        [Test]
        public void CharGhostedEyeKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Ghosted_Eye"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON"
                },
                new byte[] { 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3 }, // ForwardLit vertex
                    new[] { 0, 1, 2, 3 } // ForwardLit fragment
                },
                new string[0]);
        }

        [Test]
        public void CharEyeKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Eye"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 41 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 41 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void CharGhostedEyeVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (TEXCOORD0..n), in order.
            CheckVaryings(ShaderPath("HL_Char_Ghosted_Eye"), new[]
            {
                new[] { "float2" } // ForwardLit
            });
        }

        [Test]
        public void GameplayDreamOrbKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Dream_Orb"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 20 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayDreamOrbVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Dream_Orb"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float3", "float3" }, // DepthOnly
                new[] { "float3", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void ParticleDreamKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Particle_Dream"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "DEBUG_DISPLAY",
                    "_TIME_TYPE_SINE_TIME", "_TIME_TYPE_TIME",
                    "DOTS_INSTANCING_ON", "_ALPHATEST_ON"
                },
                new byte[] { 0, 0, 0, 0, 2, 0, 1, 1, 0, 1 },
                new[] {
                    new[] { 0, 1, 2, 3, 4 }, // Sprite Unlit (2D) vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, // Sprite Unlit (2D) fragment
                    new[] { 0, 1, 2, 3, 4 }, // Sprite Unlit (forward) vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7 } // Sprite Unlit (forward) fragment
                },
                new[] { "DOTS_INSTANCING_ON", "_ALPHATEST_ON" });
        }

        [Test]
        public void ParticleDreamVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Particle_Dream"), new[]
            {
                new[] { "float4", "float4", "float3" }, // Sprite Unlit (2D)
                new[] { "float4", "float4", "float3" } // Sprite Unlit (forward)
            });
        }

        [Test]
        public void SkyTrippyKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Sky_Trippy"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_USE_CUBEMAP_COMP",
                    "_USE_PATTERN_MASK", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void SkyTrippyVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Sky_Trippy"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new string[0] // DepthOnly
            });
        }

        [Test]
        public void ParticleAnimMeshBallSpinKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Particle_AnimMesh_BallSpin"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_GRADIENTMAPENABLE",
                    "_FRESNELENABLE", "_USE_UV_MASK",
                    "_DBUFFER_MRT1", "_DBUFFER_MRT2",
                    "_DBUFFER_MRT3", "DEBUG_DISPLAY",
                    "_SCREEN_SPACE_OCCLUSION", "_SURFACE_TYPE_TRANSPARENT",
                    "_WRITE_RENDERING_LAYERS", "_ALPHAPREMULTIPLY_ON",
                    "_ALPHAMODULATE_ON", "_ALPHATEST_ON",
                    "_GBUFFER_NORMALS_OCT", "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 15 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 15, 25 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 15, 27 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 14, 15, 25 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvWaterfallKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Waterfall"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_USE_DETAIL_MASK_MAP",
                    "_SCREEN_SPACE_OCCLUSION", "_ADDITIONAL_LIGHT_SHADOWS",
                    "_REFLECTION_PROBE_BLENDING", "_REFLECTION_PROBE_BOX_PROJECTION",
                    "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW",
                    "_SHADOWS_SOFT_MEDIUM", "_SHADOWS_SOFT_HIGH",
                    "_DBUFFER_MRT1", "_DBUFFER_MRT2",
                    "_DBUFFER_MRT3", "_LIGHT_LAYERS",
                    "DEBUG_DISPLAY", "_LIGHT_COOKIES",
                    "_WRITE_RENDERING_LAYERS", "_MIXED_LIGHTING_SUBTRACTIVE",
                    "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW", "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 42 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 38 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 23, 43 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 23, 43 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvFoliageWindKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_FoliageWind"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvSplatmapKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Splatmap"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void ParticleAnimMeshBallSpinVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Particle_AnimMesh_BallSpin"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float4", "float3", "float3" }, // DepthOnly
                new[] { "float4", "float4", "float3", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void EnvZone2BarrierKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone2_Barrier"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvZone2BarrierVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Zone2_Barrier"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void UIDreamEffectKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_UI_Dream_Effect"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON"
                },
                new byte[] { 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3 }, // UI_Dream_Effect vertex
                    new[] { 0, 1, 2, 3 } // UI_Dream_Effect fragment
                },
                new string[0]);
        }

        [Test]
        public void UIDreamEffectResourcesKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_UI_Dream_Effect_Resources"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON"
                },
                new byte[] { 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3 }, // UI_Dream_Effect vertex
                    new[] { 0, 1, 2, 3 } // UI_Dream_Effect fragment
                },
                new string[0]);
        }

        [Test]
        public void EnvCloudsKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Clouds"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvCloudsVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Clouds"), new[]
            {
                new[] { "float3", "float3" }, // Universal Forward
                new string[0] // DepthOnly
            });
        }

        [Test]
        public void GameplayLaserKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Laser"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 20 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayLaserVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Laser"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float4", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void CharBoss4EggmanKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Boss_4_Eggman"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvCrowdKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Crowd"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvCrowdVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Crowd"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void EnvZone1CliffsKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone1_Cliffs"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void UICoreLoopTransitionKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_UI_Core_Loop_Transition"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void UICoreLoopTransitionVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_UI_Core_Loop_Transition"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void EnvZone4HologramKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone4_Hologram"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 } // Universal Forward fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvZone4HologramVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Zone4_Hologram"), new[]
            {
                new[] { "float4", "float3", "float3" } // Universal Forward
            });
        }

        [Test]
        public void CharBoss02GooKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Boss_02_Goo"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void ParticleSmokeMeshKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Particle_SmokeMesh"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void ParticleSmokeMeshVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Particle_SmokeMesh"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float3", "float3" } // DepthOnly
            });
        }

        [Test]
        public void EnvZone1WaterKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone1_Water"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 41 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 41 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvZone3SpikesKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone3_Spikes"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvZone4CloudPlaneKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone4_Cloud_Plane"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 } // Universal Forward fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvZone4CloudPlaneVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Zone4_Cloud_Plane"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" } // Universal Forward
            });
        }

        [Test]
        public void EnvZone4BackgroundWindowsKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone4_Background_Windows"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvZone4BackgroundWindowsVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Zone4_Background_Windows"), new[]
            {
                new[] { "float3", "float3" }, // Universal Forward
                new string[0] // DepthOnly
            });
        }

        [Test]
        public void HubMatcapUnlitKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Hub_Matcap_Unlit"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void HubMatcapUnlitVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Hub_Matcap_Unlit"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void GameplayDreamBarrierKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Dream_Barrier"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 } // Universal Forward fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayDreamBarrierVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Dream_Barrier"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" } // Universal Forward
            });
        }

        [Test]
        public void GameplayDashRingLightKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Dash_Ring_Light"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 20 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayDashRingLightVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Dash_Ring_Light"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new string[0], // DepthOnly
                new[] { "float3" } // ShadowCaster
            });
        }

        [Test]
        public void ShockwaveNightmareKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Shockwave_nightmare"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 } // Universal Forward fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void ShockwaveNightmareVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Shockwave_nightmare"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" } // Universal Forward
            });
        }

        [Test]
        public void ParticleTrailVertexoffsetKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Particle_Trail_Vertexoffset"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_SURFACE_TYPE_TRANSPARENT", "_WRITE_RENDERING_LAYERS",
                    "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON",
                    "_ALPHATEST_ON", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 22 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 24 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 22 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void ParticleTrailVertexoffsetVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Particle_Trail_Vertexoffset"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float4", "float3" }, // DepthOnly
                new[] { "float4", "float4", "float3", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void GameplayElectricityKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Electricity"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_SURFACE_TYPE_TRANSPARENT", "_WRITE_RENDERING_LAYERS",
                    "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON",
                    "_ALPHATEST_ON", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 22 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 24 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 22 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayElectricityVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Electricity"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float4" }, // DepthOnly
                new[] { "float4", "float4", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void ParticleScreenDistortionKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Particle_ScreenDistortion"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DEPTHFADE",
                    "_DBUFFER_MRT1", "_DBUFFER_MRT2",
                    "_DBUFFER_MRT3", "DEBUG_DISPLAY",
                    "_SCREEN_SPACE_OCCLUSION", "_SURFACE_TYPE_TRANSPARENT",
                    "_WRITE_RENDERING_LAYERS", "_ALPHAPREMULTIPLY_ON",
                    "_ALPHAMODULATE_ON", "_ALPHATEST_ON",
                    "_GBUFFER_NORMALS_OCT", "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 23 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 25 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 13, 23 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void ParticleScreenDistortionVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Particle_ScreenDistortion"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float4", "float3" }, // DepthOnly
                new[] { "float4", "float4", "float3", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void EnvZone2GooPipesKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone2_Goo_Pipes"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvZone5GooKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone5_Goo"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void EnvZone5GooVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Zone5_Goo"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new string[0] // DepthOnly
            });
        }

        [Test]
        public void CharAriemStarsKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Ariem_Stars"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT",
                    "_CASTING_PUNCTUAL_LIGHT_SHADOW"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 20 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // ShadowCaster fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void CharAriemStarsVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Char_Ariem_Stars"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new string[0], // DepthOnly
                new[] { "float3" } // ShadowCaster
            });
        }

        [Test]
        public void CharBoss4EggmanMoustacheKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Boss_4_Eggman_Moustache"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 41 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 41 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void GameplayLightningBoltKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_LightningBolt"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 } // Universal Forward fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayLightningBoltVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_LightningBolt"), new[]
            {
                new[] { "float4", "float3", "float3" } // Universal Forward
            });
        }

        [Test]
        public void SkyDreamEffectPanningKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Sky_dream_effect_panning"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 } // Universal Forward fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void SkyDreamEffectPanningVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Sky_dream_effect_panning"), new[]
            {
                new[] { "float4", "float3", "float3" } // Universal Forward
            });
        }

        [Test]
        public void GameplayZone2TubeDoorKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Zone2_Tube_Door"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "DEBUG_DISPLAY", "_SCREEN_SPACE_OCCLUSION",
                    "_WRITE_RENDERING_LAYERS", "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void GameplayZone2TubeDoorVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Zone2_Tube_Door"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void ParticleDreamRippleKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Particle_Dream_Ripple"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "DEBUG_DISPLAY",
                    "DOTS_INSTANCING_ON"
                },
                new byte[] { 0, 0, 0, 0, 2, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4 }, // Sprite Unlit vertex
                    new[] { 0, 1, 2, 3, 4, 5 }, // Sprite Unlit fragment
                    new[] { 0, 1, 2, 3, 4 }, // Sprite Unlit vertex
                    new[] { 0, 1, 2, 3, 4, 5 } // Sprite Unlit fragment
                },
                new[] { "DOTS_INSTANCING_ON" });
        }

        [Test]
        public void ParticleDreamRippleVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Particle_Dream_Ripple"), new[]
            {
                new[] { "float4", "float4", "float3" }, // Sprite Unlit
                new[] { "float4", "float4", "float3" } // Sprite Unlit
            });
        }

        [Test]
        public void CharBoss01ShockwaveKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Boss_01_Shockwave"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 41 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 41 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void CharBoss03BodyKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Boss_03_Body"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DYNAMICLIGHTMAP_ON",
                    "DIRLIGHTMAP_COMBINED", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_FORWARD_PLUS", "EVALUATE_SH_MIXED",
                    "EVALUATE_SH_VERTEX", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_SCREEN_SPACE_OCCLUSION",
                    "_ADDITIONAL_LIGHT_SHADOWS", "_REFLECTION_PROBE_BLENDING",
                    "_REFLECTION_PROBE_BOX_PROJECTION", "_SHADOWS_SOFT",
                    "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM",
                    "_SHADOWS_SOFT_HIGH", "_DBUFFER_MRT1",
                    "_DBUFFER_MRT2", "_DBUFFER_MRT3",
                    "_LIGHT_LAYERS", "DEBUG_DISPLAY",
                    "_LIGHT_COOKIES", "_WRITE_RENDERING_LAYERS",
                    "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "_CASTING_PUNCTUAL_LIGHT_SHADOW",
                    "EDITOR_VISUALIZATION"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 41 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 37 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 42 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 42 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void UIWorldspaceAlwaysOnTopKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_UI_Worldspace_AlwaysOnTop"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "UNITY_UI_ALPHACLIP"
                },
                new byte[] { 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4 }, // Default vertex
                    new[] { 0, 1, 2, 3, 4 } // Default fragment
                },
                new string[0]);
        }

        [Test]
        public void UIWorldspaceAlwaysOnTopVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (TEXCOORD0..n), in order; the colour travels on COLOR0.
            CheckVaryings(ShaderPath("HL_UI_Worldspace_AlwaysOnTop"), new[]
            {
                new[] { "float2", "float4" } // Default
            });
        }

        [Test]
        public void GameplayBlobShadowKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Blob_Shadow"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "_DECAL_LAYERS", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "_DBUFFER_MRT1", "_DBUFFER_MRT2",
                    "_DBUFFER_MRT3", "_MAIN_LIGHT_SHADOWS",
                    "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS",
                    "_FORWARD_PLUS", "_DECAL_NORMAL_BLEND_LOW",
                    "_DECAL_NORMAL_BLEND_MEDIUM", "_DECAL_NORMAL_BLEND_HIGH",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_ADDITIONAL_LIGHT_SHADOWS",
                    "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW",
                    "_SHADOWS_SOFT_MEDIUM", "_SHADOWS_SOFT_HIGH",
                    "_LIGHT_COOKIES", "_GBUFFER_NORMALS_OCT",
                    "_RENDER_PASS_ENABLED", "LIGHTMAP_ON",
                    "DYNAMICLIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "LIGHTMAP_SHADOW_MIXING", "SHADOWS_SHADOWMASK",
                    "_MIXED_LIGHTING_SUBTRACTIVE"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, // DecalScreenSpaceProjector vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 }, // DecalScreenSpaceProjector fragment
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 31, 32, 33, 34, 35 }, // DecalScreenSpaceMesh vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 31, 32, 33, 34, 35 } // DecalScreenSpaceMesh fragment
                },
                new[] { "_DBUFFER_MRT1", "_DBUFFER_MRT2", "_DBUFFER_MRT3", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED", "_MIXED_LIGHTING_SUBTRACTIVE" });
        }

        private static string ShaderPath(string name) => "Assets/Shaders/Recovered/" + name + ".shader";

        // URP's FoveatedRenderingKeywords.hlsl declares this dynamic branch only when compiling for an Apple
        // platform, so the editor reports it in the Metal masks only while the build target is one.
        private const string Foveated = "_FOVEATED_RENDERING_NON_UNIFORM_RASTER";

        private static bool DeclaresFoveated(string name)
        {
            if (name != Foveated) return true;
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            return target == BuildTarget.StandaloneOSX || target == BuildTarget.iOS || target == BuildTarget.tvOS;
        }

        private static void CheckDeclarations(string path, string[] names, byte[] flags, int[][] masks, string[] unmasked)
        {
            string source = File.ReadAllText(path);
            Assert.That(Regex.IsMatch(source, @"\b(?:half|fixed)(?:[1-4])?\b"), Is.False,
                path + ": the original Metal stage and arithmetic types are float");
            int depthStart = source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal);
            if (depthStart >= 0)
            {
                string depthPass = source.Substring(depthStart);
                depthPass = depthPass.Substring(0, depthPass.IndexOf("ENDHLSL", StringComparison.Ordinal));
                Assert.That(Regex.IsMatch(depthPass, @"\bfloat4\s+Frag\s*\((?:float4\s+positionCS\s*:\s*SV_POSITION|Varyings\s+\w+)\)\s*:\s*SV_Target"),
                    Is.True, path + ": the original depth fragment has a float4 result");
            }
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            Assert.That(ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0).PassCount * 2, Is.EqualTo(masks.Length), path);
            var activeIndices = new HashSet<int>();
            var actualUnion = new HashSet<string>();
            for (int pass = 0; pass < masks.Length / 2; pass++)
            {
                for (int stage = 0; stage < 2; stage++)
                {
                    ShaderType type = stage == 0 ? ShaderType.Vertex : ShaderType.Fragment;
                    var expected = new List<string>();
                    foreach (int index in masks[pass * 2 + stage])
                    {
                        if (!DeclaresFoveated(names[index])) continue;
                        expected.Add(names[index]);
                        activeIndices.Add(index);
                    }
                    var actual = new List<string>();
                    foreach (LocalKeyword keyword in ShaderUtil.GetPassKeywords(shader,
                        new PassIdentifier(0, (uint)pass), type, ShaderCompilerPlatform.Metal))
                    {
                        actual.Add(keyword.name);
                        actualUnion.Add(keyword.name);
                    }
                    Assert.That(actual, Is.EquivalentTo(expected), path + " original Metal pass " + pass + " " + type + " mask");
                }
            }
            foreach (int index in activeIndices)
            {
                LocalKeyword keyword = shader.keywordSpace.FindKeyword(names[index]);
                Assert.That(keyword.isValid, Is.True, names[index]);
                Assert.That(keyword.isOverridable, Is.EqualTo((flags[index] & 1) == 0), names[index]);
                Assert.That(keyword.isDynamic, Is.EqualTo((flags[index] & 2) != 0), names[index]);
            }
            // Serialized table entries with no stage mask have no surviving pass location to declare.
            foreach (string name in unmasked) Assert.That(actualUnion.Contains(name), Is.False, name);
        }

        private static void CheckVaryings(string path, string[][] passes)
        {
            string source = File.ReadAllText(path);
            MatchCollection programs = Regex.Matches(source, @"HLSLPROGRAM(.*?)ENDHLSL", RegexOptions.Singleline);
            Assert.That(programs.Count, Is.EqualTo(passes.Length), path + " pass count");
            // A pass without its own Varyings uses the one its passes share in HLSLINCLUDE.
            Match shared = Regex.Match(source, @"HLSLINCLUDE(.*?)ENDHLSL", RegexOptions.Singleline);
            for (int pass = 0; pass < passes.Length; pass++)
            {
                Match varyings = Regex.Match(programs[pass].Groups[1].Value, @"struct Varyings\s*\{(.*?)\};", RegexOptions.Singleline);
                if (!varyings.Success && shared.Success)
                    varyings = Regex.Match(shared.Groups[1].Value, @"struct Varyings\s*\{(.*?)\};", RegexOptions.Singleline);
                Assert.That(varyings.Success, Is.True, path + " pass " + pass + " declares its Varyings");
                var actual = new List<string>();
                int semantic = 0;
                foreach (Match field in Regex.Matches(varyings.Groups[1].Value, @"\b(float[1-4]?)\s+\w+\s*:\s*TEXCOORD(\d+)"))
                {
                    Assert.That(int.Parse(field.Groups[2].Value), Is.EqualTo(semantic++), path + " pass " + pass + " interpolant order");
                    actual.Add(field.Groups[1].Value);
                }
                Assert.That(actual, Is.EqualTo(passes[pass]), path + " pass " + pass + " decoded vertex outputs");
            }
        }
    }
}
