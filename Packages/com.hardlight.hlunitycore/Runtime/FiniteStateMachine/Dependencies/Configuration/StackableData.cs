using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public partial class StackableData
    {
        private const RetrievalOperation DefaultRetrievalOperation = RetrievalOperation.Latest;
        private readonly OrderedDictionary m_dataDictionary = new OrderedDictionary();
        private readonly Dictionary<int, StackableCachedData> m_stackableCache = new Dictionary<int, StackableCachedData>();
        private readonly StackableDataHandle m_baseStackableDataHandle;
        private readonly List<int> m_updatedIds = new List<int>();

        // HLUnityCore.Runtime 0x06000103; ARM64 0x1aa20d0. Instance collection initializers precede the base call.
        public StackableData() { m_baseStackableDataHandle = CreateOverride(); }

        // 0x06000106/0x06000107; shared reference ARM64 0x9b00a8/0x9b0360.
        public void SetBaseValue<T>(int id, T value) { AddOverride(m_baseStackableDataHandle, id, value); }
        public void SetBaseValue<T>(int id, T value, RetrievalOperation retrievalOperation)
        {
            AddOverride(m_baseStackableDataHandle, id, value);
            SetRetrievalOperation<T>(id, retrievalOperation);
        }

        // 0x06000108; ARM64 0x1aa2208.
        public StackableDataHandle CreateOverride()
        {
            StackableDataHandle handle = new StackableDataHandle();
            m_dataDictionary.Add(handle, new Dictionary<int, StackableDataContainer>());
            return handle;
        }

        // 0x06000109; shared reference ARM64 0x9a74b4.
        public StackableDataHandle AddOverride<T>(int id, T value)
        {
            StackableDataHandle handle = new StackableDataHandle();
            m_dataDictionary.Add(handle, StackableDataContainer.ToDictionary(id, value));
            DataUpdated(id);
            return handle;
        }

        // 0x0600010a; shared reference ARM64 0x9a7d14. Invalid handles use the original registered diagnostics.
        public void AddOverride<T>(StackableDataHandle handle, int id, T value)
        {
            if (!m_dataDictionary.Contains(handle))
            {
                HLOutput.LogError("Attempting to update StackableDataContainer that has not been created yet");
                return;
            }
            Dictionary<int, StackableDataContainer> dictionary = m_dataDictionary[handle] as Dictionary<int, StackableDataContainer>;
            if (dictionary == null)
            {
                HLOutput.LogError("Stored StackableDataContainer array is not valid");
                return;
            }
            StackableDataContainer.ToDictionary(id, value, dictionary, false);
            DataUpdated(id);
        }

        // 0x0600010b; ARM64 0x1aa2438. Shared scratch list and live indexed Count preserve callback reentrancy.
        public void ClearOverrides(StackableDataHandle handle)
        {
            if (!m_dataDictionary.Contains(handle))
            {
                HLOutput.LogError("Attempting to clear StackableDataContainer that has not been created yet");
                return;
            }
            Dictionary<int, StackableDataContainer> dictionary = m_dataDictionary[handle] as Dictionary<int, StackableDataContainer>;
            if (dictionary == null)
            {
                HLOutput.LogError("Stored StackableDataContainer array is not valid");
                return;
            }
            m_updatedIds.Clear();
            m_updatedIds.AddRange(dictionary.Keys);
            dictionary.Clear();
            for (int i = 0; i < m_updatedIds.Count; i++) DataUpdated(m_updatedIds[i]);
        }

        // 0x0600010c; ARM64 0x1aa275c. Removal precedes enumeration and notifications; invalid entries are ignored.
        public void RemoveOverrides(StackableDataHandle handle)
        {
            Dictionary<int, StackableDataContainer> dictionary = m_dataDictionary[handle] as Dictionary<int, StackableDataContainer>;
            if (dictionary == null) return;
            m_dataDictionary.Remove(handle);
            foreach (KeyValuePair<int, StackableDataContainer> entry in dictionary) DataUpdated(entry.Key);
        }

        // 0x0600010d; ARM64 bool specialization 0x9ab17c. An absent/wrongly typed value is dereferenced after the lookup.
        public T GetOverride<T>(StackableDataHandle handle, int id)
        {
            if (!m_dataDictionary.Contains(handle))
            {
                HLOutput.LogError("Attempting to get StackableDataContainer that has not been created yet");
                return default(T);
            }
            Dictionary<int, StackableDataContainer> dictionary = m_dataDictionary[handle] as Dictionary<int, StackableDataContainer>;
            if (dictionary == null)
            {
                HLOutput.LogError("Stored StackableDataContainer array is not valid");
                return default(T);
            }
            return StackableDataContainer.GetFromDictionary<T>(id, dictionary).Value;
        }

        // 0x0600010e; shared reference ARM64 0x9b08b0. Operation changes do not replace an existing cache's generic type.
        public void SetRetrievalOperation<T>(int id, RetrievalOperation retrievalOperation, T defaultValue = default(T))
        {
            if (m_stackableCache.TryGetValue(id, out StackableCachedData cached))
            {
                if (cached.RetrievalOperation == retrievalOperation) return;
                cached.RetrievalOperation = retrievalOperation;
            }
            else m_stackableCache.Add(id, new StackableCachedData<T>(defaultValue, retrievalOperation));
            DataUpdated(id);
        }

        // 0x0600010f; ARM64 0x1aa2984. Presence ignores container type and value, using a reverse count snapshot.
        public bool HasData(int id)
        {
            for (int i = m_dataDictionary.Count - 1; i >= 0; i--)
            {
                Dictionary<int, StackableDataContainer> dictionary = m_dataDictionary[i] as Dictionary<int, StackableDataContainer>;
                if (dictionary != null && dictionary.ContainsKey(id)) return true;
            }
            return false;
        }

        // 0x06000110/0x06000111; shared reference ARM64 0x9a9c80/0x9b12b4.
        public T Get<T>(int id, bool updateCacheIfDirty = true, T defaultOutput = default(T))
        {
            TryGet(id, out T data, updateCacheIfDirty, defaultOutput);
            return data;
        }
        public bool TryGet<T>(int id, out T data, bool updateCacheIfDirty = true, T defaultOutput = default(T))
        {
            data = defaultOutput;
            if (m_stackableCache.TryGetValue(id, out StackableCachedData cached))
            {
                StackableCachedData<T> typed = cached as StackableCachedData<T>;
                bool found = typed != null;
                if (found) data = typed.Value;
                if (cached.IsDirty && updateCacheIfDirty)
                {
                    ResultCarrier<T> result = UpdateCachedValue<T>(id, cached.RetrievalOperation);
                    found = result.HasValue;
                    data = found ? result.Value : defaultOutput;
                }
                return found;
            }
            if (updateCacheIfDirty)
            {
                ResultCarrier<T> result = UpdateCachedValue<T>(id, DefaultRetrievalOperation);
                data = result.HasValue ? result.Value : defaultOutput;
                return result.HasValue;
            }
            return false;
        }

        // 0x06000112/0x06000113; fully shared ARM64 0x9ab920 / reference 0x9ab5dc.
        public T GetStack<T>(int id, Func<List<T>, T> callback, List<T> values = null) { return callback(GetStack(id, values)); }
        public List<T> GetStack<T>(int id, List<T> values)
        {
            if (values == null) values = new List<T>();
            values.Clear();
            for (int i = 0; i < m_dataDictionary.Count; i++)
            {
                StackableDataContainer<T> container = StackableDataContainer.GetFromDictionary<T>(id, m_dataDictionary[i]);
                if (container != null) values.Add(container.Value);
            }
            return values;
        }

        // 0x06000114; shared reference ARM64 0x9b20f8. Failed retrieval leaves the prior cache and dirty bit intact.
        private ResultCarrier<T> UpdateCachedValue<T>(int id, RetrievalOperation retrievalOperation)
        {
            ResultCarrier<T> result = Get(id, retrievalOperation, default(ResultCarrier<T>));
            if (result.HasValue)
            {
                if (m_stackableCache.TryGetValue(id, out StackableCachedData cached))
                {
                    StackableCachedData<T> typed = cached as StackableCachedData<T>;
                    if (typed != null) { typed.Set(result.Value); typed.IsDirty = false; return result; }
                    m_stackableCache.Remove(id);
                }
                m_stackableCache.Add(id, new StackableCachedData<T>(result.Value, retrievalOperation) { IsDirty = false });
            }
            return result;
        }

        // 0x06000115; shared reference ARM64 0x9a9d04. Invalid operations throw only upon finding a typed container.
        private ResultCarrier<T> Get<T>(int id, RetrievalOperation retrievalOperation, ResultCarrier<T> resultCarrier)
        {
            if (retrievalOperation == RetrievalOperation.Base) return GetBase(id, resultCarrier);
            for (int i = m_dataDictionary.Count - 1; i >= 0; i--)
            {
                StackableDataContainer<T> container = StackableDataContainer.GetFromDictionary<T>(id, m_dataDictionary[i]);
                if (container == null) continue;
                OperationAction action;
                switch (retrievalOperation)
                {
                    case RetrievalOperation.Latest: resultCarrier.SetValue(container.Value); return resultCarrier;
                    case RetrievalOperation.LogicalAnd: action = LogicalAnd(container, ref resultCarrier); break;
                    case RetrievalOperation.LogicalOr: action = LogicalOr(container, ref resultCarrier); break;
                    case RetrievalOperation.Multiply: action = Multiply(container, ref resultCarrier); break;
                    case RetrievalOperation.Addition: action = Addition(container, ref resultCarrier); break;
                    default: throw new ArgumentOutOfRangeException("retrievalOperation", retrievalOperation, "Retrieval operation not supported");
                }
                if (action == OperationAction.EarlyExit) return resultCarrier;
            }
            return resultCarrier;
        }

        // 0x06000116; shared reference ARM64 0x9aad80. Base means earliest matching container across the ordered stack.
        private ResultCarrier<T> GetBase<T>(int id, ResultCarrier<T> resultCarrier)
        {
            for (int i = 0; i < m_dataDictionary.Count; i++)
            {
                StackableDataContainer<T> container = StackableDataContainer.GetFromDictionary<T>(id, m_dataDictionary[i]);
                if (container == null) continue;
                resultCarrier.SetValue(container.Value);
                return resultCarrier;
            }
            return resultCarrier;
        }

        // 0x06000117..0x0600011a; shared reference ARM64 0x9ac0a4/0x9ad6dc/0x9ae60c/0x9a8a70.
        private static OperationAction LogicalAnd<T>(StackableDataContainer<T> container, ref ResultCarrier<T> result)
        {
            if (!LogicalAnds.ContainsKey(typeof(T))) throw new NotImplementedException(string.Format("No LogicalAnd operation available for type: {0}", typeof(T)));
            return ((ResultCarrier<T>.Operation)LogicalAnds[typeof(T)])(container, ref result);
        }
        private static OperationAction LogicalOr<T>(StackableDataContainer<T> container, ref ResultCarrier<T> result)
        {
            if (!LogicalOrs.ContainsKey(typeof(T))) throw new NotImplementedException(string.Format("No LogicalOr operation available for type: {0}", typeof(T)));
            return ((ResultCarrier<T>.Operation)LogicalOrs[typeof(T)])(container, ref result);
        }
        private static OperationAction Multiply<T>(StackableDataContainer<T> container, ref ResultCarrier<T> result)
        {
            if (!Multiplies.ContainsKey(typeof(T))) throw new NotImplementedException(string.Format("No multiplication operation available for type: {0}", typeof(T)));
            return ((ResultCarrier<T>.Operation)Multiplies[typeof(T)])(container, ref result);
        }
        private static OperationAction Addition<T>(StackableDataContainer<T> container, ref ResultCarrier<T> result)
        {
            if (!Additions.ContainsKey(typeof(T))) throw new NotImplementedException(string.Format("No addition operation available for type: {0}", typeof(T)));
            return ((ResultCarrier<T>.Operation)Additions[typeof(T)])(container, ref result);
        }
    }
}
