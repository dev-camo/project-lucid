using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "GlobalConstantDefinition_Input", menuName = "HardlightProject/DefinitionData/Definitions/GlobalConstantDefinition_Input")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class GlobalConstantDefinition_Input : GlobalConstantDefinition
    {
        [Tooltip("Composite movement modifiers by settings type.")]
        public SerializableDictionary<InputModifierType, List<InputModifier>> CompositeMovementModifierLookups =
            new SerializableDictionary<InputModifierType, List<InputModifier>>(HardlightEnumComparers.InputModifierTypeComparer);
        [Tooltip("Modifier that delays input until a certain amount is reached.")]
        public ModifierDelayUntil DelayUntilModifier;

        // Original Game.Runtime 06001d20: comparer-backed dictionary before base constructor.
        public GlobalConstantDefinition_Input() { }
    }
}
