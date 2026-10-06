using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    internal sealed class UILinked : UIContainerManager
    {
        private readonly Transform m_parent;
        private readonly LinkedList<UIContainer> m_linkedContainers = new LinkedList<UIContainer>();

        // Original060000e4: initializer precedes the real base constructor.
        internal UILinked(Transform parent) { m_parent = parent; }
        // Original060000e5: reuse hides every entry, then activates the captured
        // lookup result. Creating a new entry follows the distinct last-only path.
        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            UIContainer container = null;
            if (TryGet(containerPrefab.Identifier, out container))
            {
                HideAll();
                container.gameObject.SetActive(true);
                return container;
            }
            return CreateNewContainer(containerPrefab);
        }
        // Original060000e6: genuine value enumerator and foreach disposal.
        private void HideAll()
        {
            foreach (UIContainer container in m_linkedContainers)
                container.gameObject.SetActive(false);
        }
        // Original060000e7: node enumeration is disposed before destruction.
        // Release the passed identifier, without reactivating an earlier entry.
        public override void Close(UIContainerIdentifier identifier)
        {
            LinkedListNode<UIContainer> node = null;
            foreach (LinkedListNode<UIContainer> current in m_linkedContainers.Nodes())
            {
                if (current.Value.Identifier != identifier) continue;
                node = current;
                break;
            }
            if (node == null) return;
            Object.Destroy(node.Value.gameObject);
            RemoveAheadOf(node);
            m_linkedContainers.Remove(node);
            identifier.Close();
        }
        // Original060000e8: identifier equality through the original GUID base.
        public override bool IsOpen(UIContainerIdentifier identifier)
        {
            foreach (UIContainer container in m_linkedContainers)
                if (container.Identifier == identifier) return true;
            return false;
        }
        // Original060000e9: count only; hidden entries still count as open.
        public override bool IsAnyOpen() => m_linkedContainers.Count > 0;
        // Original060000ea: subset membership, with vacuous empty success.
        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
        {
            foreach (UIContainer container in m_linkedContainers)
                if (!ReadOnlyListExtensions.Contains(identifiers, container.Identifier)) return false;
            return true;
        }
        // Original060000eb: reload node.Value after GUID inequality; write the
        // success result before disposal and the failed null result after it.
        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            foreach (LinkedListNode<UIContainer> node in m_linkedContainers.Nodes())
            {
                if (node.Value.Identifier != identifier) continue;
                container = node.Value;
                return true;
            }
            container = null;
            return false;
        }
        // Original060000ec: gather identifiers first, then dispatch virtual Close.
        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor)
        {
            List<UIContainerIdentifier> identifiers = new List<UIContainerIdentifier>();
            foreach (UIContainer container in m_linkedContainers)
                if (!ReadOnlyListExtensions.Contains(exceptFor, container.Identifier)) identifiers.Add(container.Identifier);
            foreach (UIContainerIdentifier identifier in identifiers) Close(identifier);
        }
        // Original060000ed: release each stored identifier before Destroy;
        // clear the reloaded collection only after successful enumeration.
        public override void CloseAll()
        {
            foreach (UIContainer container in m_linkedContainers)
            {
                container.Identifier.Close();
                Object.Destroy(container.gameObject);
            }
            m_linkedContainers.Clear();
        }
        // Original060000ee: instantiate first, capture Last, hide only that
        // entry, then append the captured new clone and return it.
        private UIContainer CreateNewContainer(UIContainer containerPrefab)
        {
            UIContainer container = Object.Instantiate(containerPrefab, m_parent);
            LinkedListNode<UIContainer> last = m_linkedContainers.Last;
            if (last != null) last.Value.gameObject.SetActive(false);
            m_linkedContainers.AddLast(container);
            return container;
        }
        // Original060000ef preserves the detached-node advance. LinkedList.Remove
        // clears the removed node's links; ordinary cleanup removes one successor.
        private void RemoveAheadOf(LinkedListNode<UIContainer> node)
        {
            while (node.Next != null)
            {
                LinkedListNode<UIContainer> next = node.Next;
                Object.Destroy(node.Next.Value.gameObject);
                m_linkedContainers.Remove(node.Next);
                node = next;
            }
        }
    }
}
