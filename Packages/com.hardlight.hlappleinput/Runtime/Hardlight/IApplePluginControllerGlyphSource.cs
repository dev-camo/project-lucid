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
    public interface IApplePluginControllerGlyphSource : Hardlight.IBaseInputGlyphSource<Apple.GameController.Controller.GCControllerInputName>
    {
        // Original 0x06000046; genuine abstract declaration.
        void SetGlyphRenderSettings(Apple.GameController.Controller.GCControllerSymbolScale symbolScale, Apple.GameController.Controller.GCControllerRenderingMode renderingMode, System.Single pointSize, Apple.GameController.Controller.GCControllerSymbolWeight symbolWeight, System.Boolean useFillStyle);

    }
}
