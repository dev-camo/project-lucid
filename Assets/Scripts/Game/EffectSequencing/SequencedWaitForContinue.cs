using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200062c; native 0x56B1C0.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedWaitForContinue : SequencedEffect
    {
        [Tooltip("If true, if DoContinue is called before the effect is triggered, this will be saved and the effect will resolve immediately when triggered.")]
        [SerializeField]
        private bool m_allowContinueBeforeTriggered;
        [Tooltip("If true, the effect will only cause a wait the first time it is triggered.")]
        [SerializeField]
        private bool m_onlyTriggerOnce;
        private bool m_triggered;
        private bool m_continued;

        // Original06002105: a stored early continue resolves the wait now; otherwise the effect stays open.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (!m_triggered || !m_onlyTriggerOnce)
            {
                m_triggered = true;
                if (!m_continued || !m_allowContinueBeforeTriggered)
                {
                    m_continued = false;
                    return;
                }
                if (!m_onlyTriggerOnce) m_continued = false;
            }
            m_onComplete();
        }

        // Original06002106: only the first continue counts until the next trigger; an early one is stored when allowed.
        public void DoContinue()
        {
            if (m_continued) return;
            if (!m_triggered)
            {
                if (m_allowContinueBeforeTriggered) m_continued = true;
                return;
            }
            m_continued = true;
            if (!m_onlyTriggerOnce) m_triggered = false;
            m_onComplete?.Invoke();
        }

        // Implicit original06002107.
    }
}
