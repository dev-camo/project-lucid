using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SourceTouchScreen : IInputTouchSource
    {
        public int GetTouchCount() => Input.touchCount; // Original060002a4.
        public Touch GetTouch(int touchIndex) => Input.GetTouch(touchIndex); // Original060002a5, no bounds fallback.
        public Vector3 GetTilt() => Input.acceleration; // Original060002a6.
        public SourceTouchScreen() { } // Original060002a7.
    }
}
