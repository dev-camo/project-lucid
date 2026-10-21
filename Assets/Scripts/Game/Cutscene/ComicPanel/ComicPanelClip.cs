using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 0200042e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ComicPanelClip : PlayableAsset
    {
        [SerializeField] private ComicPanel m_comicPanelTemplate;
        [SerializeField] private RenderTexture m_renderTexture;
        [SerializeField] private Vector2 m_screenPosition;
        [SerializeField] private Vector3 m_rotation;
        [SerializeField] private float m_scaleMultiplier = 1f;
        [SerializeField] private AnimationClip m_enterAnim;
        [SerializeField] private AnimationClip m_loopAnim;
        [SerializeField] private AnimationClip m_exitAnim;
        [NonSerialized] private bool m_initialised;

        // 0x06001895 (0x79DE90). The render texture is sized lazily on the first
        // playable; the scale multiplier is applied to both axes.
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (!m_initialised)
                InitialiseRenderTexture();
            ScriptPlayable<ComicPanelBehaviour> playable = ScriptPlayable<ComicPanelBehaviour>.Create(graph);
            ComicPanelBehaviour behaviour = playable.GetBehaviour();
            behaviour.PanelTemplate = m_comicPanelTemplate;
            behaviour.RenderTexture = m_renderTexture;
            behaviour.ScreenPosition = m_screenPosition;
            behaviour.Rotation = m_rotation;
            behaviour.Scale = new Vector2(m_scaleMultiplier, m_scaleMultiplier);
            behaviour.EnterAnim = m_enterAnim;
            behaviour.LoopAnim = m_loopAnim;
            behaviour.ExitAnim = m_exitAnim;
            return playable;
        }

        // 0x06001896 (0x79E130): back to the default render size, without marking the
        // clip initialised.
        private void OnDisable()
        {
            VisualQualityManager_SDT visualQualityManager = ProcessManager.GetSystemSafe<VisualQualityManager_SDT>();
            if (visualQualityManager == null)
                return;
            SetRenderTextureSize(visualQualityManager.DefaultRenderSize);
        }

        // 0x06001897 (0x79E020): size the texture for cutscenes.
        private void InitialiseRenderTexture()
        {
            VisualQualityManager_SDT visualQualityManager = ProcessManager.GetSystemSafe<VisualQualityManager_SDT>();
            if (visualQualityManager == null)
                return;
            SetRenderTextureSize(visualQualityManager.CutsceneRenderSize);
            m_initialised = true;
        }

        // 0x06001898 (0x79E240): recreate the texture at the truncated size.
        private void SetRenderTextureSize(Vector2 textureSize)
        {
            m_renderTexture.Release();
            m_renderTexture.width = (int)textureSize.x;
            m_renderTexture.height = (int)textureSize.y;
            m_renderTexture.Create();
        }
    }
}
