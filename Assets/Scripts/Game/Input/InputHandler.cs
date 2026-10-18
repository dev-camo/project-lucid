using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020006a8; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class InputHandler : IInputButton
    {
        // 060023b1..060023b6 (1.10.1 RVA 0x58A210..0x58A4E0): compiler-generated accessors.
        public event ButtonHandler.OnHeld ButtonHandlers;
        public event ButtonHandler.OnDown ButtonDownHandlers;
        public event ButtonHandler.OnUp ButtonUpHandlers;

        private bool m_registered;
        private readonly SystemRef<InputSystem> m_inputSystemRef = ProcessManager.GetSystemRef<InputSystem>();

        // 060023b7 (1.10.1 RVA 0x58A570): stays unregistered while no InputSystem is available.
        public void Register()
        {
            if (m_registered) return;
            if (!m_inputSystemRef.TryGet(out InputSystem inputSystem)) return;
            inputSystem.ControlMapping.RegisterButtonProvider(this);
            m_registered = true;
        }

        // 060023b8 (1.10.1 RVA 0x58A5F0)
        public void Unregister()
        {
            if (!m_registered) return;
            if (!m_inputSystemRef.TryGet(out InputSystem inputSystem)) return;
            inputSystem.ControlMapping.UnregisterButtonProvider(this);
            m_registered = false;
        }

        // 060023b9 (1.10.1 RVA 0x58A1E0): handlers always receive joystick index 0.
        public void OnButtonHeld(GameInput gameInput, float value, InputType inputType)
        {
            if (m_registered) ButtonHandlers?.Invoke(0, gameInput, value, inputType);
        }

        // 060023ba (1.10.1 RVA 0x58A660)
        public void OnButtonDown(GameInput gameInput, float value, InputType inputType)
        {
            if (m_registered) ButtonDownHandlers?.Invoke(0, gameInput, value, inputType);
        }

        // 060023bb (1.10.1 RVA 0x58A690)
        public void OnButtonUp(GameInput gameInput, float value, InputType inputType)
        {
            if (m_registered) ButtonUpHandlers?.Invoke(0, gameInput, value, inputType);
        }

        // 060023bc (1.10.1 RVA 0x589FA0): only the SystemRef initializer precedes the Object constructor.
        protected InputHandler()
        {
        }
    }
}
