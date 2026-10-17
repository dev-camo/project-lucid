using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000012. Every method body follows the complete ARM64 and x86-64 originals.
namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/GameInputGlyphMaps/Create ApplePluginControllerGlyphMap")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerGlyphMap : BaseGlyphMap<GCControllerInputName>
    {
        // Original 0x06000033
        public ApplePluginControllerGlyphMap()
        {
        }
    }
}
