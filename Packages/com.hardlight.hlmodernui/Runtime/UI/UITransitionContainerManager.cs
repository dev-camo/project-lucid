using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UITransitionContainerManager : UIContainerManager
    {
        private readonly Transform m_parent;
        private UIContainer m_transitionContainer;

        // Original06000157: genuine base then readonly parent assignment.
        public UITransitionContainerManager(Transform parent) { m_parent = parent; }
        // Original06000158 replaces every live prior transition container.
        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            if (m_transitionContainer != null) Close(m_transitionContainer.Identifier);
            UIContainer created = Object.Instantiate(containerPrefab, m_parent);
            m_transitionContainer = created;
            return created;
        }
        // Original06000159 uses passed identifier after clearing manager field.
        public override void Close(UIContainerIdentifier identifier)
        {
            if (m_transitionContainer == null || m_transitionContainer.Identifier != identifier) return;
            Object.Destroy(m_transitionContainer.gameObject);
            m_transitionContainer = null;
            identifier.Close();
        }
        // Original0600015a closes the reloaded current field's identifier before
        // clearing that field, including any native release callback boundary.
        public override void CloseAll()
        {
            if (m_transitionContainer == null) return;
            Object.Destroy(m_transitionContainer.gameObject);
            m_transitionContainer.Identifier.Close();
            m_transitionContainer = null;
        }
        // Original0600015b/5c retain true Unity object predicates and GUID equality.
        public override bool IsOpen(UIContainerIdentifier identifier) => m_transitionContainer != null && m_transitionContainer.Identifier == identifier;
        public override bool IsAnyOpen() => m_transitionContainer != null && m_transitionContainer.Identifier != null;
        // Original0600015d: empty manager is true irrespective of requested list.
        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers) => m_transitionContainer == null || ReadOnlyListExtensions.Contains(identifiers, m_transitionContainer.Identifier);
        // Original0600015e reloads field after successful GUID comparison.
        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            if (m_transitionContainer != null && m_transitionContainer.Identifier == identifier)
            {
                container = m_transitionContainer;
                return true;
            }
            container = null;
            return false;
        }
        // Original0600015f deliberately calls virtual Close with a live field's
        // identifier rather than virtual CloseAll as UISingular does.
        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor)
        {
            if (m_transitionContainer == null || ReadOnlyListExtensions.Contains(exceptFor, m_transitionContainer.Identifier)) return;
            Close(m_transitionContainer.Identifier);
        }
    }
}
