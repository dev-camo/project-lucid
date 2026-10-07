using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class BaseBinding
    {
        [SerializeField] private List<InputModifier> m_modifiers;
        [HideInInspector, SerializeField] private List<string> m_modifierNames;

        // HLInput.Runtime 06000041: the supplied mutable list is exposed through its original read-only API.
        public IReadOnlyList<InputModifier> Modifiers { get { return m_modifiers; } }

        // 06000042: Object construction precedes storing the exact supplied reference; null is preserved.
        protected BaseBinding(List<InputModifier> modifiers) { m_modifiers = modifiers; }

        // 06000043: reuse the saved-name list, clear it before reading modifiers, skip Unity-null objects.
        // Use the genuine read-only getter for Count/indexing; native dispatches both BCL interfaces.
        // Keep the repeated index read and Add receiver evaluation: authored collection/name callbacks can observe them.
        public void SaveModifierName()
        {
            if (m_modifierNames == null) m_modifierNames = new List<string>();
            m_modifierNames.Clear();
            for (int i = 0; i < Modifiers.Count; i++)
            {
                if (Modifiers[i] == null) continue;
                m_modifierNames.Add(Modifiers[i].name);
            }
        }

        // 06000044: rebuild in the supplied object's order, using name membership rather than saved-name order.
        // Duplicate supplied objects remain duplicates; null input objects and missing names retain the original faults.
        public void LoadModifierByName(IReadOnlyList<InputModifier> allModifiers)
        {
            if (m_modifiers == null) m_modifiers = new List<InputModifier>();
            m_modifiers.Clear();
            for (int i = 0; i < allModifiers.Count; i++)
            {
                if (m_modifierNames.Contains(allModifiers[i].name))
                    m_modifiers.Add(allModifiers[i]);
            }
        }
    }
}
