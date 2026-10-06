using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    internal sealed class UIFree : UIContainerManager
    {
        private readonly Transform m_parent;
        private readonly Dictionary<UIContainerIdentifier, UIContainer> m_freeContainers = new Dictionary<UIContainerIdentifier, UIContainer>();

        // Original060000da: collection field initialized before base, then parent.
        internal UIFree(Transform parent) { m_parent = parent; }
        // Original060000db: original virtual lookup can return a null cached value.
        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            UIContainer container = null;
            if (TryGet(containerPrefab.Identifier, out container)) return container;
            container = Object.Instantiate(containerPrefab, m_parent);
            m_freeContainers.Add(container.Identifier, container);
            return container;
        }
        // Original060000dc: virtual lookup, then count, then a second dictionary
        // lookup into the caller's out argument; failed first gates write null.
        private bool TryGetFreeContainer(UIContainerIdentifier identifier, out UIContainer container)
        {
            UIContainer existing = null;
            if (TryGet(identifier, out existing) && m_freeContainers.Count > 0)
                return m_freeContainers.TryGetValue(identifier, out container);
            container = null;
            return false;
        }
        // Original060000dd: private helper, Destroy, remove passed key, passed ID release.
        public override void Close(UIContainerIdentifier identifier)
        {
            UIContainer container = null;
            if (!TryGetFreeContainer(identifier, out container)) return;
            Object.Destroy(container.gameObject);
            m_freeContainers.Remove(identifier);
            identifier.Close();
        }
        // Original060000de: each current value Identifier is compared, not keys.
        public override bool IsOpen(UIContainerIdentifier identifier)
        {
            foreach (UIContainer container in m_freeContainers.Values)
                if (container.Identifier == identifier) return true;
            return false;
        }
        // Original060000df: stored count, not live-value filtering.
        public override bool IsAnyOpen() => m_freeContainers.Count > 0;
        // Original060000e0: membership-only condition, empty collection true.
        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
        {
            foreach (UIContainer container in m_freeContainers.Values)
                if (!ReadOnlyListExtensions.Contains(identifiers, container.Identifier)) return false;
            return true;
        }
        // Original060000e1: genuine direct dictionary call.
        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container) => m_freeContainers.TryGetValue(identifier, out container);
        // Original060000e2: allocate first, enumerate keys and collect exclusions,
        // dispose that enumerator, then enumerate the new list and virtual Close.
        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor)
        {
            var identifiers = new List<UIContainerIdentifier>();
            foreach (UIContainerIdentifier identifier in m_freeContainers.Keys)
                if (!ReadOnlyListExtensions.Contains(exceptFor, identifier)) identifiers.Add(identifier);
            foreach (UIContainerIdentifier identifier in identifiers) Close(identifier);
        }
        // Original060000e3 reverses the individual Close release/Destroy order.
        // Enumerate live values with genuine disposal; clear only after success.
        public override void CloseAll()
        {
            foreach (UIContainer container in m_freeContainers.Values)
            {
                container.Identifier.Close();
                Object.Destroy(container.gameObject);
            }
            m_freeContainers.Clear();
        }
    }
}
