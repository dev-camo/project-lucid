using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BaseInputBinding<TBindingData> : ScriptableObject, IBaseInputBindingProvider where TBindingData : BaseBindingData
    {
        [SerializeField, Tooltip("Expected to always be available.")] private bool m_forcedBinding;
        [SerializeField] private bool m_allowRemapping;
        [Tooltip("Controller will fallback to the first identifier matching this input type."), SerializeField] private InputType m_fallbackInputType = InputType.Unsupported;
        [Tooltip("Input type of this binding. It can be overridden if an identifier is matched at runtime."), SerializeField] private InputType m_bindingInputType = InputType.Unsupported;
        [SerializeField] private List<TBindingData> m_bindingData;
        [SerializeField] private List<BindingIdentifier> m_bindingIdentifiers;
        private InputType m_inputTypeOverride = InputType.Unsupported;
        // HLInput.Runtime 06000107..111: actual fields, including override selection by Unsupported equality.
        public IReadOnlyList<BindingIdentifier> BindingIdentifiers { get { return m_bindingIdentifiers; } }
        public int JoystickIndex { get; private set; } = -1;
        public int Priority { get; private set; } = -1;
        [NonSerialized] private bool m_isInitialised;

        public bool AllowRemapping { get { return m_allowRemapping; } }
        public bool ForcedBinding { get { return m_forcedBinding; } }
        public InputType InputTypeOverride { get { return m_inputTypeOverride; } }
        public InputType BindingInputType { get { return m_inputTypeOverride == InputType.Unsupported ? m_bindingInputType : m_inputTypeOverride; } }
        public InputType FallbackInputType { get { return m_fallbackInputType; } }
        public IReadOnlyList<TBindingData> BindingData { get { return m_bindingData; } }

        private void Awake() { if (m_forcedBinding) JoystickIndex = -1; } // 06000112
        public void Setup(int joystickIndex, int priority, string identifier)
        {
            // 06000113: latch before traversing identifiers, preserve last matching override and partial state on faults.
            if (m_isInitialised) return;
            m_isInitialised = true;
            JoystickIndex = joystickIndex;
            Priority = priority;
            for (int i = 0; i < m_bindingIdentifiers.Count; i++)
            {
                BindingIdentifier bindingIdentifier = m_bindingIdentifiers[i];
                if (identifier.Contains(bindingIdentifier.Identifier)) m_inputTypeOverride = bindingIdentifier.InputType;
            }
        }
        public void PrepareToSave()
        {
            for (int i = 0; i < m_bindingData.Count; i++) m_bindingData[i].SaveModifierNames(); // 06000114
        }
        public void UpdateFromSave()
        {
            for (int i = 0; i < m_bindingData.Count; i++) m_bindingData[i].LoadModifiersByName(); // 06000115
        }
        protected BaseInputBinding() { } // 06000116: only the five signed enum/index defaults above.
    }
}
