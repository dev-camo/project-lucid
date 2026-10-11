// Preservation reconstruction of complete original Hardlight.UIContainerNavigation in HLModernUI.Runtime.
// Original C# text is unavailable; identifiers/signatures/annotations and both whole native slices
// are preserved independently. Optimizer, source spelling, implicit faults and reentrant mutation
// remain held; no current compiler, provider service or Unity runtime execution is claimed.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerNavigation : MonoBehaviour
    {
        [SerializeField] private bool m_exclusive;
        [InspectorReadOnly, SerializeField] private UIWidgetNavigation[] m_navigableWidgets;
        private readonly SystemRef<UINavigationManager> m_uiNavigationManagerRef = ProcessManager.GetSystemRef<UINavigationManager>();

        public bool Exclusive => m_exclusive;
        public UIWidgetNavigation[] NavigableWidgets => m_navigableWidgets;

        private void OnEnable() { m_uiNavigationManagerRef.InvokeOnValid(OnNavigationManagerValid); }

        private void OnDisable()
        {
            if (m_exclusive && m_uiNavigationManagerRef.TryGet(out UINavigationManager uiNavigationManager))
                uiNavigationManager.DeregisterExclusiveContainer(this);
        }

        private void Reset() { UpdateChildWidgets(); }
        private void OnNavigationManagerValid(UINavigationManager uiNavigationManager) { uiNavigationManager.RegisterExclusiveContainer(this); }

        public bool IsAnySelected()
        {
            foreach (UIWidgetNavigation widget in m_navigableWidgets)
                if (widget.IsSelected) return true;
            return false;
        }

        [ContextMenu("Update Widgets")]
        public void UpdateChildWidgets() { m_navigableWidgets = GetComponentsInChildren<UIWidgetNavigation>(true); }
        public UIContainerNavigation() { }
    }
}
