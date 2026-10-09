using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime0200001f: complete20 owner APIs (11 native,9 abstract) + nested ctor1.
    // Concrete input and binding providers remain genuine graph dependencies.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ControlMap : SingleScriptableObject
    {
        [SerializeField] protected ActiveGameInputBindings m_inputBinding;
        [SerializeField] private PlatformsWithBlockedInputTypes[] m_blockedInputTypes = Array.Empty<PlatformsWithBlockedInputTypes>();
        private StackableData m_gameInputsDisabled;
        private readonly Dictionary<GameInput, Action<GameInput, bool>> m_gameInputsDisabledCallbacks =
            new Dictionary<GameInput, Action<GameInput, bool>>(HardlightEnumComparers.GameInputComparer);

        // Original02000020: Serializable flag, private nested class, no list allocation/default substitution.
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class PlatformsWithBlockedInputTypes
        {
            public RuntimePlatform Platform;
            public List<InputType> InputTypes;
            // Implicit public0600007c calls only Object constructor.
        }

        // Original abstract06000068..70; exact generic names and authentic constraints.
        public abstract void Initialise<TKey, TButtonBinding>(BaseInputButton<TKey, TButtonBinding> inputButton)
            where TButtonBinding : BaseBinding, IKeyProvider<TKey>;
        public abstract void Initialise<TAxis, TAxisBinding>(BaseInputAxis<TAxis, TAxisBinding> inputAxis)
            where TAxisBinding : BaseBinding, IAxisProvider<TAxis>;
        public abstract void Initialise<TBindingType, TBindingData>(BaseInputVectorisedGameInput<TBindingType, TBindingData> inputVectorisedGameInput)
            where TBindingType : BaseInputBinding<TBindingData> where TBindingData : BaseBindingData;
        public abstract void Initialise(InputSwipe inputSwipe, InputTouch inputTouch, InputPointer inputPointer);
        public abstract void Shutdown<TKey, TButtonBinding>(BaseInputButton<TKey, TButtonBinding> inputButton)
            where TButtonBinding : BaseBinding, IKeyProvider<TKey>;
        public abstract void Shutdown<TAxis, TAxisBinding>(BaseInputAxis<TAxis, TAxisBinding> inputAxis)
            where TAxisBinding : BaseBinding, IAxisProvider<TAxis>;
        public abstract void Shutdown<TBindingType, TBindingData>(BaseInputVectorisedGameInput<TBindingType, TBindingData> inputVectorisedGameInput)
            where TBindingType : BaseInputBinding<TBindingData> where TBindingData : BaseBindingData;
        public abstract void Shutdown(InputSwipe inputSwipe, InputTouch inputTouch, InputPointer inputPointer);
        public abstract void ClearFrameInputs();

        // Original06000071: publish the real data object before enum initialization, subscribe after the loop, clear last.
        public void InitialiseStackableData()
        {
            if (m_gameInputsDisabled != null) return;
            m_gameInputsDisabled = new StackableData();
            foreach (GameInput gameInput in EnumUtilities.GetValues<GameInput>())
            {
                m_gameInputsDisabled.SetBaseValue((int)gameInput, false);
                m_gameInputsDisabled.SetRetrievalOperation((int)gameInput, StackableData.RetrievalOperation.LogicalOr, false);
            }
            m_gameInputsDisabled.OnDataUpdated += OnInputsDisabledUpdated;
            m_gameInputsDisabledCallbacks.Clear();
        }
        // Original06000072: unsubscribe first, enumerate the live dictionary with virtual callbacks, clear then discard data.
        public void ShutdownStackableData()
        {
            if (m_gameInputsDisabled == null) return;
            m_gameInputsDisabled.OnDataUpdated -= OnInputsDisabledUpdated;
            foreach (KeyValuePair<GameInput, Action<GameInput, bool>> pair in m_gameInputsDisabledCallbacks)
            {
                // 06000072: both shipped CPUs call Deconstruct with key and callback outputs before notification.
                pair.Deconstruct(out GameInput gameInput, out Action<GameInput, bool> callback);
                NotifyInputDisabledUpdated(gameInput, false);
            }
            m_gameInputsDisabledCallbacks.Clear();
            m_gameInputsDisabled = null;
        }
        // Original06000073: real data retrieval updates a dirty cache, default false.
        private void OnInputsDisabledUpdated(int id)
        {
            NotifyInputDisabledUpdated((GameInput)id, m_gameInputsDisabled.Get<bool>(id, true, false));
        }
        // Original06000074: a present null callback still faults.
        protected virtual void NotifyInputDisabledUpdated(GameInput gameInput, bool disabled)
        {
            if (m_gameInputsDisabledCallbacks.TryGetValue(gameInput, out Action<GameInput, bool> callback))
                callback(gameInput, disabled);
        }
        // Original06000075: combine/store before immediate invocation; callback parameter itself is invoked unguarded.
        public void SubscribeInputDisabled(GameInput gameInput, Action<GameInput, bool> callback, bool callImmediately = true)
        {
            m_gameInputsDisabledCallbacks.TryGetValue(gameInput, out Action<GameInput, bool> combined);
            combined += callback;
            m_gameInputsDisabledCallbacks[gameInput] = combined;
            if (callImmediately) callback(gameInput, IsGameInputDisabled(gameInput));
        }
        // Original06000076: absent key does nothing, removing the last callback removes its key.
        public void UnsubscribeInputDisabled(GameInput gameInput, Action<GameInput, bool> callback)
        {
            if (!m_gameInputsDisabledCallbacks.TryGetValue(gameInput, out Action<GameInput, bool> combined)) return;
            combined -= callback;
            if (combined == null) m_gameInputsDisabledCallbacks.Remove(gameInput);
            else m_gameInputsDisabledCallbacks[gameInput] = combined;
        }
        // Original06000077: no initialization/null guard.
        public StackableDataHandle AddGameInputDisabled(GameInput gameInput)
        {
            return m_gameInputsDisabled.AddOverride((int)gameInput, true);
        }
        // Original06000078 uses managed pointer null, retaining the handle if data is absent.
        public void RemoveGameInputDisabled(StackableDataHandle handle) { m_gameInputsDisabled?.RemoveOverrides(handle); }
        // Original06000079: absent data returns false; otherwise updateCacheIfDirty=true, defaultOutput=false.
        public bool IsGameInputDisabled(GameInput gameInput)
        {
            return m_gameInputsDisabled != null && m_gameInputsDisabled.Get<bool>((int)gameInput, true, false);
        }
        // Original0600007a: captured authored array; platform getter is read once per visited record, matching platform's null list faults.
        protected bool IsInputTypeBlocked(InputType inputType)
        {
            foreach (PlatformsWithBlockedInputTypes blocked in m_blockedInputTypes)
                if (blocked.Platform == Application.platform && blocked.InputTypes.Contains(inputType)) return true;
            return false;
        }
        // Implicit protected0600007b initializes empty array then comparer-backed dictionary before genuine base constructor.
    }
}
