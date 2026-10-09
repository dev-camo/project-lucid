namespace Hardlight
{
    // Original 0200005a is genuinely static; its three nested delegate types have only ctor/Invoke runtime APIs.
    // C# adds BeginInvoke/EndInvoke wrappers: these are unbound compiler context, never original body credit.
    public static class ButtonHandler
    {
        public delegate void OnHeld(int joystickIndex, GameInput gameInput, float value, InputType inputType); // 060001dd/de
        public delegate void OnDown(int joystickIndex, GameInput gameInput, float value, InputType inputType); // 060001df/e0
        public delegate void OnUp(int joystickIndex, GameInput gameInput, float value, InputType inputType); // 060001e1/e2
    }

    public interface IInputButton // 02000062: six actual abstract accessor declarations.
    {
        event ButtonHandler.OnHeld ButtonHandlers; // 060001e9/ea
        event ButtonHandler.OnDown ButtonDownHandlers; // 060001eb/ec
        event ButtonHandler.OnUp ButtonUpHandlers; // 060001ed/ee
    }

    public interface IBaseInputKeySource<TKey> // 02000093: three actual abstract methods.
    {
        bool GetKey(TKey keyCode, int joystickIndex); // 06000296
        bool GetKeyDown(TKey keyCode, int joystickIndex); // 06000297
        bool GetKeyUp(TKey keyCode, int joystickIndex); // 06000298
    }
}
