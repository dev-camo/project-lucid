using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000070, all six original native methods.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputTilt
    {
        // Original setter 0x06000228 and <Tilt>k__BackingField 0x0400015a.
        // C# requires a getter for an auto-property. This private compiler-only
        // getter is unbound, has zero original credit, and is a held shape gap.
        private Vector3 Tilt { get; set; }
        private IInputTouchSource m_inputTouchSource; // 0x0400015b

        // 0x06000229: no gyro query when the genuine support query is false.
        public void SetGyroEnabled(bool enabled)
        {
            if (SystemInfo.supportsGyroscope)
                Input.gyro.enabled = enabled;
        }

        public void Initialise(IInputTouchSource inputTouchSource) // 0x0600022a
        {
            m_inputTouchSource = inputTouchSource;
            SetGyroEnabled(false);
        }

        public void Shutdown() { m_inputTouchSource = null; } // 0x0600022b
        public void Update() { Tilt = m_inputTouchSource.GetTilt(); } // 0x0600022c
        public InputTilt() { } // 0x0600022d: System.Object only.
    }
}
