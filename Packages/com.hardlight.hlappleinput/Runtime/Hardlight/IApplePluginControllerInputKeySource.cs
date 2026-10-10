using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000019. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    public interface IApplePluginControllerInputKeySource : Hardlight.IBaseInputKeySource<Apple.GameController.Controller.GCControllerInputName>, Hardlight.IBaseInputGlyphSource<Apple.GameController.Controller.GCControllerInputName>
    {
    }
}
