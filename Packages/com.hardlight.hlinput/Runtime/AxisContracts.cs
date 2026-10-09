namespace Hardlight
{
    // Original 0200005e interface has zero remaining owner APIs plus its three genuine runtime delegate types.
    // Additional C# delegate BeginInvoke/EndInvoke wrappers are unbound compiler context with zero original credit.
    public interface IInputAxis
    {
        public delegate void OnAxisStartHandler(int joystickIndex, GameInput gameInput, float value, InputType inputType); // 060001e3/e4
        public delegate void OnAxisHandler(int joystickIndex, GameInput gameInput, float value, InputType inputType); // 060001e5/e6
        public delegate void OnAxisEndHandler(int joystickIndex, GameInput gameInput, float value, InputType inputType); // 060001e7/e8
    }

    public interface IBaseInputAxisSource<TAxis>
    {
        float GetAxis(TAxis axis, int joystickIndex); // 06000294: actual abstract original declaration.
    }
}
