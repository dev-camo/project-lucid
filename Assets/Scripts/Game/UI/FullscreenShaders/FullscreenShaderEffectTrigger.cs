using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200076c: starts one fullscreen shader override
    // from authored settings and stops it on request or on destruction.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FullscreenShaderEffectTrigger : MonoBehaviour
    {
        [SerializeField]
        private FullscreenShaderParametersType m_type;
        [SerializeField]
        [Tooltip("If ticked, effect will continue for an unlimited time until the StopEffect function is called.")]
        private bool m_untilStopTriggered;
        [Tooltip("Duration of effect if not unlimited.")]
        [SerializeField]
        [Min(0)]
        private float m_duration = 1f;
        [Tooltip("If this shader parameter definition has normalised anim curves, apply them over the given duration.")]
        [SerializeField]
        private bool m_applyCurveOverDuration;
        [Tooltip("If ticked, effect will continue to its natural end even if this object that triggered it is destroyed.")]
        [SerializeField]
        private bool m_persistAfterTriggerDestroyed;
        private bool m_playing;
        private FullscreenShaderManager m_fullscreenShaderManager;
        private FullscreenShaderManager.ParametersHandle m_parametersHandle;

        private void Awake()
        {
            m_fullscreenShaderManager = ProcessManager.GetSystem<FullscreenShaderManager>();
        }

        private void OnDestroy()
        {
            if (m_playing && !m_persistAfterTriggerDestroyed)
            {
                m_fullscreenShaderManager.StopEffectOverride(m_parametersHandle);
                m_playing = false;
            }
            m_fullscreenShaderManager = null;
        }

        // The override's result is ignored: the trigger counts as playing even
        // when the manager has no definition for m_type.
        public void StartEffect()
        {
            if (m_playing) return;
            m_fullscreenShaderManager.StartEffectOverride(m_type, out m_parametersHandle, m_duration,
                m_applyCurveOverDuration, m_untilStopTriggered, () => m_playing = false);
            m_playing = true;
        }

        public void StopEffect()
        {
            if (!m_playing) return;
            m_fullscreenShaderManager.StopEffectOverride(m_parametersHandle);
            m_playing = false;
        }

        // Only an exact zero (either sign) means "until stopped".
        public void SetDuration(float duration)
        {
            m_untilStopTriggered = duration == 0f;
            m_duration = duration;
        }

        public void SetFullscreenShaderParametersType(FullscreenShaderParametersType type)
        {
            m_type = type;
        }

        // Retail keeps only the CanValidate call, and its result is unused.
        private void OnValidate()
        {
            gameObject.CanValidate();
        }
    }
}
