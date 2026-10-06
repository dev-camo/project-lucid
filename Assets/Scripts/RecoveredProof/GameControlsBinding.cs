using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GameControlsBinding
    {
        [HashEnum(typeof(GameInput))] [SerializeField] private GameInput m_gameInput;
        [SerializeField] private List<InputModifier> m_modifiers;
        public GameInput GameInput { get { return m_gameInput; } } // 06001c0e
        public GameControlsBinding(GameInput gameInput) { m_gameInput = gameInput; } // 06001c0f
        public void ApplyModifiers(ref float value, float deltaTime) // 06001c10
        {
            if (m_modifiers == null) return;
            // Original foreach captures this list; later callbacks still read the live input/value.
            foreach (InputModifier modifier in m_modifiers)
                value = modifier.Modify(value, deltaTime, m_gameInput);
        }
    }
}
