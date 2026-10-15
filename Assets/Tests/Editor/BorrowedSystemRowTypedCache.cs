using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Tests
{
    // A registry row left by earlier work is borrowed, not owned, but
    // ProcessManager.GetSystemRef<T> (0x06000e3f) still caches a typed
    // reference in its SystemRefDictionary. This snapshots that cache and
    // removes only the entries added while borrowed; a replaced row or a
    // changed prior entry is reported instead of being overwritten.
    internal sealed class BorrowedSystemRowTypedCache : IDisposable
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly IDictionary m_registry;
        private readonly string m_name;
        private readonly object m_row;
        private readonly IDictionary m_cache;
        private readonly Dictionary<object, object> m_prior = new Dictionary<object, object>();
        private bool m_released;

        private BorrowedSystemRowTypedCache(IDictionary registry, string name)
        {
            m_registry = registry;
            m_name = name;
            m_row = registry[name];
            m_cache = (IDictionary)m_row.GetType().GetField("SystemRefDictionary", InstanceFields).GetValue(m_row);
            foreach (DictionaryEntry entry in m_cache) m_prior.Add(entry.Key, entry.Value);
        }

        internal static IDictionary Registry => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // Returns null for an absent row; the caller then acquires it owned.
        internal static BorrowedSystemRowTypedCache TryBorrow(string name)
        {
            IDictionary registry = Registry;
            return registry.Contains(name) ? new BorrowedSystemRowTypedCache(registry, name) : null;
        }

        public void Dispose()
        {
            if (m_released) return;
            m_released = true;
            if (!ReferenceEquals(m_registry[m_name], m_row) ||
                !ReferenceEquals(m_row.GetType().GetField("SystemRefDictionary", InstanceFields).GetValue(m_row), m_cache))
                throw new InvalidOperationException("Borrowed system row was replaced: " + m_name);
            foreach (KeyValuePair<object, object> entry in m_prior)
                if (!m_cache.Contains(entry.Key) || !ReferenceEquals(m_cache[entry.Key], entry.Value))
                    throw new InvalidOperationException("Borrowed system row typed cache changed: " + m_name + "/" + entry.Key);
            var added = new List<object>();
            foreach (DictionaryEntry entry in m_cache) if (!m_prior.ContainsKey(entry.Key)) added.Add(entry.Key);
            foreach (object key in added) m_cache.Remove(key);
        }
    }
}
