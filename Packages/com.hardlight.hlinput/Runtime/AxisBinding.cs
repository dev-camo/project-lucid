using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class AxisBinding : BaseBinding, IAxisProvider<string>
    {
        [SerializeField] private string m_axis;
        // HLInput.Runtime 0600003f: preserve the authored string reference, including null.
        public string Axis { get { return m_axis; } }
        // 06000040: genuine BaseBinding list initialization precedes the axis store.
        public AxisBinding(string axis, List<InputModifier> modifiers) : base(modifiers) { m_axis = axis; }
    }
}
