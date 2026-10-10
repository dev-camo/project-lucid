using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000012. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [UnityEngine.CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create ApplePluginControllerGlyphMap")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerGlyphMap : Hardlight.BaseGlyphMap<Apple.GameController.Controller.GCControllerInputName>
    {
        // Original 0x06000033; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerGlyphMap()
        {
        }

    }
}
