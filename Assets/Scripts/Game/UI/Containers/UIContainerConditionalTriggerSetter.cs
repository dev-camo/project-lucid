using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000990; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerConditionalTriggerSetter : UIContainerValueSetter
    {
        [SerializeField]
        private ConditionalTriggerEvent m_conditionalTrigger;
        private int m_progressCurrent;
        private int m_progressTotal;

        // 06003746 (1.10.1 RVA 0x696AC0)
        private void OnEnable()
        {
            m_progressCurrent = 0;
            m_progressTotal = m_conditionalTrigger.GetConditionsCount();
            UIContainer?.SetEnabled(true);
        }

        // 06003747 (1.10.1 RVA 0x696C20): the shown count never goes back down.
        private void Update()
        {
            m_progressCurrent = Math.Max(m_progressCurrent, m_conditionalTrigger.GetConditionsPassedCount());
            UIContainer?.SetProgress(m_progressCurrent, m_progressTotal);
        }

        // 06003748 (1.10.1 RVA 0x696D40)
        private void OnDisable()
        {
            UIContainer?.SetEnabled(false);
        }

        // 06003749 (1.10.1 RVA 0x696E20)
        public UIContainerConditionalTriggerSetter()
        {
        }
    }
}
