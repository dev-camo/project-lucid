using System;
using System.Collections.Generic;

namespace Hardlight
{
    public class FSMStorage : IGraphStorage
    {
        // 0x060004c2; arm64 0x1ad1684, original registration order.
        private static readonly int s_stateHistoryId = GraphNameLookup.ConvertNameToId("stateHistory");
        private static readonly int s_stateHistoryMaxCountId = GraphNameLookup.ConvertNameToId("stateHistoryMaxCount");
        private readonly Dictionary<GraphStorageKey, object> m_objects;
        private readonly int m_stateHistoryMaxCount;

        // 0x060004b7..0x060004b9; stored values, not the constructor field.
        public int StateHistoryMaxCount => GetValue<int>(s_stateHistoryMaxCountId, 0, false);
        public IReadOnlyDictionary<int, Queue<FSMStateChangeAction>> StateHistory => MyStateHistory;
        private Dictionary<int, Queue<FSMStateChangeAction>> MyStateHistory =>
            GetValue<Dictionary<int, Queue<FSMStateChangeAction>>>(s_stateHistoryId, null, false);

        // 0x060004ba; arm64 0x1ad123c.
        public FSMStorage(int stateHistoryMaxCount = 5)
        {
            m_objects = new Dictionary<GraphStorageKey, object>();
            m_stateHistoryMaxCount = stateHistoryMaxCount;
            Initialise();
        }

        // 0x060004bb; arm64 0x1ad12e0. Reinitialization overwrites history;
        // it does not clear other storage or destroy a previous history.
        public void Initialise()
        {
            if (m_stateHistoryMaxCount < 1) return;
            SetValue<int>(s_stateHistoryMaxCountId, m_stateHistoryMaxCount);
            SetValue(s_stateHistoryId, new Dictionary<int, Queue<FSMStateChangeAction>>());
        }

        // 0x060004bc; arm64 0x1ad142c.
        public IReadOnlyDictionary<GraphStorageKey, object> GetCollection() => m_objects;

        // 0x060004bd; arm64 0x1ad1434. Clear does not reinitialize history.
        public void Clear()
        {
            ClearStateHistory();
            m_objects.Clear();
        }

        // 0x060004be; arm64 0x1ad1490.
        public void ClearStateHistory()
        {
            if (StateHistoryMaxCount < 1) return;
            Dictionary<int, Queue<FSMStateChangeAction>> history = MyStateHistory;
            if (history == null) return;
            foreach (KeyValuePair<int, Queue<FSMStateChangeAction>> entry in history)
                while (entry.Value.Count > 0) entry.Value.Dequeue().Destroy(true);
        }

        // 0x060004bf; arm64 generic reference 0x9099a8 / int 0x9093d8.
        // Value types mutate Var<T>; reference types are stored directly.
        public void SetValue<T>(GraphStorageKey storageKey, T value)
        {
            if (typeof(T).IsValueType)
            {
                IGraphStorage.Var<T> wrapped;
                if (m_objects.TryGetValue(storageKey, out object existing))
                    wrapped = (IGraphStorage.Var<T>)existing;
                else
                {
                    wrapped = new IGraphStorage.Var<T>(value);
                    m_objects.Add(storageKey, wrapped);
                }
                wrapped.Value = value;
            }
            else m_objects[storageKey] = value;
        }

        // 0x060004c0; arm64 generic reference 0x907ba0 / int 0x90790c.
        public T GetValue<T>(GraphStorageKey storageKey, T defaultValue = default(T), bool storeDefault = true)
        {
            if (m_objects.TryGetValue(storageKey, out object value))
                return this.ConvertValue<T>(storageKey, value);
            if (storeDefault) SetValue(storageKey, defaultValue);
            return defaultValue;
        }

        // 0x060004c1; native Dictionary.Remove; T is not consulted.
        public void RemoveValue<T>(GraphStorageKey storageKey) => m_objects.Remove(storageKey);
    }
}
