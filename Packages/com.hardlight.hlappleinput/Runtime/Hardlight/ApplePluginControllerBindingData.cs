using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000007. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerBindingData : Hardlight.BaseBindingData, Hardlight.IButtonBindingProvider<Hardlight.ApplePluginControllerBinding>, Hardlight.IBaseBindingDataProvider, Hardlight.IAxisBindingProvider<Hardlight.ApplePluginControllerBinding>
    {
        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<Hardlight.ApplePluginControllerBinding> m_buttonBindings;
        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<Hardlight.ApplePluginControllerBinding> m_axisBindings;

        // Original 0x0600000b; complete ARM64 and x86-64 bodies retained.
        public System.Collections.Generic.IReadOnlyList<Hardlight.ApplePluginControllerBinding> ButtonBindings
        {
            get { return m_buttonBindings; }
        }

        // Original 0x0600000c; complete ARM64 and x86-64 bodies retained.
        public System.Collections.Generic.IReadOnlyList<Hardlight.ApplePluginControllerBinding> AxisBindings
        {
            get { return m_axisBindings; }
        }

        // Original 0x0600000d; complete ARM64 and x86-64 bodies retained.
        public override void SaveModifierNames()
        {
            InternalSaveModifierNames(m_buttonBindings);
            InternalSaveModifierNames(m_axisBindings);
            InternalSaveModifierNames(m_vectorisedBindings);
        }

        // Original 0x0600000e; complete ARM64 and x86-64 bodies retained.
        public override void LoadModifiersByName()
        {
            InternalLoadModifiersByName(m_buttonBindings);
            InternalLoadModifiersByName(m_axisBindings);
            InternalLoadModifiersByName(m_vectorisedBindings);
        }

        // Original 0x0600000f; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerBindingData()
        {
        }

    }
}
