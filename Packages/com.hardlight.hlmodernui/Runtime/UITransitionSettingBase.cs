using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 02000035, its one field, getter and protected base-only constructor.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UITransitionSettingBase
    {
        [SerializeField] private UITransitionState m_state;
        public UITransitionState State => m_state;
        protected UITransitionSettingBase() { }
    }
}
