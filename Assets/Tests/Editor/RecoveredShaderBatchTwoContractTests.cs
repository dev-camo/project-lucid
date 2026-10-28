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
    // Declaration contracts for the batch-two recovered shaders (#448): the original keyword tables and
    // stage masks, float stage types, four-lane depth outputs, and the decoded unlit vertex payloads.
    public sealed class RecoveredShaderBatchTwoContractTests
    {
        [Test]
        public void EnvZone2GooKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone2_Goo"),
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
        public void EnvStandardTriPlanarKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Standard_TriPlanar"),
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
                    "FOG_EXP2", "_USE_FRESNEL",
                    "_USE_TRIPLANAR_DETAIL", "_USE_EMISSION",
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
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 24, 25, 44 }, // ShadowCaster vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 24, 25 }, // ShadowCaster fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 24, 25 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 24, 25 }, // DepthOnly fragment
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 24, 25 }, // DepthNormals vertex
                    new[] { 0, 1, 2, 3, 17, 18, 19, 23, 24, 25, 40 }, // DepthNormals fragment
                    new[] { 0, 1, 2, 3, 18, 23, 24, 25, 45 }, // Meta vertex
                    new[] { 0, 1, 2, 3, 18, 23, 24, 25, 45 } // Meta fragment
                },
                new[] { "_MIXED_LIGHTING_SUBTRACTIVE", "_GBUFFER_NORMALS_OCT", "_RENDER_PASS_ENABLED" });
        }

        [Test]
        public void EnvZone5TrackKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Zone5_Track"),
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
        public void EnvStandardUnlitKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Env_Standard_Unlit"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "DOTS_INSTANCING_ON",
                    "_FOVEATED_RENDERING_NON_UNIFORM_RASTER", "INSTANCING_ON",
                    "FOG_LINEAR", "FOG_EXP",
                    "FOG_EXP2", "_USE_SCROLLING_UVS",
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
        public void HubStandardUnlitKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Hub_Standard_Unlit"),
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
        public void SkyboxPortalKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Skybox_Portal"),
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
        public void GameplayTimedPlatformKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Gameplay_Timed_Platform"),
                new[] {
                    "STEREO_INSTANCING_ON", "UNITY_SINGLE_PASS_STEREO",
                    "STEREO_MULTIVIEW_ON", "STEREO_CUBEMAP_RENDER_ON",
                    "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED",
                    "_SAMPLE_GI", "_FOG_LINEAR",
                    "DOTS_INSTANCING_ON", "_FOVEATED_RENDERING_NON_UNIFORM_RASTER",
                    "INSTANCING_ON", "FOG_LINEAR",
                    "FOG_EXP", "FOG_EXP2",
                    "_DBUFFER_MRT1", "_DBUFFER_MRT2",
                    "_DBUFFER_MRT3", "DEBUG_DISPLAY",
                    "_SCREEN_SPACE_OCCLUSION", "_WRITE_RENDERING_LAYERS",
                    "_GBUFFER_NORMALS_OCT"
                },
                new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] {
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, // Universal Forward vertex
                    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }, // Universal Forward fragment
                    new[] { 0, 1, 2, 3, 7, 8, 9, 10 }, // DepthOnly vertex
                    new[] { 0, 1, 2, 3, 7, 8, 9, 10 } // DepthOnly fragment
                },
                new[] { "_GBUFFER_NORMALS_OCT" });
        }

        [Test]
        public void CharBoss03BodyBlobsKeywordScopesAndPassMasksMatchOriginal()
        {
            // Literal original m_KeywordNames/Flags and every Metal stage mask, in table order.
            CheckDeclarations(ShaderPath("HL_Char_Boss_03_Body_Blobs"),
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
        public void EnvStandardUnlitVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Env_Standard_Unlit"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float3" }, // DepthOnly
                new[] { "float4", "float3", "float3" } // ShadowCaster
            });
        }

        [Test]
        public void HubStandardUnlitVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Hub_Standard_Unlit"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4", "float4", "float3" } // DepthOnly
            });
        }

        [Test]
        public void SkyboxPortalVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Skybox_Portal"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void GameplayTimedPlatformVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Gameplay_Timed_Platform"), new[]
            {
                new[] { "float4", "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }

        [Test]
        public void CharBoss03BodyBlobsVaryingsKeepDecodedVertexOutputs()
        {
            // Each pass's decoded Metal Mtl_VertexOut interpolants (INTERP0..n), in order.
            CheckVaryings(ShaderPath("HL_Char_Boss_03_Body_Blobs"), new[]
            {
                new[] { "float4", "float3", "float3" }, // Universal Forward
                new[] { "float4" } // DepthOnly
            });
        }
        private static string ShaderPath(string name) => "Assets/Shaders/Recovered/" + name + ".shader";

        private static void CheckDeclarations(string path, string[] names, byte[] flags, int[][] masks, string[] unmasked)
        {
            string source = File.ReadAllText(path);
            Assert.That(Regex.IsMatch(source, @"\b(?:half|fixed)(?:[1-4])?\b"), Is.False,
                path + ": the original Metal stage and arithmetic types are float");
            string depthPass = source.Substring(source.IndexOf("Name \"DepthOnly\"", StringComparison.Ordinal));
            depthPass = depthPass.Substring(0, depthPass.IndexOf("ENDHLSL", StringComparison.Ordinal));
            Assert.That(Regex.IsMatch(depthPass, @"\bfloat4\s+Frag\s*\((?:float4\s+positionCS\s*:\s*SV_POSITION|Varyings\s+\w+)\)\s*:\s*SV_Target"),
                Is.True, path + ": the original depth fragment has a float4 result");
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
                    var original = new List<string>();
                    foreach (int index in masks[pass * 2 + stage])
                    {
                        original.Add(names[index]);
                        activeIndices.Add(index);
                    }
                    var actual = new List<string>();
                    foreach (LocalKeyword keyword in ShaderUtil.GetPassKeywords(shader,
                        new PassIdentifier(0, (uint)pass), type, ShaderCompilerPlatform.Metal))
                    {
                        actual.Add(keyword.name);
                        actualUnion.Add(keyword.name);
                    }
                    ImportedKeywordContext.AssertPass(actual, original, path + " original Metal pass " + pass + " " + type + " mask");
                }
            }
            foreach (int index in activeIndices) ImportedKeywordContext.AssertKeyword(shader, names[index], flags[index]);
            // Serialized table entries with no stage mask have no surviving pass location to declare.
            foreach (string name in unmasked) Assert.That(actualUnion.Contains(name), Is.False, name);
        }

        private static void CheckVaryings(string path, string[][] passes)
        {
            string source = File.ReadAllText(path);
            MatchCollection programs = Regex.Matches(source, @"HLSLPROGRAM(.*?)ENDHLSL", RegexOptions.Singleline);
            Assert.That(programs.Count, Is.EqualTo(passes.Length), path + " pass count");
            for (int pass = 0; pass < passes.Length; pass++)
            {
                Match varyings = Regex.Match(programs[pass].Groups[1].Value, @"struct Varyings\s*\{(.*?)\};", RegexOptions.Singleline);
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
