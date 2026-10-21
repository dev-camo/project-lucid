using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x020006a7. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraInputHandler : InputHandler
    {
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly CharacterControlsMappingDefinition.CameraControlsMapping m_controlsMapping;

        // 0x060023af: the camera mapping comes from the Character global constants, when they are that definition.
        public CameraInputHandler()
        {
            GlobalConstantDefinition_Character definition =
                m_dataManagerRef.Get().GlobalConstantDefinitions[GlobalConstantType.Character]
                    as GlobalConstantDefinition_Character;
            if (definition != null)
                m_controlsMapping = definition.ControlsMappingDefinition.CameraMapping;
        }

        // 0x060023b0: each axis is reported as a held button of the mapped direction with the axis magnitude.
        public void ApplyInput(Vector2 input)
        {
            GameInput horizontal = input.x > 0f ? m_controlsMapping.Right : m_controlsMapping.Left;
            OnButtonHeld(horizontal, Mathf.Abs(input.x), ControlMapping.LastInputType);
            GameInput vertical = input.y > 0f ? m_controlsMapping.Up : m_controlsMapping.Down;
            OnButtonHeld(vertical, Mathf.Abs(input.y), ControlMapping.LastInputType);
        }
    }
}
