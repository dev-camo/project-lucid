using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x0200000b. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [UnityEngine.CreateAssetMenu(menuName = "Hardlight/HLAppleInput/Input Bindings/Apple Plugin Controller Input Binding")]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    public class ApplePluginControllerInputBinding : Hardlight.BaseInputBinding<Hardlight.ApplePluginControllerBindingData>, Hardlight.IInputButtonProvider<Hardlight.ApplePluginControllerBinding>, Hardlight.IBaseInputBindingProvider, Hardlight.IInputAxisProvider<Hardlight.ApplePluginControllerBinding>
    {
        // Original 0x0600001b; complete ARM64 and x86-64 bodies retained.
        public System.Collections.Generic.IReadOnlyList<Hardlight.IButtonBindingProvider<Hardlight.ApplePluginControllerBinding>> ButtonProviders
        {
            get { return BindingData; }
        }

        // Original 0x0600001c; complete ARM64 and x86-64 bodies retained.
        public System.Collections.Generic.IReadOnlyList<Hardlight.IAxisBindingProvider<Hardlight.ApplePluginControllerBinding>> AxisProviders
        {
            get { return BindingData; }
        }

        // Original 0x0600001d; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerInputBinding()
        {
        }

    }
}
