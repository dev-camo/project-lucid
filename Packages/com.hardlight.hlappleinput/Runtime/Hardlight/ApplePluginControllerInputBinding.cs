using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000b. Every method body follows the complete ARM64 and x86-64 originals.
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLAppleInput/Input Bindings/Apple Plugin Controller Input Binding")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputBinding
        : BaseInputBinding<ApplePluginControllerBindingData>,
          IInputButtonProvider<ApplePluginControllerBinding>,
          IBaseInputBindingProvider,
          IInputAxisProvider<ApplePluginControllerBinding>
    {
        // Original 0x0600001b
        public IReadOnlyList<IButtonBindingProvider<ApplePluginControllerBinding>> ButtonProviders
        {
            get { return BindingData; }
        }

        // Original 0x0600001c
        public IReadOnlyList<IAxisBindingProvider<ApplePluginControllerBinding>> AxisProviders
        {
            get { return BindingData; }
        }

        // Original 0x0600001d
        public ApplePluginControllerInputBinding()
        {
        }
    }
}
