using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SerializableDictionaryBase<TKey, TValue, TKeyValuePair> :
        IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IDictionary,
        ISerializationCallbackReceiver
    {
        private readonly Dictionary<TKey, TValue> m_dictionary;
        [FormerlySerializedAs("m_listKVPs"), SerializeField]
        private List<TKeyValuePair> m_values = new List<TKeyValuePair>();

        // Original HLUnityCore.Runtime 0x06000fbb..fbf: direct dictionary APIs.
        public int Count => m_dictionary.Count;
        public ICollection<TKey> Keys => m_dictionary.Keys;
        public ICollection<TValue> Values => m_dictionary.Values;
        public TValue this[TKey key]
        {
            get => m_dictionary[key];
            set => m_dictionary[key] = value;
        }

        // 0x06000fc0/fc1: the list field initializer precedes Object..ctor;
        // dictionary construction follows it, including a null comparer.
        protected SerializableDictionaryBase() { m_dictionary = new Dictionary<TKey, TValue>(); }
        protected SerializableDictionaryBase(IEqualityComparer<TKey> comparer)
        { m_dictionary = new Dictionary<TKey, TValue>(comparer); }

        // 0x06000fc2, arm64 0xbc2f04: clear before enumerating the argument.
        // Passing the backing dictionary consequently empties it; a null
        // argument fails only after the existing dictionary was cleared.
        public void Set(Dictionary<TKey, TValue> dictionary)
        {
            Clear();
            foreach (var (key, value) in dictionary) Add(key, value);
        }

        // 0x06000fc3: Clear() leaves serialized rows intact; Reset clears both.
        public void Reset() { Clear(); m_values.Clear(); }

        // Original abstract contracts 0x06000fc4/fc5; no native body credit.
        protected abstract TKeyValuePair SerializeKeyValuePair(TKey key, TValue value);
        protected abstract void DeserializeKeyValuePair(TKeyValuePair pair, out TKey key, out TValue value);

        // 0x06000fc6, arm64 0xbc30fc: no duplicate/null filtering or rollback.
        // A failed row retains earlier additions and the original serialized list.
        public virtual void Deserialize()
        {
            Clear();
            foreach (TKeyValuePair pair in m_values)
            {
                DeserializeKeyValuePair(pair, out TKey key, out TValue value);
                Add(key, value);
            }
        }

        // 0x06000fc7, arm64 0xbc326c: virtual conversion occurs inside the
        // dictionary enumeration. Failure retains rows already appended.
        public virtual void Serialize()
        {
            m_values.Clear();
            foreach (var (key, value) in m_dictionary)
            {
                // Native 0xbc335c converts before 0xbc3364 reloads m_values.
                // The converter may replace the mutable serialized row list.
                TKeyValuePair pair = SerializeKeyValuePair(key, value);
                m_values.Add(pair);
            }
        }

        // 0x06000fc8, arm64 0xbc3460: checks only the final two rows. This
        // private original API is not invoked by either serialization callback.
        private bool ValidateValues()
        {
            int count = m_values.Count;
            if (count == 0) return true;
            DeserializeKeyValuePair(m_values[count - 1], out TKey lastKey, out TValue lastValue);
            if (lastKey == null) return false;
            if (count == 1) return true;
            DeserializeKeyValuePair(m_values[count - 2], out TKey previousKey, out TValue previousValue);
            return !lastKey.Equals(previousKey);
        }

        // 0x06000fc9..fd3 preserve Dictionary's actual generic interface APIs.
        public bool IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)m_dictionary).IsReadOnly;
        public void Add(TKey key, TValue value) { m_dictionary.Add(key, value); }
        public bool ContainsKey(TKey key) => m_dictionary.ContainsKey(key);
        public bool Remove(TKey key) => m_dictionary.Remove(key);
        public void RemoveAll(Predicate<KeyValuePair<TKey, TValue>> predicate)
        { m_dictionary.RemoveAll(predicate); }
        public bool TryGetValue(TKey key, out TValue value) => m_dictionary.TryGetValue(key, out value);
        public void Add(KeyValuePair<TKey, TValue> item)
        { ((ICollection<KeyValuePair<TKey, TValue>>)m_dictionary).Add(item); }
        public bool Contains(KeyValuePair<TKey, TValue> item)
            => ((ICollection<KeyValuePair<TKey, TValue>>)m_dictionary).Contains(item);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        { ((ICollection<KeyValuePair<TKey, TValue>>)m_dictionary).CopyTo(array, arrayIndex); }
        public bool Remove(KeyValuePair<TKey, TValue> item)
            => ((ICollection<KeyValuePair<TKey, TValue>>)m_dictionary).Remove(item);
        public void Clear() { m_dictionary.Clear(); }

        // 0x06000fd4..fdf: forwarding through Dictionary's non-generic
        // interfaces preserves its boxing, type validation and exception order.
        public bool IsFixedSize => ((IDictionary)m_dictionary).IsFixedSize;
        public bool IsSynchronized => ((ICollection)m_dictionary).IsSynchronized;
        public object SyncRoot => ((ICollection)m_dictionary).SyncRoot;
        public object this[object key]
        {
            get => ((IDictionary)m_dictionary)[key];
            set => ((IDictionary)m_dictionary)[key] = value;
        }
        ICollection IDictionary.Values => ((IDictionary)m_dictionary).Values;
        ICollection IDictionary.Keys => ((IDictionary)m_dictionary).Keys;
        public void Add(object key, object value) { ((IDictionary)m_dictionary).Add(key, value); }
        public void CopyTo(Array array, int index) { ((ICollection)m_dictionary).CopyTo(array, index); }
        public bool Contains(object key) => ((IDictionary)m_dictionary).Contains(key);
        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)m_dictionary).GetEnumerator();
        public void Remove(object key) { ((IDictionary)m_dictionary).Remove(key); }

        // 0x06000fe0/fe1 forward the generic collection getters.
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        // 0x06000fe2/fe3: callbacks dispatch the virtual methods directly.
        void ISerializationCallbackReceiver.OnBeforeSerialize() { Serialize(); }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { Deserialize(); }

        // 0x06000fe4/fe5: both routes box the Dictionary struct enumerator.
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => m_dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
