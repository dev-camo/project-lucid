using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class InputAxisSubscription : InputSubscription
    {
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private SerializableDictionary<InputModifierType, System.Collections.Generic.List<InputModifier>> m_modifierLookups =
            new SerializableDictionary<InputModifierType, System.Collections.Generic.List<InputModifier>>(HardlightEnumComparers.InputModifierTypeComparer);

        // Original 060023ca: both initializers precede the base constructor.
        protected InputAxisSubscription(GameControlsBinding binding, ValidatorFunc validator) : base(binding, validator) { }

        // Original 060023cb: dictionary indexer and an as-cast, no fallback dictionary.
        public override void Subscribe()
        {
            GlobalConstantDefinition_Input definition = ProcessManager.GetSystem<DataManager>(null, true).GlobalConstantDefinitions[GlobalConstantType.Input] as GlobalConstantDefinition_Input;
            m_modifierLookups = definition != null ? definition.CompositeMovementModifierLookups : null;
        }

        public override void Unsubscribe() { } // Original 060023cc is empty.

        // Original 060023cd: saved sensitivity and every modifier are read even if base rejected.
        // Preserve the original pre-modifier result, per-element delta time and foreach disposal.
        protected override bool ApplyModifiers(ref float value)
        {
            bool result = base.ApplyModifiers(ref value);
            InputModifierType sensitivity = m_saveManagerRef.Get().GetSaveDataSettings().ControlStickSensitivity;
            if (m_modifierLookups.TryGetValue(sensitivity, out System.Collections.Generic.List<InputModifier> modifiers))
            {
                foreach (InputModifier modifier in modifiers)
                    value = modifier.Modify(value, Time.deltaTime);
            }
            return result;
        }
    }
}
