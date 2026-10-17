using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000015.
namespace Hardlight
{
    public interface ISourceApplePluginControllerProvider
        : IBaseSourceProvider<
            IApplePluginControllerInputKeySource,
            GCControllerInputName,
            IApplePluginControllerInputAxisSource,
            GCControllerInputName>
    {
    }
}
