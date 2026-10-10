using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000015. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    public interface ISourceApplePluginControllerProvider : Hardlight.IBaseSourceProvider<Hardlight.IApplePluginControllerInputKeySource, Apple.GameController.Controller.GCControllerInputName, Hardlight.IApplePluginControllerInputAxisSource, Apple.GameController.Controller.GCControllerInputName>
    {
    }
}
