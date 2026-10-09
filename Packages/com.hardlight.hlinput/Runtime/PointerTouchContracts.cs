using UnityEngine;

namespace Hardlight
{
    // HLInput.Runtime 0x02000039: both declarations are genuinely abstract.
    public interface IInputPointerSource
    {
        Vector3 GetPosition(); // 0x06000137
        float GetScrollWheelDelta(); // 0x06000138
    }

    // HLInput.Runtime 0x0200003a: three genuine abstract declarations.
    public interface IInputTouchSource
    {
        int GetTouchCount(); // 0x06000139
        Touch GetTouch(int touchIndex); // 0x0600013a
        Vector3 GetTilt(); // 0x0600013b
    }
}
