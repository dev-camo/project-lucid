using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BaseActiveBindings<TInputBindingProvider, TBindingData> : SingleScriptableObject, ICurrentBaseInputBindingsProvider
        where TInputBindingProvider : BaseInputBinding<TBindingData>
        where TBindingData : BaseBindingData
    {
        private List<TInputBindingProvider> m_activeBindings;
        private readonly bool m_allowSaveLoad;
        private readonly List<TInputBindingProvider> m_currentInputBindings = new List<TInputBindingProvider>();
        private IBaseControllerProvider m_baseControllerProvider;

        // HLInput.Runtime 0600000b..10: exact shared list; field-like events recover genuine compare-exchange loops.
        // Original stripped interfaces no longer declare these six FinalVirtual APIs; emission flags remain qualified.
        public IReadOnlyList<TInputBindingProvider> CurrentInputBindings { get { return m_currentInputBindings; } }
        public event Action<IReadOnlyList<TInputBindingProvider>> OnCurrentInputBindingsUpdate;
        public IReadOnlyList<IBaseInputBindingProvider> CurrentBaseInputBindings { get { return m_currentInputBindings; } }
        public event Action<IReadOnlyList<IBaseInputBindingProvider>> OnCurrentBaseInputBindingsUpdate;
        protected abstract void SetupBindings(); // 06000011: actual abstract contract.
        protected void SetActiveBindings(List<TInputBindingProvider> newActiveBindings) { m_activeBindings = newActiveBindings; } // 06000012
        public void Initialise(IBaseControllerProvider baseControllerProvider)
        {
            // 06000013: store, virtual setup, refresh, then genuine mutable FastAction property subscription.
            m_baseControllerProvider = baseControllerProvider;
            SetupBindings();
            RefreshBindings();
            m_baseControllerProvider.OnControllerConnectionUpdate += RefreshBindings;
        }
        public void Shutdown()
        {
            // 06000014: remove serialization handlers first; retain both original null stores and live current list.
            RemovePropertyStoreHandlers();
            m_baseControllerProvider.OnControllerConnectionUpdate -= RefreshBindings;
            m_baseControllerProvider = null;
            m_baseControllerProvider = null;
        }
        private void AddPropertyStoreHandlers()
        {
            // 06000015: readonly flag remains false in the recovered constructor, but original branch is retained.
            if (!m_allowSaveLoad) return;
            HLPropertyStore.AddLoadHandler(OnPropertyStoreLoad, true);
            HLPropertyStore.AddSaveHandler(OnPropertyStoreSave);
        }
        private void RemovePropertyStoreHandlers()
        {
            if (!m_allowSaveLoad) return; // 06000016
            HLPropertyStore.RemoveLoadHandler(OnPropertyStoreLoad);
            HLPropertyStore.RemoveSaveHandler(OnPropertyStoreSave);
        }
        private void RefreshBindings()
        {
            // 06000017: empty/null authored active lists leave the prior current list and events untouched.
            if (m_activeBindings == null || m_activeBindings.Count == 0) return;
            m_currentInputBindings.Clear();
            IReadOnlyList<string> controllerNames = m_baseControllerProvider.GetControllerNames();
            for (int i = 0; i < controllerNames.Count; i++)
            {
                string controllerName = controllerNames[i];
                int priority = m_activeBindings.FindIndex(binding =>
                {
                    // Genuine DisplayClass20_0 predicate 06000022: Unity-null guard, live identifiers, locale-sensitive ignore case.
                    if (binding == null) return false;
                    for (int j = 0; j < binding.BindingIdentifiers.Count; j++)
                    {
                        if (controllerName.Contains(binding.BindingIdentifiers[j].Identifier, StringComparison.CurrentCultureIgnoreCase)) return true;
                    }
                    if (binding.ForcedBinding && binding.BindingIdentifiers.Count > 0 && binding.FallbackInputType != InputType.Unsupported)
                    {
                        for (int j = 0; j < binding.BindingIdentifiers.Count; j++)
                        {
                            if (binding.BindingIdentifiers[j].InputType == binding.FallbackInputType)
                            {
                                controllerName = binding.BindingIdentifiers[j].Identifier;
                                return true;
                            }
                        }
                    }
                    return false;
                });
                if (priority < 0) continue;
                TInputBindingProvider selectedBinding = m_activeBindings[priority];
                if (selectedBinding != null)
                {
                    TInputBindingProvider currentBinding = UnityEngine.Object.Instantiate(selectedBinding);
                    currentBinding.Setup(i, priority, controllerName);
                    m_currentInputBindings.Add(currentBinding);
                }
            }
            // Genuine <>c predicate 0600001f: first forced binding, without an added Unity-null guard.
            TInputBindingProvider forcedBinding = m_activeBindings.Find(binding => binding.ForcedBinding);
            if (forcedBinding != null)
            {
                // Preserve original comparison against the authored object, although current entries above are clones.
                if (m_currentInputBindings.Contains(forcedBinding))
                {
                    if (m_currentInputBindings.Count > 0) HLOutput.LogError("Controller detected as the forced binding, this is not allowed.");
                }
                else
                {
                    TInputBindingProvider currentBinding = UnityEngine.Object.Instantiate(forcedBinding);
                    currentBinding.Setup(-1, 0, forcedBinding.name);
                    m_currentInputBindings.Add(currentBinding);
                }
            }
            m_currentInputBindings.Sort((a, b) => a.Priority.CompareTo(b.Priority)); // 06000020
            InternalRefreshBindings();
            OnCurrentBaseInputBindingsUpdate?.Invoke(m_currentInputBindings);
            OnCurrentInputBindingsUpdate?.Invoke(m_currentInputBindings);
        }
        protected virtual void InternalRefreshBindings() { } // 06000018: genuine RET, not a replacement hook body.
        private void OnPropertyStoreSave(HLPropertyList properties)
        {
            for (int i = 0; i < m_activeBindings.Count; i++) // 06000019: authored providers, live Count.
            {
                TInputBindingProvider binding = m_activeBindings[i];
                binding.PrepareToSave();
                string json = JsonUtility.ToJson(binding);
                properties.AddProperty(binding.name, json);
            }
        }
        private void OnPropertyStoreLoad(HLPropertyList properties, bool isnewfile)
        {
            for (int i = 0; i < m_activeBindings.Count; i++) // 0600001a: isnewfile is genuinely unused.
            {
                TInputBindingProvider binding = m_activeBindings[i];
                string json = properties.AsString(binding.name, string.Empty);
                if (string.IsNullOrEmpty(json)) continue;
                JsonUtility.FromJsonOverwrite(json, binding);
                if (binding != null) binding.UpdateFromSave();
            }
        }
        private void OnValidate()
        {
            RemovePropertyStoreHandlers(); // 0600001b: no added refresh or controller-null guard.
            SetupBindings();
            AddPropertyStoreHandlers();
        }
        protected BaseActiveBindings() { } // 0600001c: only current-list allocation, allowSaveLoad retains false.
        // Compiler natural <>c/DisplayClass constructors and cctor 0600001d/1e/21: genuine Object/allocation only.
        // No exact compiler closure-token/member-shape parity is claimed by this source candidate.
    }
}
