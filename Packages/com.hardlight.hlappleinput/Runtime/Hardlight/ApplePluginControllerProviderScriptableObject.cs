using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000013. Every method body follows the complete ARM64 and x86-64 originals.
namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLAppleInput/ControllerProviders/Create ApplePluginControllerProvider")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplePluginControllerProviderScriptableObject : ScriptableControllerProvider
    {
        [SerializeField]
        private bool m_useInOSXStandaloneEditor;

        // Original 0x06000034
        public override BaseControllerProvider GetControllerNameProvider(float controllerConnectionPollingRateInSeconds)
        {
            return new ApplePluginControllerProvider(controllerConnectionPollingRateInSeconds);
        }

        // Original 0x06000035
        public override bool IsActiveProvider()
        {
            // Both shipping macOS slices return true without reading the authored Editor flag.
            return true;
        }

        // Original 0x06000036
        public ApplePluginControllerProviderScriptableObject()
        {
        }
    }
}
