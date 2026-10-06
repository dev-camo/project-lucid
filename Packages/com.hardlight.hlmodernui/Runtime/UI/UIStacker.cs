using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    internal sealed class UIStacker : UIContainerManager
    {
        private readonly Transform m_parent;
        private readonly Stack<UIContainer> m_stackContainers = new Stack<UIContainer>();

        // Original0600014e: stack initializer before base, then readonly parent.
        internal UIStacker(Transform parent) { m_parent = parent; }
        // Original0600014f: virtual lookup before instantiate/push; reuse as-is.
        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            UIContainer container = null;
            if (TryGet(containerPrefab.Identifier, out container)) return container;
            container = Object.Instantiate(containerPrefab, m_parent);
            m_stackContainers.Push(container);
            return container;
        }
        // Original06000150: destroy popped containers ahead of the requested
        // identifier without releasing their IDs. Only the captured TryGet
        // result and passed requested ID receive the final destroy/release.
        public override void Close(UIContainerIdentifier identifier)
        {
            UIContainer container = null;
            if (!TryGet(identifier, out container) || m_stackContainers.Count < 1) return;
            UIContainer top = m_stackContainers.Pop();
            while (top.Identifier != identifier)
            {
                Object.Destroy(top.gameObject);
                if (m_stackContainers.Count < 1) break;
                top = m_stackContainers.Pop();
            }
            Object.Destroy(container.gameObject);
            identifier.Close();
        }
        // Original06000151: genuine LIFO enumerator, identifier equality.
        public override bool IsOpen(UIContainerIdentifier identifier)
        {
            foreach (UIContainer container in m_stackContainers)
                if (container.Identifier == identifier) return true;
            return false;
        }
        // Original06000152: raw stored count > 0, no Unity predicate/filtering.
        public override bool IsAnyOpen() => m_stackContainers.Count > 0;
        // Original06000153: every current entry is in the requested list.
        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
        {
            foreach (UIContainer container in m_stackContainers)
                if (!ReadOnlyListExtensions.Contains(identifiers, container.Identifier)) return false;
            return true;
        }
        // Original06000154 writes success before enumerator disposal; failed
        // null result is written only after the original enumeration completes.
        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            foreach (UIContainer current in m_stackContainers)
            {
                if (current.Identifier == identifier)
                {
                    container = current;
                    return true;
                }
            }
            container = null;
            return false;
        }
        // Original06000155 stops at the first excluded stack top. It reloads
        // Peek after membership and dispatches virtual Close on that live ID.
        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor)
        {
            while (m_stackContainers.Count > 0 && !ReadOnlyListExtensions.Contains(exceptFor, m_stackContainers.Peek().Identifier))
                Close(m_stackContainers.Peek().Identifier);
        }
        // Original06000156: pop, release its stored ID, then Destroy; Clear
        // still executes after successful exhaustion, including initially empty.
        public override void CloseAll()
        {
            while (m_stackContainers.Count > 0)
            {
                UIContainer container = m_stackContainers.Pop();
                container.Identifier.Close();
                Object.Destroy(container.gameObject);
            }
            m_stackContainers.Clear();
        }
    }
}
