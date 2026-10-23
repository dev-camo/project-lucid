using System;
using System.Linq;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.Editor
{
    // The render pipeline the shipped QualitySettings select, read back from the
    // prepared project. FullscreenShaderManager.Awake (060035bf) casts
    // GraphicsSettings.currentRenderPipeline to UniversalRenderPipelineAsset with
    // no null check, takes renderer 0's FullScreenPassRendererFeature and its
    // passMaterial, and renderer 1's UIBlurRenderFeature, each by exact GetType()
    // equality (060035ce). URP 14.0.11's GetRenderer only warns and falls back to
    // renderer 0 for a missing renderer, so renderer 1 is checked by identity. The
    // negatives show the lookups are index-sensitive; a derived feature instance
    // would need a mutated pipeline asset and is not exercised here.
    public sealed class OriginalRenderPipelineSelectionTests
    {
        private static readonly string[] LevelNames = { "Very Low", "Low", "Medium", "High", "Very High" };
        private static readonly string[] PipelineNames = { "URPAsset_VeryLow", "URPAsset_Low", "URPAsset_Med", "URPAsset_High", "URPAsset_VeryHigh" };

        [Test]
        public void EveryShippedQualityLevelSelectsItsPreparedPipelineAsset()
        {
            Assert.That(QualitySettings.names, Is.EqualTo(LevelNames));
            for (int level = 0; level < LevelNames.Length; level++)
            {
                RenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(level);
                Assert.That(asset, Is.Not.Null, LevelNames[level]);
                Assert.That(asset.GetType(), Is.EqualTo(typeof(UniversalRenderPipelineAsset)), LevelNames[level]);
                Assert.That(asset.name, Is.EqualTo(PipelineNames[level]), LevelNames[level]);
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo("Assets/Recovered/MonoBehaviour/" + PipelineNames[level] + ".asset"));
            }
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(QualitySettings.GetRenderPipelineAssetAt(0)),
                        "GraphicsSettings' default is the Very Low asset, as shipped.");
            // The selected global settings carry the shipped rendering layers.
            RenderPipelineGlobalSettings settings = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            Assert.That(settings, Is.Not.Null);
            // The URP type is internal; name it exactly.
            Assert.That(settings.GetType().FullName, Is.EqualTo("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings"));
            Assert.That(AssetDatabase.GetAssetPath(settings), Is.EqualTo("Assets/UniversalRenderPipelineGlobalSettings.asset"));
            using (var serialized = new SerializedObject(settings))
            {
                Assert.That(serialized.FindProperty("m_ValidRenderingLayers").intValue, Is.EqualTo(3));
                SerializedProperty names = serialized.FindProperty("m_RenderingLayerNames");
                Assert.That(Enumerable.Range(0, names.arraySize).Select(i => names.GetArrayElementAtIndex(i).stringValue).ToArray(),
                            Is.EqualTo(new[] { "Light Layer default", "Character Layer" }));
            }
        }

        [Test]
        public void FullscreenShaderManagerLookupsFindTheirExactFeaturesAtEveryLevel()
        {
            int original = QualitySettings.GetQualityLevel();
            try
            {
                for (int level = 0; level < LevelNames.Length; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                    Assert.That(pipeline, Is.Not.Null, LevelNames[level]);
                    Assert.That(pipeline.name, Is.EqualTo(PipelineNames[level]));
                    ScriptableRenderer game = pipeline.GetRenderer(0), ui = pipeline.GetRenderer(1);
                    Assert.That(game, Is.Not.Null); Assert.That(ui, Is.Not.Null);
                    Assert.That(ReferenceEquals(game, ui), Is.False, "Renderer 1 is Renderer_UI itself, not the renderer 0 fallback.");

                    FullScreenPassRendererFeature vignette = FullscreenShaderManager.GetRendererFeature<FullScreenPassRendererFeature>(0);
                    Assert.That(vignette, Is.Not.Null, LevelNames[level]);
                    Assert.That(vignette.GetType(), Is.EqualTo(typeof(FullScreenPassRendererFeature)));
                    Assert.That(vignette.passMaterial, Is.Not.Null, "Awake reads passMaterial without a null check.");
                    Assert.That(vignette.passMaterial.name, Is.EqualTo("render_feature_fullscreen_effects"));
                    UIBlurRenderFeature blur = FullscreenShaderManager.GetRendererFeature<UIBlurRenderFeature>(1);
                    Assert.That(blur, Is.Not.Null, LevelNames[level]);
                    Assert.That(blur.GetType(), Is.EqualTo(typeof(UIBlurRenderFeature)));

                    // Negative controls: the lookups are index-sensitive and exact.
                    Assert.That(FullscreenShaderManager.GetRendererFeature<UIBlurRenderFeature>(0), Is.Null);
                    Assert.That(FullscreenShaderManager.GetRendererFeature<FullScreenPassRendererFeature>(1), Is.Null);
                }
                // URP's missing-renderer fallback only warns, which LogAssert does not
                // catch; the renderer identity check above is what rules it out. This
                // still refuses any error or exception the lookups log.
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                QualitySettings.SetQualityLevel(original, false);
            }
        }
    }
}
