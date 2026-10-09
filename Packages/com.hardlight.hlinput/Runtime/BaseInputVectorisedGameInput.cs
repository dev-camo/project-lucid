using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000057: eleven owner methods plus all eleven
    // methods of the real private nested callback provider, 0x02000059.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BaseInputVectorisedGameInput<TBindingType, TBindingData>
        where TBindingType : BaseInputBinding<TBindingData>
        where TBindingData : BaseBindingData
    {
        // 0x02000058: original runtime .ctor/Invoke, 0x060001d0/0x060001d1.
        // Compiler-emitted asynchronous delegate wrappers remain unbound.
        public delegate void OnVectorisedGameInputHandler(GameInput gameInput, Vector3 vectorisedInputValue);
        public event OnVectorisedGameInputHandler VectorisedGameInputHandlers; // 0x060001c5/0x060001c6

        // 0x060001cf: shared by every instance of this closed generic provider.
        // First binding for a GameInput owns the callbacks reused by later bindings.
        private static Dictionary<GameInput, VectorisedGameInputComponentCallbacks> m_vectorComponentLookup =
            new Dictionary<GameInput, VectorisedGameInputComponentCallbacks>(HardlightEnumComparers.GameInputComparer);
        private IReadOnlyList<TBindingType> m_gameInputBindings;

        // 0x060001c7: no clearing or duplicate-initialization guard.
        public void Initialise(IReadOnlyList<TBindingType> gameInputBindings)
        {
            m_gameInputBindings = gameInputBindings;
            for (int i = 0; i < m_gameInputBindings.Count; ++i)
            {
                TBindingType binding = m_gameInputBindings[i];
                IReadOnlyList<TBindingData> bindingData = binding.BindingData;
                for (int j = 0; j < bindingData.Count; ++j)
                {
                    TBindingData data = bindingData[j];
                    GameInput gameInput = data.GameInput;
                    if (data.VectorisedBindings.Count < 1)
                        continue;

                    for (int k = 0; k < data.VectorisedBindings.Count; ++k)
                    {
                        VectorisedBinding current = data.VectorisedBindings[k];
                        if (m_vectorComponentLookup.TryGetValue(gameInput, out VectorisedGameInputComponentCallbacks componentCallbacks))
                        {
                            SubscribeComponentCallbacks(current, componentCallbacks, binding.JoystickIndex);
                        }
                        else
                        {
                            componentCallbacks = new VectorisedGameInputComponentCallbacks(gameInput, this, current);
                            SubscribeComponentCallbacks(current, componentCallbacks, binding.JoystickIndex);
                            m_vectorComponentLookup.Add(gameInput, componentCallbacks);
                        }
                    }
                }
            }
        }

        // 0x060001c8: subscriptions are removed before the shared lookup is
        // cleared and the instance binding list is nulled. Faults retain that state.
        public void Shutdown()
        {
            for (int i = 0; i < m_gameInputBindings.Count; ++i)
            {
                TBindingType binding = m_gameInputBindings[i];
                IReadOnlyList<TBindingData> bindingData = binding.BindingData;
                for (int j = 0; j < bindingData.Count; ++j)
                {
                    TBindingData data = bindingData[j];
                    GameInput gameInput = data.GameInput;
                    if (data.VectorisedBindings.Count < 1)
                        continue;
                    for (int k = 0; k < data.VectorisedBindings.Count; ++k)
                    {
                        VectorisedBinding current = data.VectorisedBindings[k];
                        if (m_vectorComponentLookup.TryGetValue(gameInput, out VectorisedGameInputComponentCallbacks componentCallbacks))
                            UnsubscribeComponentCallbacks(current, componentCallbacks, binding.JoystickIndex);
                    }
                }
            }
            m_vectorComponentLookup.Clear();
            m_gameInputBindings = null;
        }

        // 0x060001c9: genuine Dictionary enumerator/deconstruction/finally disposal.
        public void Update()
        {
            foreach (var (gameInput, componentCallbacks) in m_vectorComponentLookup)
                componentCallbacks.Update();
        }

        // 0x060001ca: each component is read separately for Held and Up; lists
        // are reread for every index/count, including callback-driven mutations.
        private static void SubscribeComponentCallbacks(VectorisedBinding current,
            VectorisedGameInputComponentCallbacks componentCallbacks, int joystickIndex)
        {
            for (int i = 0; i < current.XComponents.Count; ++i)
            {
                ControlMapping.Subscribe(current.XComponents[i], componentCallbacks.XComponentChangeCallback, InputTrigger.Held, joystickIndex);
                ControlMapping.Subscribe(current.XComponents[i], componentCallbacks.XComponentResetCallback, InputTrigger.Up, joystickIndex);
            }
            for (int i = 0; i < current.YComponents.Count; ++i)
            {
                ControlMapping.Subscribe(current.YComponents[i], componentCallbacks.YComponentChangeCallback, InputTrigger.Held, joystickIndex);
                ControlMapping.Subscribe(current.YComponents[i], componentCallbacks.YComponentResetCallback, InputTrigger.Up, joystickIndex);
            }
            for (int i = 0; i < current.ZComponents.Count; ++i)
            {
                ControlMapping.Subscribe(current.ZComponents[i], componentCallbacks.ZComponentChangeCallback, InputTrigger.Held, joystickIndex);
                ControlMapping.Subscribe(current.ZComponents[i], componentCallbacks.ZComponentResetCallback, InputTrigger.Up, joystickIndex);
            }
        }

        // 0x060001cb: each matching delegate is reconstructed for Unsubscribe.
        private static void UnsubscribeComponentCallbacks(VectorisedBinding current,
            VectorisedGameInputComponentCallbacks componentCallbacks, int joystickIndex)
        {
            for (int i = 0; i < current.XComponents.Count; ++i)
            {
                ControlMapping.Unsubscribe(current.XComponents[i], componentCallbacks.XComponentChangeCallback, joystickIndex);
                ControlMapping.Unsubscribe(current.XComponents[i], componentCallbacks.XComponentResetCallback, joystickIndex);
            }
            for (int i = 0; i < current.YComponents.Count; ++i)
            {
                ControlMapping.Unsubscribe(current.YComponents[i], componentCallbacks.YComponentChangeCallback, joystickIndex);
                ControlMapping.Unsubscribe(current.YComponents[i], componentCallbacks.YComponentResetCallback, joystickIndex);
            }
            for (int i = 0; i < current.ZComponents.Count; ++i)
            {
                ControlMapping.Unsubscribe(current.ZComponents[i], componentCallbacks.ZComponentChangeCallback, joystickIndex);
                ControlMapping.Unsubscribe(current.ZComponents[i], componentCallbacks.ZComponentResetCallback, joystickIndex);
            }
        }

        // 0x060001cc: original modifier category filter, no null-object guard.
        private void ApplyModifiers(VectorisedBinding vectorisedBinding, ref float floatValue, GameInput gameInput)
        {
            IReadOnlyList<InputModifier> modifiers = vectorisedBinding.Modifiers;
            for (int i = 0; i < modifiers.Count; ++i)
            {
                InputModifier modifier = modifiers[i];
                if (modifier.ModifierType == InputModifier.InputModifierType.Float)
                    floatValue = modifier.Modify(floatValue, Time.deltaTime, gameInput);
            }
        }

        // 0x060001cd: gameInput is authentically unused by this vector overload.
        private void ApplyModifiers(VectorisedBinding vectorisedBinding, ref Vector3 vectorValue, GameInput gameInput)
        {
            IReadOnlyList<InputModifier> modifiers = vectorisedBinding.Modifiers;
            for (int i = 0; i < modifiers.Count; ++i)
            {
                InputModifier modifier = modifiers[i];
                if (modifier.ModifierType == InputModifier.InputModifierType.Vector3)
                    vectorValue = modifier.Modify(vectorValue, Time.deltaTime);
            }
        }

        public BaseInputVectorisedGameInput() { } // 0x060001ce: System.Object only.

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class VectorisedGameInputComponentCallbacks
        {
            private GameInput m_gameInput;
            private BaseInputVectorisedGameInput<TBindingType, TBindingData> m_inputVectorisedGameInput;
            private Vector3 m_vector = Vector3.zero;
            private Vector3 m_previousVector = Vector3.zero;
            private VectorisedBinding m_vectorisedBinding;

            // 0x060001d2: vector initialization before base; then these three stores.
            public VectorisedGameInputComponentCallbacks(GameInput gameInput,
                BaseInputVectorisedGameInput<TBindingType, TBindingData> inputVectorisedGameInput,
                VectorisedBinding vectorisedBinding)
            {
                m_gameInput = gameInput;
                m_inputVectorisedGameInput = inputVectorisedGameInput;
                m_vectorisedBinding = vectorisedBinding;
            }

            public void XComponentChangeCallback(float value) { OnValue(value, ref m_vector.x); } // 0x060001d3
            public void XComponentResetCallback(float value) { OnReset(value, ref m_vector.x); } // 0x060001d4
            public void YComponentChangeCallback(float value) { OnValue(value, ref m_vector.y); } // 0x060001d5
            public void YComponentResetCallback(float value) { OnReset(value, ref m_vector.y); } // 0x060001d6
            public void ZComponentChangeCallback(float value) { OnValue(value, ref m_vector.z); } // 0x060001d7
            public void ZComponentResetCallback(float value) { OnReset(value, ref m_vector.z); } // 0x060001d8

            // 0x060001d9: modifier fault retains the existing output component.
            private void OnValue(float value, ref float outputValue)
            {
                m_inputVectorisedGameInput.ApplyModifiers(m_vectorisedBinding, ref value, m_gameInput);
                outputValue = value;
            }

            private void OnReset(float value, ref float outputValue) { outputValue = value; } // 0x060001da

            // 0x060001db: the original delegate is captured for this invocation.
            private void InvokeHandler()
            {
                m_inputVectorisedGameInput.VectorisedGameInputHandlers?.Invoke(m_gameInput, m_vector);
            }

            // 0x060001dc: positive magnitude invokes every update; transition from
            // positive previous magnitude invokes once. ARM B.LE and x86 JBE both
            // take their non-positive branch on unordered/NaN comparisons.
            public void Update()
            {
                if (m_vector.sqrMagnitude > 0f)
                {
                    m_inputVectorisedGameInput.ApplyModifiers(m_vectorisedBinding, ref m_vector, m_gameInput);
                    InvokeHandler();
                }
                else if (m_previousVector.sqrMagnitude > 0f)
                {
                    InvokeHandler();
                }
                m_previousVector = m_vector;
            }
        }
    }
}
