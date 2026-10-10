using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000013. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [UnityEngine.CreateAssetMenu(menuName = "Hardlight/HLAppleInput/ControllerProviders/Create ApplePluginControllerProvider")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class ApplePluginControllerProviderScriptableObject : Hardlight.ScriptableControllerProvider
    {
        [UnityEngine.SerializeField]
        private System.Boolean m_useInOSXStandaloneEditor;

        // Original 0x06000034; complete ARM64 and x86-64 bodies retained.
        public override Hardlight.BaseControllerProvider GetControllerNameProvider(System.Single controllerConnectionPollingRateInSeconds)
        {
            return new ApplePluginControllerProvider(controllerConnectionPollingRateInSeconds);
        }

        // Original 0x06000035; complete ARM64 and x86-64 bodies retained.
        public override System.Boolean IsActiveProvider()
        {
            // Both shipping macOS slices return true without reading the authored Editor flag.
            return true;
        }

        // Original 0x06000036; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerProviderScriptableObject()
        {
        }

    }
}
