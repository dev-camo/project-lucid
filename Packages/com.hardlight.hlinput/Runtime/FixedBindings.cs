using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FixedBindings : BaseBinding
    {
        public bool BindToPointer;
        public bool BindToScrollWheel;
        public bool BindToTap;
        public bool BindToDoubleTap;
        public bool BindToTouch;
        public bool BindToTouchRelease;
        public bool BindToMultiTouch;
        public bool BindToMultiTouchRelease;
        public bool BindToHold;
        public bool BindToHoldPressure;
        [HideInInspector] public bool Expand;

        // HLInput.Runtime 06000047: original ordered ten-way OR; Expand is inspector state only.
        public bool AnySet()
        {
            return BindToPointer || BindToScrollWheel || BindToTap || BindToDoubleTap || BindToTouch
                || BindToTouchRelease || BindToMultiTouch || BindToMultiTouchRelease || BindToHold || BindToHoldPressure;
        }
        // 06000048: the genuine base constructor receives null; all Boolean fields keep their zero defaults.
        public FixedBindings() : base(null) { }
    }
}
