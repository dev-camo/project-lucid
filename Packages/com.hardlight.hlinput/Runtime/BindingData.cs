using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BindingData : BaseBindingData, IButtonBindingProvider<ButtonBinding>, IBaseBindingDataProvider, IAxisBindingProvider<AxisBinding>
    {
        [SerializeField] private List<ButtonBinding> m_buttonBindings;
        [SerializeField] private List<AxisBinding> m_axisBindings;
        [SerializeField] private List<SwipeBinding> m_swipeBindings;
        [SerializeField] private FixedBindings m_fixedBindings = new FixedBindings();

        // HLInput.Runtime 0600002e..31: expose exact authored references, including null.
        public IReadOnlyList<ButtonBinding> ButtonBindings { get { return m_buttonBindings; } }
        public IReadOnlyList<SwipeBinding> SwipeBindings { get { return m_swipeBindings; } }
        public IReadOnlyList<AxisBinding> AxisBindings { get { return m_axisBindings; } }
        public FixedBindings FixedBindings { get { return m_fixedBindings; } }
        public void SetButtonBindings(List<ButtonBinding> newButtonBindings) { m_buttonBindings = newButtonBindings; } // 06000032
        public void SetAxisBindings(List<AxisBinding> newAxisBindings) { m_axisBindings = newAxisBindings; } // 06000033
        public void ClearButtonBindings() { m_buttonBindings.Clear(); } // 06000034
        public void ClearAxisBindings() { m_axisBindings.Clear(); } // 06000035
        public void ClearSwipeBindings() { m_swipeBindings.Clear(); } // 06000036
        public override void SaveModifierNames()
        {
            // 06000037: button, axis, swipe, vectorised, fixed; no null guards.
            InternalSaveModifierNames(m_buttonBindings);
            InternalSaveModifierNames(m_axisBindings);
            InternalSaveModifierNames(m_swipeBindings);
            InternalSaveModifierNames(m_vectorisedBindings);
            InternalSaveModifierName(m_fixedBindings);
        }
        public override void LoadModifiersByName()
        {
            // 06000038: each helper resolves the genuine shared cache in the same order.
            InternalLoadModifiersByName(m_buttonBindings);
            InternalLoadModifiersByName(m_axisBindings);
            InternalLoadModifiersByName(m_swipeBindings);
            InternalLoadModifiersByName(m_vectorisedBindings);
            InternalLoadModifierByName(m_fixedBindings);
        }
        public BindingData() { } // 06000039: only the fixed binding initializer precedes the base call.
    }
}
