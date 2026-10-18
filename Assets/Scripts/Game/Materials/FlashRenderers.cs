using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a05: after the initial delay, inverts every
    // renderer's enabled state once per flash interval.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FlashRenderers : TimeScaledComponent_SDT
    {
        [SerializeField]
        private Renderer[] m_renderers;
        [SerializeField]
        private float m_initialDelay;
        [SerializeField]
        private float m_flashInterval;
        private float m_timerSeconds;
        private float m_nextFlashIntervalSeconds;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_timerSeconds = 0f;
            m_nextFlashIntervalSeconds = m_flashInterval + m_initialDelay;
        }

        // Only "still waiting" comparisons return early, so a NaN timer flashes.
        protected override void InternalUpdate(float deltaTime)
        {
            m_timerSeconds += deltaTime;
            if (m_timerSeconds < m_initialDelay || m_timerSeconds < m_nextFlashIntervalSeconds) return;
            foreach (Renderer renderer in m_renderers)
                renderer.enabled = !renderer.enabled;
            m_nextFlashIntervalSeconds += m_flashInterval;
        }

        public void Action_EnableAllRenderers()
        {
            foreach (Renderer renderer in m_renderers)
                renderer.enabled = true;
        }
    }
}
