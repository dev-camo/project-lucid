using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    internal sealed class UISingular : UIContainerManager
    {
        private readonly Transform m_parent;
        private UIContainer m_singleContainer;

        // Original06000145: genuine base construction then readonly parent.
        public UISingular(Transform parent) { m_parent = parent; }
        // Original06000146: replace any live existing container, including the
        // same identifier. Native virtual Close receives the live field's ID.
        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            if (m_singleContainer != null) Close(m_singleContainer.Identifier);
            UIContainer created = Object.Instantiate(containerPrefab, m_parent);
            m_singleContainer = created;
            return created;
        }
        // Original06000147: Destroy, clear manager field, then close the passed
        // identifier (which may be a distinct original object with equal GUID).
        public override void Close(UIContainerIdentifier identifier)
        {
            if (m_singleContainer == null || m_singleContainer.Identifier != identifier) return;
            Object.Destroy(m_singleContainer.gameObject);
            m_singleContainer = null;
            identifier.Close();
        }
        // Original06000148 differs: Destroy, reload current field's identifier,
        // close that identifier, and only then clear the manager's current field.
        public override void CloseAll()
        {
            if (m_singleContainer == null) return;
            Object.Destroy(m_singleContainer.gameObject);
            m_singleContainer.Identifier.Close();
            m_singleContainer = null;
        }
        // Original06000149/4a retain true Unity destroyed-object predicates.
        public override bool IsOpen(UIContainerIdentifier identifier) => m_singleContainer != null && m_singleContainer.Identifier == identifier;
        public override bool IsAnyOpen() => m_singleContainer != null && m_singleContainer.Identifier != null;
        // Original0600014b: empty manager is true even for a nonempty/null list;
        // no requested-count test or substituted exclusivity definition.
        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers) => m_singleContainer == null || ReadOnlyListExtensions.Contains(identifiers, m_singleContainer.Identifier);
        // Original0600014c: always assign the out argument on normal return.
        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            if (m_singleContainer != null && m_singleContainer.Identifier == identifier)
            {
                container = m_singleContainer;
                return true;
            }
            container = null;
            return false;
        }
        // Original0600014d calls virtual CloseAll, not Close(identifier).
        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor)
        {
            if (m_singleContainer == null || ReadOnlyListExtensions.Contains(exceptFor, m_singleContainer.Identifier)) return;
            CloseAll();
        }
    }
}
