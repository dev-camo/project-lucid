using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000017. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    public interface IApplePluginControllerGlyphSource : IBaseInputGlyphSource<GCControllerInputName>
    {
        void SetGlyphRenderSettings(GCControllerSymbolScale symbolScale, GCControllerRenderingMode renderingMode,
            float pointSize, GCControllerSymbolWeight symbolWeight, bool useFillStyle);
    }
}
