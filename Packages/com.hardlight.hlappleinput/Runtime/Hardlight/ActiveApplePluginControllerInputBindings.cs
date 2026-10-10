using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000006. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ActiveApplePluginControllerInputBindings : Hardlight.BaseActiveBindings<Hardlight.ApplePluginControllerInputBinding,Hardlight.ApplePluginControllerBindingData>
    {
        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<Hardlight.ApplePluginControllerInputBinding> m_iOSBindings;
        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<Hardlight.ApplePluginControllerInputBinding> m_tvOSBindings;
        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<Hardlight.ApplePluginControllerInputBinding> m_macOSBindings;
        public const System.String DefaultFileName = "ActiveApplePluginControllerInputBindings";

        // Original 0x06000009; complete ARM64 and x86-64 bodies retained.
        protected override void SetupBindings()
        {
            // Both shipping macOS architectures select the macOS list. The other authored
            // platform fields remain preserved; their unshipped selection branches are unproven.
            SetActiveBindings(m_macOSBindings);
        }

        // Original 0x0600000a; complete ARM64 and x86-64 bodies retained.
        public ActiveApplePluginControllerInputBindings()
        {
        }

    }
}
