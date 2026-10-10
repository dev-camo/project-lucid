using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIBlurRenderFeature : ScriptableRendererFeature
    {
        public KawaseBlurSettings settings = new KawaseBlurSettings();
        private CustomRenderPass m_scriptablePass;

        // Game.Runtime 0x06002b14: publish the pass before looking up the original shader.
        // Missing shader leaves that pass present without applying the settings.
        public override void Create()
        {
            m_scriptablePass = new CustomRenderPass("KawaseBlurPass");
            Shader shader = Shader.Find("HardLight/RenderFeature/HL_UI_Blur_Render_Feature");
            if (shader == null) return;
            m_scriptablePass.blurMaterial = CoreUtils.CreateEngineMaterial(shader);
            m_scriptablePass.Passes = settings.Passes;
            m_scriptablePass.Downsample = settings.Downsample;
            m_scriptablePass.renderPassEvent = settings.renderPassEvent;
        }

        // Game.Runtime 0x06002b15: enqueue without camera/material/null filtering.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_scriptablePass);
        }

        // Game.Runtime 0x06002b16: the original readonly renderingData parameter is unused.
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            m_scriptablePass.SetTarget(renderer.cameraColorTargetHandle);
        }

        // Game.Runtime 0x06002b17: false is a no-op; true retains the original pass dereference.
        protected override void Dispose(bool disposing)
        {
            if (disposing) m_scriptablePass.Dispose();
        }

        // Game.Runtime 0x06002b18: settings allocation precedes the original renderer-feature base.
        public UIBlurRenderFeature() { }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class KawaseBlurSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            [Tooltip("The lower this value is, the more performant the effect.")]
            [Range(1, 10)]
            public int Passes;
            [Range(1, 8)]
            [Tooltip("The higher this value is, the more performant the effect.")]
            public int Downsample;

            // Game.Runtime 0x06002b19: only event 500 is initialized; ranges do not clamp values.
            public KawaseBlurSettings() { }
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class CustomRenderPass : ScriptableRenderPass
        {
            [SerializeField] private string m_targetName = "_blurTexture";
            [SerializeField] private string m_profilerTag;
            public int Passes;
            public int Downsample;
            public Material blurMaterial;
            private RTHandle m_tmpRT1;
            private RTHandle m_tmpRT2;
            private RTHandle m_source;
            private RTHandle m_final;
            private Vector2Int m_viewportSize = Vector2Int.zero;
            private bool m_requiresBlur = true;

            // Game.Runtime 0x06002b1a: store the supplied handle without releasing its predecessor.
            public void SetTarget(RTHandle colorHandle) { m_source = colorHandle; }

            // Game.Runtime 0x06002b1b: initializers precede the base; profiler tag is assigned afterward.
            public CustomRenderPass(string m_profilerTag) { this.m_profilerTag = m_profilerTag; }

            // Game.Runtime 0x06002b1c: viewport and allocation changes invalidate the cached blur.
            // All three ConfigureTarget calls and the ignored final-allocation result are original.
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                cameraTextureDescriptor.depthBufferBits = 0;
                if (RTHandles.rtHandleProperties.currentViewportSize != m_viewportSize)
                {
                    RTHandles.ResetReferenceSize(cameraTextureDescriptor.width, cameraTextureDescriptor.height);
                    m_requiresBlur = true;
                    m_viewportSize = RTHandles.rtHandleProperties.currentViewportSize;
                }
                m_requiresBlur |= RenderingUtils.ReAllocateIfNeeded(ref m_tmpRT1,
                    Vector2.one / Downsample, in cameraTextureDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, false, 1, 0f, "");
                ConfigureTarget(m_tmpRT1);
                m_requiresBlur |= RenderingUtils.ReAllocateIfNeeded(ref m_tmpRT2,
                    Vector2.one / Downsample, in cameraTextureDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, false, 1, 0f, "");
                ConfigureTarget(m_tmpRT2);
                RenderingUtils.ReAllocateIfNeeded(ref m_final,
                    Vector2.one / Downsample, in cameraTextureDescriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, false, 1, 0f, "");
                ConfigureTarget(m_final);
            }

            // Game.Runtime 0x06002b1d: regenerate only after invalidation, always publish the cached texture.
            // Keep field swaps, float addition order, and command execution/clear/release order intact.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_profilerTag);
                if (m_requiresBlur)
                {
                    cmd.SetGlobalFloat("_offset", 1.5f);
                    Blitter.BlitCameraTexture(cmd, m_source, m_tmpRT1, blurMaterial, 0);
                    for (int i = 1; i < unchecked(Passes - 1); i = unchecked(i + 1))
                    {
                        cmd.SetGlobalFloat("_offset", (float)i + 0.5f);
                        Blitter.BlitCameraTexture(cmd, m_tmpRT1, m_tmpRT2, blurMaterial, 0);
                        RTHandle temporary = m_tmpRT1;
                        m_tmpRT1 = m_tmpRT2;
                        m_tmpRT2 = temporary;
                    }
                    cmd.SetGlobalFloat("_offset", ((float)Passes + 0.5f) + -1f);
                    Blitter.BlitCameraTexture(cmd, m_tmpRT1, m_final, blurMaterial, 0);
                    m_requiresBlur = false;
                }
                cmd.SetGlobalTexture(m_targetName, m_final);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            // Game.Runtime 0x06002b1e: release all non-null handles, including the supplied source.
            // The shipped method retains its fields and material after release.
            public void Dispose()
            {
                m_tmpRT1?.Release();
                m_tmpRT2?.Release();
                m_source?.Release();
                m_final?.Release();
            }
        }
    }
}
