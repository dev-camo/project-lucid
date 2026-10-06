using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MetadataGroups
    {
        [SerializeField] private List<MetadataKeyGroupPair> m_groups;
        private Dictionary<MetadataGroupKey, MetadataGroup> m_groupDictionary;
        public bool HasData => m_groups.Count > 0;
        public MetadataGroups()
        {
            // The list is allocated in the constructor body after Object construction.
            m_groups = new List<MetadataKeyGroupPair>();
        }
        public MetadataGroup GetGroup(MetadataGroupKey key)
        {
            CreateDictionary();
            return m_groupDictionary.TryGetValue(key, out MetadataGroup group) ? group : null;
        }
        private void CreateDictionary()
        {
            if (m_groupDictionary != null) return;
            // Publish the cache before enumeration; failures retain the partially populated cache.
            m_groupDictionary = new Dictionary<MetadataGroupKey, MetadataGroup>(m_groups.Count);
            foreach (MetadataKeyGroupPair pair in m_groups)
                if (pair.Key != null)
                    m_groupDictionary[pair.Key] = pair.Group;
        }
    }
}
