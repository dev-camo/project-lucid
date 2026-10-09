using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLInput.Runtime 0x02000068: all nine original owner methods. Both shipped
    // architectures retain the same stores, live collection reads and callback order.
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class InputPointer
    {
        // Runtime delegate APIs 0x0600020e..0x06000211. Any C# compiler-generated
        // BeginInvoke/EndInvoke methods are not bound to original native methods.
        public delegate void OnPointerHandler(GameInput gameInput, Vector3 position);
        public delegate void OnScrollWheelHandler(GameInput gameInput, float value);

        // 0x0400012a/0x0400012b; original add/remove methods 0x06000205..0x06000208.
        public event OnPointerHandler PointerHandlers;
        public event OnScrollWheelHandler ScrollWheelHandlers;

        private IInputPointerSource m_inputPointerSource; // 0x0400012c
        private IReadOnlyList<GameInputBinding> m_gameInputBindings; // 0x0400012d
        private Vector3 m_previousPointerPosition = Vector3.zero; // 0x0400012e

        // 0x06000209: stores precede the source query, including on a query fault.
        public void Initialise(IInputPointerSource inputPointerSource,
            IReadOnlyList<GameInputBinding> gameInputBindings)
        {
            m_inputPointerSource = inputPointerSource;
            m_gameInputBindings = gameInputBindings;
            m_previousPointerPosition = m_inputPointerSource.GetPosition();
        }

        // 0x0600020a: previous position and subscribed handlers survive shutdown.
        public void Shutdown()
        {
            m_inputPointerSource = null;
            m_gameInputBindings = null;
        }

        // 0x0600020b: scroll is sampled first and remains one shared, cumulatively
        // modified value across every binding. Handler availability is sampled only
        // for the pointer decision; each actual callback rereads its event field.
        public void Update()
        {
            float scrollWheelDelta = m_inputPointerSource.GetScrollWheelDelta();
            Vector3 pointerPosition = m_inputPointerSource.GetPosition();
            bool sendPointer = pointerPosition != m_previousPointerPosition && PointerHandlers != null;

            for (int i = 0; i < m_gameInputBindings.Count; ++i)
            {
                IReadOnlyList<BindingData> bindingData = m_gameInputBindings[i].BindingData;
                for (int j = 0; j < bindingData.Count; ++j)
                {
                    BindingData data = bindingData[j];
                    if (data != null && sendPointer && data.FixedBindings.BindToPointer)
                        PointerHandlers(data.GameInput, pointerPosition);

                    // Null data is not skipped: only the pointer branch guards it.
                    ApplyModifiers(data.FixedBindings, ref scrollWheelDelta, data.GameInput);
                    bool withinTolerance = MathUtilities.WithinTolerance(scrollWheelDelta, 0f, 0.0001f);
                    if (data != null && !withinTolerance && data.FixedBindings.BindToScrollWheel)
                        ScrollWheelHandlers(data.GameInput, scrollWheelDelta);
                }
            }

            // Exceptions in providers, modifiers or callbacks retain the old position.
            m_previousPointerPosition = pointerPosition;
        }

        // 0x0600020c: no Unity-object/null filtering; original scaled delta time.
        private void ApplyModifiers(FixedBindings fixedBinding, ref float value, GameInput gameInput)
        {
            IReadOnlyList<InputModifier> modifiers = fixedBinding.Modifiers;
            for (int i = 0; i < modifiers.Count; ++i)
            {
                InputModifier modifier = modifiers[i];
                value = modifier.Modify(value, Time.deltaTime, gameInput);
            }
        }

        // 0x0600020d: Vector3.zero field initialization precedes System.Object::.ctor.
        public InputPointer() { }
    }
}
