using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    internal sealed class UIDebug : UIContainerManager
    {
        private readonly Transform m_parent;
        private readonly Dictionary<UIContainerIdentifier, UIContainer> m_debugContainers = new Dictionary<UIContainerIdentifier, UIContainer>();

        // Original060000d1: dictionary field initializer before base, then parent.
        internal UIDebug(Transform parent) { m_parent = parent; }
        // Original060000d2: virtual TryGet result is returned as-is, including a
        // null/destroyed cached value. A new clone's actual Identifier is the key.
        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            UIContainer container = null;
            if (TryGet(containerPrefab.Identifier, out container)) return container;
            container = Object.Instantiate(containerPrefab, m_parent);
            m_debugContainers.Add(container.Identifier, container);
            return container;
        }
        // Original060000d3: two lookups, with the count and dictionary reloaded
        // after the virtual TryGet boundary. Release receives the passed ID.
        public override void Close(UIContainerIdentifier identifier)
        {
            UIContainer container = null;
            UIContainer existing = null;
            if (!TryGet(identifier, out container)) return;
            if (m_debugContainers.Count < 1 || !m_debugContainers.TryGetValue(identifier, out existing)) return;
            Object.Destroy(existing.gameObject);
            m_debugContainers.Remove(identifier);
            identifier.Close();
        }
        // Original060000d4: values and their current IDs, not dictionary keys;
        // the genuine value enumerator retains its finally/disposal behavior.
        public override bool IsOpen(UIContainerIdentifier identifier)
        {
            foreach (UIContainer container in m_debugContainers.Values)
                if (container.Identifier == identifier) return true;
            return false;
        }
        // Original060000d5: original Count > 0, with no live-object filtering.
        public override bool IsAnyOpen() => m_debugContainers.Count > 0;
        // Original060000d6: every stored value's ID must be in the requested
        // list; an empty dictionary returns true without reading that list.
        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
        {
            foreach (UIContainer container in m_debugContainers.Values)
                if (!ReadOnlyListExtensions.Contains(identifiers, container.Identifier)) return false;
            return true;
        }
        // Original060000d7: direct dictionary lookup, no Unity object predicate.
        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container) => m_debugContainers.TryGetValue(identifier, out container);
        // Original060000d8/d9: both shipped native implementations are RET.
        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor) { }
        public override void CloseAll() { }
    }
}
