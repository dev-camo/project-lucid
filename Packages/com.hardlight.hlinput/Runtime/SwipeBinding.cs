using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Complete original SwipeScreenZone is maintained in InputEnums.cs.
    // HLInput.Runtime 0x02000015: one declared method, three declared fields,
    // and NO original native constructor. The implicit C# constructor cannot
    // call the genuine BaseBinding(List<InputModifier>) constructor. This is
    // an explicit unresolved source-emission frontier, never base(null) credit.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public partial class SwipeBinding : BaseBinding
    {
        public InputSwipe.Direction Direction; // 0x0400002e
        public SwipeScreenZone ScreenZone; // 0x0400002f
        public Rect CustomActivationZone; // 0x04000030

        // 0x0600004b: None skips engine reads; every other zone samples width
        // then height, including horizontal-only zones. Custom uses raw Rect
        // x/y plus width/height with strict upper bounds, not normalized xMin.
        public bool ValidSwipePosition(Vector2 screenPoint)
        {
            if (ScreenZone == SwipeScreenZone.None) return true;
            int width = Screen.width;
            int height = Screen.height;
            float x = screenPoint.x / width;
            switch (ScreenZone)
            {
                case SwipeScreenZone.Left: return x <= 0.5f;
                case SwipeScreenZone.Right: return x > 0.5f;
                case SwipeScreenZone.Custom:
                    if (!(x >= CustomActivationZone.x) ||
                        !(x < CustomActivationZone.x + CustomActivationZone.width)) return false;
                    float y = screenPoint.y / height;
                    return y >= CustomActivationZone.y &&
                        y < CustomActivationZone.y + CustomActivationZone.height;
                default: return true;
            }
        }
    }
}
