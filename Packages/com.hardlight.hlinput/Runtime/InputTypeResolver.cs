using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Shipped HLInput.Runtime02000034 is public sealed BeforeFieldInit with one static field and two static methods.
    // The ordinary C# compiler's implicit constructor has no shipped declaration/body supplier and receives zero original credit.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class InputTypeResolver
    {
        private static bool m_touchDevice;
        // Original0600012c reads the uninitialized false field; no inferred platform detection is added.
        public static InputType ResolveTouchInput() { return m_touchDevice ? InputType.Touch : InputType.Mouse; }
        // Original0600012d: Unsupported permits fallback; Mouse wins over joystick sign, other overrides return verbatim.
        public static InputType ResolveButtonInput(int joystickIndex, InputType inputTypeOverride, bool isMouse = false)
        {
            if (inputTypeOverride != InputType.Unsupported) return inputTypeOverride;
            return isMouse ? InputType.Mouse : joystickIndex >= 0 ? InputType.DefaultController : InputType.Keyboard;
        }
    }
}
