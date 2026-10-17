using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLAppleInput.Runtime 0x02000008. Every method body follows the complete ARM64 and x86-64 originals.
// Apple service calls are preserved and remain unexecuted.
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ApplePluginControllerBinding
        : BaseBinding, IKeyProvider<GCControllerInputName>, IAxisProvider<GCControllerInputName>
    {
        [HashEnum(typeof(GCControllerInputName))]
        [SerializeField]
        private GCControllerInputName m_inputName;

        public GCControllerInputName Key
        {
            get { return m_inputName; }
        }

        public GCControllerInputName Axis
        {
            get { return m_inputName; }
        }

        public ApplePluginControllerBinding(List<InputModifier> modifiers) : base(modifiers)
        {
        }
    }
}
