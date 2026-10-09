using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class BaseInputAxis<TAxis, TAxisBinding> : IInputAxis
        where TAxisBinding : BaseBinding, IAxisProvider<TAxis>
    {
        // HLInput.Runtime 02000055: whole fourteen native methods plus actual abstract UpdateAxisBinding.
        // 060001a8..ad preserve original compare-exchange event operations. The stripped IInputAxis has zero owner APIs;
        // ordinary C# event-accessor FinalVirtual flags cannot match those remaining native flags without an invented interface.
        public event IInputAxis.OnAxisStartHandler AxisStartHandlers;
        public event IInputAxis.OnAxisHandler AxisHandlers;
        public event IInputAxis.OnAxisEndHandler AxisEndHandlers;
        private IBaseInputAxisSource<TAxis> m_inputAxisSource;
        private IReadOnlyList<IInputAxisProvider<TAxisBinding>> m_inputAxisProviders;
        private HashSet<string> m_trackingLookup = new HashSet<string>();

        public void Initialise(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<TAxis, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IBaseInputAxisSource<TAxis> inputTouchSource,
            IReadOnlyList<IInputAxisProvider<TAxisBinding>> inputAxisProviders)
        {
            m_inputAxisSource = inputTouchSource; // 060001ae: original parameter name retained.
            m_inputAxisProviders = inputAxisProviders;
            PopulateToGlyphMap(ref gameInputGlyphMap, getGlyphForKeyAndInputType, m_inputAxisProviders);
        }

        public void Shutdown()
        {
            m_inputAxisSource = null; // 060001af: tracking and callbacks remain live across shutdown.
            m_inputAxisProviders = null;
        }

        public void Update()
        {
            for (int i = 0; i < m_inputAxisProviders.Count; i++) // 060001b0: original live field/list loop.
            {
                IInputAxisProvider<TAxisBinding> provider = m_inputAxisProviders[i];
                int joystickIndex = provider.JoystickIndex;
                InputType inputTypeOverride = provider.InputTypeOverride;
                InputType inputType = InputTypeResolver.ResolveButtonInput(joystickIndex, inputTypeOverride, false);
                IReadOnlyList<IAxisBindingProvider<TAxisBinding>> axisProviders = provider.AxisProviders;
                for (int j = 0; j < axisProviders.Count; j++)
                {
                    IAxisBindingProvider<TAxisBinding> axisProvider = axisProviders[j];
                    GameInput gameInput = axisProvider.GameInput;
                    IReadOnlyList<TAxisBinding> bindings = axisProvider.AxisBindings;
                    for (int k = 0; k < bindings.Count; k++)
                    {
                        TAxisBinding binding = bindings[k];
                        UpdateAxisBinding(gameInput, binding, m_inputAxisSource, inputType, joystickIndex, binding.Modifiers);
                    }
                }
            }
        }

        protected abstract void UpdateAxisBinding(GameInput gameInput, IAxisProvider<TAxis> axisProvider,
            IBaseInputAxisSource<TAxis> inputAxisSource, InputType inputType, int joystickIndex,
            IReadOnlyList<InputModifier> modifiers); // 060001b1: original abstract API, no native body.

        protected void ProcessAxis(GameInput gameInput, TAxis axis, IBaseInputAxisSource<TAxis> inputKeySource,
            string trackingKey, InputType inputType, int joystickIndex, IReadOnlyList<InputModifier> modifiers)
        {
            // 060001b2: get-axis precedes Contains, which precedes modifiers; tracking state is captured before callbacks.
            float value = inputKeySource.GetAxis(axis, joystickIndex);
            bool tracked = m_trackingLookup.Contains(trackingKey);
            ApplyModifiers(modifiers, ref value, gameInput);
            if (MathUtilities.WithinTolerance(value, 0f, 0.0001f))
            {
                if (tracked)
                {
                    m_trackingLookup.Remove(trackingKey);
                    if (AxisEndHandlers != null)
                    {
                        ResetModifiers(modifiers);
                        AxisEndHandlers(joystickIndex, gameInput, value, inputType);
                    }
                }
                return;
            }
            // A tracked axis with no continuous handler still falls back to the start callback in both original binaries.
            if (tracked && AxisHandlers != null)
                AxisHandlers(joystickIndex, gameInput, value, inputType);
            else
                AxisStartHandlers?.Invoke(joystickIndex, gameInput, value, inputType);
            m_trackingLookup.Add(trackingKey);
        }

        private void ApplyModifiers(IReadOnlyList<InputModifier> modifiers, ref float value, GameInput gameInput)
        {
            for (int i = 0; i < modifiers.Count; i++) // 060001b3: Unity-null modifiers skipped; unscaled delta time.
            {
                InputModifier modifier = modifiers[i];
                if (modifier == null) continue;
                value = modifier.Modify(value, Time.unscaledDeltaTime, gameInput);
            }
        }

        private void ResetModifiers(IReadOnlyList<InputModifier> modifiers)
        {
            for (int i = 0; i < modifiers.Count; i++) // 060001b4: live Count and Unity-null filtering.
            {
                InputModifier modifier = modifiers[i];
                if (modifier != null) modifier.Reset();
            }
        }

        private void PopulateToGlyphMap(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<TAxis, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IReadOnlyList<IInputAxisProvider<TAxisBinding>> inputAxisProviders)
        {
            Dictionary<InputType, List<Texture2D>> inputTypeGlyphs = null; // 060001b5: both out locals declared outside loops.
            List<Texture2D> glyphs = null;
            for (int i = 0; i < inputAxisProviders.Count; i++)
            {
                IInputAxisProvider<TAxisBinding> provider = inputAxisProviders[i];
                InputType inputType = provider.BindingInputType;
                int joystickIndex = provider.JoystickIndex;
                IReadOnlyList<IAxisBindingProvider<TAxisBinding>> axisProviders = provider.AxisProviders;
                for (int j = 0; j < axisProviders.Count; j++)
                {
                    IAxisBindingProvider<TAxisBinding> axisProvider = axisProviders[j];
                    GameInput gameInput = axisProvider.GameInput;
                    IReadOnlyList<TAxisBinding> bindings = axisProvider.AxisBindings;
                    for (int k = 0; k < bindings.Count; k++)
                    {
                        TAxisBinding binding = bindings[k];
                        Texture2D glyph = getGlyphForKeyAndInputType(binding.Axis, inputType, joystickIndex);
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

        protected BaseInputAxis() { } // 060001b6: genuine HashSet allocation before Object constructor.
    }
}
