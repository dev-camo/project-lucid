using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000976; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class OnScreenInputButton : MonoBehaviour, IInputButton
    {
        // 06003691..06003696 (1.10.1 RVA 0x6918D0..0x691BA0): compiler-generated accessors.
        public event ButtonHandler.OnHeld ButtonHandlers;
        public event ButtonHandler.OnDown ButtonDownHandlers;
        public event ButtonHandler.OnUp ButtonUpHandlers;

        // 06003697..06003699 (RVA 0x68C3B0, 0x68C830, 0x68C460): every raise uses joystick index 0.
        protected void OnButtonDown(GameInput gameInput, float value, InputType inputType)
        {
            ButtonDownHandlers?.Invoke(0, gameInput, value, inputType);
        }

        protected void OnButtonHeld(GameInput gameInput, float value, InputType inputType)
        {
            ButtonHandlers?.Invoke(0, gameInput, value, inputType);
        }

        protected void OnButtonUp(GameInput gameInput, float value, InputType inputType)
        {
            ButtonUpHandlers?.Invoke(0, gameInput, value, inputType);
        }

        // 0600369a (RVA 0x68C540)
        protected void SupplyGameInputSequence(GameInput gameInput, InputType inputType)
        {
            StartCoroutine(SupplyGameInputCoroutine(gameInput, inputType));
        }

        // 0600369b (RVA 0x691C30); MoveNext 0600369f (0x691CE0). Down, held and up are each reported
        // with value 1 (constant 1.0f at 0x29A6BE0), one frame apart.
        private IEnumerator SupplyGameInputCoroutine(GameInput gameInput, InputType inputType)
        {
            ButtonDownHandlers?.Invoke(0, gameInput, 1f, inputType);
            yield return null;
            ButtonHandlers?.Invoke(0, gameInput, 1f, inputType);
            yield return null;
            ButtonUpHandlers?.Invoke(0, gameInput, 1f, inputType);
        }

        // 0600369c (RVA 0x68C720)
        protected OnScreenInputButton()
        {
        }
    }
}
