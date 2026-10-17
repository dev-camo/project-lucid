using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000019.
namespace Hardlight
{
    public interface IApplePluginControllerInputKeySource
        : IBaseInputKeySource<GCControllerInputName>, IBaseInputGlyphSource<GCControllerInputName>
    {
    }
}
