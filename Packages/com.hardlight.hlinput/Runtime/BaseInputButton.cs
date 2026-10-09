using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class BaseInputButton<TKey, TButtonBinding> : IInputButton
        where TButtonBinding : BaseBinding, IKeyProvider<TKey>
    {
        // HLInput.Runtime 02000056: all five original fields, thirteen native methods and one actual abstract API.
        // 060001b7..bc: field-like events reproduce the original Delegate.Combine/Remove compare-exchange loops.
        public event ButtonHandler.OnHeld ButtonHandlers;
        public event ButtonHandler.OnDown ButtonDownHandlers;
        public event ButtonHandler.OnUp ButtonUpHandlers;
        private IBaseInputKeySource<TKey> m_inputKeySource;
        private IReadOnlyList<IInputButtonProvider<TButtonBinding>> m_inputButtonProviders;

        public virtual void Initialise(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<TKey, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IBaseInputKeySource<TKey> inputKeySource,
            IReadOnlyList<IInputButtonProvider<TButtonBinding>> inputButtonProviders)
        {
            m_inputKeySource = inputKeySource; // 060001bd: source store precedes providers and glyph population.
            m_inputButtonProviders = inputButtonProviders;
            PopulateToGlyphMap(ref gameInputGlyphMap, getGlyphForKeyAndInputType, m_inputButtonProviders);
        }

        public void Shutdown()
        {
            m_inputKeySource = null; // 060001be: original callbacks remain installed.
            m_inputButtonProviders = null;
        }

        public void Update()
        {
            for (int i = 0; i < m_inputButtonProviders.Count; i++) // 060001bf: live field and Count reads.
            {
                IInputButtonProvider<TButtonBinding> provider = m_inputButtonProviders[i];
                int joystickIndex = provider.JoystickIndex;
                InputType inputTypeOverride = provider.InputTypeOverride;
                IReadOnlyList<IButtonBindingProvider<TButtonBinding>> buttonProviders = provider.ButtonProviders;
                for (int j = 0; j < buttonProviders.Count; j++)
                {
                    IButtonBindingProvider<TButtonBinding> buttonProvider = buttonProviders[j];
                    GameInput gameInput = buttonProvider.GameInput;
                    IReadOnlyList<TButtonBinding> bindings = buttonProvider.ButtonBindings;
                    for (int k = 0; k < bindings.Count; k++)
                    {
                        TButtonBinding binding = bindings[k];
                        UpdateButtonBinding(gameInput, binding, m_inputKeySource, inputTypeOverride, joystickIndex, binding.Modifiers);
                    }
                }
            }
        }

        protected abstract void UpdateButtonBinding(GameInput gameInput, IKeyProvider<TKey> keyProvider,
            IBaseInputKeySource<TKey> inputKeySource, InputType inputTypeOverride, int joystickIndex,
            IReadOnlyList<InputModifier> modifiers); // 060001c0: actual abstract declaration, no original native body.

        protected void ProcessKey(GameInput gameInput, TKey key, IBaseInputKeySource<TKey> inputKeySource,
            InputType inputType, int joystickIndex, IReadOnlyList<InputModifier> modifiers)
        {
            // 060001c1: each key query runs regardless of handler presence; modifiers run only after that handler check.
            if (inputKeySource.GetKeyDown(key, joystickIndex) && ButtonDownHandlers != null)
            {
                float value = 1f;
                ApplyModifiers(modifiers, ref value, gameInput);
                if (!MathUtilities.WithinTolerance(value, 0f, 0.0001f))
                    ButtonDownHandlers(joystickIndex, gameInput, value, inputType);
            }
            if (inputKeySource.GetKey(key, joystickIndex) && ButtonHandlers != null)
            {
                float value = 1f;
                ApplyModifiers(modifiers, ref value, gameInput);
                if (!MathUtilities.WithinTolerance(value, 0f, 0.0001f))
                    ButtonHandlers(joystickIndex, gameInput, value, inputType);
            }
            if (inputKeySource.GetKeyUp(key, joystickIndex) && ButtonUpHandlers != null)
                ButtonUpHandlers(joystickIndex, gameInput, 0f, inputType);
        }

        private void ApplyModifiers(IReadOnlyList<InputModifier> modifiers, ref float value, GameInput gameInput)
        {
            for (int i = 0; i < modifiers.Count; i++) // 060001c2: no added Unity-null guard, scaled delta time.
            {
                InputModifier modifier = modifiers[i];
                value = modifier.Modify(value, Time.deltaTime, gameInput);
            }
        }

        private void PopulateToGlyphMap(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<TKey, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IReadOnlyList<IInputButtonProvider<TButtonBinding>> inputButtonProviders)
        {
            // 060001c3: callbacks precede dictionary queries; preserve repeated ref-map/index reads and null/duplicate glyphs.
            Dictionary<InputType, List<Texture2D>> inputTypeGlyphs = null;
            List<Texture2D> glyphs = null;
            for (int i = 0; i < inputButtonProviders.Count; i++)
            {
                IInputButtonProvider<TButtonBinding> provider = inputButtonProviders[i];
                InputType inputType = provider.BindingInputType;
                int joystickIndex = provider.JoystickIndex;
                IReadOnlyList<IButtonBindingProvider<TButtonBinding>> buttonProviders = provider.ButtonProviders;
                for (int j = 0; j < buttonProviders.Count; j++)
                {
                    IButtonBindingProvider<TButtonBinding> buttonProvider = buttonProviders[j];
                    GameInput gameInput = buttonProvider.GameInput;
                    IReadOnlyList<TButtonBinding> bindings = buttonProvider.ButtonBindings;
                    for (int k = 0; k < bindings.Count; k++)
                    {
                        TButtonBinding binding = bindings[k];
                        Texture2D glyph = getGlyphForKeyAndInputType(binding.Key, inputType, joystickIndex);
                        if (!gameInputGlyphMap.TryGetValue(gameInput, out inputTypeGlyphs))
                        {
                            inputTypeGlyphs = new Dictionary<InputType, List<Texture2D>>(HardlightInputEnumComparers.InputTypeComparer);
                            inputTypeGlyphs.Add(inputType, new List<Texture2D>());
                            gameInputGlyphMap.Add(gameInput, inputTypeGlyphs);
                        }
                        if (!gameInputGlyphMap[gameInput].TryGetValue(inputType, out glyphs))
                        {
                            glyphs = new List<Texture2D>();
                            gameInputGlyphMap[gameInput].Add(inputType, glyphs);
                        }
                        glyphs.Add(glyph);
                    }
                }
            }
        }

        protected BaseInputButton() { } // 060001c4: genuine Object-only constructor; no provider/list allocation.
    }
}
