using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "GameControlsMappingDefinition", menuName = "HardlightProject/DefinitionData/Definitions/GameControlsMappingDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class GameControlsMappingDefinition : ScriptableObject
    {
        [SerializeField] private List<GameControlsBinding> m_gamepadBindings;
        [SerializeField] private List<GameControlsBinding> m_keyboardBindings;
        [SerializeField] private List<GameControlsBinding> m_touchBindings;
        public List<GameControlsBinding> GamepadInputs { get { return m_gamepadBindings; } } // 06001c11
        public List<GameControlsBinding> KeyboardInputs { get { return m_keyboardBindings; } } // 06001c12
        public List<GameControlsBinding> TouchInputs { get { return m_touchBindings; } } // 06001c13
        public GameControlsMappingDefinition() { } // 06001c14: original null lists.
    }
}
