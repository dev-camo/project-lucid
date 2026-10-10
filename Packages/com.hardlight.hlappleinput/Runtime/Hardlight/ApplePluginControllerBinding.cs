using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000008. Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    public class ApplePluginControllerBinding : Hardlight.BaseBinding, Hardlight.IKeyProvider<Apple.GameController.Controller.GCControllerInputName>, Hardlight.IAxisProvider<Apple.GameController.Controller.GCControllerInputName>
    {
        [Hardlight.Utils.HashEnum(typeof(Apple.GameController.Controller.GCControllerInputName))]
        [UnityEngine.SerializeField]
        private Apple.GameController.Controller.GCControllerInputName m_inputName;

        // Original 0x06000010; complete ARM64 and x86-64 bodies retained.
        public Apple.GameController.Controller.GCControllerInputName Key
        {
            get { return m_inputName; }
        }

        // Original 0x06000011; complete ARM64 and x86-64 bodies retained.
        public Apple.GameController.Controller.GCControllerInputName Axis
        {
            get { return m_inputName; }
        }

        // Original 0x06000012; complete ARM64 and x86-64 bodies retained.
        public ApplePluginControllerBinding(System.Collections.Generic.List<Hardlight.InputModifier> modifiers) : base(modifiers)
        {
        }

    }
}
