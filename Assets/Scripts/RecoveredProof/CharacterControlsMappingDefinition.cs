using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "CharacterControlsMappingDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CharacterControlsMappingDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterControlsMappingDefinition : ScriptableObject
    {
        [SerializeField] private List<CharacterControlsMapping> m_controlsMapping = new List<CharacterControlsMapping>();
        [SerializeField] private SerializableDictionary<GameAction, GameInput> m_movementMapping = new SerializableDictionary<GameAction, GameInput>(HardlightEnumComparers.GameActionComparer);
        [SerializeField] private List<InputType> m_decoratorInputTypes = new List<InputType>();
        [SerializeField] private SerializableDictionary<GameAction, Texture2D> m_decoratorMapping = new SerializableDictionary<GameAction, Texture2D>(HardlightEnumComparers.GameActionComparer);
        [SerializeField] private CameraControlsMapping m_cameraMapping = new CameraControlsMapping();
        public List<CharacterControlsMapping> ControlsMapping { get { return m_controlsMapping; } } // 06001bd7
        public CameraControlsMapping CameraMapping { get { return m_cameraMapping; } } // 06001bd8
        public bool TryGetGameInput(InputType inputType, GameAction action, out GameInput gameInput) // 06001bd9
        {
            foreach (CharacterControlsMapping mapping in m_controlsMapping)
            {
                if (mapping.Action != action) continue;
                // Native raw reference choice, with no Unity fake-null comparison.
                GameControlsMappingDefinition definition = mapping.MappingSecondary ?? mapping.Mapping;
                if (inputType == InputType.Touch) gameInput = definition.TouchInputs[0].GameInput;
                else if (inputType == InputType.Keyboard) gameInput = definition.KeyboardInputs[0].GameInput;
                else gameInput = definition.GamepadInputs[0].GameInput;
                return true;
            }
            return m_movementMapping.TryGetValue(action, out gameInput);
        }
        public bool TryGetDecoratorImage(InputType inputType, GameAction action, out Texture2D image) // 06001bda
        {
            // Dictionary assigns the out value before the input eligibility check, even on false.
            return m_decoratorMapping.TryGetValue(action, out image) && m_decoratorInputTypes.Contains(inputType);
        }
        public CharacterControlsMappingDefinition() { } // 06001bdb: five ordered field initializers.
        [Serializable]
        public class CharacterControlsMapping
        {
            [HashEnum(typeof(GameAction))] [SerializeField] private GameAction m_action;
            [SerializeField] private GameControlsMappingDefinition m_mapping;
            [SerializeField] private GameControlsMappingDefinition m_mappingSecondary;
            public GameAction Action { get { return m_action; } } // 06001bdc
            public GameControlsMappingDefinition Mapping { get { return m_mapping; } } // 06001bdd
            public GameControlsMappingDefinition MappingSecondary { get { return m_mappingSecondary; } } // 06001bde
            public CharacterControlsMapping() { } // 06001bdf: original zero/null fields.
        }
        [Serializable]
        public class CameraControlsMapping
        {
            [SerializeField] [HashEnum(typeof(GameInput))] private GameInput m_left = GameInput.Left;
            [HashEnum(typeof(GameInput))] [SerializeField] private GameInput m_right = GameInput.Right;
            [HashEnum(typeof(GameInput))] [SerializeField] private GameInput m_up = GameInput.Up;
            [SerializeField] [HashEnum(typeof(GameInput))] private GameInput m_down = GameInput.Down;
            public GameInput Left { get { return m_left; } } // 06001be0
            public GameInput Right { get { return m_right; } } // 06001be1
            public GameInput Up { get { return m_up; } } // 06001be2
            public GameInput Down { get { return m_down; } } // 06001be3
            public CameraControlsMapping() { } // 06001be4: four original defaults before Object base.
        }
    }
}
