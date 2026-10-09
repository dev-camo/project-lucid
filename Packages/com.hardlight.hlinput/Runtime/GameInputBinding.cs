using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Input Bindings/Game Input Binding")]
    public class GameInputBinding : BaseInputBinding<BindingData>, IInputButtonProvider<ButtonBinding>, IBaseInputBindingProvider, IInputAxisProvider<AxisBinding>
    {
        // HLInput.Runtime 06000118/119: covariant read-only view, with the original exact list/null reference.
        public IReadOnlyList<IButtonBindingProvider<ButtonBinding>> ButtonProviders { get { return BindingData; } }
        public IReadOnlyList<IAxisBindingProvider<AxisBinding>> AxisProviders { get { return BindingData; } }
        public void RemapGameInput(GameInput input, KeyCode newKeyCode)
        {
            // 0600011a: genuine read-only Find predicate 06000120; clear all three before replacing the button list.
            BindingData bindingData = BindingData.Find(data => data.GameInput == input);
            ClearPreviouslyMappedInput(bindingData);
            bindingData.SetButtonBindings(new List<ButtonBinding> { new ButtonBinding(newKeyCode, new List<InputModifier>()) });
        }
        public void RemapGameInput(GameInput input, string newAxis, List<InputModifier> newModifiers)
        {
            // 0600011b: genuine predicate 06000122; modifiers remain the caller's exact reference, including null.
            BindingData bindingData = BindingData.Find(data => data.GameInput == input);
            ClearPreviouslyMappedInput(bindingData);
            bindingData.SetAxisBindings(new List<AxisBinding> { new AxisBinding(newAxis, newModifiers) });
        }
        private static void ClearPreviouslyMappedInput(BindingData bindingData)
        {
            bindingData.ClearButtonBindings(); // 0600011c: preserve button/axis/swipe clear order and original faults.
            bindingData.ClearAxisBindings();
            bindingData.ClearSwipeBindings();
        }
        private Dictionary<GameInput, IReadOnlyList<SwipeBinding>> CreateGameInputToSwipeLookup()
        {
            // 0600011d: genuine shipped comparer, live source list, Add preserves duplicate-key faults.
            var lookup = new Dictionary<GameInput, IReadOnlyList<SwipeBinding>>(HardlightEnumComparers.GameInputComparer);
            for (int i = 0; i < BindingData.Count; i++)
            {
                BindingData bindingData = BindingData[i];
                lookup.Add(bindingData.GameInput, bindingData.SwipeBindings);
            }
            return lookup;
        }
        public GameInputBinding() { } // 0600011e: genuine closed base constructor only.
        // Compiler closure constructors 0600011f/121 are Object-only; their exact emitted names/tokens remain unbound.
    }
}
