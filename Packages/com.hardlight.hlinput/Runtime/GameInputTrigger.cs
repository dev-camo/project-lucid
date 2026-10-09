using System;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class GameInputTrigger : MonoBehaviour
    {
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        public class InputEvent : UnityEvent<float>
        {
            public InputEvent() { } // 06000050: actual UnityEvent<float> base.
        }

        [SerializeField] private GameInput m_gameInput;
        [SerializeField] private InputTrigger m_intputTrigger;
        [SerializeField] private InputEvent OnGameInput;

        // 0600004c: load input, create original callback, then read trigger.
        private void OnEnable() { ControlMapping.Subscribe(m_gameInput, OnInput, m_intputTrigger, -1); }
        private void OnDisable() { ControlMapping.Unsubscribe(m_gameInput, OnInput, -1); } // 0600004d
        private void OnInput(float value) { OnGameInput.Invoke(value); } // 0600004e: no null guard.
        public GameInputTrigger() { } // 0600004f: no synthetic event allocation.
    }
}
