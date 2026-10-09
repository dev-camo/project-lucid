using System;
using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class BaseBindingData
    {
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_gameInput;
        [SerializeField] protected List<VectorisedBinding> m_vectorisedBindings;
        private static InputModifier[] s_allModifiers;

        // HLInput.Runtime 06000023/24: preserve the actual list reference and signed input bits.
        // Shipped GameInput is FinalVirtual after stripping; no surviving base interface may be invented.
        public IReadOnlyList<VectorisedBinding> VectorisedBindings { get { return m_vectorisedBindings; } }
        public GameInput GameInput { get { return m_gameInput; } }

        // 06000025: CLR-null cache test, genuine Resources query, then reread the static field.
        private static InputModifier[] GetCachedAllModifiers()
        {
            if (s_allModifiers == null) s_allModifiers = Resources.FindObjectsOfTypeAll<InputModifier>();
            return s_allModifiers;
        }

        // 06000026/29: genuine abstract contracts, with no original native body.
        public abstract void SaveModifierNames();
        protected void InternalSaveModifierNames(IReadOnlyList<BaseBinding> bindings)
        {
            // 06000027: Count remains live; null collection and entries retain their original faults.
            for (int i = 0; i < bindings.Count; i++) bindings[i].SaveModifierName();
        }
        protected void InternalSaveModifierName(BaseBinding binding) { binding.SaveModifierName(); } // 06000028
        public abstract void LoadModifiersByName();
        protected void InternalLoadModifiersByName(IReadOnlyList<BaseBinding> bindings)
        {
            // 0600002a: resolve/cache before touching the collection, once for this traversal.
            InputModifier[] allModifiers = GetCachedAllModifiers();
            for (int i = 0; i < bindings.Count; i++) InternalLoadModifierByName(bindings[i], allModifiers);
        }
        protected void InternalLoadModifierByName(BaseBinding binding)
        {
            InternalLoadModifierByName(binding, GetCachedAllModifiers()); // 0600002b
        }
        private void InternalLoadModifierByName(BaseBinding binding, IReadOnlyList<InputModifier> allModifiers)
        {
            binding.LoadModifierByName(allModifiers); // 0600002c
        }
        protected BaseBindingData() { } // 0600002d: Object constructor only, no list allocation.
    }
}
