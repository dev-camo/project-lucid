using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000017.
namespace Hardlight
{
    public interface IApplePluginControllerGlyphSource : IBaseInputGlyphSource<GCControllerInputName>
    {
        // Original 0x06000046
        void SetGlyphRenderSettings(GCControllerSymbolScale symbolScale, GCControllerRenderingMode renderingMode,
            float pointSize, GCControllerSymbolWeight symbolWeight, bool useFillStyle);
    }
}
