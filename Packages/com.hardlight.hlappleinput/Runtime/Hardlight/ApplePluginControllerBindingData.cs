using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000007. Every method body follows the complete ARM64 and x86-64 originals.
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerBindingData
        : BaseBindingData,
          IButtonBindingProvider<ApplePluginControllerBinding>,
          IBaseBindingDataProvider,
          IAxisBindingProvider<ApplePluginControllerBinding>
    {
        [SerializeField]
        private List<ApplePluginControllerBinding> m_buttonBindings;
        [SerializeField]
        private List<ApplePluginControllerBinding> m_axisBindings;

        // Original 0x0600000b
        public IReadOnlyList<ApplePluginControllerBinding> ButtonBindings
        {
            get { return m_buttonBindings; }
        }

        // Original 0x0600000c
        public IReadOnlyList<ApplePluginControllerBinding> AxisBindings
        {
            get { return m_axisBindings; }
        }

        // Original 0x0600000d
        public override void SaveModifierNames()
        {
            InternalSaveModifierNames(m_buttonBindings);
            InternalSaveModifierNames(m_axisBindings);
            InternalSaveModifierNames(m_vectorisedBindings);
        }

        // Original 0x0600000e
        public override void LoadModifiersByName()
        {
            InternalLoadModifiersByName(m_buttonBindings);
            InternalLoadModifiersByName(m_axisBindings);
            InternalLoadModifiersByName(m_vectorisedBindings);
        }

        // Original 0x0600000f
        public ApplePluginControllerBindingData()
        {
        }
    }
}
