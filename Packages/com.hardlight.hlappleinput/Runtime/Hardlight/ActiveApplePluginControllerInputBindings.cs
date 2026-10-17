using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000006. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActiveApplePluginControllerInputBindings
        : BaseActiveBindings<ApplePluginControllerInputBinding, ApplePluginControllerBindingData>
    {
        [SerializeField]
        private List<ApplePluginControllerInputBinding> m_iOSBindings;
        [SerializeField]
        private List<ApplePluginControllerInputBinding> m_tvOSBindings;
        [SerializeField]
        private List<ApplePluginControllerInputBinding> m_macOSBindings;
        public const string DefaultFileName = "ActiveApplePluginControllerInputBindings";

        protected override void SetupBindings()
        {
            // Both shipping macOS architectures select the macOS list. The other authored
            // platform fields remain preserved; their unshipped selection branches are unproven.
            SetActiveBindings(m_macOSBindings);
        }

        public ActiveApplePluginControllerInputBindings()
        {
        }
    }
}
