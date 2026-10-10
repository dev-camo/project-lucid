using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RenderScaleConfiguration : VisualQualityConfiguration
    {
        [Range(0f, 1f)] [SerializeField] private float m_renderScale = 1f;

        // Game.Runtime 0x06002db0: direct original field getter.
        public float RenderScale { get { return m_renderScale; } }

        // 0x06002db1: the original pipeline cast is only an eligibility gate.
        // Publish the value to the required manager rather than the pipeline asset.
        public override void Apply()
        {
            UniversalRenderPipelineAsset pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
                ProcessManager.GetSystem<VisualQualityManager_SDT>(null, true).GameplayRenderScale = m_renderScale;
        }

        // 0x06002db2: the authored float1 initializer precedes the base ctor.
        public RenderScaleConfiguration() { }
    }
}
